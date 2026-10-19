using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public static class BattleEntry
{
    static readonly string[] PrefabNames = { "bibi", "edgar", "mari_geo", "spike" };

    // 没走过 48（编辑器直进 BattleScene Play 自测）时退回的两人位，跟服务端老出生点一致
    static readonly float s_FallbackX = -5f, s_FallbackZ = 2f;

    // 编辑器直接从 BattleScene Play：RuntimeInitialize(AfterSceneLoad) 会跑一次
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnBootSpawn() { TrySpawn(); }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneLoads()
    {
        SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name == "BattleScene") TrySpawn();
        };
    }

    // 名单（48 带过来）里几个人就出几个：自己 = LocalHero（带摇杆/瞄准），别人 = GuestHero + RemoteHero
    static void TrySpawn()
    {
        if (SceneManager.GetActiveScene().name != "BattleScene") return;

        AudioManager.PlayBgm(AudioManager.BgmBattle);
        AudioManager.PlaySfx(AudioManager.SfxBattleLoad);   // 开场那一下，盖在 BGM 上响一次

        new GameObject("HitFeedback").AddComponent<HitFeedback>(); // 受击反馈：伤害飘字 + 自己挨打白屏闪

        MapGrid.EnsureLoaded();   // 下面要算地图中心（出生朝向用），没加载时 Rows/Cols 是 0

        List<BattleRoster.Member> roster = BuildRoster();
        string myUid = PlayerInfo.Instance.UID;

        GameObject local = null;
        PlayerState myBar = null;
        List<BattleSync.Remote> remotes = new List<BattleSync.Remote>();

        foreach (BattleRoster.Member m in roster)
        {
            if (m.UID == myUid)
            {
                local = SpawnHero(m.HeroId, m.X, m.Z, true);
                if (local != null) myBar = SpawnHpBar(local, false);
            }
            else
            {
                GameObject go = SpawnHero(m.HeroId, m.X, m.Z, false);
                if (go == null) continue;
                remotes.Add(new BattleSync.Remote
                {
                    UID = m.UID,
                    Hero = go.AddComponent<RemoteHero>(),
                    Bar = SpawnHpBar(go, true)   // 别人的血条一律红色
                });
            }
        }

        // 名单里没有自己 = 48 的数据不对，出人也没人喂输入，直接别出免得半个场景
        if (local == null || myBar == null)
        {
            Debug.LogError("[BattleEntry] 名单里没有自己（UID=" + myUid + "）→ 不出人，查 48 的 Members");
            return;
        }

        // 出生朝向 = 朝地图中心（对齐服务端建局的初始朝向）。服务端第一帧 44 也会下发朝向，这里只是别背对着人开局
        FaceCenter(local);
        foreach (BattleSync.Remote r in remotes) FaceCenter(r.Hero.gameObject);

        BattleSync sync = new GameObject("BattleSync").AddComponent<BattleSync>();
        sync.Init(local.GetComponent<PlayerMove>(), remotes, myBar);

        CameraFollow cam = Object.FindObjectOfType<CameraFollow>();
        if (cam != null) cam.target = local.transform;
    }

    // 有 48 名单就用名单；没有（编辑器直进战斗场景自测）就退回写死的两人，免得 play 进去是空场
    static List<BattleRoster.Member> BuildRoster()
    {
        if (BattleRoster.Members.Count > 0) return BattleRoster.Members;

        int oppId = Mathf.Clamp(MatchPanel.OpponentHeroId, 0, PrefabNames.Length - 1);
        // 昵称空 = 没走过 48（编辑器直进自测）→ 对手落回 edgar，与旧占位观感一致
        if (string.IsNullOrEmpty(MatchPanel.OpponentNickName)) oppId = 1;

        List<BattleRoster.Member> two = new List<BattleRoster.Member>();
        two.Add(new BattleRoster.Member { UID = PlayerInfo.Instance.UID, NickName = PlayerInfo.Instance.NickName, HeroId = Mathf.Clamp(MatchPanel.HeroId, 0, PrefabNames.Length - 1), X = s_FallbackX, Z = s_FallbackZ });
        // 兜底那位 UID 留空：没有服务端就没 44，反正它也不会动（只为了编辑器里能看到两个人）
        two.Add(new BattleRoster.Member { UID = "", NickName = "", HeroId = oppId, X = -s_FallbackX, Z = s_FallbackZ });
        return two;
    }

    // 实例化第 id 个英雄 → 摆到出生点（x/z 用名单的，y 保留 prefab 自带贴地值）→ 配置后激活
    static GameObject SpawnHero(int id, float x, float z, bool isSelf)
    {
        GameObject prefab = Resources.Load<GameObject>("PlayerPrefabs/" + PrefabNames[Mathf.Clamp(id, 0, PrefabNames.Length - 1)]);
        if (prefab == null)
        {
            Debug.LogError("[BattleEntry] 缺 Resources/PlayerPrefabs/" + PrefabNames[Mathf.Clamp(id, 0, PrefabNames.Length - 1)]);
            return null;
        }

        GameObject go = Object.Instantiate(prefab);
        go.name = isSelf ? "LocalHero" : "GuestHero";
        Transform t = go.transform;
        Vector3 p = t.position;
        p.x = x;
        p.z = z;
        t.position = p;

        MoveJoystick attack = FindAttackJoystick();
        PlayerMove pm = go.GetComponent<PlayerMove>();
        RangeIndicator ri = go.GetComponentInChildren<RangeIndicator>(true);

        if (isSelf)
        {
            // 不能直接 pm.attackJoy = attack：prefab 根是 active，Awake 在 Instantiate 已跑过（attackJoy 当时为空， OnRelease 订阅被跳过）→ 必须走 BindAttackJoystick 在赋值时补订
            if (pm != null && attack != null) pm.BindAttackJoystick(attack);
            if (ri == null)   // prefab 没带瞄准指示器 → 建空子节点挂（勿挂根：旋转会带动模型）
            {
                GameObject holder = new GameObject("AimRange");
                holder.transform.SetParent(t, false);
                ri = holder.AddComponent<RangeIndicator>();
            }
            if (pm != null) pm.heroIndex = id;   // 前摇时长从这查（出手后锁朝向用）
            ri.attackJoy = attack;
            ri.heroIndex = id;
            ri.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            go.AddComponent<GrassGhost>();   // 进草 X 光剪影。只给自己 —— 给别人挂了就是透视挂
        }
        else
        {
            if (pm != null) Object.Destroy(pm);
            if (ri != null) Object.Destroy(ri);
        }

        go.SetActive(true);
        return go;
    }

    // 头顶血条
    static PlayerState SpawnHpBar(GameObject hero, bool isOpponent)
    {
        GameObject prefab = Resources.Load<GameObject>("UIPrefabs/PlayerState");
        if (prefab == null)
        {
            Debug.LogError("[BattleEntry] 缺 Resources/UIPrefabs/PlayerState");
            return null;
        }
        PlayerState bar = Object.Instantiate(prefab).GetComponent<PlayerState>();
        if (bar == null)
        {
            Debug.LogError("[BattleEntry] PlayerState 预制体根上没挂 HpBar 脚本");
            return null;
        }
        bar.Init(hero.transform, isOpponent);
        return bar;
    }

    // 攻击摇杆
    static MoveJoystick FindAttackJoystick()
    {
        MoveJoystick fallback = null;
        foreach (MoveJoystick mj in Object.FindObjectsOfType<MoveJoystick>())
        {
            if (fallback == null) fallback = mj;
            if (mj.transform.name == "attackJoyStickBg") return mj;
        }
        return fallback;
    }

    static void FaceCenter(GameObject go)
    {
        float cx = MapCore.MinX + MapCore.Cols * 0.5f;
        float cz = MapCore.MaxZ - MapCore.Rows * 0.5f;
        Vector3 dir = new Vector3(cx - go.transform.position.x, 0f, cz - go.transform.position.z);
        if (dir.sqrMagnitude > 0.001f)
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}

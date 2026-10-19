using SimpleServer.Proto;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 挂 FinishCanvas 上（常驻 + 常激活）。FinishPanel 子物体平时 SetActive(false)，收到 45 才亮。
//
// ⚠ 别把本脚本挂到 FinishPanel 自己身上：面板 inactive → Start 不跑 → 45 的监听永远注册不上。
//   而 ProtoManager 分发是字典查表且没有 else 分支，收不到的协议是【静默丢弃】，一点日志都没有
public class FinishPanel : MonoBehaviour
{
    static readonly string[] HeroPrefabs = { "bibi", "edgar", "mari_geo", "spike" };

    public GameObject panel;         // 留空 = 用第一个子物体（FinishPanel）
    public Text ranking;             // 比分 / 名次
    public Button exitBtn;           // 回大厅

    public Camera heroCam;           // FinishHeroPreviewCamera：战斗期间不渲，出结算才开

    public Vector3 heroPos = new Vector3(0f, -1.6f, 14f);
    public float heroYaw = 180f;

    static FinishPanel s_Instance;   // 4 人先死的时候 BattleSync 要找回它报名次
    bool m_HeroShown;                // 英雄只出一个（面板可能先亮过一次）

    void Awake() { s_Instance = this; }

    void Start()
    {
        if (panel == null && transform.childCount > 0) panel = transform.GetChild(0).gameObject;
        if (panel != null) panel.SetActive(false);
        if (exitBtn != null) exitBtn.onClick.AddListener(Exit);
        AudioManager.BindClicks(transform);

        // FinishHeroPreviewCamera 只渲 layer 9 的结算英雄，战斗期间场上一个都没有 ——
        // 开着就是每帧白跑一趟（还带一个 RenderTexture）。等真要出人再开。
        if (heroCam == null) Debug.LogError("[FinishPanel] heroCam 没拖（FinishHeroPreviewCamera）→ 结算不出人");
        else heroCam.gameObject.SetActive(false);

        ProtoManager.AddBattleResultListener(OnResult);
    }

    // 跟着 FinishCanvas 一起随场景销毁。不摘的话切回大厅后这个委托还挂在 NetManager 上，
    // 指向一个已销毁的 MonoBehaviour
    void OnDestroy()
    {
        if (s_Instance == this) s_Instance = null;
        NetManager.Instance.RemoveProtoListener(ProtocolEnum.MsgBattleResult);
    }

    void OnResult(MsgBattleResult msg)
    {
        FreezeLocal();   // 两种模式都要停手，所以放在分流之前

        // 4 人混战：ranking 那格显示名次（不是比分）。死在前面时已经先亮过一次，这次刷成 45 的权威值
        if (msg.Mode == BattleRoster.ModeSolo4)
        {
            int rank = RankOfMe(msg);
            if (rank <= 0)
            {
                Debug.LogError("[FinishPanel] 4 人结算的 45 里没有自己（Entries 缺 UID）");
                return;
            }
            Show(RankText(rank), rank == 1);
            return;
        }

        bool win = msg.WinnerUID == PlayerInfo.Instance.UID;
        Show(string.Format("{0} : {1}",
                win ? msg.WinnerKills : msg.LoserKills,
                win ? msg.LoserKills : msg.WinnerKills), win);
    }

    // 4 人淘汰制：死在前面时先把名次亮出来（44 里数得到死掉几个）。1v1 会复活，不弹
    public static void ShowEliminated(int rank)
    {
        if (BattleRoster.Mode != BattleRoster.ModeSolo4) return;
        if (s_Instance != null) s_Instance.Show(RankText(rank), rank == 1);
    }

    // 面板只亮一次、英雄只出一个：4 人先死的那次已经出过人了，45 到了只换文字
    void Show(string score, bool win)
    {
        if (panel != null) panel.SetActive(true);
        if (ranking != null) ranking.text = score;
        // 胜负只靠英雄动画（Win/Lose）—— 没有胜负图，文字那格只放比分/名次

        if (m_HeroShown) return;
        m_HeroShown = true;
        // 跟 m_HeroShown 一起只做一次：4 人先死那次已经播过，45 到了只换文字不重播
        AudioManager.PlayBgm(win ? AudioManager.BgmWin : AudioManager.BgmLose);
        SpawnHero(win);
    }

    static string RankText(int rank) { return "第 " + rank + " 名"; }

    static int RankOfMe(MsgBattleResult msg)
    {
        if (msg.Entries == null) return 0;
        string me = PlayerInfo.Instance.UID;
        foreach (BattleResultEntry e in msg.Entries) if (e.UID == me) return e.Rank;
        return 0;
    }

    // 停手：44 断了以后服务端不再校正位置，本地还能推着摇杆满地图跑。
    // 顺带把 BattleSync 停了 —— 它每 50ms 还在发 43（服务端已 Finished，收了也白收）
    void FreezeLocal()
    {
        PlayerMove pm = FindObjectOfType<PlayerMove>();   // 对手那份在 BattleEntry 里 Destroy 了，场上只剩自己
        if (pm != null) pm.enabled = false;
        BattleSync sync = FindObjectOfType<BattleSync>();
        if (sync != null) sync.enabled = false;
    }

    // 自己那个英雄，按胜负播动画。层级/朝向在预制体里已经烘好了（layer 9 = FinishHeroPreview），
    // 由 FinishHeroPreviewCamera（cullingMask 只勾了这一层）渲进 characterShow 的 RenderTexture
    void SpawnHero(bool win)
    {
        int id = Mathf.Clamp(MatchPanel.HeroId, 0, HeroPrefabs.Length - 1);
        string name = HeroPrefabs[id];
        GameObject prefab = Resources.Load<GameObject>("FinishHeroPrefabs/" + name);
        if (prefab == null)
        {
            Debug.LogError("[FinishPanel] 缺 Resources/FinishHeroPrefabs/" + name);
            return;
        }

        GameObject go = Instantiate(prefab, heroPos, Quaternion.Euler(0f, heroYaw, 0f));
        // 开关走 GameObject，【不是】Camera.enabled：场景里这个物体本身是关着的（m_IsActive: 0），
        // 物体关着的时候给组件 enabled = true 什么都不会发生 —— 就是"结算没看到人"那次
        if (heroCam != null) heroCam.gameObject.SetActive(true);
        Play(go.GetComponentInChildren<Animator>(true), win ? "Win" : "Lose");
    }

    // bibi/edgar 的 controller 里，Win/Lose 的转移是【两个条件与】：
    //   Finish(Bool)==true  且  Win/Lose(Trigger) 触发
    // Finish 默认 false 而且没有别的地方设它 → 只 SetTrigger 的话转移永远够不着，
    // 人站在那儿一直播 Idle，看起来就是"没有结束动画"。所以两个都要设。
    static void Play(Animator a, string name)
    {
        if (a == null || a.runtimeAnimatorController == null) return;
        Set(a, "Finish", true);
        Set(a, name, true);
    }

    // 按参数实际类型调用：Bool 和 Trigger 都能播。
    // 参数不存在就静默跳过 —— mari_geo/spike 的 controller 一个参数都没有，
    // 直接 SetTrigger 会每局刷一条 Unity 报错
    static void Set(Animator a, string name, bool value)
    {
        foreach (AnimatorControllerParameter p in a.parameters)
        {
            if (p.name != name) continue;
            if (p.type == AnimatorControllerParameterType.Trigger) a.SetTrigger(name);
            else if (p.type == AnimatorControllerParameterType.Bool) a.SetBool(name, value);
            return;
        }
    }

    void Exit()
    {
        SceneManager.LoadScene("HallScene");
    }
}

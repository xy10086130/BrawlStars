using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 音频总入口。挂在 ServerRoot 那个物体上（ServerRoot.Awake 里 AddComponent）——
// ServerRoot 已经是全项目唯一的 DontDestroyOnLoad 物体，所以"BGM 跨场景不断"是白送的，
// 不要再自己 DontDestroyOnLoad 一次（多一个独立物体反而看不懂）。
//
// 素材：Assets/Resources/Audio/ 下。常量值可以带子目录（如 "character/bibi_swing_03"）——
//   Load() 拼的是 "Audio/" + name，斜杠天然吃得住，不用给子目录另开加载路径。
//
// ⚠ 每个静态方法都必须在 Instance == null 时静默返回：编辑器直接 Play BattleScene / HallScene
//   自测时场景里没有 ServerRoot（BattleEntry 有 RuntimeInitialize 兜底那条路），
//   不判空就会每次自测刷一屏 NRE，把真正的报错埋掉。
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    // 音量旋钮就这两个字段（本轮不做设置 UI）。每次播放都重新读，运行时在 Inspector 改了立刻生效
    public float BgmVolume = 0.5f;
    public float SfxVolume = 0.8f;

    // ── 打点表 ──────────────────────────────────────────────
    // 加新音：文件丢进 Assets/Resources/Audio/ → 这里加一行常量 + 加进 AllPaths
    public const string BgmHall      = "bolt_menu_01";        // 大厅（用户 2026-09-20 定；旧 HallBGM 留作备选）
    public const string BgmBattle    = "InBattleBGM";         // 战斗 BGM（循环）
    public const string BgmWin       = "win_Loop";            // 结算·胜（循环版，面板停留期间一直有）
    public const string BgmLose      = "lose_Loop";           // 结算·负
    public const string SfxBattleLoad = "brawl_fighting_load_01"; // 一进 BattleScene 放一下的开场音（不循环，BGM 是 InBattleBGM）
    // 倒计时音（countdown_01）用户 2026-09-20 听过后去掉 → 常量已删，文件留作备选
    public const string SfxGo        = "brawl_go_01";         // 开战横幅
    // 出手/命中音（tap_guns_02 / tap_normal_02）用户 2026-09-20 听过说"不匹配"去掉 → 常量已删，文件留作备选
    public const string SfxMenuClose = "menu_go_back_01";     // 面板关闭（面板打开不用音，用户 2026-09-20 去掉的）
    public const string SfxHeal      = "heal_loop_01";        // 回血（每回一格响一次）
    public const string SfxDeath     = "death_plop_01";       // 死亡
    // 常量名按【用途】取，不按文件取 —— 用户听着实机定的配对是"选英雄确认键 = PlayeBtn 那个文件、
    // 开始匹配 = select_brawler_01 那个文件"，跟文件名对不上，所以别拿名字猜文件、看后面的字符串
    public const string SfxSelect    = "PlayeBtn";            // 选英雄·确认键
    public const string SfxStart     = "select_brawler_01";   // 开始匹配（MainPanel 的 PlayBtn）
    public const string SfxNormalBtn = "normalBtn";           // 其余所有普通按钮（ButtonSfx 默认值）

    // ── 角色音（按英雄下标 0=bibi 1=edgar 2=mari_geo 3=spike）──
    // 命名是 <英雄>_<动作>_<序号>，序号是素材自带的，没规律可循 → 只能显式列表
    public const string BibiSwing    = "character/bibi_swing_03";
    public const string BibiImpact   = "character/bibibat_impact_01";
    public const string BibiStartVo  = "character/bibi_start_vo_05";
    public const string EdgarPunch   = "character/edgar_punch_01";
    public const string EdgarImpact  = "character/edgar_punch_impact_01";
    public const string EdgarStartVo = "character/edgar_start_vo_04";

    // 第二维下标 = HeroSfx 枚举值，别改动顺序
    public enum HeroSfx { Attack = 0, Hit = 1, StartVo = 2 }

    // 空位 = 这个英雄还没有音 → 静音跳过。**不要拿别的音凑**：用户 2026-09-20 听过
    // tap_guns_02 / tap_normal_02 说"不匹配"，已经删掉了
    static readonly string[,] HeroSfxTable =
    {
        //  出手        命中         开场语音
        { BibiSwing,   BibiImpact,  BibiStartVo  },   // 0 bibi
        { EdgarPunch,  EdgarImpact, EdgarStartVo },   // 1 edgar
        { null,        null,        null         },   // 2 mari_geo（还没音）
        { null,        null,        null         },   // 3 spike（还没音）
    };

    // Tools/检查音频资源 用它查漏。只列代码里真用到的
    public static readonly string[] AllPaths =
    {
        BgmHall, BgmBattle, BgmWin, BgmLose,
        SfxBattleLoad, SfxGo, SfxMenuClose, SfxSelect,
        SfxStart, SfxNormalBtn, SfxHeal, SfxDeath,
        BibiSwing, BibiImpact, BibiStartVo,
        EdgarPunch, EdgarImpact, EdgarStartVo,
    };
    // ────────────────────────────────────────────────────────

    AudioSource m_Bgm;
    AudioSource m_Sfx;
    AudioSource m_Heal;       // 回血专用：用户要求"不允许打断"（见 PlayHeal）
    string m_CurBgm;          // 正在播哪个，用来去重
    readonly Dictionary<string, AudioClip> m_Cache = new Dictionary<string, AudioClip>();

    void Awake()
    {
        Instance = this;

        m_Bgm = gameObject.AddComponent<AudioSource>();
        m_Bgm.playOnAwake = false;
        m_Bgm.loop = true;

        m_Sfx = gameObject.AddComponent<AudioSource>();
        m_Sfx.playOnAwake = false;
        m_Sfx.volume = 1f;    // 音量交给 PlayOneShot 的第二个参数给，这样 SfxVolume 改了立刻生效

        m_Heal = gameObject.AddComponent<AudioSource>();
        m_Heal.playOnAwake = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 同一个正在播就什么都不做 —— 否则每次回大厅 BGM 都从头重来一遍
    public static void PlayBgm(string name, bool loop = true)
    {
        if (Instance == null) return;
        Instance.PlayBgmInner(name, loop);
    }

    void PlayBgmInner(string name, bool loop)
    {
        if (m_CurBgm == name && m_Bgm.isPlaying) return;

        AudioClip clip = Load(name);
        if (clip == null) return;

        m_CurBgm = name;
        m_Bgm.clip = clip;
        m_Bgm.loop = loop;
        m_Bgm.volume = BgmVolume;
        m_Bgm.Play();
    }

    public static void StopBgm()
    {
        if (Instance == null) return;
        Instance.m_Bgm.Stop();
        Instance.m_CurBgm = null;
    }

    // 热更换包后，缓存里那些「包没到时回退到 Resources 拿的」clip 就过期了 —— 不清会一直放旧的
    public static void ClearCache()
    {
        if (Instance != null) Instance.m_Cache.Clear();
    }

    // volumeScale 是在 SfxVolume 上再乘一档。目前没人用（原打算给"对手出手小声一点"，
    // 但用户 2026-09-21 定的是**完全听不到对方的**，见 PlayHeroSfx）
    public static void PlaySfx(string name, float volumeScale = 1f)
    {
        if (Instance == null) return;

        AudioClip clip = Instance.Load(name);
        if (clip == null) return;

        Instance.m_Sfx.PlayOneShot(clip, Instance.SfxVolume * volumeScale);
    }

    // 角色音统一入口。**只给"我"播**：用户定的规则是"对方的音效我不应该听到"，
    // 出手/命中/开场语音一视同仁 —— 所以调用点只该出现在本地英雄身上，
    // 远端（RemoteHero）那条线一行都不加，别"顺手"补上。
    public static void PlayHeroSfx(int heroId, HeroSfx kind)
    {
        if (Instance == null) return;
        if (heroId < 0 || heroId >= HeroSfxTable.GetLength(0)) return;

        string name = HeroSfxTable[heroId, (int)kind];
        // 还没音的英雄静音跳过。必须在这挡掉：放过去会走 Load() 的"缺音频"红字（Load 找不到就 LogError）
        if (string.IsNullOrEmpty(name)) return;

        PlaySfx(name);
    }

    // 回血音：用户要求"不允许打断" → 走**专用源 + 主 clip**（不是 PlayOneShot）：
    //   ① 单独一个源，别的音效/BGM 怎么调都碰不到它；
    //   ② 用 clip + Play()（而不是 PlayOneShot）才好在"还在播"时判得准 ——
    //      PlayOneShot 是一次性 voice，isPlaying 不反映它；主 clip 的 isPlaying 是确定的。
    //   ③ 还在播就直接跳过这次：回血每 500ms 一格，clip 比这长的话会听成"被掐掉重来"。
    //      代价是慢一拍（下一次回血才补上），换来的是每一遍都能放完。
    public static void PlayHeal()
    {
        if (Instance == null) return;
        if (Instance.m_Heal.isPlaying) return;

        AudioClip clip = Instance.Load(SfxHeal);
        if (clip == null) return;

        Instance.m_Heal.clip = clip;
        Instance.m_Heal.volume = Instance.SfxVolume;
        Instance.m_Heal.Play();
    }

    // 给 root 下所有按钮挂普通点击音（ButtonSfx）。按 GetComponent 去重，可重复调用 ——
    // 面板会被缓存复用、OnOpen 跑多次，动态生成的行（好友列表/邀请列表）是 OnOpen 里才出现的，
    // 所以调用点要放在面板自己的 OnOpen 之后。已挂过的按钮不动（PlayBtn 就是先在 MainPanel 里
    // 显式挂成 PlayeBtn，再走这次批量，才不会被覆盖成 normalBtn）
    public static void BindClicks(Transform root)
    {
        if (root == null) return;
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
            if (b.GetComponent<ButtonSfx>() == null)
                b.gameObject.AddComponent<ButtonSfx>();
    }

    AudioClip Load(string name)
    {
        AudioClip clip;
        if (m_Cache.TryGetValue(name, out clip)) return clip;

        // 包优先（键 = 名字，跟图同一套）；没下包/包里没这名 → 回退 Resources 自带那份
        clip = HotUpdateManager.GetAsset<AudioClip>(name);
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null)
        {
            // 静默不出声最难查，报到能直接去捡的地方
            Debug.LogError("[AudioManager] 缺音频：" + name +
                           "\n应放在 Assets/Resources/Audio/" + name + ".ogg");
            return null;
        }

        m_Cache[name] = clip;
        return clip;
    }
}

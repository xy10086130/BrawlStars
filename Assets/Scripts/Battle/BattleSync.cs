using SimpleServer.Proto;
using System.Collections.Generic;
using UnityEngine;
public class BattleSync : MonoBehaviour
{
    public const float SendInterval = 0.05f;    // 20Hz，对齐服务端 TickMs
    public const float SnapDist = 1.5f;         // 瞬移阈值（服务端一步 0.25 格的 6 倍）
    public const float ConvergeSpeed = 4f;      // 静止时的收敛速度（格/秒），比走速慢，看不出来
    public const float StopThreshold = 0.05f;   // 静止误差
    public const float SilentWarnSec = 3f;      // 多少秒没收到 44 就报一次（诊断用）

    public static int SendCount;                // 43 发出条数
    public static int RecvCount;                // 44 收到条数

    public static int MySwings;
    public static int MyHits;

    // 别人（1v1 = 1 个，4 人 = 3 个）：模型 + 血条 + 上次看到的挥击序号
    public class Remote
    {
        public string UID;
        public RemoteHero Hero;
        public PlayerState Bar;
        public int AttackSeq;
    }

    PlayerMove m_Local;
    PlayerState m_MyBar;
    List<Remote> m_Remotes = new List<Remote>();
    float m_Timer;
    float m_SilenceTimer;   // 距上一条 44 的秒数
    float m_AliveTimer;     // "还活着"那行日志的间隔
    int m_Seq;
    bool m_Placed;
    bool m_Died;    // 已经报过死亡了（只在跳变时报一次）
    int m_MyAttackSeq;
    float m_LastMyHp = -1f;   // 回血音用：上一帧自己的血。-1 = 还没初始化（第一帧满血不算"涨"）
    bool m_LastAlive;         // 回血音用：复活那帧 Alive 由 false→true 且血猛涨，也得挡住

    public void Init(PlayerMove local, List<Remote> remotes, PlayerState myBar)
    {
        m_Local = local;
        m_MyBar = myBar;
        m_Remotes = remotes != null ? remotes : new List<Remote>();
        MySwings = MyHits = 0;
        m_LastMyHp = -1f;
        m_LastAlive = false;
        ProtoManager.AddBattleStateListener(OnBattleState);
    }

    void OnDestroy()
    {
        NetManager.Instance.RemoveProtoListener(ProtocolEnum.MsgBattleState);
    }

    void Update()
    {
        // 44 断流诊断（这一条就是为了抓"过一会就不能打、敌人也不动"）：
        //  还在发 43 却收不到 44 = 这条连接半开了（服务端同时会把你打成挂机，两边互相以为对面死了）
        //  两个计数器都不动了 = 客户端主线程卡住，跟网络无关
        m_SilenceTimer += Time.deltaTime;
        if (m_SilenceTimer >= SilentWarnSec && m_Local != null)
        {
            m_SilenceTimer = 0f;
            // 打墙上时间：Player.log 自己没有时间戳，出问题要跟服务端日志/别的机器对齐
            Debug.LogError("[" + System.DateTime.Now.ToString("HH:mm:ss.fff") + "][BattleSync] " + SilentWarnSec
                           + " 秒没收到 44：已发 " + SendCount + " 条输入、收到 " + RecvCount + " 条同步。"
                           + "（正常结算/掉线时也会打，看两个数还涨不涨）");
        }

        if (m_Local == null) return;

        // 每 5 秒一条"我还活着"（墙上时间 + 帧号）。三种故障靠这一行就能分开：
        //   这行停住、帧号也不涨          = 主线程卡死（跟网络无关，进程还在）
        //   发 N 一直涨、服务端日志却说你挂机 = 发包链断了（包堆在发送队列里没出去）
        //   发 N 涨、收 M 不涨            = 收包链断/半开（同时会刷上面那条 3 秒告警）
        m_AliveTimer += Time.deltaTime;
        if (m_AliveTimer >= 5f)
        {
            m_AliveTimer = 0f;
            Debug.Log("[BattleSync][" + System.DateTime.Now.ToString("HH:mm:ss.fff") + "] 发 " + SendCount
                      + " 收 " + RecvCount + " 帧 " + Time.frameCount);
        }

        m_Timer += Time.deltaTime;
        if (m_Timer < SendInterval) return;
        m_Timer -= SendInterval;

        Vector3 d = m_Local.MoveDir;
        Vector3 aim = m_Local.AimDir;

        int buttons = 0;
        Vector3 pulseAim;
        if (m_Local.ConsumeFirePulse(out pulseAim))
        {
            buttons = 1;
            if (pulseAim.sqrMagnitude > 0.01f) aim = pulseAim;
        }
        ProtoManager.CSBattleInput(++m_Seq, d.x, d.z, aim.x, aim.z, buttons);   // 停手也照发 (0,0)
        SendCount++;
    }

    void OnBattleState(MsgBattleState msg)
    {
        RecvCount++;
        m_SilenceTimer = 0f;
        string myUid = PlayerInfo.Instance.UID;
        BattleHeroState mine = Find(msg.Players, myUid);
        if (mine == null) return;   // 44 是每人一份（草丛/视野），没有自己那份就没得同步

        // 血量/存活先落（第一帧也要），再做位置
        Apply(mine, m_MyBar);

        // 回血音：服务端不发事件，自己那份 44 的 Hp 涨没涨就是权威判据
        //（用户定的原则：客户端能从已有权威数据推出来的，别加协议字段 —— 挨打要服务端发是因为打你的是别人）
        if (m_LastMyHp >= 0f && m_LastAlive && mine.Alive && mine.Hp > m_LastMyHp)
            AudioManager.PlayHeal();
        m_LastMyHp = mine.Hp;
        m_LastAlive = mine.Alive;
        foreach (Remote r in m_Remotes)
        {
            BattleHeroState s = Find(msg.Players, r.UID);
            if (s == null) continue;   // 出局的可能是没在广播里（服务端每人一份，不保证都在）
            Apply(s, r.Bar);
            // 草丛：看不见就把模型关了（血条在 Apply 里一起判）。
            // 死了也关 —— 4 人淘汰制不复活，留具会站桩的尸体在场上没意义（1v1 复活时真坐标一到就重新落位）
            if (r.Hero != null) r.Hero.SetVisible(s.CanSee && s.Alive);
        }
        LogHits(msg.Events);

        // 4 人淘汰制：死 = 直接看结算（不做观战）。1v1 会复活，FinishPanel 自己按模式拦掉
        if (mine.Alive) m_Died = false;
        else if (!m_Died)
        {
            m_Died = true;
            int dead = 0;
            foreach (BattleHeroState h in msg.Players) if (!h.Alive) dead++;
            FinishPanel.ShowEliminated(msg.Players.Count - dead + 1);
        }


        if (m_Local != null) m_Local.enabled = mine.Alive;

        if (!m_Placed)
        {
            // 第一帧：全员硬落位。位置来自 48 的出生点，客户端已按它摆过，这一下基本看不出来
            m_Placed = true;
            if (m_Local != null)
            {
                m_Local.ApplyServerPos(mine.X, mine.Z);
                m_Local.transform.rotation = Quaternion.Euler(0f, mine.Facing, 0f);
            }
            foreach (Remote r in m_Remotes)
            {
                BattleHeroState s = Find(msg.Players, r.UID);
                if (s == null || r.Hero == null) continue;
                r.Hero.SnapTo(s.X, s.Z, s.Facing, s.MoveMag);
                r.AttackSeq = s.AttackSeq;
            }
            m_MyAttackSeq = mine.AttackSeq;
            return;
        }


        if (m_Local != null)
        {
            if (mine.AttackSeq != m_MyAttackSeq)
            {
                MySwings += mine.AttackSeq - m_MyAttackSeq;
                m_MyAttackSeq = mine.AttackSeq;
                m_Local.PlayAttack();
                AudioManager.PlayHeroSfx(m_Local.heroIndex, AudioManager.HeroSfx.Attack);
            }
            CorrectLocal(mine.X, mine.Z);
        }
        foreach (Remote r in m_Remotes)
        {
            BattleHeroState s = Find(msg.Players, r.UID);
            if (s == null || r.Hero == null) continue;
            if (s.AttackSeq != r.AttackSeq)
            {
                r.AttackSeq = s.AttackSeq;
                r.Hero.PlayAttack();   // 只播动画，不播音：用户定的"对方的音效我不应该听到"（别顺手补上）
            }
            r.Hero.SetTarget(s.X, s.Z, s.Facing, s.MoveMag);
        }
    }

    static BattleHeroState Find(List<BattleHeroState> list, string uid)
    {
        foreach (BattleHeroState h in list) if (h.UID == uid) return h;
        return null;
    }

    static void Apply(BattleHeroState s, PlayerState bar)
    {
        if (s == null || bar == null) return;
        bar.SetHp(s.Hp, s.MaxHp);
        // 对手的弹药条也会跟着亮 —— 荒野乱斗里对手弹药是公开信息，保留。
        // 想藏起来就给 Apply 加个 isMe 参数，在这拦掉
        bar.SetAmmo(s.Ammo);
        // 活着但躲在草丛里 → 血条也不显示（SetAlive 就是显隐整条血条，正好复用）。
        //  只能在这里一起判：Apply 每帧无条件跑，漏了这句下一帧 44 的 SetAlive(true) 会把藏起来的血条顶回来
        bar.SetAlive(s.Alive && s.CanSee);
    }

    void LogHits(List<BattleHitEvent> events)
    {
        if (events == null) return;
        string myUid = PlayerInfo.Instance.UID;
        // 扇形/穿透一次打中好几个 → 命中事件是好几条，逐条播会叠成几倍音量，只响这次的第一下
        bool impactPlayed = false;
        foreach (BattleHitEvent e in events)
        {
            bool isMe = e.TargetUID == myUid;
            if (e.AttackerUID == myUid)
            {
                MyHits++;
                // 命中音只认"我打出去的"。对方打中我 = 对方的音效 → 不播（用户 2026-09-21 定）
                if (!impactPlayed && m_Local != null)
                {
                    impactPlayed = true;
                    AudioManager.PlayHeroSfx(m_Local.heroIndex, AudioManager.HeroSfx.Hit);
                }
            }
            if (e.Died) AudioManager.PlaySfx(AudioManager.SfxDeath);   // 谁死都响（死亡音只有击杀事件这一个来源）
            // 飘字从"被打那位"的血条往上飘（headY 读血条自己的数，别在这边再抄一份）
            PlayerState bar = BarOf(e.TargetUID);
            HitFeedback.Hit(e, isMe, bar != null ? bar.headY : 2.2f);
        }
    }

    PlayerState BarOf(string uid)
    {
        if (uid == PlayerInfo.Instance.UID) return m_MyBar;
        foreach (Remote r in m_Remotes) if (r.UID == uid) return r.Bar;
        return null;
    }


    void CorrectLocal(float ax, float az)
    {
        Vector3 p = m_Local.transform.position;
        float err = Mathf.Sqrt((ax - p.x) * (ax - p.x) + (az - p.z) * (az - p.z));
        if (err >= SnapDist) { m_Local.ApplyServerPos(ax, az); return; }
        if (m_Local.MoveDir.sqrMagnitude > 0.01f) return;
        if (err <= StopThreshold) return;
        m_Local.CorrectTo(ax, az, ConvergeSpeed * Time.deltaTime);
    }
}

using SimpleServer.Game;
using SimpleServer.Proto;
using System;

namespace SimpleServer
{
    // 假房间 + 假发送 + 自造时钟直接推 BattleInstance，验证权威移动 / 广播节奏 / 防作弊 / 挂机
    public static class BattleBotTest
    {
        // 一台虚拟"客户端"：自己走时钟，每 50ms 发一条 43，收到 44 就记账
        class Sim
        {
            public BattleInstance Battle;
            public int Now;
            public int Seq = 1;
            public int Sends;
            public MsgBattleState Last;
            public MsgBattleState MsgA, MsgB;   // 按收件人分开存：可见性是"对谁而言"的，只看 Last 会拿到另一个人那份
            public MsgBattleResult ResultA, ResultB;   // 45 按收件人各存一份
            public int ResultSends;
            public int BlockedSeen;

            public float AimX, AimZ;     // 每次发送都带上（不设 = 没在瞄准）
            public int Buttons;          // 位掩码：1=普攻
            public float BMoveX, BMoveZ; // B 的持续移动方向（0 = B 完全不动，一条包都不发）

            int m_NextSend;
            int m_NextSendB;
            int m_SeqB = 1;
            System.Collections.Generic.Dictionary<string, MsgBattleState> m_ByUID =
                new System.Collections.Generic.Dictionary<string, MsgBattleState>();

            public Sim() : this(0, 1) { }   // 默认 BIBI(0) vs Edgar(1)

            public Sim(int heroA, int heroB) : this(new int[] { heroA, heroB }, BattleModes.Duel1v1) { }

            public Sim(int heroA, int heroB, bool realSpawns) : this(new int[] { heroA, heroB }, BattleModes.Duel1v1, realSpawns) { }

            // realSpawns = false（默认）：照真流程抽完签再把坐标钉死成测试夹具，
            //    第 2~24 条的走位数字全是按"两个出生点 = (-5,2)/(5,2)、相距 10 格、同在 z=2"写死的，
            //    出生点改成随机之后不钉死，它们每跑一次测的位置都不一样（红得莫名其妙，还可能假绿）
            // realSpawns = true：真抽签（只有第 25 条出生点用例要）
            public Sim(int[] heroIds, int mode, bool realSpawns = false)
            {
                // 座位 UID 固定 A/B/C/D（OnSend 按它分收件人）
                RoomPlayer[] players = new RoomPlayer[heroIds.Length];
                for (int i = 0; i < heroIds.Length; i++)
                    players[i] = new RoomPlayer { UID = ((char)('A' + i)).ToString(), HeroId = heroIds[i] };
                SpawnTable.FillSpawns(players);
                if (!realSpawns)
                {
                    players[0].SpawnX = -5f; players[0].SpawnZ = 2f;
                    if (players.Length > 1) { players[1].SpawnX = 5f; players[1].SpawnZ = 2f; }
                    for (int i = 2; i < players.Length; i++) { players[i].SpawnX = ParkX; players[i].SpawnZ = ParkZ; }
                }
                Battle = new BattleInstance(new Room("t", players, mode), OnSend);
                // 默认关掉脱战回血：几何/节奏用例靠"Hp 差值 = 这一棒掉了多少"测命中，窗口动辄 1s 以上，
                // 回血会把差值搅掉。第 28 组自己要开回来
                Battle.RegenRatePct = 0f;
            }

            public BattleHero A { get { return Battle.Heroes[0]; } }
            public BattleHero B { get { return Battle.Heroes[1]; } }

            // 第 i 个座位 / 发给第 i 个座位的那份 44（4 人时挨个收件人查可见性）
            public BattleHero H(int i) { return Battle.Heroes[i]; }
            public MsgBattleState Seat(int i) { return m_ByUID[Battle.Heroes[i].UID]; }

            // 让第 i 个座位朝 (ax,az) 挥一下（直接推包，不走 Advance 那条 A 专用的通道）
            // ⚠ A 的持续包（Advance 里那串）会和这一发落在同一个 tick，瞄准字段不跟着设的话
            //    会被它盖成 0 → 出手不转身，扇形朝的还是出生朝向
            public void Hit(int i, float ax, float az)
            {
                if (i == 0) { AimX = ax; AimZ = az; }
                Battle.PushInput(Battle.Heroes[i].UID, new MsgBattleInput
                {
                    Seq = Seq++,
                    MoveX = 0f,
                    MoveZ = 0f,
                    AimX = ax,
                    AimZ = az,
                    Buttons = 1
                }, Now);
            }

            public const float ParkX = -5f, ParkZ = -25f;   // 离下面的战场够远（> 扇形 4+1 格）

            // 把 shooter 摆到 (x,z)、victim 摆到正东 1.5 格只剩 1 血，一棒打死
            // （y=z 平面上的空地由调用方保证；距离 1.5 在最短射程之内，中间也不可能有墙）
            public void Slay(int shooter, int victim, float x, float z)
            {
                // 围观的挪到远处再打：出生点是洗牌抽的，可能正好抽到 (x,z) 站在扇形里被顺手打死
                for (int i = 0; i < Battle.Heroes.Length; i++)
                    if (i != shooter && i != victim) { H(i).X = ParkX; H(i).Z = ParkZ; }

                H(shooter).X = x; H(shooter).Z = z; H(shooter).Alive = true;
                H(victim).X = x + 1.5f; H(victim).Z = z; H(victim).Alive = true; H(victim).Hp = 1;
                if (H(shooter).ShotCdMs > 0) Advance(H(shooter).ShotCdMs + 100, 0f, 0f);   // 连着宰几个时先等冷却
                Hit(shooter, 1f, 0f);
                Advance(HeroTable.Get(H(shooter).HeroId).WindupMs + 100, 0f, 0f);
            }

            void OnSend(string uid, MsgBase msg)
            {
                Sends++;
                // 45 和 44 走同一个发送口，先按类型分（硬转 MsgBattleState 会炸）
                MsgBattleResult res = msg as MsgBattleResult;
                if (res != null)
                {
                    ResultSends++;
                    if (uid == "A") ResultA = res;
                    if (uid == "B") ResultB = res;
                    return;
                }
                Last = (MsgBattleState)msg;
                if (uid == "A") MsgA = Last;
                if (uid == "B") MsgB = Last;
                m_ByUID[uid] = Last;
            }

            // 推进 ms 毫秒（5ms 粒度 + 收尾对齐到整毫秒，仿主循环）；每 50ms 发一条输入；方向传 null = 挂机不发
            public void Advance(int ms, float? dx, float? dz)
            {
                int target = Now + ms;
                for (; Now <= target; Now += 5)
                {
                    if (dx.HasValue && Now >= m_NextSend)
                    {
                        Battle.PushInput("A", new MsgBattleInput
                        {
                            Seq = Seq++,
                            MoveX = dx.Value,
                            MoveZ = dz.Value,
                            AimX = AimX,
                            AimZ = AimZ,
                            Buttons = Buttons
                        }, Now);
                        m_NextSend = Now + 50;
                    }
                    if ((BMoveX != 0f || BMoveZ != 0f) && Now >= m_NextSendB)
                    {
                        Battle.PushInput("B", new MsgBattleInput
                        {
                            Seq = m_SeqB++,
                            MoveX = BMoveX,
                            MoveZ = BMoveZ
                        }, Now);
                        m_NextSendB = Now + 50;
                    }
                    Battle.Tick(Now);

                    // 不变式：任何时刻角色都不能站在墙里（半径比真实值略小，避开浮点边界）
                    if (BattleMap.Blocked(A.X, A.Z, BattleInstance.Radius - 0.01f) ||
                        BattleMap.Blocked(Battle.Heroes[1].X, Battle.Heroes[1].Z, BattleInstance.Radius - 0.01f))
                        BlockedSeen++;
                }
                Now = target;
            }

            // A 的前摇 / 两击间隔（都按 A 自己的英雄查表 —— 前摇是英雄差异化的）
            public int Windup { get { return HeroTable.Get(A.HeroId).WindupMs; } }
            public int Interval { get { return HeroTable.Get(A.HeroId).AttackIntervalMs; } }

            // 推完前摇：伤害在这之后才落地（每次挥击后都要走一遍，否则断言的还是"前摇前"的血量）
            public void Land() { Advance(Windup + 100, 0f, 0f); }
        }

        static int fails;

        // 1v1 的击杀目标。⚠ 别当成通用值：4 人模式的 KillTarget 是 0（淘汰制，看剩几个人）
        static readonly int KillTarget = BattleModes.Get(BattleModes.Duel1v1).KillTarget;

        static void Check(bool ok, string what, object detail = null)
        {
            if (ok) Debug.Log("  [通过] " + what);
            else { fails++; Debug.LogError("  [失败] " + what + (detail == null ? "" : " → " + detail)); }
        }

        // 找一格草丛（格子中心）：正东 1~3 格**既无墙也无草**。
        //   无墙 = 挨打那条用例要让 B 从东边 3 格够得着（BIBI 扇形 4 格 + 身体 1）；
        //   无草 = "草外的人贴到 3 格也看不见"那条要 B 真的站在草外 —— 东边有草的话 B 自己也进草了，
        //          新判据下那条会变成"看得见"（旧判据下碰巧也能过），测的东西就跑了
        static bool FindBushWithClearEast(out float bx, out float bz)
        {
            for (int r = 0; r < MapCore.Rows; r++)
                for (int c = 0; c < MapCore.Cols; c++)
                {
                    float x = MapCore.MinX + c + 0.5f;
                    float z = MapCore.MaxZ - r - 0.5f;
                    if (!MapCore.InBush(x, z)) continue;
                    bool clear = true;
                    for (int d = 1; d <= 3; d++)
                        if (MapCore.Blocked(x + d, z, 0.01f) || MapCore.InBush(x + d, z)) { clear = false; break; }
                    if (!clear) continue;
                    bx = x;
                    bz = z;
                    return true;
                }
            bx = bz = 0f;
            return false;
        }

        // 在 (x,z) 的 maxDist 格内再找一格草：摆"双方都进草 → 互见"那一幕用。
        // 格心间距正好 1，所以 ±1 步长落到的就是邻格格心。找不到就让 Check 报出来 ——
        // 导出器按覆盖面积 ≥50% 落格，孤立的草块也是"十字 5 格"，正常不会没有邻居
        static bool FindBushNear(float x, float z, float maxDist, out float nx, out float nz)
        {
            for (float dz = -maxDist; dz <= maxDist; dz += 1f)
                for (float dx = -maxDist; dx <= maxDist; dx += 1f)
                {
                    if (dx == 0f && dz == 0f) continue;                  // 别把原来的格子自己当成"第二格"
                    if (dx * dx + dz * dz > maxDist * maxDist) continue;
                    if (!MapCore.InBush(x + dx, z + dz)) continue;
                    nx = x + dx;
                    nz = z + dz;
                    return true;
                }
            nx = nz = 0f;
            return false;
        }

        public static int Run()
        {
            Debug.Log("===== BattleInstance 自测 =====");
            BattleMap.EnsureLoaded();

            // 1 地图 & 出生点
            Check(MapCore.Rows > 0 && MapCore.Cols > 0, "地图已加载", MapCore.Rows + " 行 × " + MapCore.Cols + " 列");
            Check(SpawnTable.Count >= 2, "出生点文件读到了（Maps/spawns_1v1.txt，1v1 至少要 2 个）",
                "读到 " + SpawnTable.Count + " 个");
            // ⚠ 第 2~24 条的走位数字全是照"两个出生点相距 10 格、同在 z=2"写死的，
            //    所以 Sim 默认把座位钉在这两点上（见 Sim 构造）。这两点要是进了墙，那些用例会红得莫名其妙
            //    —— 这条替它们先红一次
            Check(!BattleMap.Blocked(-5f, 2f, BattleInstance.Radius) && !BattleMap.Blocked(5f, 2f, BattleInstance.Radius),
                "测试钉死的出生点 (-5,2)/(5,2) 不在墙里（第 2~24 条走位用例的前提）", "半径=" + BattleInstance.Radius);

            // 2 速度：200ms 直走 = 1 格（5 格/秒）
            Sim s1 = new Sim();
            s1.Advance(200, 1f, 0f);
            Check(Math.Abs(s1.A.X - (-5f + 1f)) < 0.0001f, "200ms 右移 1 格", "实际位移 " + (s1.A.X + 5f));
            Check(Math.Abs(s1.A.Facing - 90f) < 0.01f, "朝右走时朝向 = 90 度", "实际 " + s1.A.Facing);

            // 3 方向截断：超长方向不能加速（改包加速的堵口）
            Sim s3 = new Sim();
            s3.Advance(200, 10f, 0f);
            Check(Math.Abs(s3.A.X - (-5f + 1f)) < 0.0001f, "发 (10,0) 与 (1,0) 速度一致", "实际位移 " + (s3.A.X + 5f));

            // 4 一路走到地图右缘：不能穿墙、不能出界
            Sim s2 = new Sim();
            s2.Advance(20000, 1f, 0f);
            float edge = MapCore.MinX + MapCore.Cols;
            float stop = edge - BattleInstance.Radius;    // 理论最远：圆心离边界一个半径（还没算 0.25 步长的量化）
            Check(s2.A.X <= stop + 0.001f && s2.A.X > stop - 1.5f,
                "撞到地图右缘停下（圆心停在 半径 处，没穿出去）", "x=" + s2.A.X + " 理论上限=" + stop);
            Check(s2.BlockedSeen == 0, "全程没有站进墙里", "越界次数 " + s2.BlockedSeen);

            // 5 广播节奏：每 tick 一帧 44，每帧发双方
            Check(s2.Battle.TickCount == 400, "20s 正好 400 tick（20Hz 不漂）", "TickCount=" + s2.Battle.TickCount);
            Check(s2.Battle.BroadcastCount == 400, "每 tick 一帧 44（400 tick → 400 帧）", "帧数=" + s2.Battle.BroadcastCount);
            Check(s2.Sends == s2.Battle.BroadcastCount * 2, "每帧 44 发双方",
                "Sends=" + s2.Sends + " 帧数=" + s2.Battle.BroadcastCount);
            Check(s2.Last.Players.Count == 2 && s2.Last.Players[0].UID == "A" && s2.Last.Players[1].UID == "B",
                "44 带双方 uid");
            Check(Math.Abs(s2.Last.Players[0].X - s2.A.X) < 0.0001f, "44 里 A 的位置 = 权威位置");
            // 顶着墙按键：位置一动不动，但 MoveMag 仍是 1（远端播走动画只能靠它，不能靠"位置有没有变"）
            Check(s2.Last.Players[0].MoveMag > 0.99f, "位置被墙顶住时 MoveMag 仍为 1", "MoveMag=" + s2.Last.Players[0].MoveMag);

            // 6 挂机：>500ms 无输入 → 方向归零，原地不动（掉线不判负的那条规则）
            Sim s4 = new Sim();
            s4.Advance(1000, 1f, 0f);
            s4.Advance(2000, null, null);      // 头 450ms 会把手里的方向走完，之后停住
            float idleX = s4.A.X;
            s4.Advance(1000, null, null);
            Check(Math.Abs(s4.A.X - idleX) < 0.0001f, "挂机后原地不动", "又走了 " + (s4.A.X - idleX));
            Check(s4.Last.Players[0].MoveMag == 0f, "挂机后 MoveMag 归零（对方屏幕上停住）", "MoveMag=" + s4.Last.Players[0].MoveMag);

            // 7 序号防重放：序号回退的包丢弃
            Sim s5 = new Sim();
            s5.Battle.PushInput("A", new MsgBattleInput { Seq = 5, MoveX = 1f, MoveZ = 0f }, 0);
            s5.Battle.PushInput("A", new MsgBattleInput { Seq = 3, MoveX = -1f, MoveZ = 0f }, 0);   // 旧包，应被丢
            s5.Advance(200, null, null);
            Check(s5.A.X > -5f, "序号回退的包被丢弃（还往右走）", "x=" + s5.A.X);

            // 7b 序号没前进 ≠ 挂机：防重放丢掉的只是一条包，不能因此把人判成挂机
            //    （2026-09-24 四人联机全员冻住就是这个：LastInputMs 原来在 seq 判断后面才更新，
            //      只要有一条包被判重，这人就永久挂机 —— 后面包照到也没用，得等 seq 追上来）
            s5.Advance(1000, null, null);
            Check(s5.A.Idle, "停手 1 秒 → 确实挂机了（这条的前提）", "idle=" + s5.A.Idle);
            s5.Battle.PushInput("A", new MsgBattleInput { Seq = 3, MoveX = 1f, MoveZ = 0f }, s5.Now);
            s5.Advance(50, null, null);        // 下一 tick 才会把 Idle 清掉
            Check(!s5.A.Idle, "收到序号没前进的包也算人还在，不该判挂机", "idle=" + s5.A.Idle);

            // 7c 开局时钟对齐：LastInputMs 默认 0，而真服务端的 nowMs 是启动起算的几十万毫秒 ——
            //    不对齐的话第一 tick 全员被 500ms 判成挂机，开局那一下攻击还会被 UpdateIdle 清掉（打空）
            //    （夹具默认从 Now=0 起，永远碰不到这个坑，所以这里手动把时钟拨到"服务端已经跑了一阵子"）
            Sim s6 = new Sim();
            s6.Now = 1000000;
            s6.Advance(50, null, null);
            Check(!s6.A.Idle, "刚开局还没收到过输入 → 不判挂机", "idle=" + s6.A.Idle);

            // 8 数值表：必须等于 CharacterInfo.json（导出漏了/过期了这里会红）
            HeroInfo bibi = HeroTable.Get(0), edgar = HeroTable.Get(1);
            Check(bibi.Hp == 5000 && bibi.Attack == 1400 && bibi.Shape == "fan" && bibi.Range == 4f && bibi.Size == 75f,
                "BIBI 数值来自配置（5000血/1400伤/fan 4格/半角75°）",
                bibi.Hp + "/" + bibi.Attack + "/" + bibi.Shape + " " + bibi.Range + "/" + bibi.Size);
            Check(edgar.Hp == 2800 && edgar.Attack == 1080 && edgar.Shape == "rect" && edgar.Range == 3.3f && edgar.Size == 1f,
                "Edgar 数值来自配置（2800血/1080伤/rect 3.3格/半宽1）",
                edgar.Hp + "/" + edgar.Attack + "/" + edgar.Shape + " " + edgar.Range + "/" + edgar.Size);

            // 9 扇形：正前命中 + 冷却节奏 + 打死
            Sim c1 = new Sim();                       // BIBI(0) 西 vs Edgar(1) 东
            Check(c1.A.Hp == 5000 && c1.A.MaxHp == 5000 && c1.B.MaxHp == 2800, "开局血量 = 各自配置");
            c1.Advance(1600, 1f, 0f);                 // 走到距对手 2 格
            int eHp = c1.B.Hp;
            int seq0 = c1.A.AttackSeq;
            c1.AimX = 1f; c1.Buttons = 1;             // 朝东挥棒
            c1.Advance(50, 0f, 0f);
            Check(c1.A.AttackSeq == seq0 + 1, "挥击 AttackSeq +1（远端靠它播动画）", "AttackSeq=" + c1.A.AttackSeq);
            Check(c1.B.Hp == eHp, "前摇没走完不掉血（动画正在挥）", "掉了 " + (eHp - c1.B.Hp));
            c1.Buttons = 0;
            c1.Land();                                // 走完前摇（Land = 前摇 + 100ms）
            Check(c1.B.Hp == eHp - 1400, "BIBI 扇形正前命中：掉 1400", "掉了 " + (eHp - c1.B.Hp));

            // 冷却 850ms（间隔被 tick 的 50ms 量化，实际第 17 个 tick 才归零 = 850ms）
            // 距上一击 50 + Land(375) + 300 = 725ms 时按下也打不出。
            // ⚠ 这里必须真的按 Buttons：不按的话 AttackSeq 本来就不会变，断言等于什么都没测（改前就是这样）
            c1.Advance(300, 0f, 0f);
            c1.Buttons = 1;
            c1.Advance(50, 0f, 0f);                   // 775ms < 850ms
            c1.Buttons = 0;
            Check(c1.A.AttackSeq == seq0 + 1, "两击间隔没到打不出第二击", "AttackSeq=" + c1.A.AttackSeq);
            c1.Advance(500, 0f, 0f);                  // 累计 1275ms ≥ 850ms
            c1.Buttons = 1;
            c1.Advance(50, 0f, 0f);
            Check(c1.A.AttackSeq == seq0 + 2, "间距到了能出第二击", "AttackSeq=" + c1.A.AttackSeq);
            c1.Buttons = 0;
            c1.Land();
            Check(c1.B.Hp == 0 && !c1.B.Alive, "血量归零 → 死亡", "Hp=" + c1.B.Hp + " Alive=" + c1.B.Alive);

            // 10 死亡期间：不再被命中（血量不为负），3000ms 后回出生点满血
            c1.Buttons = 0;
            c1.Advance(1000, 0f, 0f);
            Check(c1.B.Hp == 0, "死亡期间停手，血量不为负", "Hp=" + c1.B.Hp);
            c1.Advance(2500, 0f, 0f);
            Check(c1.B.Alive && c1.B.Hp == 2800, "3000ms 后复活满血", "Alive=" + c1.B.Alive + " Hp=" + c1.B.Hp);
            Check(Math.Abs(c1.B.X - 5f) < 0.0001f && Math.Abs(c1.B.Z - 2f) < 0.0001f,
                "复活回出生点", "(" + c1.B.X + "," + c1.B.Z + ")");

            // 11 扇形边界：偏 180° 不中。半角 75° 看着很宽，但判定还要再加"身体补偿角" asin(1/距离)：
            //    2 格处 = 75+30 = 105° 之内都算中，所以必须瞄背后才测得出"够不着"
            Sim c2 = new Sim();
            c2.Advance(1600, 1f, 0f);
            c2.AimX = -1f; c2.Buttons = 1;            // 对手在正东，瞄正西 = 偏 180°
            int hp2 = c2.B.Hp, seq2 = c2.A.AttackSeq;
            c2.Advance(50, 0f, 0f);
            c2.Buttons = 0;
            c2.Land();
            Check(c2.A.AttackSeq == seq2 + 1 && c2.B.Hp == hp2, "BIBI 扇形：偏 180° 挥到了也不中", "掉了 " + (hp2 - c2.B.Hp));

            // 12 扇形边界：判定区 = 画的扇形 再外扩一个身体半径（Radius 1），所以圆心要 5 格（射程 4 + 身体 1）才算够不着
            Sim c3 = new Sim();
            c3.Advance(1100, 1f, 0f);                 // 走 5.5 格 → 距 4.5 格
            c3.AimX = 1f; c3.Buttons = 1;
            int hp3 = c3.B.Hp;
            c3.Advance(50, 0f, 0f);
            c3.Buttons = 0;
            c3.Land();
            // 圆心在射程 4 外，但它 1 格宽的身体压进扇形里了 —— 看着打到就该掉血
            Check(c3.B.Hp == hp3 - 1400, "BIBI 扇形：圆心 4.5 格（射程外）但身体压着扇边，命中", "掉了 " + (hp3 - c3.B.Hp));

            c3.Advance(1200, 0f, 0f);                 // 等冷却（50+375+1200 = 1625ms > 850ms，绰绰有余）
            c3.Advance(200, -1f, 0f);                 // 退 1 格 → 距 5.5 格（走出身体半径）
            c3.AimX = 1f; c3.Buttons = 1;
            int hp3b = c3.B.Hp, seq3 = c3.A.AttackSeq;
            c3.Advance(50, 0f, 0f);
            c3.Buttons = 0;
            c3.Land();
            Check(c3.A.AttackSeq == seq3 + 1 && c3.B.Hp == hp3b, "BIBI 扇形：5.5 格（射程 4 + 身体 1 之外）不中", "掉了 " + (hp3b - c3.B.Hp));

            // 13 矩形：正前命中 → 偏出半宽不中 → 偏一点点仍中
            //   站位 4 格：横向容差 = 半宽 1 + 身体 1 = 2 格，4 格处偏 60° 横向 3.46 才真的出界（2 格处怎么偏都在里面）
            Sim c4 = new Sim(1, 0);                   // Edgar(1) 西 vs BIBI(0) 东
            c4.Advance(1200, 1f, 0f);                 // 走 6 格 → 距 4 格
            int hp4 = c4.B.Hp;
            c4.AimX = 1f; c4.Buttons = 1;
            c4.Advance(50, 0f, 0f);
            c4.Buttons = 0;
            c4.Land();
            Check(c4.B.Hp == hp4 - 1080, "Edgar 矩形正前命中：掉 1080", "掉了 " + (hp4 - c4.B.Hp));

            c4.Advance(1000, 0f, 0f);                 // 等冷却（出手50 + Land250 + 1000 = 1300 > 670）
            c4.AimX = (float)Math.Cos(60 * Math.PI / 180); c4.AimZ = (float)Math.Sin(60 * Math.PI / 180);
            c4.Buttons = 1;                           // 偏 60° → 横向 4*sin60 = 3.46 格 > 半宽 1 + 身体 1
            int seq4 = c4.A.AttackSeq;
            c4.Advance(50, 0f, 0f);
            c4.Buttons = 0;
            c4.Land();
            Check(c4.A.AttackSeq == seq4 + 1 && c4.B.Hp == hp4 - 1080,
                "Edgar 矩形：横向偏 3.46 格不中（半宽 1 + 身体 1）", "掉了 " + (hp4 - c4.B.Hp));

            c4.Advance(1000, 0f, 0f);                 // 等冷却
            c4.AimX = (float)Math.Cos(10 * Math.PI / 180); c4.AimZ = (float)Math.Sin(10 * Math.PI / 180);
            c4.Buttons = 1;                           // 偏 10° → 横向 0.69 格 < 1，纵向 3.94 < 3.3+1
            c4.Advance(50, 0f, 0f);
            c4.Buttons = 0;
            c4.Land();
            Check(c4.B.Hp == hp4 - 2160, "Edgar 矩形：横向偏 0.69 格命中，累计掉 2160", "掉了 " + (hp4 - c4.B.Hp));

            // 14 矩形边界：正前方要 3.3 + 身体 1 = 4.3 格才算够不着（半宽在这里用不上：正前方没有横向偏移）
            Sim c5 = new Sim(1, 0);
            c5.Advance(1100, 1f, 0f);                 // 距 4.5 格
            c5.AimX = 1f; c5.Buttons = 1;
            int hp5 = c5.B.Hp, seq5 = c5.A.AttackSeq;
            c5.Advance(50, 0f, 0f);
            c5.Buttons = 0;
            c5.Land();
            Check(c5.A.AttackSeq == seq5 + 1 && c5.B.Hp == hp5, "Edgar 矩形：4.5 格（射程 3.3 + 身体 1 之外）不中", "掉了 " + (hp5 - c5.B.Hp));

            // 15 隔墙不中（用射程 8 的 Mari，位置直接摆，免得绕路）
            float wx, wz;
            Check(FindWallWithClearSides(out wx, out wz), "地图里找得到一格左右各 3 格空地的墙");
            Sim c6 = new Sim(2, 0);                   // Mari(2)：rect 8格 / 半宽 0.4 / 480伤
            c6.A.X = wx - 2f; c6.A.Z = wz;
            c6.B.X = wx + 2f; c6.B.Z = wz;
            c6.AimX = 1f; c6.Buttons = 1;
            int hp6 = c6.B.Hp;
            c6.Advance(50, 0f, 0f);
            c6.Buttons = 0;
            c6.Land();
            Check(c6.B.Hp == hp6, "隔墙不中（4 格距离，中间隔着墙格）", "掉了 " + (hp6 - c6.B.Hp));

            c6.Advance(400, 0f, 0f);                  // 等冷却（380+400 = 780 > 630）
            c6.B.X = wx - 1f;                         // 挪到墙前：同一方向、距离 1 格、中间无墙
            c6.Buttons = 1;
            c6.Advance(50, 0f, 0f);
            c6.Buttons = 0;
            c6.Land();
            Check(c6.B.Hp == hp6 - 480, "同一位置挪到墙前就命中（证明上一发是被墙挡的）", "掉了 " + (hp6 - c6.B.Hp));

            // 16 朝向：只跟移动方向；**瞄准本身不转身**，出手那一下才转过去
            //    （只转一边就会"自己屏幕朝一边、对手屏幕朝另一边"）
            Sim c7 = new Sim();
            c7.Advance(200, 1f, 0f);
            Check(Math.Abs(c7.A.Facing - 90f) < 0.01f, "朝向跟移动方向（90°）", "Facing=" + c7.A.Facing);
            c7.AimZ = 1f;                             // 瞄正北，但没出手
            c7.Advance(50, 0f, 0f);
            Check(Math.Abs(c7.A.Facing - 90f) < 0.01f, "只瞄准不出手 → 不转身（还是朝东 90°）", "Facing=" + c7.A.Facing);
            c7.Buttons = 1;                           // 松手出手
            c7.Advance(50, 0f, 0f);
            Check(Math.Abs(c7.A.Facing) < 0.01f, "出手那一下才转向瞄准（北 = 0°）", "Facing=" + c7.A.Facing);

            // 17 出手是瞬时事件：脉冲包立刻被"下一包"的 Buttons=0 追平，这一挥也不能丢
            //    （客户端 50ms 发包 / 服务端 50ms tick，相位不对齐时会落进同一个 tick 窗口）
            Sim c8 = new Sim();
            c8.Advance(1600, 1f, 0f);                 // 走到距对手 2 格
            int hp8 = c8.B.Hp;
            c8.Battle.PushInput("A", new MsgBattleInput { Seq = c8.Seq++, MoveX = 0f, MoveZ = 0f, AimX = 1f, Buttons = 1 }, c8.Now);
            c8.Battle.PushInput("A", new MsgBattleInput { Seq = c8.Seq++, MoveX = 0f, MoveZ = 0f, AimX = 1f, Buttons = 0 }, c8.Now);
            c8.Advance(50, null, null);               // 不发新包，只让 tick 跑（锁存由 tick 消费）
            Check(c8.A.AttackSeq == 1, "松手脉冲被后一包 Buttons=0 追平也不丢（锁存生效）", "AttackSeq=" + c8.A.AttackSeq);
            c8.Advance(c8.Windup + 100, null, null);  // 再让 tick 跑完前摇
            Check(c8.B.Hp == hp8 - 1400, "锁存的那一击照样扣血", "掉了 " + (hp8 - c8.B.Hp));

            // 18 冷却没到就松手 → 丢弃、不排队（换掉 Step 第 4 步的清零就是"排队"语义，这里锁死当前选择）
            int hp9 = c8.B.Hp, seq9 = c8.A.AttackSeq;
            c8.Advance(100, 0f, 0f);                  // 距上一击 50+375+100 = 525ms，间隔 850ms 还没到
            c8.Buttons = 1;
            c8.Advance(50, 0f, 0f);
            c8.Buttons = 0;
            Check(c8.A.AttackSeq == seq9, "冷却中的松手被丢弃（不排队、不延后补发）", "AttackSeq " + seq9 + "→" + c8.A.AttackSeq);
            c8.Land();
            Check(c8.B.Hp == hp9, "被丢弃的那一下过了前摇也不会突然冒出来", "hp 掉了 " + (hp9 - c8.B.Hp));

            // 19 前摇的实质：伤害按【结算那一刻】的位置算 → 前摇里跑开就是真的躲掉了
            //    （这是把前摇放服务端而不是客户端做视觉延迟的唯一理由）
            Sim c9 = new Sim();
            c9.Advance(1200, 1f, 0f);                 // 走到距对手 4 格（不能从 2 格起：前摇 275ms 只够跑 1.375 格，出不了 射程4+身体1）
            int hp19 = c9.B.Hp;
            c9.AimX = 1f; c9.Buttons = 1;
            c9.Advance(50, 0f, 0f);                   // 出手提交
            c9.Buttons = 0;
            c9.BMoveX = 1f;                           // 对方立刻往东跑（前摇 275ms，够跑 1.375 格 → 4 变 5.375 格，刚出 射程4+身体1）
            c9.Advance(c9.Windup - 100, 0f, 0f);
            Check(c9.B.Hp == hp19, "前摇没走完不掉血（动画还在挥）", "掉了 " + (hp19 - c9.B.Hp));
            c9.Advance(200, 0f, 0f);                  // 前摇到点结算，此时对方已经跑出射程
            Check(c9.B.Hp == hp19, "前摇里跑开 → 这一棒落空（伤害按结算时的位置算）", "掉了 " + (hp19 - c9.B.Hp));

            // 20 前摇 / 间隔是每个英雄自己的（差异化的那两列真的读进来了）
            Check(bibi.WindupMs == 275 && bibi.AttackIntervalMs == 850,
                "BIBI 前摇/间隔来自配置（275/850 = 动画 2 倍速）", bibi.WindupMs + "/" + bibi.AttackIntervalMs);
            Check(edgar.WindupMs == 150 && edgar.AttackIntervalMs == 670,
                "Edgar 前摇/间隔来自配置（150/670 = 动画 2 倍速）", edgar.WindupMs + "/" + edgar.AttackIntervalMs);

            // 21 草丛隐身：进草看不见 → 双方都进草(距离内) / 开火 / 挨打 才现形
            float bx, bz;
            Check(FindBushWithClearEast(out bx, out bz), "地图里找得到一格草丛（正东 3 格无墙无草）",
                "(" + bx + "," + bz + ")；没有 F 就先跑 Unity 菜单 Tools/导出 1v1 地图");
            Check(!MapCore.InBush(-5f, 2f) && !MapCore.InBush(5f, 2f), "双方出生点不在草丛里");

            Sim c10 = new Sim(2, 0);                  // Mari(2, 射程 8) vs BIBI(0)
            c10.Battle.PushInput("A", new MsgBattleInput { Seq = c10.Seq++, MoveX = 0f, MoveZ = 0f }, c10.Now);
            c10.Battle.PushInput("B", new MsgBattleInput { Seq = c10.Seq++, MoveX = 0f, MoveZ = 0f }, c10.Now);
            c10.Advance(100, null, null);
            Check(!c10.A.InBush && c10.MsgB.Players[0].CanSee, "站草外：对手看得见 A");
            float seenX = c10.A.X, seenZ = c10.A.Z;   // 出生点

            c10.A.X = bx; c10.A.Z = bz;               // 直接摆进草丛（走路已被前面几个用例盖住，这里只测判定）
            c10.Advance(100, null, null);
            Check(c10.A.InBush, "进草：InBush = true", "(" + bx + "," + bz + ")");
            Check(!c10.MsgB.Players[0].CanSee, "进草：对手看不见 A");
            // 隐身期间发的是"最后一次被看见的位置"，不是真坐标 —— 改包也定位不到人
            Check(Math.Abs(c10.MsgB.Players[0].X - seenX) < 0.0001f && Math.Abs(c10.MsgB.Players[0].Z - seenZ) < 0.0001f,
                "隐身时发旧坐标（真坐标不上线）",
                "发的 (" + c10.MsgB.Players[0].X + "," + c10.MsgB.Players[0].Z + ") 真坐标 (" + bx + "," + bz + ")");
            Check(c10.MsgA.Players[0].CanSee && Math.Abs(c10.MsgA.Players[0].X - bx) < 0.0001f,
                "自己那份永远看得见自己、坐标是真的");

            // 草外的人贴身也不现形（2026-09-14 改：距离项只在"对方也在草里"时才参与判定）
            c10.B.X = bx + BattleInstance.RevealDist; c10.B.Z = bz;   // 摆到旧判据的边界上（当年是 <=，正卡线上）
            c10.Advance(100, null, null);
            Check(!c10.B.InBush, "B 站在草外（助手保证正东 3 格无草）");
            Check(!c10.MsgB.Players[0].CanSee, "草外的人贴到 " + BattleInstance.RevealDist + " 格：仍然看不见");

            // 双方都进草 + 相距 RevealDist 以内 → 互见（这是本次改动新加的那条规则）
            float b2x, b2z;
            Check(FindBushNear(bx, bz, BattleInstance.RevealDist, out b2x, out b2z),
                "草格 " + BattleInstance.RevealDist + " 格内找得到第二格草（这条要用）", "(" + bx + "," + bz + ")");
            c10.B.X = b2x; c10.B.Z = b2z;
            c10.Advance(100, null, null);
            Check(c10.B.InBush, "B 也进了草", "(" + b2x + "," + b2z + ")");
            Check(c10.MsgB.Players[0].CanSee, "双方都进草且相距 " + BattleInstance.RevealDist + " 格内：A 现形");
            Check(c10.MsgA.Players[1].CanSee, "反向同样成立（两端对等，不是单向）");

            c10.B.X = 5f; c10.B.Z = 2f;               // 出草 → 重新隐身
            c10.Advance(100, null, null);
            Check(!c10.MsgB.Players[0].CanSee, "对手出草：重新隐身");

            c10.Battle.PushInput("A", new MsgBattleInput { Seq = c10.Seq++, MoveX = 0f, MoveZ = 0f, AimX = 1f, Buttons = 1 }, c10.Now);
            c10.Advance(100, null, null);
            Check(c10.MsgB.Players[0].CanSee, "草里开火：现形");

            c10.Advance(BattleInstance.RevealMs + 300, null, null);
            Check(!c10.MsgB.Players[0].CanSee, "开火暴露 " + BattleInstance.RevealMs + "ms 后重新隐身");

            // 挨打也现形：B 从正东 3 格朝草里挥（BIBI 扇形 4 格 + 身体 1，够得着）
            c10.B.X = bx + 3f; c10.B.Z = bz;
            c10.Battle.PushInput("B", new MsgBattleInput { Seq = c10.Seq++, MoveX = 0f, MoveZ = 0f, AimX = -1f, Buttons = 1 }, c10.Now);
            c10.Advance(800, null, null);              // 走完前摇（BIBI 275ms）才结算伤害，留足余量
            Check(c10.A.Hp < c10.A.MaxHp && c10.MsgB.Players[0].CanSee, "草里挨打：掉血且现形",
                "Hp=" + c10.A.Hp + "/" + c10.A.MaxHp + " CanSee=" + c10.MsgB.Players[0].CanSee);

            // 22 出手锁朝向：前摇里行走不许把朝向拽走（客户端 PlayerMove.faceLock 是同一条规则）
            Sim c11 = new Sim();
            c11.Advance(100, null, null);
            c11.Battle.PushInput("A", new MsgBattleInput { Seq = c11.Seq++, MoveX = 0f, MoveZ = 0f, AimX = 1f, AimZ = 0f, Buttons = 1 }, c11.Now);
            c11.Advance(50, null, null);
            Check(Math.Abs(c11.A.Facing - 90f) < 0.01f, "出手：朝向 = 瞄准方向（东 90°）", c11.A.Facing.ToString());

            // 出手后一直往北走（北 = 0°）：锁朝向时长 = Interval（整段动画），这段时间里都不该被拽走
            c11.Advance(c11.Interval / 2, 0f, 1f);
            Check(Math.Abs(c11.A.Facing - 90f) < 0.01f, "攻击动画里行走不打断出手朝向", c11.A.Facing.ToString());

            c11.Advance(c11.Interval + 100, 0f, 1f);   // 累计 > Interval，锁过期、行走接管
            Check(Math.Abs(c11.A.Facing) < 0.01f, "动画播完行走接管朝向（北 0°）", c11.A.Facing.ToString());

            // 23 结算：先到 KillTarget 杀的一方胜 → Finished + 双方各收一份 45 + 之后不再广播 44
            Sim c12 = new Sim();
            c12.Advance(1600, 1f, 0f);                // 走到距 B 2 格（c1 已经验证过这条路线是通的）
            for (int k = 0; k < KillTarget; k++)
            {
                // 把 B 拉回 A 正东 2 格并复活（跳过 3 秒复活等待：那件事第 10 条已经测过了）
                c12.B.X = c12.A.X + 2f; c12.B.Z = c12.A.Z;
                c12.B.Alive = true; c12.B.RespawnMs = 0; c12.B.Hp = 1;
                c12.AimX = 1f; c12.Buttons = 1;
                c12.Advance(50, 0f, 0f);
                c12.Buttons = 0;
                c12.Advance(c12.Interval + 100, 0f, 0f);   // 走完前摇，顺带过掉冷却
            }
            Check(c12.Battle.Finished && c12.A.Kills == KillTarget,
                "打满 " + KillTarget + " 杀 → Finished", "Kills=" + c12.A.Kills + " Finished=" + c12.Battle.Finished);
            Check(c12.ResultSends == 2 && c12.ResultA != null && c12.ResultB != null,
                "45 双方各一份", "发出 " + c12.ResultSends + " 份");
            Check(c12.ResultA != null && c12.ResultA.WinnerUID == "A"
                  && c12.ResultA.WinnerKills == KillTarget && c12.ResultA.LoserKills == 0,
                "45 内容：胜者 A，" + KillTarget + " : 0",
                c12.ResultA == null ? "没收到" : c12.ResultA.WinnerUID + " " + c12.ResultA.WinnerKills + ":" + c12.ResultA.LoserKills);
            Check(c12.ResultA != null && ReferenceEquals(c12.ResultA, c12.ResultB), "双方收到同一份内容（同一次 45）");

            int bc = c12.Battle.BroadcastCount, sc = c12.Sends;
            c12.Advance(1000, 0f, 0f);
            Check(c12.Battle.BroadcastCount == bc && c12.Sends == sc,
                "结算后不再广播 44（房间该被主循环回收了）", "又发了 " + (c12.Sends - sc) + " 条");

            // 24 弹药装填：3 格满 / 打空后卡的是装填不是冷却 / 进度保留
            HeroInfo bi = HeroTable.Get(0);
            Check(bi.ReloadMsPerShell > 0, "heroes.txt 第 9 列 reloadMsPerShell 读进来了（>0，要当除数）", bi.ReloadMsPerShell);
            Check(bi.ReloadMsPerShell > bi.AttackIntervalMs,
                "装填比冷却慢 → 连打的瓶颈是装填（(b) 方案的前提）",
                "装填 " + bi.ReloadMsPerShell + " vs 冷却 " + bi.AttackIntervalMs);

            Sim a1 = new Sim();
            a1.Advance(100, null, null);
            Check(a1.A.Ammo == BattleInstance.AmmoMax && a1.MsgA.Players[0].Ammo == BattleInstance.AmmoMax,
                "开局 " + BattleInstance.AmmoMax + " 格满，且 44 把 Ammo 带出来了（浮点原值）",
                "服务端 " + a1.A.Ammo + " / 协议 " + a1.MsgA.Players[0].Ammo);

            // 只剩 1 格：直接摆，不去凑前面那几发的账（那个数依赖冷却和装填的比例，改数值就飘）
            a1.A.Ammo = 1f;
            a1.AimX = 1f;
            int seqA = a1.A.AttackSeq;
            a1.Buttons = 1;
            a1.Advance(50, 0f, 0f);
            a1.Buttons = 0;
            Check(a1.A.AttackSeq == seqA + 1 && a1.A.Ammo < 1f,
                "剩 1 格打得出去，打完 Ammo < 1", "AttackSeq +" + (a1.A.AttackSeq - seqA) + " Ammo=" + a1.A.Ammo);

            // ⚠ 必须先把冷却等过去，否则拦这一发的是冷却、这条就又变成"测冷却"了（第 9 条的老毛病）
            a1.Advance(a1.Interval + 100, 0f, 0f);
            Check(a1.A.ShotCdMs <= 0 && a1.A.Ammo < 1f,
                "冷却已过但弹药还没满一格（这条的前提）", "ShotCdMs=" + a1.A.ShotCdMs + " Ammo=" + a1.A.Ammo);
            int seqB = a1.A.AttackSeq;
            a1.Buttons = 1;
            a1.Advance(50, 0f, 0f);
            a1.Buttons = 0;
            Check(a1.A.AttackSeq == seqB, "弹药不足：冷却早过了也打不出（卡的是装填）",
                "AttackSeq " + seqB + "→" + a1.A.AttackSeq + " Ammo=" + a1.A.Ammo);

            // 等够一格的装填量 → 又能打
            a1.Advance(bi.ReloadMsPerShell, 0f, 0f);
            Check(a1.A.Ammo >= 1f, "等够 ReloadMsPerShell 后 Ammo 回到 ≥1", a1.A.Ammo);
            a1.Buttons = 1;
            a1.Advance(50, 0f, 0f);
            a1.Buttons = 0;
            Check(a1.A.AttackSeq == seqB + 1, "装满一格后又打得出了", "AttackSeq " + seqB + "→" + a1.A.AttackSeq);

            // 进度保留：1.5 打一发 → 0.5（剩的那半格往前挪一格）+ 本 tick 装填的那一点。
            // 不是 0 也不是 1 —— 做成 3 个独立格 + 3 个计时器的话，这条必错
            Sim a2 = new Sim();
            a2.Advance(100, null, null);
            a2.A.Ammo = 1.5f;
            a2.AimX = 1f;
            a2.Buttons = 1;
            a2.Advance(50, 0f, 0f);
            a2.Buttons = 0;
            float keep = 0.5f + BattleInstance.TickMs / (float)bi.ReloadMsPerShell;
            Check(Math.Abs(a2.A.Ammo - keep) < 0.01f,
                "进度保留：1.5 打一发 → ≈" + keep.ToString("0.###") + "（不是 0 也不是 1）", a2.A.Ammo);

            // 死人不装填 / 复活整条回满（跟冷却一样，复活是"清账"不是"接着算"）
            a2.A.Ammo = 0.2f;
            a2.A.Alive = false;
            a2.A.RespawnMs = 200;
            a2.Advance(100, null, null);
            Check(a2.A.Ammo == 0.2f, "死亡期间不装填", a2.A.Ammo);
            a2.Advance(300, null, null);
            Check(a2.A.Alive && a2.A.Ammo == BattleInstance.AmmoMax,
                "复活弹药整条回满", "Alive=" + a2.A.Alive + " Ammo=" + a2.A.Ammo);

            // 25 四人模式：人数 / 出生点（文件里的点都能站 + 抽签不重复、不卡墙、朝地图中心）
            Sim q = new Sim(new int[] { 0, 1, 2, 0 }, BattleModes.Solo4);
            Check(q.Battle.Heroes.Length == 4, "4 人模式建出 4 个座位", "实际 " + q.Battle.Heroes.Length);
            Check(SpawnTable.Count >= 4, "出生点文件至少 4 个（4 人模式要抽 4 个不重复的）", "读到 " + SpawnTable.Count + " 个");

            // 文件侧：每个点都得能站人（抽签是随机的，抽查 4 个不够 —— 剩下的点 4 人局里也会被抽到）
            bool spawnInWall = false, spawnInBush = false;
            for (int i = 0; i < SpawnTable.Count; i++)
            {
                float px, pz;
                SpawnTable.Get(i, out px, out pz);
                if (BattleMap.Blocked(px, pz, BattleInstance.Radius - 0.01f)) spawnInWall = true;
                if (BattleMap.InBush(px, pz)) spawnInBush = true;   // 出生在草里 = 开局就隐身
            }
            Check(!spawnInWall, "出生点文件里每个点都不卡墙（卡了就去 Unity 里挪点）");
            Check(!spawnInBush, "出生点文件里每个点都不在草丛里");

            // 抽签侧：多跑几轮（4 人 + 1v1），每轮的点必须都在文件里、互不重复、朝地图中心
            string drawWhy = "";
            for (int round = 0; round < 12 && drawWhy == ""; round++)
            {
                drawWhy = SpawnDrawProblem(new Sim(new int[] { 0, 1, 2, 0 }, BattleModes.Solo4, true), 4);
                if (drawWhy == "") drawWhy = SpawnDrawProblem(new Sim(0, 1, true), 2);
            }
            Check(drawWhy == "", "抽签 24 轮：抽到的点都在文件里、互不重复、朝地图中心", drawWhy);

            // 26 淘汰制：死了不复活 / 只剩一个才结束 / 名次按死亡顺序倒序
            Check(BattleModes.Get(BattleModes.Solo4).KillTarget == 0 && !BattleModes.Get(BattleModes.Solo4).Respawn,
                "4 人模式：不按击杀数判负、不复活");
            q.Slay(0, 2, -5f, 2f);                    // C 第一个死（-5,2 那段是第 2~14 条走通的空地）
            Check(!q.H(2).Alive && !q.Battle.Finished, "死一个、还剩 3 个活人 → 没结束");
            q.Advance(4000, 0f, 0f);                  // 跨过 1v1 的 3000ms 复活时长
            Check(!q.H(2).Alive, "淘汰制：等过 3000ms 也不复活", "Alive=" + q.H(2).Alive);
            q.Slay(0, 3, -5f, 2f);                    // D 第二个死
            Check(!q.Battle.Finished, "还剩 2 个活人 → 还没结束");
            q.Slay(0, 1, -5f, 2f);                    // B 最后死 → 只剩 A
            Check(q.Battle.Finished && q.H(0).Alive, "只剩 1 个活人 → 结束",
                "Finished=" + q.Battle.Finished + " 活着的=" + q.H(0).Alive);
            Check(q.ResultSends == 4, "45 发给 4 个人", "发出 " + q.ResultSends + " 份");
            Check(q.ResultA != null && q.ResultA.Mode == BattleModes.Solo4, "45 带模式 = 4 人");
            Check(q.ResultA != null && q.ResultA.WinnerUID == "A", "45 胜者 = 唯一活着的那个");
            Check(RankOf(q.ResultA, "A") == 1 && RankOf(q.ResultA, "B") == 2
                  && RankOf(q.ResultA, "D") == 3 && RankOf(q.ResultA, "C") == 4,
                "名次 = 死亡顺序倒序（活着的第 1、最后死的第 2、最早死的垫底）",
                "A" + RankOf(q.ResultA, "A") + " B" + RankOf(q.ResultA, "B")
                + " D" + RankOf(q.ResultA, "D") + " C" + RankOf(q.ResultA, "C"));

            // 27 草丛可见性是"逐人一份"的（4 人时一张 [观察者,目标] 矩阵）
            Sim q2 = new Sim(new int[] { 0, 1, 2, 0 }, BattleModes.Solo4);
            float sx, sz, sx2, sz2;
            SpawnTable.Get(0, out sx, out sz);
            SpawnTable.Get(1, out sx2, out sz2);
            q2.H(0).X = sx; q2.H(0).Z = sz;           // 两个出生点第 21 条验过不在草里
            q2.H(1).X = sx2; q2.H(1).Z = sz2;
            q2.H(3).X = sx2; q2.H(3).Z = sz2;         // 站明处待一帧 → "最后被看见的位置"有确定值
            q2.Advance(100, null, null);

            FindBushWithClearEast(out bx, out bz);
            q2.H(3).X = bx; q2.H(3).Z = bz;           // 进草
            q2.Advance(100, null, null);
            Check(!q2.Seat(0).Players[3].CanSee, "4 人：第 4 个进草 → 1 号看不见他");
            Check(q2.Seat(0).Players[1].CanSee, "同一份 44 里没进草的 2 号照常可见（可见性逐人算，不是整条瞎了）");
            Check(Math.Abs(q2.Seat(0).Players[3].X - sx2) < 0.0001f,
                "隐身时发的是最后被看见的位置，真坐标不上线", "发的 " + q2.Seat(0).Players[3].X + " 真坐标 " + bx);
            Check(q2.Seat(3).Players[3].CanSee && Math.Abs(q2.Seat(3).Players[3].X - bx) < 0.0001f,
                "他自己那份永远看得见自己、坐标是真的");

            // 28 脱战回血：命中/挨打才进战斗，脱战满 2s **且** 弹药满格才回，到点**一次给足一格**
            Sim r = new Sim();                        // A=BIBI(0) 在西、B=Edgar(1) 在东
            r.Battle.RegenRatePct = BattleInstance.RegenPctPerSec;   // 夹具默认关着，这一组要真值
            r.H(0).X = -5f; r.H(0).Z = 2f;
            r.H(1).X = -3.5f; r.H(1).Z = 2f;          // B 在 A 正东 1.5 格（Slay 同款摆位：中间没墙、够得着）
            r.Advance(100, 0f, 0f);
            r.Hit(0, 1f, 0f);                         // A 朝东一棒 → 打中 B
            r.Advance(400, 0f, 0f);                   // 等前摇结算

            Check(r.H(0).AttackSeq == 1, "A 的第一棒挥出去了（AttackSeq=1）", "AttackSeq=" + r.H(0).AttackSeq);
            Check(r.H(1).Hp < r.H(1).MaxHp, "B 挨了一棒掉了血（本组全部用例的前提）", "Hp=" + r.H(1).Hp);
            Check(r.H(0).LastCombatTick > 0 && r.H(0).LastCombatTick == r.H(1).LastCombatTick,
                "命中方和挨打方都被盖了战斗戳（同一 tick、非 0）",
                "A=" + r.H(0).LastCombatTick + " B=" + r.H(1).LastCombatTick);

            int delayTicks = BattleInstance.RegenDelayMs / BattleInstance.TickMs;
            int since = r.Battle.TickCount - r.H(1).LastCombatTick;
            Check(since < delayTicks, "挨打到此刻还没到回血点（本组用例的前提）", "已过 " + since + " tick");

            int hp0 = r.H(1).Hp;
            r.Advance(Math.Max(0, delayTicks - since - 2) * BattleInstance.TickMs, 0f, 0f);
            Check(r.H(1).Hp == hp0, "脱战不足 " + BattleInstance.RegenDelayMs + "ms → 一点不回",
                "Hp " + hp0 + " → " + r.H(1).Hp);

            r.Advance(300, 0f, 0f);
            Check(r.H(1).Hp > hp0, "脱战满 " + BattleInstance.RegenDelayMs + "ms → 开始回血",
                "Hp " + hp0 + " → " + r.H(1).Hp);

            // 一次给足一格，不是每 tick 滴一点：非节拍 tick 纹丝不动，走满一个节拍正好跳一格整
            int chunkHp = (int)(r.H(1).MaxHp * BattleInstance.RegenPctPerSec * BattleInstance.RegenChunkMs / 100000f);
            int hp1 = r.H(1).Hp;
            r.Advance(BattleInstance.TickMs, 0f, 0f);
            Check(r.H(1).Hp == hp1, "节拍之外的 tick 不回血（一次一格，不是一点一点滴）",
                "Hp " + hp1 + " → " + r.H(1).Hp);
            r.Advance(BattleInstance.RegenChunkMs - BattleInstance.TickMs, 0f, 0f);
            Check(r.H(1).Hp == hp1 + chunkHp, "走满一个节拍正好跳 " + chunkHp + " 血（Hp 的 " + BattleInstance.RegenChunkMs + "ms 为一格）",
                "Hp " + hp1 + " → " + r.H(1).Hp);


            int reload = HeroTable.Get(r.H(1).HeroId).ReloadMsPerShell;
            r.H(1).Ammo = 2f;                         // 手动打掉一格（直接摆字段，跟别处 Hp=1 一个路数）
            hp0 = r.H(1).Hp;
            r.Advance(reload / 2, 0f, 0f);            // 半格装填时间补不满 → 全程不该涨
                                                      //Check(r.H(1).Hp == hp0, "弹药不满 → 回血断（条件是每 tick 现判的，不是"开始过就一直回"）",
                                                      //  "Hp " + hp0 + " → " + r.H(1).Hp);

            r.Advance(reload, 0f, 0f);                // 前一半补满格，后一半该接着回
            Check(r.H(1).Hp > hp0, "补满弹接着回，不重新等那 1 秒", "Hp " + hp0 + " → " + r.H(1).Hp);

            r.H(1).Hp = r.H(1).MaxHp;
            r.Advance(300, 0f, 0f);
            Check(r.H(1).Hp == r.H(1).MaxHp, "满血不回（也不溢出）", "Hp=" + r.H(1).Hp);

            r.H(1).Alive = false; r.H(1).Hp = 500; r.H(1).RespawnMs = BattleInstance.RespawnMs;
            r.Advance(1500, 0f, 0f);
            Check(r.H(1).Hp == 500 && !r.H(1).Alive, "死人不回血（复活时长没到，别把复活满血混进来）",
                "Hp=" + r.H(1).Hp + " Alive=" + r.H(1).Alive);

            // 挥空不进战斗 —— 前提是这一棒真挥出去了，否则下面那条是空过
            r.H(1).X = Sim.ParkX; r.H(1).Z = Sim.ParkZ;   // B 挪到 27 格外，这一棒打不到任何人
            // ⚠ 别用 seqA 这名字：上面弹药组（:593）已经有个同名的，本组在它作用域里，
            //    这行一旦被注释掉，下面的断言会静默地拿那个过期变量去比（踩过）
            int seqW = r.H(0).AttackSeq;
            int aCombat = r.H(0).LastCombatTick;
            r.Hit(0, 0f, 1f);                             // A 朝北空挥
            r.Advance(400, 0f, 0f);
            Check(r.H(0).AttackSeq == seqW + 1, "这一棒确实挥出去了（不然下一条是空过）",
                "AttackSeq " + seqW + " → " + r.H(0).AttackSeq);
            Check(r.H(0).LastCombatTick == aCombat, "挥空不进战斗 → 脱战计时不动",
                "戳 " + aCombat + " → " + r.H(0).LastCombatTick);

            Debug.Log(fails == 0 ? "===== 全部通过 =====" : "===== 失败 " + fails + " 项 =====");
            return fails == 0 ? 0 : 1;
        }

        // 一轮抽签的自检：n 个座位的出生点必须都在文件里、互不重复、朝向地图中心。返回 "" = 没问题
        static string SpawnDrawProblem(Sim s, int n)
        {
            float centerX = MapCore.MinX + MapCore.Cols * 0.5f, centerZ = MapCore.MaxZ - MapCore.Rows * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = s.H(i).X, z = s.H(i).Z;
                if (SpawnIndexOf(x, z) < 0) return "座位 " + i + " 抽到 (" + x + "," + z + ")，不在文件里";
                if (BattleMap.Blocked(x, z, BattleInstance.Radius - 0.01f)) return "座位 " + i + " 卡墙";
                double f = s.H(i).Facing * Math.PI / 180.0;   // 朝向 (sin,cos) 与"出生点 → 地图中心"同向（点积 > 0）
                if (Math.Sin(f) * (centerX - x) + Math.Cos(f) * (centerZ - z) <= 0.0) return "座位 " + i + " 背对地图中心";
                for (int j = 0; j < i; j++)
                    if (Math.Abs(x - s.H(j).X) < 0.001f && Math.Abs(z - s.H(j).Z) < 0.001f)
                        return "座位 " + i + " 和 " + j + " 抽到同一点 (" + x + "," + z + ")";
            }
            return "";
        }

        // 坐标是不是出生点文件里的某个点（-1 = 不是）
        static int SpawnIndexOf(float x, float z)
        {
            for (int i = 0; i < SpawnTable.Count; i++)
            {
                float px, pz;
                SpawnTable.Get(i, out px, out pz);
                if (Math.Abs(px - x) < 0.001f && Math.Abs(pz - z) < 0.001f) return i;
            }
            return -1;
        }

        // 45 名次表里某人第几名（找不到 = -1）
        static int RankOf(MsgBattleResult r, string uid)
        {
            if (r == null || r.Entries == null) return -1;
            foreach (BattleResultEntry e in r.Entries)
                if (e.UID == uid) return e.Rank;
            return -1;
        }

        // 找一格墙，且左右各 3 格都是空地（保证测试摆位不会自己踩进墙里，也不会被旁边的墙干扰）
        static bool FindWallWithClearSides(out float wx, out float wz)
        {
            for (float z = MapCore.MaxZ - 0.5f; z > MapCore.MaxZ - MapCore.Rows; z -= 1f)
                for (float x = MapCore.MinX + 0.5f; x < MapCore.MinX + MapCore.Cols; x += 1f)
                {
                    if (!MapCore.Blocked(x, z, 0.01f)) continue;
                    bool clear = true;
                    for (int d = -3; d <= 3 && clear; d++)
                        if (d != 0 && MapCore.Blocked(x + d, z, 0.01f)) clear = false;
                    if (!clear) continue;
                    wx = x;
                    wz = z;
                    return true;
                }
            wx = wz = 0f;
            return false;
        }
    }
}

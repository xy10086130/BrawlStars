using SimpleServer.Proto;
using System;
using System.Collections.Generic;

namespace SimpleServer.Game
{
    public class BattleHero
    {
        public string UID;
        public int HeroId;
        public float X, Z;
        public float Facing;        // 朝向角度（度）= atan2(dirX, dirZ)
        public float MoveX, MoveZ;  // 最新意图方向
        public float MoveMag;
        public float AimX, AimZ;    // 最新瞄准方向
        public int Buttons;         // 位掩码：1=普攻
        public bool AttackPressed;  // 普攻锁存：收到过 Buttons&1 → 下一个 tick 消费掉
        public int Hp, MaxHp;
        public bool Alive = true;
        public int Kills;
        public int RespawnMs;       // >0 = 正在等复活
        public int AttackSeq;       // 每次挥击 +1
        public int ShotCdMs;        // 攻击冷却
        public float Ammo;
        public int FacingLockUntilTick;   // 出手到这个 tick 为止，行走不许抢朝向
        public int LastSeq;         // 防重放：序号 ≤ 上一条的直接丢
        public int LastInputMs;     // 上次收到输入的毫秒时刻（挂机判定）
        public bool Idle;           // 已转入挂机。
        public float SpawnX, SpawnZ;
        public bool InBush;         // 本 tick 站位在不在草里
        public int RevealUntilTick; // 暴露到这一 tick 为止
        public int DeathOrder;      // 第几个死的（1 起，0 = 没死过）。名次按它倒序，最后死的第 2 名
        public int LastCombatTick;  // 最后一次"命中别人 / 被别人命中"的 tick，脱战回血从它算起
        // 隐身期间的"最后一次被看见的位置"不再是英雄自己的字段：4 人时每个人看见的位置不同，
        // 一份存不下 —— 挪到 BattleInstance 的 [观察者,目标] 矩阵里
    }

    public class BattleInstance
    {
        public const int TickMs = 50;            // 20Hz
        public const int StateEveryTicks = 1;
        public const float Speed = 5f;           // 格/秒
        public const float Radius = 1f;

        public const int InputTimeoutMs = 500;
        public const int RespawnMs = 3000;       // 死亡到复活

        public const int AmmoMax = 3;

        public const float HitPad = Radius;

        public const float RevealDist = 4f;      // 格
        public const int RevealMs = 1000;

        public const int RegenDelayMs = 2000;    // 脱战多久开始回血
        public const int RegenChunkMs = 500;     // 回血节拍：每 500ms 一次，**一次给足一格**（不是每 tick 滴一点）
        public const float RegenPctPerSec = 8f;  // 折算下来每秒回最大血量的百分之几（正式值）
                                                 // ⚠ 按百分比不按定值：四个英雄血从 2400 到 5000，定值必有一个手感崩。
                                                 // ⚠ 一次 = MaxHp × Pct × ChunkMs / 100000（5000→200、2800→112、2400→96 整数）
                                                 // ⚠ 嫌回得多/少就只动 PctPerSec，改完重编译重启服务端
                                                 // 运行时值，0 = 关闭。自测夹具默认调 0：那批几何用例靠"Hp 差值 = 这一棒掉了多少"测命中，
                                                 // 量血窗口动辄 1s 以上，回血会把差值搅掉 —— 那是夹具的事，不是玩法的事
        public float RegenRatePct = RegenPctPerSec;

        class PendingHit
        {
            public BattleHero Attacker;
            public HeroInfo Info;
            public float Facing;    // 出手瞬间锁定的方向：前摇里人会走、朝向会跟着变，结算得用当时那一版
            public int DueTick;
        }
        readonly List<PendingHit> m_Pending = new List<PendingHit>();


        public BattleHero[] Heroes;
        public bool Finished;
        public int TickCount { get; private set; }
        public int BroadcastCount { get; private set; }

        Action<string, MsgBase> m_Send;
        ModeInfo m_Mode;
        int m_LastTickMs = -1;
        int m_LastSeqWarnMs = -1000;                        // "序号没前进"那条告警的限流（-1000 = 开局就能打第一条）
        int m_DeathOrder;                                   // 已死人数，死一个 +1（名次全靠它）
        List<BattleHitEvent> m_Events = new List<BattleHitEvent>();

        // [观察者, 目标] —— 4 人时"谁能看见谁"是一张矩阵，一个人一份存不下
        bool[,] m_CanSee;
        float[,] m_LastSeenX, m_LastSeenZ;

        public BattleInstance(Room room, Action<string, MsgBase> send)
        {
            m_Send = send;
            m_Mode = BattleModes.Get(room.Mode);

            int n = room.Players.Length;
            Heroes = new BattleHero[n];
            m_CanSee = new bool[n, n];
            m_LastSeenX = new float[n, n];
            m_LastSeenZ = new float[n, n];

            BattleMap.EnsureLoaded();
            string roster = "";
            for (int i = 0; i < n; i++)
            {
                // 出生点/朝向都来自建房时抽好的签（RoomPlayer），这边只读
                RoomPlayer p = room.Players[i];
                HeroInfo info = HeroTable.Get(p.HeroId);
                Heroes[i] = new BattleHero
                {
                    UID = p.UID,
                    HeroId = p.HeroId,
                    X = p.SpawnX,
                    Z = p.SpawnZ,
                    SpawnX = p.SpawnX,
                    SpawnZ = p.SpawnZ,
                    Facing = Angle(CenterX - p.SpawnX, CenterZ - p.SpawnZ),
                    Hp = info.Hp,
                    MaxHp = info.Hp,
                    Ammo = AmmoMax
                };
                m_LastSeenX[i, i] = p.SpawnX;
                m_LastSeenZ[i, i] = p.SpawnZ;
                roster += string.Format("{0}(英雄{1} {2}血) ", p.UID, p.HeroId, info.Hp);
            }
            Debug.Log("战斗开始：模式{0} {1}人 —— {2}", room.Mode, n, roster);
        }

        // 地图中心
        static float CenterX { get { return MapCore.MinX + MapCore.Cols * 0.5f; } }
        static float CenterZ { get { return MapCore.MaxZ - MapCore.Rows * 0.5f; } }

        public int IndexOf(string uid)
        {
            for (int i = 0; i < Heroes.Length; i++)
                if (Heroes[i].UID == uid) return i;
            return -1;
        }

        public void PushInput(string uid, MsgBattleInput msg, int nowMs)
        {
            int i = IndexOf(uid);
            if (i < 0) return;
            BattleHero h = Heroes[i];
            // "包到了"先落，再判重放：防重放丢的只是一条包，不能把人一直判成挂机。
            // （这行原来在 seq 判断后面 —— 只要有一次序号没前进，这人就永久挂机，后面包照到也没用）
            h.LastInputMs = nowMs;
            if (msg.Seq <= h.LastSeq)
            {
                // 限流打一条：真出问题时这是 20Hz × N 人的刷屏源
                if (nowMs - m_LastSeqWarnMs > 1000)
                {
                    m_LastSeqWarnMs = nowMs;
                    Debug.LogWarn("玩家{0} 输入序号没前进（收到 {1} ≤ 已记 {2}），本包丢弃", uid, msg.Seq, h.LastSeq);
                }
                return;
            }
            h.LastSeq = msg.Seq;

            // 意图是"单位方向"；长度顺带当移动强度回传（客户端播走动画用）
            float len = (float)Math.Sqrt(msg.MoveX * msg.MoveX + msg.MoveZ * msg.MoveZ);
            if (float.IsNaN(len) || float.IsInfinity(len)) { h.MoveX = 0f; h.MoveZ = 0f; h.MoveMag = 0f; }
            else if (len > 1f) { h.MoveX = msg.MoveX / len; h.MoveZ = msg.MoveZ / len; h.MoveMag = 1f; }
            else { h.MoveX = msg.MoveX; h.MoveZ = msg.MoveZ; h.MoveMag = len; }

            // 瞄准同理（长度 >1 截断防改包加速，NaN 归零防脏值传进 atan2）
            float aimLen = (float)Math.Sqrt(msg.AimX * msg.AimX + msg.AimZ * msg.AimZ);
            if (float.IsNaN(aimLen) || float.IsInfinity(aimLen)) { h.AimX = 0f; h.AimZ = 0f; }
            else if (aimLen > 1f) { h.AimX = msg.AimX / aimLen; h.AimZ = msg.AimZ / aimLen; }
            else { h.AimX = msg.AimX; h.AimZ = msg.AimZ; }

            h.Buttons = msg.Buttons;
            // 普攻是"松手那一下"的瞬时事件，不是按住状态 → 锁存，等 tick 消费
            if ((msg.Buttons & 1) != 0) h.AttackPressed = true;
        }


        public void Tick(int nowMs)
        {
            if (Finished) return;
            if (m_LastTickMs < 0)                                          // 首次只对表
            {
                m_LastTickMs = nowMs;
                // 顺带把"上次收到输入"对齐到开局这一刻：LastInputMs 默认 0，而 nowMs 是服务端启动起算的
                // 毫秒（开局时已经几十万），不对齐的话第一 tick 全员被 500ms 判成挂机 ——
                // 表现就是开局 500ms 内发的第一下攻击被 UpdateIdle 把 AttackPressed 清掉（打空）
                for (int i = 0; i < Heroes.Length; i++) Heroes[i].LastInputMs = nowMs;
                return;
            }
            if (nowMs - m_LastTickMs < TickMs) return;
            if (nowMs - m_LastTickMs > TickMs * 5) m_LastTickMs = nowMs;   // 卡顿后不连跑几十 tick（防瞬移）
            while (nowMs - m_LastTickMs >= TickMs)
            {
                Step(nowMs);
                m_LastTickMs += TickMs;
                TickCount++;
            }
        }

        void Step(int nowMs)
        {
            if (Finished) return;

            for (int i = 0; i < Heroes.Length; i++)
            {
                BattleHero h = Heroes[i];
                HeroInfo info = HeroTable.Get(h.HeroId);

                UpdateIdle(h, nowMs);
                UpdateMove(h);
                TryAttack(h, info);
                UpdateTimers(h, info);
                UpdateRegen(h);          // 排在装填后面：这一 tick 弹满没满，得用刚装填完的值
                UpdateRespawn(h);
            }

            ResolvePending();

            if (TickCount % StateEveryTicks == 0)
                Broadcast();

            CheckFinish();
        }

        // 挂机处理
        void UpdateIdle(BattleHero h, int nowMs)
        {
            if (nowMs - h.LastInputMs > InputTimeoutMs)
            {
                if (!h.Idle)
                {
                    h.Idle = true;

                    Debug.LogWarn("玩家{0}(英雄{1}) 已 {2}ms 没上报输入，转入挂机",
                                  h.UID, h.HeroId, InputTimeoutMs);
                }
                h.MoveX = 0f;
                h.MoveZ = 0f;
                h.MoveMag = 0f;
                h.Buttons = 0;
                h.AttackPressed = false;
            }
            else if (h.Idle)
            {
                h.Idle = false;
                Debug.Log("玩家{0}(英雄{1}) 输入恢复，退出挂机", h.UID, h.HeroId);
            }
        }

        // 移动 + 草丛 + 行走朝向，
        // 朝向要排在 TryAttack 之前 —— 出手那一下会用瞄准方向把它盖掉
        void UpdateMove(BattleHero h)
        {
            if (h.Alive)
            {
                PlayerMoveStep.Step(h.X, h.Z, h.MoveX, h.MoveZ, Speed * (TickMs / 1000f), Radius,
                                    BattleMap.Blocked, out float nx, out float nz);
                h.X = nx;
                h.Z = nz;
            }

            h.InBush = BattleMap.InBush(h.X, h.Z);

            if ((h.MoveX != 0f || h.MoveZ != 0f) && TickCount >= h.FacingLockUntilTick)
                h.Facing = Angle(h.MoveX, h.MoveZ);
        }

        // 普攻出手。松手那一下的锁存在这里消费掉，两道闸（存活/冷却/弹药）哪道没过都当场丢，
        // 不排队 —— 和"冷却没到就丢弃"同语义
        void TryAttack(BattleHero h, HeroInfo info)
        {
            if (!h.AttackPressed) return;
            h.AttackPressed = false;
            if (!h.Alive || h.ShotCdMs > 0 || h.Ammo < 1f) return;

            h.ShotCdMs = info.AttackIntervalMs;
            h.Ammo -= 1f;
            // 出手这一下才转身
            if (h.AimX != 0f || h.AimZ != 0f) h.Facing = Angle(h.AimX, h.AimZ);
            h.FacingLockUntilTick = TickCount + MsToTicks(info.AttackIntervalMs);
            h.AttackSeq++;
            // 开火即现形
            h.RevealUntilTick = TickCount + MsToTicks(RevealMs);
            //等待攻击结算
            m_Pending.Add(new PendingHit
            {
                Attacker = h,
                Info = info,
                Facing = h.Facing,
                DueTick = TickCount + MsToTicks(info.WindupMs)
            });
        }

        void UpdateTimers(BattleHero h, HeroInfo info)
        {
            if (h.ShotCdMs > 0) h.ShotCdMs -= TickMs;
            // 装填：死人不装填
            if (h.Alive)
            {
                h.Ammo += TickMs / (float)info.ReloadMsPerShell;
                if (h.Ammo > AmmoMax) h.Ammo = AmmoMax;
                float snapped = (float)Math.Round(h.Ammo);
                if (Math.Abs(h.Ammo - snapped) < 1e-3f) h.Ammo = snapped;
            }
        }

        // 脱战回血：脱战够久 **且** 弹药满格才回，**到点一次给足一格**（不是每 tick 滴一点）。
        // 条件是"当下这一 tick"现判的，不满足就停 —— 打一发打断，补满弹接着回，
        // 不重新等那 RegenDelayMs（0 弹药条是连续值，打一发就立刻不满格 → 每 tick 判天然等价于"停手才回血"）
        // 节拍锚在 LastCombatTick 上：脱战满 RegenDelayMs 的那一 tick 就是第一格（不额外多半拍），
        // 被打断也不重算节拍 —— 所以这里不需要额外的"下次回血 tick"字段
        void UpdateRegen(BattleHero h)
        {
            if (RegenRatePct <= 0f) return;
            if (!h.Alive || h.Hp >= h.MaxHp) return;

            int since = TickCount - h.LastCombatTick;
            int delay = MsToTicks(RegenDelayMs);
            int chunk = MsToTicks(RegenChunkMs);
            if (since < delay || (since - delay) % chunk != 0) return;
            if (h.Ammo < AmmoMax) return;

            h.Hp += (int)(h.MaxHp * RegenRatePct * RegenChunkMs / 100000f);
            if (h.Hp > h.MaxHp) h.Hp = h.MaxHp;
        }

        // 复活排在最后：本 tick 活过来，朝向要下一 tick 才更新
        void UpdateRespawn(BattleHero h)
        {
            if (!m_Mode.Respawn) return;    // 淘汰制：死了就是死了，名次已定
            if (h.Alive) return;

            h.RespawnMs -= TickMs;
            if (h.RespawnMs > 0) return;

            h.X = h.SpawnX;
            h.Z = h.SpawnZ;
            h.Hp = h.MaxHp;
            h.Alive = true;
            h.RespawnMs = 0;
            h.ShotCdMs = 0;
            h.Ammo = AmmoMax;
            h.MoveX = h.MoveZ = 0f;
            h.MoveMag = 0f;
            Debug.Log("玩家{0}(英雄{1}) 复活于出生点，满血 {2}", h.UID, h.HeroId, h.MaxHp);
        }


        void CheckFinish()
        {
            if (Finished) return;

            // 击杀数模式（1v1）：先攒够击杀的人赢
            if (m_Mode.KillTarget > 0)
            {
                foreach (BattleHero h in Heroes)
                    if (h.Kills >= m_Mode.KillTarget) { Finish(); return; }
                return;
            }

            // 淘汰制（4 人）：只剩一个活人是唯一出口 —— 没有时限，
            int alive = 0;
            foreach (BattleHero h in Heroes)
                if (h.Alive) alive++;
            if (alive <= 1) Finish();
        }

        // 名次：活着的排最前（淘汰制到这一步只剩一个），死掉的一律按死亡顺序倒序 ——
        // 最后死的第 2 名，第一个死的垫底。击杀数只在同序时才分先后（1v1 用得上），血量不参与
        static int CompareRank(BattleHero a, BattleHero b)
        {
            if (a.Alive != b.Alive) return a.Alive ? -1 : 1;
            if (a.DeathOrder != b.DeathOrder) return b.DeathOrder - a.DeathOrder;
            return b.Kills - a.Kills;
        }

        void Finish()
        {
            Finished = true;

            int n = Heroes.Length;
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            // n≤4，冒泡足够；用下标排是为了同分时按下标定序（List.Sort 不稳定）
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                    if (CompareRank(Heroes[order[b]], Heroes[order[a]]) < 0)
                    { int t = order[a]; order[a] = order[b]; order[b] = t; }

            List<BattleResultEntry> entries = new List<BattleResultEntry>(n);
            string rankLog = "";
            for (int r = 0; r < n; r++)
            {
                BattleHero h = Heroes[order[r]];
                entries.Add(new BattleResultEntry
                {
                    UID = h.UID,
                    Rank = r + 1,
                    Kills = h.Kills,
                    HeroId = h.HeroId
                });
                rankLog += string.Format("{0}(英雄{1} {2}杀) ", r + 1, h.HeroId, h.Kills);
            }

            BattleHero win = Heroes[order[0]];
            Debug.Log("战斗结束：模式{0} 胜者{1}(英雄{2}) | 名次 {3}", m_Mode.Mode, win.UID, win.HeroId, rankLog);

            if (m_Send == null) return;
            MsgBattleResult msg = new MsgBattleResult
            {
                Mode = m_Mode.Mode,
                // WinnerUID/WinnerKills/LoserKills 是 1v1 老客户端的字段，多人看 Entries
                WinnerUID = win.UID,
                WinnerKills = win.Kills,
                LoserKills = n > 1 ? Heroes[order[1]].Kills : 0,
                Entries = entries
            };
            foreach (BattleHero h in Heroes)
                m_Send(h.UID, msg);
        }

        // 前摇到点的挥击在这里结算
        void ResolvePending()
        {
            for (int i = m_Pending.Count - 1; i >= 0; i--)
            {
                PendingHit p = m_Pending[i];
                if (p.DueTick > TickCount) continue;
                m_Pending.RemoveAt(i);
                if (!p.Attacker.Alive) continue;   // 前摇里被打死了，这一棒挥不出去
                Swing(p.Attacker, p.Info, p.Facing);
            }
        }

        static int MsToTicks(int ms)
        {
            return (ms + TickMs - 1) / TickMs;     // 向上取整
        }

        static float Angle(float dx, float dz)
        {
            return (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
        }

        // 一次挥击的结算
        // facing 由调用方传（= 出手瞬间锁的那一版），不读 h.Facing：前摇里他可能已经转身了
        void Swing(BattleHero h, HeroInfo info, float facing)
        {
            float ax = (float)Math.Sin(facing * Math.PI / 180.0);
            float az = (float)Math.Cos(facing * Math.PI / 180.0);

            for (int i = 0; i < Heroes.Length; i++)
            {
                BattleHero e = Heroes[i];
                if (e == h || !e.Alive) continue;
                if (!InShape(h, e, info, ax, az)) continue;

                // 几何上够得着还不够：中间隔墙不算
                float dx = e.X - h.X, dz = e.Z - h.Z;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dist > 0.0001f && BattleMap.RayBlocked(h.X, h.Z, dx / dist, dz / dist, dist)) continue;

                // 挨打现形
                e.RevealUntilTick = TickCount + MsToTicks(RevealMs);
                // 命中 / 挨打都算进战斗 —— 挥空不算（判定过了才走到这）
                e.LastCombatTick = TickCount;
                h.LastCombatTick = TickCount;

                e.Hp -= info.Attack;
                if (e.Hp <= 0)
                {
                    e.Hp = 0;
                    e.Alive = false;
                    e.DeathOrder = ++m_DeathOrder;   // 名次的唯一依据
                    if (m_Mode.Respawn) e.RespawnMs = RespawnMs;
                    h.Kills++;
                }
                m_Events.Add(new BattleHitEvent
                {
                    AttackerUID = h.UID,
                    TargetUID = e.UID,
                    Damage = info.Attack,
                    HpLeft = e.Hp,
                    Died = !e.Alive,
                    X = e.X,
                    Z = e.Z
                });


                Debug.Log("命中 {0}(英雄{1}) → {2}  -{3}  剩 {4}/{5}{6}",
                          h.UID, h.HeroId, e.UID, info.Attack,
                          Math.Max(e.Hp, 0), e.MaxHp, e.Alive ? "" : "   [击杀]");
            }
        }


        static bool InShape(BattleHero h, BattleHero e, HeroInfo info, float ax, float az)
        {
            float dx = e.X - h.X, dz = e.Z - h.Z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);

            // 粗判先筛掉range之外的
            float cull = info.Shape == "fan" ? info.Range + HitPad : info.Range + info.Size + HitPad;
            if (dist > cull) return false;

            if (info.Shape == "fan")
            {
                if (dist <= Radius) return true;                             // 双方重叠
                float cos = (dx * ax + dz * az) / dist;                      // 点积
                // 加上补偿角 Math.Asin(Radius / dist)
                float half = info.Size + (float)(Math.Asin(Radius / dist) * 180.0 / Math.PI);
                return cos >= Math.Cos(half * Math.PI / 180.0);
            }

            // rect：把敌人拆成"往我正前方多远"和"往旁边偏多少"
            float forward = dx * ax + dz * az;
            float lateral = Math.Abs(dx * az - dz * ax);
            return forward >= -HitPad && forward <= info.Range + HitPad && lateral <= info.Size + HitPad;
        }


        // o 看得见 t 吗（t 的真坐标能不能发给 o）
        bool CanSee(BattleHero o, BattleHero t)
        {
            if (o == t) return true;        // 自己永远看得见自己
            if (!o.Alive) return true;      // 出局的人当观众，全场可见（他反正不打）
            if (!t.InBush) return true;     // 不在草里，谁都看得见
            float dx = t.X - o.X, dz = t.Z - o.Z;
            if (o.InBush && dx * dx + dz * dz <= RevealDist * RevealDist) return true;   // 同一丛草里挨着
            return TickCount < t.RevealUntilTick;    // 刚开火 / 刚挨打，短时间现形
        }

        void Broadcast()
        {
            if (m_Send == null) return;

            int n = Heroes.Length;
            for (int o = 0; o < n; o++)
                for (int t = 0; t < n; t++)
                {
                    bool see = CanSee(Heroes[o], Heroes[t]);
                    m_CanSee[o, t] = see;
                    if (see) { m_LastSeenX[o, t] = Heroes[t].X; m_LastSeenZ[o, t] = Heroes[t].Z; }
                }

            for (int v = 0; v < n; v++)
            {
                BattleHero me = Heroes[v];
                MsgBattleState msg = new MsgBattleState
                {
                    Players = new List<BattleHeroState>(n),
                    // Events 不按收件人过滤：命中事件的两个端点这一帧必定在现形窗口里
                    //（开火现形 / 挨打现形），4 人也一样 —— 过滤等于空转
                    Events = m_Events
                };
                for (int t = 0; t < n; t++)
                {
                    BattleHero h = Heroes[t];
                    bool canSee = m_CanSee[v, t];
                    msg.Players.Add(new BattleHeroState
                    {
                        UID = h.UID,
                        // 隐身时发"最后一次被看见的位置"：客户端反正把模型藏了，位置对不对无所谓，
                        // 但真坐标不上线 —— 抓包也看不到
                        X = canSee ? h.X : m_LastSeenX[v, t],
                        Z = canSee ? h.Z : m_LastSeenZ[v, t],
                        Facing = h.Facing,
                        MoveMag = h.MoveMag,
                        HeroId = h.HeroId,
                        Hp = h.Hp,
                        MaxHp = h.MaxHp,
                        Alive = h.Alive,
                        Kills = h.Kills,
                        RespawnMs = h.RespawnMs,
                        AttackSeq = h.AttackSeq,
                        Ammo = h.Ammo,
                        CanSee = canSee
                    });
                }
                m_Send(me.UID, msg);
            }
            BroadcastCount++;

            if (m_Events.Count > 0) m_Events = new List<BattleHitEvent>();
        }
    }
}

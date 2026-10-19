using System.Collections.Generic;

namespace SimpleServer.Game
{
    // 一个模式的全部差异：人数 / 胜负 / 复活。
    public class ModeInfo
    {
        public int Mode;
        public int PlayerCount;
        public int KillTarget;   // >0 = 先到这个击杀数获胜；0 = 不按击杀数（只剩一个活人）
        public bool Respawn;     // false = 淘汰不复活（死了就出局）
    }

    public static class BattleModes
    {
        public const int Duel1v1 = 1;   // 组队约战房 恒用这个
        public const int Solo4 = 2;     // 单人随机匹配

        static readonly Dictionary<int, ModeInfo> s_Table = new Dictionary<int, ModeInfo>
        {
            { Duel1v1, new ModeInfo { Mode = Duel1v1, PlayerCount = 2, KillTarget = 3, Respawn = true  } },
            { Solo4,   new ModeInfo { Mode = Solo4,   PlayerCount = 4, KillTarget = 0, Respawn = false } },
        };

        public static bool IsValid(int mode) { return s_Table.ContainsKey(mode); }

        // 未知模式按 1v1 兜底（老客户端发 46 不带模式也能跑）
        public static ModeInfo Get(int mode)
        {
            ModeInfo m;
            return s_Table.TryGetValue(mode, out m) ? m : s_Table[Duel1v1];
        }
    }
}

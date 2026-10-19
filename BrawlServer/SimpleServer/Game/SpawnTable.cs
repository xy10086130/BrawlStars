using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleServer.Game
{
    // 出生点 = 关卡数据（Unity 里手摆 → 写进 Maps/spawns_1v1.txt，每行 "x z"）。
    // 不并进地图 txt 的字符集：它跟碰撞格无关，MapCore 是零依赖共享文件，别让它多背一个概念
    // 用法：建房时 FillSpawns 全表洗牌抽 PlayerCount 个不重复的（1v1 也抽 —— 2026-09-16 用户定，没有"前两个"这种特殊行）
    public static class SpawnTable
    {
        static bool loaded;
        static readonly List<float> s_X = new List<float>();
        static readonly List<float> s_Z = new List<float>();
        static readonly Random s_Random = new Random();

        // 文件读不到时的兜底（就是最早写死在 BattleInstance 里的那两点）：
        // 至少 1v1 还能开一局，4 人必然摆不开 —— 那种情况下面 FillSpawns 会报错
        static readonly float[] FallbackX = { -5f, 5f };
        static readonly float[] FallbackZ = { 2f, 2f };

        public static int Count { get { EnsureLoaded(); return s_X.Count; } }

        public static void Get(int i, out float x, out float z)
        {
            EnsureLoaded();
            x = s_X[i];
            z = s_Z[i];
        }

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "spawns_1v1.txt");
            if (!File.Exists(path))
            {
                Debug.LogError("读不到出生点文件：" + path + "（先跑一次 Unity 菜单 Tools/导出 1v1 地图）");
                return;
            }
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) continue;
                float x, z;
                if (!float.TryParse(p[0], out x) || !float.TryParse(p[1], out z))
                {
                    Debug.LogError("出生点文件这行看不懂（要 \"x z\"）：" + line);
                    continue;
                }
                s_X.Add(x);
                s_Z.Add(z);
            }
            Debug.Log("出生点加载完成：{0} 个", s_X.Count);
        }

        // 抽签：给每个座位定死出生点（结果存进 RoomPlayer，48 带出去，BattleInstance 只读不算）。
        // ⚠ 必须在建房时做 —— 48 在建房时就发了，BattleInstance 是收齐 41 才建的，反了 48 里没东西可带
        public static void FillSpawns(RoomPlayer[] players)
        {
            EnsureLoaded();
            int n = players.Length;

            if (s_X.Count < n)
            {
                Debug.LogError("出生点不够用：需要 {0} 个，文件里只有 {1} 个 → 退回写死的老出生点。"
                               + "去 Unity 里摆好点再写进 Maps/spawns_1v1.txt", n, s_X.Count);
                for (int i = 0; i < n; i++)
                {
                    players[i].SpawnX = FallbackX[i % FallbackX.Length];
                    players[i].SpawnZ = FallbackZ[i % FallbackZ.Length];
                }
                return;
            }

            // 全表洗牌取不重复的前 n 个：座位序 = 抽到的先后（谁抽哪不预设，对称性只能靠点位本身）
            int[] idx = new int[s_X.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            for (int i = idx.Length - 1; i > 0; i--)
            {
                int j = s_Random.Next(i + 1);
                int t = idx[i]; idx[i] = idx[j]; idx[j] = t;
            }
            for (int i = 0; i < n; i++)
            {
                players[i].SpawnX = s_X[idx[i]];
                players[i].SpawnZ = s_Z[idx[i]];
            }
        }
    }
}

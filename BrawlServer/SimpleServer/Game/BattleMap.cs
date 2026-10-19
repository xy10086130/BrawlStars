using System;
using System.IO;

namespace SimpleServer.Game
{
    public static class BattleMap
    {
        static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "map_1v1.txt");
            if (!File.Exists(path))
            {
                Debug.LogError("读不到地图文件：" + path);
                return;
            }
            MapCore.Load(File.ReadAllText(path));
            Debug.Log("地图加载完成：{0} 行 × {1} 列", MapCore.Rows, MapCore.Cols);
        }


        public static bool Blocked(float x, float z, float r)
        {
            EnsureLoaded();
            return MapCore.Blocked(x, z, r);
        }


        // 草丛：点所在格是不是 'F'。草不挡路（Blocked 不认它），只决定对手看不看得见你
        public static bool InBush(float x, float z)
        {
            EnsureLoaded();
            return MapCore.InBush(x, z);
        }

        // 射线遮挡：沿射线每 0.25 格采样一次，命中墙格即挡（1 格宽的墙不可能被 0.25 步长跳过）
        // dist 传"到目标为止"的距离 —— 敌人身后的墙不该让这一击作废
        public static bool RayBlocked(float x0, float z0, float dx, float dz, float dist)
        {
            EnsureLoaded();
            for (float t = 0.25f; t < dist; t += 0.25f)
                if (MapCore.Blocked(x0 + dx * t, z0 + dz * t, 0.01f)) return true;
            return false;
        }
    }
}

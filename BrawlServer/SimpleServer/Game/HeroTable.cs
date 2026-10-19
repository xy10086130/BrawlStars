using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SimpleServer.Game
{
    // 一个英雄的数值 + 攻击判定形状
    public class HeroInfo
    {
        public int Id;
        public int Hp;
        public int Attack;
        public string Shape;    // "fan"=扇形（Size 是半角，度）/ "rect"=矩形（Size 是半宽，格）
        public float Range;
        public float Size;
        public int WindupMs;        // 挥击 → 结算伤害的延迟
        public int AttackIntervalMs;// 两次挥击最短间隔
        public int ReloadMsPerShell;// 弹药：回满 1 格要多少毫秒。
    }

    // 英雄数值表：读输出目录 Data/heroes.txt

    public static class HeroTable
    {
        static Dictionary<int, HeroInfo> table;

        public static HeroInfo Get(int heroId)
        {
            if (table == null) Load();
            HeroInfo info;
            if (table.TryGetValue(heroId, out info)) return info;

            // 兜底：表里没这个英雄（导出漏了/文件坏了）→ 给个能打的默认值。
            // 不能返回 null 或 0 血：那会变成"打得中但不掉血"，比报错更难查
            Debug.LogError("英雄数值表里没有 HeroId=" + heroId + "，用兜底值（rect 3×±0.5 / 3000血 / 500伤害）");
            info = new HeroInfo
            {
                Id = heroId,
                Hp = 3000,
                Attack = 500,
                Shape = "rect",
                Range = 3f,
                Size = 0.5f,
                WindupMs = 400,
                AttackIntervalMs = 800,
                ReloadMsPerShell = 1500
            };
            table[heroId] = info;    // 缓存，别每次挥击都刷一条日志
            return info;
        }

        static void Load()
        {
            table = new Dictionary<int, HeroInfo>();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "heroes.txt");
            if (!File.Exists(path))
            {
                Debug.LogError("读不到英雄数值表：" + path);
                return;
            }

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 6) continue;
                // 旧格式（没有 windupMs/attackIntervalMs 两列）必须吵出来：静默跳过 = 每行都进兜底值，
                // 症状是"所有英雄都变成同一个数值"，比直接报错难查得多
                if (p.Length < 9)
                {
                    Debug.LogError("heroes.txt 是旧格式，缺 windupMs/attackIntervalMs/reloadMsPerShell 三列 —— " +
                                   "请跑 Unity 菜单「Tools/导出 1v1 英雄数据」重新导出。跳过的行：" + line);
                    continue;
                }

                int reload = int.Parse(p[8]);
                // 0（或负数）当除数是灾难：要么炸，要么这个英雄永远装不满格。
                // 不静默跳过整行 —— 那会退化成兜底值、变成"英雄数值全错"，比一个坏值更难查
                if (reload <= 0)
                {
                    Debug.LogError("heroes.txt 里 " + p[0] + " 号英雄的 reloadMsPerShell = " + reload +
                                   "，按 1500 用。请跑 Unity 菜单「Tools/导出 1v1 英雄数据」重新导出");
                    reload = 1500;
                }

                HeroInfo info = new HeroInfo
                {
                    Id = int.Parse(p[0]),
                    Hp = int.Parse(p[1]),
                    Attack = int.Parse(p[2]),
                    Shape = p[3],
                    Range = float.Parse(p[4], CultureInfo.InvariantCulture),
                    Size = float.Parse(p[5], CultureInfo.InvariantCulture),
                    WindupMs = int.Parse(p[6]),
                    AttackIntervalMs = int.Parse(p[7]),
                    ReloadMsPerShell = reload
                };
                table[info.Id] = info;
            }
            Debug.Log("英雄数值表加载完成：{0} 个英雄", table.Count);
        }
    }
}

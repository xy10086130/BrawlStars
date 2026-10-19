using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 工具：3D 地图 ShowDown_Night → 字符地图 txt（客户端 + 服务端两端同源）
// 用法：菜单 Tools/导出 1v1 地图。Console 每步打印校验数据，全部对上才算成功。
// 约定（方案定案）：1 格 = 1 世界单位；'.'=空地 '#'=墙/箱（不可走、挡弹道）
//
// 关键认知：本图障碍"中心在整数坐标、AABB 边缘在 k+0.5"，即格中心 = 整数。
// 所以网格原点从障碍包围盒极值推（全部 .5 自洽），不能取全图总 Bounds.min（会被非半格物体污染）。
public static class MapExporter
{
    const string PrefabPath = "Assets/MapPrefabs/ShowDown_Night.prefab";
    const string ClientTxt = "Resources/Maps/map_1v1.txt";        // dataPath 已是 .../Assets，拼相对部分即可
    const string ServerTxt = "BrawlServer/Maps/map_1v1.txt";      // 相对 Unity 项目根

    [MenuItem("Tools/导出 1v1 地图")]
    static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { ExportRoot(root); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ExportRoot(GameObject root)
    {
        // 1) 全图总包围盒：只作参考打印。若远大于 ~79，打印最大的 3 个 Renderer 定位混入的装饰物
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Debug.LogError("[导出] prefab 里没有 Renderer"); return; }
        Bounds total = renderers[0].bounds;
        foreach (Renderer r in renderers) total.Encapsulate(r.bounds);
        Debug.Log($"[导出] 全图 Bounds = {total.size.x:F2} × {total.size.z:F2}（参考，期望 ~79×79）");
        if (total.size.x > 85f || total.size.z > 85f)
        {
            System.Array.Sort(renderers, (a, b) =>
                (b.bounds.size.x * b.bounds.size.z).CompareTo(a.bounds.size.x * a.bounds.size.z));
            StringBuilder big = new StringBuilder();
            for (int i = 0; i < 3 && i < renderers.Length; i++)
                big.AppendLine($"  {renderers[i].name}  {renderers[i].bounds.size.x:F2}×{renderers[i].bounds.size.z:F2} @ {renderers[i].bounds.center}");
            Debug.Log("[导出] Bounds 异常偏大，最大 Renderer 前 3：\n" + big);
        }

        // 2) 障碍判据：墙/箱按名字；Bone_* 是装饰骨头箱，排除（否则 119≠112）
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        List<Transform> obstacles = new List<Transform>();
        foreach (Transform t in all)
        {
            if (t.name.Contains("Bone")) continue;
            if (t.name.Contains("Wall_Tile_Block") || t.name.Contains("Chest"))
                obstacles.Add(t);
        }
        Debug.Log($"[导出] 障碍命中 {obstacles.Count} 个（期望 112 = 墙 84 + 箱 28）");
        if (obstacles.Count == 0)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Mathf.Min(all.Length, 40); i++) sb.AppendLine(all[i].name);
            Debug.LogError("[导出] 障碍 0 命中，实际物体名单(前40)请查：\n" + sb);
            return;
        }

        // 3) 障碍包围盒极值 = 网格边界（中心整数 → 边缘在 k+0.5，极值差为整数）
        float minEX = float.MaxValue, maxEX = float.MinValue;
        float minEZ = float.MaxValue, maxEZ = float.MinValue;
        Bounds[] obsBounds = new Bounds[obstacles.Count];
        for (int i = 0; i < obstacles.Count; i++)
        {
            Collider col = obstacles[i].GetComponentInChildren<Collider>();
            Bounds tb = col != null ? col.bounds : new Bounds(obstacles[i].position, Vector3.one);
            obsBounds[i] = tb;
            minEX = Mathf.Min(minEX, tb.min.x); maxEX = Mathf.Max(maxEX, tb.max.x);
            minEZ = Mathf.Min(minEZ, tb.min.z); maxEZ = Mathf.Max(maxEZ, tb.max.z);
        }
        int W = Mathf.RoundToInt(maxEX - minEX);
        int H = Mathf.RoundToInt(maxEZ - minEZ);
        Debug.Log($"[导出] 障碍范围 → 网格 {H}×{W}（期望 79×79）");

        // 4) 摆放规整校验：尺寸整数格、四边都对齐网格边界（相对 minEX/maxEZ 为整数）
        if (!CheckAligned(obsBounds, minEX, maxEZ)) return;

        // 5) 填格：c 列覆盖 x∈[minEX+c, minEX+c+1]；第 0 行 = maxEZ 一侧（z 向下）
        char[,] grid = new char[H, W];
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
                grid[r, c] = '.';               // 默认空地，障碍按覆盖格改 '#'
        int walls = 0;
        for (int i = 0; i < obsBounds.Length; i++)
        {
            Bounds tb = obsBounds[i];
            int c0 = FloorToInt(tb.min.x - minEX);
            int c1 = CeilToInt(tb.max.x - minEX) - 1;
            int r0 = FloorToInt(maxEZ - tb.max.z);
            int r1 = CeilToInt(maxEZ - tb.min.z) - 1;
            // 越界 = 判据误伤或摆到了网格外，属数据异常：中止暴露，不能静默 Clamp 到边缘
            if (c0 < 0 || c1 >= W || r0 < 0 || r1 >= H)
            {
                Debug.LogError($"[导出] 障碍越界中止：center={tb.center} 格区[{r0}..{r1}]×[{c0}..{c1}]，网格 {H}×{W}。查判据或该物体摆放");
                return;
            }
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                    if (grid[r, c] == '.') { grid[r, c] = '#'; walls++; }
        }
        Debug.Log($"[导出] # 共 {walls} 个（期望 112）");

        // 5.5) 草丛：按【模型真实覆盖面积】落格，不能照抄墙那套
        //   墙的兜底（无 Collider → new Bounds(position, Vector3.one)）把 2.13 宽的模型压成 1 格；
        //   草丛模型宽 2.27、摆放间距 2（相邻两块故意重叠 0.27 连成片），照抄会留下
        //   "站在看得见的草里、判定说没进草丛"的缝 —— 和扇形指示器那个 bug 同一类。
        //   阈值 0.5：草块半宽 1.135、格半宽 0.5，每块必然压到左右邻居各 63.5%，成片时两块叠起来 100%。
        //   取 0.5 的后果是"判定比看起来略大"（孤立一块草铺成 3×3 里的十字 5 格，四个角只有 41.9% 不够），这个方向是安全的：
        //   玩家感觉不到多判，但"眼睛在草里判定说不在"一定会被当成 bug。
        List<Transform> grass = new List<Transform>();
        foreach (Transform t in all)
            if (t.name.Contains("Grass_Tile_Night")) grass.Add(t);
        Debug.Log($"[导出] 草丛命中 {grass.Count} 个（期望 351）");
        if (grass.Count == 0) { Debug.LogError("[导出] 草丛 0 命中，名字判据要改"); return; }

        float[,] cover = new float[H, W];
        int outside = 0;
        foreach (Transform t in grass)
        {
            Renderer rend = t.GetComponentInChildren<Renderer>();
            if (rend == null) { Debug.LogError($"[导出] 草丛 {t.name} 没有 Renderer，中止"); return; }
            Bounds b = rend.bounds;   // 世界空间 AABB：模型实际多大就多大（自带旋转/缩放，草丛那个 -90° Y 旋转也自动算对）
            float xMinC = b.min.x - minEX, xMaxC = b.max.x - minEX;   // 换算到"格坐标"
            float zMinC = maxEZ - b.max.z, zMaxC = maxEZ - b.min.z;
            int c0 = Mathf.FloorToInt(xMinC), c1 = Mathf.CeilToInt(xMaxC) - 1;
            int r0 = Mathf.FloorToInt(zMinC), r1 = Mathf.CeilToInt(zMaxC) - 1;
            // 外圈 ±41 的装饰草落在竞技场（±39.5）外：跳过，不算数据异常（墙/箱越界才中止）
            if (c1 < 0 || c0 >= W || r1 < 0 || r0 >= H) { outside++; continue; }
            for (int r = Mathf.Max(r0, 0); r <= Mathf.Min(r1, H - 1); r++)
                for (int c = Mathf.Max(c0, 0); c <= Mathf.Min(c1, W - 1); c++)
                {
                    float ox = Mathf.Min(xMaxC, c + 1f) - Mathf.Max(xMinC, c);   // 该格被这块草盖住多少
                    float oz = Mathf.Min(zMaxC, r + 1f) - Mathf.Max(zMinC, r);
                    if (ox > 0f && oz > 0f) cover[r, c] += ox * oz;   // 累加 = 并集；重叠处会过 1，无害（只影响已超阈值的格）
                }
        }
        int bushes = 0;
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
                if (cover[r, c] >= 0.5f && grid[r, c] == '.') { grid[r, c] = 'F'; bushes++; }   // 墙优先：'#' 不动
        Debug.Log($"[导出] F 共 {bushes} 个；场外草丛 {outside} 块（越界跳过，期望 164 = 外圈 ±41 那圈装饰草）");

        // 6) 写两端文件（同源同一份内容），并与旧文件对比
        try
        {
            string clientPath = Path.Combine(Application.dataPath, ClientTxt);
            string serverPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ServerTxt);
            string text = GridToText(grid);
            string old = File.ReadAllText(clientPath);
            File.WriteAllText(clientPath, text);
            File.WriteAllText(serverPath, text);
            if (old == text) Debug.Log("[导出] 完成：与旧 txt 完全一致（旧文件没有错，问题在使用方）");
            else Debug.LogWarning($"[导出] 完成，但与旧 txt 不同，首个差异在第 {FirstDiffLine(old, text)} 行——查 prefab 布局是否改过，或行序方向反了");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[导出] 写文件失败：" + e);
        }
    }

    // 摆放规整性：尺寸为整数格、四边相对网格边界为整数距离。分布打印供核对，异常列名中止。
    static bool CheckAligned(Bounds[] obs, float minEX, float maxEZ)
    {
        Dictionary<string, int> sizeCount = new Dictionary<string, int>();
        List<string> bad = new List<string>();
        foreach (Bounds tb in obs)
        {
            bool sizeOk = IsIntCell(tb.size.x) && IsIntCell(tb.size.z);
            bool edgeOk = EdgeOnGrid(tb.min.x, minEX) && EdgeOnGrid(tb.max.x, minEX)
                       && EdgeOnGrid(maxEZ - tb.max.z, 0f) && EdgeOnGrid(maxEZ - tb.min.z, 0f);
            if (!sizeOk || !edgeOk)
            {
                bad.Add($"{tb.center}  size=({tb.size.x:F2},{tb.size.z:F2})");
                continue;
            }
            string key = $"{Mathf.RoundToInt(tb.size.x)}×{Mathf.RoundToInt(tb.size.z)}";
            sizeCount[key] = sizeCount.ContainsKey(key) ? sizeCount[key] + 1 : 1;
        }
        foreach (var kv in sizeCount)
            Debug.Log($"[导出] 障碍尺寸 {kv.Key} × {kv.Value} 个");
        if (bad.Count > 0)
        {
            Debug.LogError($"[导出] {bad.Count} 个障碍不规整（非整数格/边缘不对齐网格），中止：\n" + string.Join("\n", bad));
            return false;
        }
        return true;
    }

    static bool IsIntCell(float s) { return Mathf.Abs(s - Mathf.Round(s)) < 0.05f; }    // 尺寸 ≈ 整数格
    static bool EdgeOnGrid(float d, float origin)   // d 相对 origin 的距离 ≈ 整数格
    {
        float r = (d - origin) - Mathf.Floor(d - origin);
        return r < 0.05f || r > 0.95f;
    }

    // 边缘容差 ±0.001：浮点摆放的小误差不该把格算跳
    static int FloorToInt(float f) { return Mathf.FloorToInt(f + 0.001f); }
    static int CeilToInt(float f) { return Mathf.CeilToInt(f - 0.001f); }

    static string GridToText(char[,] grid)
    {
        StringBuilder sb = new StringBuilder();
        for (int r = 0; r < grid.GetLength(0); r++)
        {
            for (int c = 0; c < grid.GetLength(1); c++) sb.Append(grid[r, c]);
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    static int FirstDiffLine(string a, string b)
    {
        string[] la = a.Split('\n'), lb = b.Split('\n');
        for (int i = 0; i < Mathf.Min(la.Length, lb.Length); i++)
            if (la[i] != lb[i]) return i + 1;
        return Mathf.Min(la.Length, lb.Length) + 1;
    }
}

using UnityEngine;

// 调试用：在 Scene 视图把 map txt 的墙画成半透明红块，叠在 3D 地图上对照
// 用法：挂场景任意空物体（地图须摆在原点），两者重合=数据正确。验证完删。
public class MapDebugDraw : MonoBehaviour
{
    public string mapPath = "Maps/map_1v1";     // Resources 相对路径（不带扩展名）
    public float cell = 1f;
    public float minX = -39.5f;                 // 格网左缘世界 x（地图导出时打印的 minEX）
    public float maxZ = 39.5f;                  // 格网上缘世界 z（txt 第 0 行 = 该侧）
    public Vector3 offset;                      // 地图根的世界坐标（地图不在原点时填；或把本物体挂地图根下 local(0,0,0) 留 0）

    void OnDrawGizmos()
    {
        TextAsset ta = Resources.Load<TextAsset>(mapPath);
        if (ta == null) return;
        string[] lines = ta.text.Split('\n');
        for (int r = 0; r < lines.Length; r++)
        {
            string line = lines[r];
            if (string.IsNullOrEmpty(line)) continue;
            for (int c = 0; c < line.Length; c++)
            {
                char ch = line[c];
                if (ch != '#' && ch != 'F') continue;   // '#'=墙/箱 红  'F'=草丛 绿（叠在 3D 地图上对照，绿格该严丝合缝盖住每片草）
                Gizmos.color = ch == '#' ? new Color(1f, 0.25f, 0.25f, 0.5f) : new Color(0.25f, 1f, 0.35f, 0.4f);
                // row 0 = z 最大一侧；格 [r,c] 中心 = (minX + (c+0.5), maxZ - (r+0.5))，整体 + offset（地图根位置）
                float x = (minX + (c + 0.5f)) * cell + offset.x;
                float z = (maxZ - (r + 0.5f)) * cell + offset.z;
                Gizmos.DrawCube(transform.position + new Vector3(x, 0.5f * cell + offset.y, z),
                                new Vector3(cell, cell, cell));
            }
        }
    }
}

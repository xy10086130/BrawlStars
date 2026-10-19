using UnityEngine;

// 客户端薄壳：只负责"从 Resources 读文本"这一件 Unity 事，碰撞/解析全在 MapCore
public static class MapGrid
{
    const string ResourcesPath = "Maps/map_1v1";

    static bool loaded;

    public static void EnsureLoaded() { LoadIfNeeded(); }

    static void LoadIfNeeded()
    {
        if (loaded) return;
        loaded = true;
        TextAsset ta = Resources.Load<TextAsset>(ResourcesPath);
        if (ta == null) { Debug.LogError("[MapGrid] 读不到 Resources/" + ResourcesPath); return; }
        MapCore.Load(ta.text);
    }

    public static bool Blocked(float cx, float cz, float r)
    {
        LoadIfNeeded();
        return MapCore.Blocked(cx, cz, r);
    }
}

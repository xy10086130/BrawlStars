using System;



public static class MapCore
{
    public const float MinX = -39.5f;
    public const float MaxZ = 39.5f;

    static char[,] cells;
    static int rows, cols;

    public static int Rows { get { return rows; } }
    public static int Cols { get { return cols; } }


    public static void Load(string text)
    {
        string[] lines = text.Replace("\r", "").TrimEnd('\n').Split('\n');
        rows = lines.Length;
        cols = 0;
        for (int i = 0; i < rows; i++) cols = Math.Max(cols, lines[i].Length);
        cells = new char[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                cells[r, c] = c < lines[r].Length ? lines[r][c] : '.';
    }


    // 草丛判定：点所在的那一格是不是 'F'（草不挡路，只决定"对手看不看得见你"）
    // 用点判而不是半径判：草丛是"你站在里面"的二元状态，判定范围 = 导出时按覆盖面积算出的那些格
    public static bool InBush(float cx, float cz)
    {
        if (cells == null) return false;
        int c = (int)Math.Floor((double)(cx - MinX));
        int r = (int)Math.Floor((double)(MaxZ - cz));
        if (r < 0 || r >= rows || c < 0 || c >= cols) return false;
        return cells[r, c] == 'F';
    }


    public static bool Blocked(float cx, float cz, float r)
    {
        if (cells == null) return true;
        int c0 = (int)Math.Floor((double)(cx - r - MinX)), c1 = (int)Math.Floor((double)(cx + r - MinX));
        int r0 = (int)Math.Floor((double)(MaxZ - (cz + r))), r1 = (int)Math.Floor((double)(MaxZ - (cz - r)));
        for (int rr = r0; rr <= r1; rr++)
            for (int cc = c0; cc <= c1; cc++)
            {
                if (rr < 0 || rr >= rows || cc < 0 || cc >= cols) return true;
                if (cells[rr, cc] != '#') continue;
                float cellMinX = MinX + cc, cellMaxX = cellMinX + 1f;
                float cellMaxZ = MaxZ - rr, cellMinZ = cellMaxZ - 1f;
                float dx = Math.Max(cellMinX - cx, Math.Max(0f, cx - cellMaxX));
                float dz = Math.Max(cellMinZ - cz, Math.Max(0f, cz - cellMaxZ));
                if (dx * dx + dz * dz <= r * r) return true;
            }
        return false;
    }
}

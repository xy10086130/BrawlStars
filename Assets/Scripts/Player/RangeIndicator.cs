using UnityEngine;

// 世界空间攻击范围指示器：按住攻击摇杆显示、方向跟手；松手摇杆 Dir 归零自动隐藏
// （开火已由 PlayerMove 的 OnRelease 处理，这里只负责瞄准画面）。
// 形状/射程读 CharacterInfo.json 的 aim：fan=扇形 / rect=矩形。
// 挂法：玩家模型下的独立空物体上挂本组件，MeshFilter/MeshRenderer 由代码自建。
// 每帧只转朝向，aim 参数变化才重建 Mesh。
public class RangeIndicator : MonoBehaviour
{
    public MoveJoystick attackJoy;   // 拖场景 attackJoyStickBg 上的 MoveJoystick
    public int heroIndex;            // 读哪个英雄的 aim（单测=0；出生逻辑做好后运行时指定）
    public float groundY = 0.03f;    // 贴地高度：mesh 顶点抬离模型原点，防与地面 z-fight
    public int arcSegments = 24;     // 扇形圆弧分段数
    public Material aimMat;          // 半透明指示材质；留空自动生成淡蓝半透明

    MeshRenderer meshRenderer;
    Mesh mesh;
    string lastShape;
    float lastRange, lastSize;

    void Awake()
    {
        // 英雄模型靠根节点缩放归一化（bibi 2.39× / edgar 1.5× / spike 0.31×），指示器挂在根下面
        // 会跟着一起缩放 → 画出来的扇形就不是"格"了（bibi 画 7.17 格、服务端只判 3）。
        // mesh 顶点单位 = 服务端判定的格，必须 1:1，这里把父链缩放抵消掉
        Transform p = transform.parent;
        if (p != null)
        {
            Vector3 s = p.lossyScale;
            if (s.x != 0f && s.y != 0f && s.z != 0f)
                transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();

        mesh = new Mesh();
        mesh.name = "AimRange";
        filter.mesh = mesh;
        meshRenderer.enabled = false;

        if (aimMat == null) aimMat = CreateDefaultMat();
        meshRenderer.sharedMaterial = aimMat;
    }

    // 兜底：没拖材质也能先跑起来；想换颜色/形状质感再自己建材质拖到 Aim Mat
    static Material CreateDefaultMat()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) return null;
        Material m = new Material(s);
        m.SetFloat("_Surface", 1f);  // Transparent
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(0.4f, 0.8f, 1f, 0.25f));
        return m;
    }

    void Update()
    {
        // 摇杆按下且过死区 = 正在瞄准（Dir 归零即松手，隐藏交给下一帧）
        Vector2 joy = attackJoy != null ? attackJoy.Dir : Vector2.zero;
        if (joy.sqrMagnitude < 0.01f)
        {
            if (meshRenderer.enabled) meshRenderer.enabled = false;
            return;
        }

        CharacterInfoData data = CharacterInfoConfig.Get(heroIndex);
        AimInfo aim = data != null ? data.aim : null;
        if (aim == null || string.IsNullOrEmpty(aim.shape) || aim.range <= 0f)
        {
            if (meshRenderer.enabled) meshRenderer.enabled = false;
            return;
        }

        // aim 参数变了（换英雄/编辑器调 json）才重建；平时只转朝向
        if (lastShape != aim.shape ||
            Mathf.Abs(lastRange - aim.range) > 0.001f ||
            Mathf.Abs(lastSize - aim.size) > 0.001f)
        {
            if (aim.shape == "fan") BuildFan(aim.range, aim.size);
            else if (aim.shape == "rect") BuildRect(aim.range, aim.size);
            lastShape = aim.shape;
            lastRange = aim.range;
            lastSize = aim.size;
        }

        // 扇形/矩形中轴朝瞄准方向（UI 上/右 = 世界 z/x，与 PlayerMove 同一约定）
        transform.rotation = Quaternion.LookRotation(new Vector3(joy.x, 0f, joy.y));
        if (!meshRenderer.enabled) meshRenderer.enabled = true;
    }

    // 扇形：原点 + 半径 range、半角 size(度) 的圆弧，mesh 朝 local +Z 展开
    void BuildFan(float range, float sizeDeg)
    {
        int n = arcSegments;
        float half = sizeDeg * Mathf.Deg2Rad;
        Vector3[] verts = new Vector3[n + 2];
        int[] tris = new int[n * 3];
        verts[0] = new Vector3(0f, groundY, 0f);
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(-half, half, (float)i / n);
            verts[i + 1] = new Vector3(Mathf.Sin(a) * range, groundY, Mathf.Cos(a) * range);
            if (i < n)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }

    void BuildRect(float range, float halfWidth)
    {
        Vector3[] verts =
        {
            new Vector3(-halfWidth, groundY, 0f),
            new Vector3(halfWidth, groundY, 0f),
            new Vector3(halfWidth, groundY, range),
            new Vector3(-halfWidth, groundY, range),
        };
        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };   // 逆时针绕序，法线朝 +Y（否则俯视相机下被背面剔除）
        mesh.RecalculateBounds();
    }
}

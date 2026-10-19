using UnityEngine;

// 自机进草 → 整个英雄换成 X 光剪影材质（半透明 + ZTest Always，忽略草丛遮挡）→ 出草淡回原色再换回原材质。
// 判据用 MapCore.InBush（导出地图的 'F' 格，跟服务端同一份数据同一个点判），坐标取本地预测位置。
//   —— 别在场景里另搭 Trigger：那是第二个事实来源，边界差半格就会出现"我看自己是半透明、
//      服务端认为我没进草、对手照样能打我"这类对不上的 bug。
//
// ⚠ 只挂 LocalHero（BattleEntry 里 isSelf 那支）。GuestHero / RemoteHero 永远不换 ——
//    "敌人看不见"的唯一防线是服务端 44 的 CanSee，渲染层不能加码，否则等于透视挂。
//
// ⚠ 只收 SkinnedMeshRenderer：BattleEntry 给自机加的 AimRange（RangeIndicator 自己 AddComponent<MeshRenderer>）
//    就挂在英雄根下面，一起换会把瞄准指示器也变成剪影。
public class GrassGhost : MonoBehaviour
{
    public float ghostAlpha = 0.5f;        // 草里的透明度
    public Color ghostTint = Color.white;  // 剪影染色（要"紫色轮廓"就改这里）
    public float fadeSpeed = 3f;           // alpha 变化速度（每秒）→ 0.5 / 3 ≈ 0.17s

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    SkinnedMeshRenderer[] m_Rend;
    Material[][] m_Ghost;     // 与 m_Rend 一一对应：每个 renderer 一套剪影材质
    Material[][] m_Orig;      // 原始材质数组（出草整组换回）
    bool m_Ghosting;
    float m_Alpha = 1f;

    void Awake()
    {
        Shader sh = Resources.Load<Shader>("Shaders/GrassGhost");
        if (sh == null)
        {
            // 放 Resources 而不是 Shader.Find：没有资产引用的 shader 打包会被剥掉（编辑器里却好好的）
            Debug.LogError("[GrassGhost] 缺 Resources/Shaders/GrassGhost → 进草没有任何表现");
            enabled = false;
            return;
        }

        m_Rend = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        m_Ghost = new Material[m_Rend.Length][];
        m_Orig = new Material[m_Rend.Length][];
        for (int i = 0; i < m_Rend.Length; i++)
        {
            Material[] src = m_Rend[i].sharedMaterials;
            m_Orig[i] = src;
            Material[] g = new Material[src.Length];
            for (int j = 0; j < src.Length; j++)
            {
                Material m = new Material(sh);
                // 逐套拷贴图（bibi = character/glow/metal 三套），不拷就是纯白剪影
                if (src[j] != null) m.SetTexture(BaseMapId, src[j].mainTexture);
                g[j] = m;
            }
            m_Ghost[i] = g;
        }
    }

    void Update()
    {
        bool want = MapCore.InBush(transform.position.x, transform.position.z);
        if (want == m_Ghosting)
        {
            // 状态没跳变，只推进淡入淡出
            Fade(want ? ghostAlpha : 1f);
            return;
        }

        if (want)
        {
            Swap(true);
            m_Ghosting = true;
            Fade(ghostAlpha);
        }
        else
        {
            // 出草：先淡回不透明，淡到位了才换回原材质（不然"啪"地跳回原色）
            Fade(1f);
            if (m_Alpha >= 1f) { Swap(false); m_Ghosting = false; }
        }
    }

    void Fade(float target)
    {
        if (Mathf.Approximately(m_Alpha, target)) return;
        m_Alpha = Mathf.MoveTowards(m_Alpha, target, fadeSpeed * Time.deltaTime);
        Color c = ghostTint;
        c.a = m_Alpha;
        for (int i = 0; i < m_Ghost.Length; i++)
            for (int j = 0; j < m_Ghost[i].Length; j++)
                m_Ghost[i][j].SetColor(ColorId, c);
    }

    // 整组换材质数组：不动 _Surface/_Blend/_ZWrite，也绕开 _SURFACE_TYPE_TRANSPARENT
    // 那个 shader_feature 打包被剥的坑
    void Swap(bool ghost)
    {
        for (int i = 0; i < m_Rend.Length; i++)
            m_Rend[i].sharedMaterials = ghost ? m_Ghost[i] : m_Orig[i];
    }

    // 随场景销毁时把运行时 new 出来的材质一起收掉（不然每局攒十几个）
    void OnDestroy()
    {
        if (m_Ghost == null) return;
        for (int i = 0; i < m_Ghost.Length; i++)
            for (int j = 0; j < m_Ghost[i].Length; j++)
                if (m_Ghost[i][j] != null) Destroy(m_Ghost[i][j]);
    }
}

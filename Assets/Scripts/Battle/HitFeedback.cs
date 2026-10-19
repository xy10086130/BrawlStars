using SimpleServer.Proto;
using UnityEngine;

// 受击反馈：伤害飘字 + 自己被打时屏幕上闪一下白光（OnGUI 全屏贴图）。
// 由 BattleSync 收到 44 的 Events 时逐条调用 —— 服务端判定的同一份数据，两端都出飘字，白屏只有自己挨打才闪。

public class HitFeedback : MonoBehaviour
{
    public const float FlashTime = 0.25f;    // 一次白光的淡出时长
    public const float FlashAlpha = 0.45f;   // 最亮时的透明度 —— 调手感就改这两个数

    // #cb2322 扣到自己身上 / #efefef 打到对面
    static readonly Color HurtColor = new Color(203f / 255f, 35f / 255f, 34f / 255f);
    static readonly Color DealColor = new Color(239f / 255f, 239f / 255f, 239f / 255f);

    public static HitFeedback Instance;

    float m_Flash;                           // 1 → 0

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Hit(BattleHitEvent e, bool isMe, float headY)
    {
        DamagePopup.Spawn(e.X, e.Z, e.Damage, headY, isMe ? HurtColor : DealColor);
        if (!isMe) return;
        if (Instance != null) Instance.m_Flash = 1f;
        else Debug.LogWarning("[HitFeedback] 挨打了但 Instance 为空 —— 白屏不会闪，查 BattleEntry 有没有建它");
    }

    void Update()
    {
        if (m_Flash <= 0f) return;
        m_Flash = Mathf.Max(0f, m_Flash - Mathf.Min(Time.deltaTime / FlashTime, 0.34f));
    }

    void OnGUI()
    {
        if (m_Flash <= 0f) return;
        GUI.color = new Color(1f, 1f, 1f, m_Flash * FlashAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

// 一撮飘字：从血条位置起飘、上浮固定距离，停一下再淡出，然后自毁。
public class DamagePopup : MonoBehaviour
{
    const float Rise = 1.2f;       // 上浮距离（格）
    const float RiseTime = 0.4f;   // 上浮用时（秒）
    const float FadeTime = 0.35f;  // 停住后淡出用时（秒）
    const float CharSize = 0.12f;  // 字号（世界单位/字）

    TextMesh m_Text;
    Vector3 m_From;
    Color m_Color;
    float m_T;

    public static void Spawn(float x, float z, int damage, float headY, Color color)
    {
        GameObject go = new GameObject("DamagePopup");
        go.transform.position = new Vector3(x, headY, z);
        DamagePopup p = go.AddComponent<DamagePopup>();
        p.Setup(damage, color);
    }

    void Setup(int damage, Color color)
    {
        m_From = transform.position;
        m_Color = color;

        m_Text = gameObject.AddComponent<TextMesh>();
        Font font = Resources.Load<Font>("font/Brawl_Stars_Deputy");
        m_Text.font = font;
        m_Text.fontSize = 64;
        m_Text.characterSize = CharSize;
        m_Text.anchor = TextAnchor.MiddleCenter;
        m_Text.alignment = TextAlignment.Center;
        m_Text.text = damage.ToString();
        m_Text.color = m_Color;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();   // TextMesh 不保证自带，缺了这里会抛异常把 Hit() 整条中断
        mr.sharedMaterial = font.material;
    }

    void Update()
    {
        m_T += Time.deltaTime;

        float rise = Mathf.Clamp01(m_T / RiseTime);
        transform.position = m_From + Vector3.up * (Rise * rise);
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;   // 正对相机（同 PlayerState 的血条）

        float alpha = m_T <= RiseTime ? 1f : 1f - Mathf.Clamp01((m_T - RiseTime) / FadeTime);
        if (alpha <= 0f) { Destroy(gameObject); return; }
        m_Text.color = new Color(m_Color.r, m_Color.g, m_Color.b, alpha);
    }
}

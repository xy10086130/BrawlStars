using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayerState : MonoBehaviour
{
    public Transform target;          // 跟随的英雄
    public float headY = 2.2f;        // 头顶高度
    public Image fill;
    public Text hpValue;              // 当前血量数字
    public float drainTime = 0.25f;   // 掉血条下滑时长

    Tween m_Drain;                    // 正在跑的掉血 tween，新伤害来了要先杀掉，否则两条抢同一个 fillAmount

    // 弹药条：AttackCount 底下每格一颗子弹（bullet1/2/3），每颗底下一个 fill。
    // 底图（bulletN 自己）一直不动，满格/空格全靠 fill 的 fillAmount 表达，不切图也不切色
    Image[] m_AmmoFill;

    const string GreenPath = "PlayerState/Full_hpProgress";
    const string RedPath = "PlayerState/Less_hpProgress";

    void Awake()
    {
        if (fill == null)
        {
            Transform t = transform.Find("Fill");
            if (t != null) fill = t.GetComponent<Image>();
        }
        if (hpValue == null)
        {
            Transform t = transform.Find("hpValue");
            if (t != null) hpValue = t.GetComponent<Text>();
        }
        // 格数不写死，按 prefab 的子节点数走：改弹药上限只要改 prefab + 服务端 AmmoMax
        Transform box = transform.Find("AttackCount");
        if (box != null)
        {
            m_AmmoFill = new Image[box.childCount];
            for (int i = 0; i < box.childCount; i++)
            {
                Transform t = box.GetChild(i).Find("fill");
                if (t == null)
                {
                    Debug.LogError("[PlayerState] AttackCount/" + box.GetChild(i).name + " 底下没有 fill，弹药条少一根");
                    continue;
                }
                m_AmmoFill[i] = t.GetComponent<Image>();
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + Vector3.up * headY;
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }

    public void Init(Transform owner, bool isOpponent)
    {
        target = owner;
        if (fill != null)
        {
            fill.sprite = Resources.Load<Sprite>(isOpponent ? RedPath : GreenPath);
            fill.fillAmount = 1f;
        }
    }

    public void SetHp(int cur, int max)
    {
        if (hpValue != null) hpValue.text = Mathf.Max(cur, 0).ToString();
        if (fill == null) return;

        float t = max <= 0 ? 0f : Mathf.Clamp01((float)cur / max);
        // 掉血才滑
        bool drain = t < fill.fillAmount;
        if (m_Drain != null) { m_Drain.Kill(); m_Drain = null; }
        if (!drain) { fill.fillAmount = t; return; }

        m_Drain = DOTween.To(() => fill.fillAmount, x => fill.fillAmount = x, t, drainTime);
    }

    public void SetAlive(bool alive) { gameObject.SetActive(alive); }

    // ammo = 0..格数 的【连续值】，不是整数格数：第 i 根（0 起）的进度 = Clamp01(ammo - i)。
    // "打一发时正在充的那半格往前挪一格"是服务端用小数部分算好的，这里只管照着画
    public void SetAmmo(float ammo)
    {
        if (m_AmmoFill == null) return;
        for (int i = 0; i < m_AmmoFill.Length; i++)
        {
            if (m_AmmoFill[i] == null) continue;      // Awake 已经报过错了，别每帧再报一次
            m_AmmoFill[i].fillAmount = Mathf.Clamp01(ammo - i);
        }
    }

    // 场景卸载时 tween 还差几帧跑完的话，回调里那个已被销毁的 Image 会抛 MissingReference
    void OnDestroy() { if (m_Drain != null) m_Drain.Kill(); }


}

using UnityEngine;

public class RemoteHero : MonoBehaviour
{
    public float followSpeed = 5.2f;  // 追赶速度（格/秒）：只比走速 5 高一点。

    public float snapDist = 2f;       // 差太多（丢包/中途落位）直接摆过去
    public float rotateSpeed = 10f;   // 与 PlayerMove 同手感

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackHash = Animator.StringToHash("Attack");

    Vector3 m_Target;
    float m_Facing;
    float m_MoveMag;   // 服务端回传的移动意图强度：播走动画
    bool m_Has;
    Animator m_Anim;
    Renderer[] m_Rend;   // 显隐开关的作用对象
    bool m_AnimSpeedOk;
    bool m_AnimAttackOk;
    float m_LastSpeed = -1f;
    bool m_Visible = true;

    void Awake()
    {
        m_Anim = GetComponentInChildren<Animator>();
        m_Rend = GetComponentsInChildren<Renderer>(true);   // true = 连未激活的子物体一起收
        if (m_Anim != null)
            foreach (AnimatorControllerParameter p in m_Anim.parameters)
            {
                if (p.nameHash == SpeedHash) m_AnimSpeedOk = true;
                else if (p.nameHash == AttackHash) m_AnimAttackOk = true;
            }
    }

    // 对方挥击（服务端 AttackSeq 变了）
    public void PlayAttack()
    {
        if (m_AnimAttackOk) m_Anim.SetTrigger(AttackHash);
    }

    // 硬落位（第一帧 / 丢包太久）
    public void SnapTo(float x, float z, float facing, float moveMag)
    {
        m_Has = true;
        m_Facing = facing;
        m_MoveMag = moveMag;
        m_Target = new Vector3(x, transform.position.y, z);
        transform.position = m_Target;
        transform.rotation = Quaternion.Euler(0f, facing, 0f);
        SetAnimSpeed(moveMag);
    }

    // 草丛隐身：服务端说看不见就把模型渲染关掉。藏起来期间 SetTarget 照旧被调用 ——
    // 服务端发的是"最后一次被看见的位置"，所以不会偷偷攒位移。
    // ⚠ 别用 gameObject.SetActive：那会连 Animator 一起停掉，重新出现时它的 Speed 参数已被重置回默认，
    //    而下面 m_LastSpeed 这层缓存还停在旧值 → "值没变"直接跳过 SetFloat，行走动画从此不再播（站着滑步）。
    //    只开关 Renderer 就没这一出：动画一直在跑，重新出现时人已经在走。
    //    血条不受影响（它是独立物体，不是 hero 的子节点，走 PlayerState.SetAlive）
    public void SetVisible(bool v)
    {
        if (m_Rend == null) return;
        for (int i = 0; i < m_Rend.Length; i++)
            if (m_Rend[i] != null) m_Rend[i].enabled = v;

        if (m_Visible == v) return;
        m_Visible = v;
        // 隐身 → 可见：这一帧刚收到的坐标是【真坐标】，而模型还停在"最后一次被看见的位置"上，
        // 两个位置可能差着一段（人家在草里又走了）。差值没超 snapDist 的话 SetTarget 会走
        // "朝目标慢慢走"那条路 —— 画面上就是人又演一遍"走进草丛"，顺带把对方藏在哪边漏给你了。
        // 把 m_Has 清掉 → 下一帧 SetTarget 直接走 SnapTo 硬落位（复用"第一帧落位"那条路径）
        if (v) m_Has = false;
    }

    // 收到一帧权威位置
    public void SetTarget(float x, float z, float facing, float moveMag)
    {
        m_MoveMag = moveMag;
        if (!m_Has) { SnapTo(x, z, facing, moveMag); return; }
        m_Target = new Vector3(x, transform.position.y, z);
        m_Facing = facing;
        Vector3 p = transform.position;
        float dx = m_Target.x - p.x, dz = m_Target.z - p.z;
        if (dx * dx + dz * dz > snapDist * snapDist) SnapTo(x, z, facing, moveMag);
    }

    void Update()
    {
        if (!m_Has) return;

        // 朝目标点走，不越过（这一步不查地图碰撞：两个权威点之间只差一步 0.25 格，直线补间不会穿墙）
        Vector3 p = transform.position;
        float dx = m_Target.x - p.x, dz = m_Target.z - p.z;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        if (d > 0.0001f)
        {
            float step = followSpeed * Time.deltaTime;
            if (step >= d) p = new Vector3(m_Target.x, p.y, m_Target.z);
            else p = new Vector3(p.x + dx / d * step, p.y, p.z + dz / d * step);
            transform.position = p;
        }

        SetAnimSpeed(m_MoveMag);
        transform.rotation = Quaternion.Slerp(transform.rotation,
                                              Quaternion.Euler(0f, m_Facing, 0f),
                                              rotateSpeed * Time.deltaTime);
    }

    void SetAnimSpeed(float v)
    {
        if (!m_AnimSpeedOk || v == m_LastSpeed) return;
        m_LastSpeed = v;
        m_Anim.SetFloat(SpeedHash, v);
    }
}

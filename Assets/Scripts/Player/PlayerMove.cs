using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float speed = 5f;          // 格/秒，
    public float rotateSpeed = 10f;   // 转向平滑速度

    public float radius = 1f;
    public int heroIndex;             // 读 CharacterInfoConfig 取前摇时长

    Animator animator;
    public MoveJoystick attackJoy;   // 攻击摇杆
    Vector3 moveDir;                  // 移动方向
    float firePulse;
    Vector3 fireAim;
    float faceLock;                   // >0 = 出手后的这段时间里，行走不抢朝向

    static readonly int Speed = Animator.StringToHash("Speed");
    static readonly int Attack = Animator.StringToHash("Attack");

    void Awake()
    {
        MapGrid.EnsureLoaded();
        animator = GetComponentInChildren<Animator>();
        if (attackJoy != null) attackJoy.OnRelease += OnAttackRelease;
    }


    public void BindAttackJoystick(MoveJoystick joy)
    {
        attackJoy = joy;
        joy.OnRelease += OnAttackRelease;
    }


    public Vector3 MoveDir { get { return moveDir; } }


    public Vector3 AimDir
    {
        get
        {
            if (attackJoy == null) return Vector3.zero;
            Vector2 d = attackJoy.Dir;
            return new Vector3(d.x, 0f, d.y);
        }
    }


    public bool ConsumeFirePulse(out Vector3 aim)
    {
        bool b = firePulse > 0f;
        aim = fireAim;
        firePulse = 0f;
        fireAim = Vector3.zero;
        return b;
    }

    // 挥击动画由服务端 AttackSeq 驱动
    public void PlayAttack()
    {
        if (animator != null) animator.SetTrigger(Attack);
    }

    // 硬定位
    public void ApplyServerPos(float x, float z)
    {
        transform.position = new Vector3(x, transform.position.y, z);
    }

    // 44 权威位置：朝目标走一小步
    public void CorrectTo(float x, float z, float step)
    {
        Vector3 p = transform.position;
        float dx = x - p.x, dz = z - p.z;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        if (d <= 0.0001f) return;
        if (step >= d) { ApplyServerPos(x, z); return; }
        PlayerMoveStep.Step(p.x, p.z, dx / d, dz / d, step, radius, MapGrid.Blocked, out float nx, out float nz);
        transform.position = new Vector3(nx, p.y, nz);
    }

    void OnAttackRelease()
    {
        // 松手 = 出手
        Vector2 d = attackJoy.Dir;
        fireAim = d.sqrMagnitude > 0.01f ? new Vector3(d.x, 0f, d.y) : Vector3.zero;   // 没拖出死区 = 朝当前朝向挥
        if (fireAim != Vector3.zero) transform.rotation = Quaternion.LookRotation(fireAim);

        CharacterInfoData ci = CharacterInfoConfig.Get(heroIndex);
        faceLock = ci == null ? 0f : ci.attackIntervalMs / 1000f;
        firePulse = 1f;
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0, v);

        if (input.sqrMagnitude > 1f) input.Normalize();   // 归一化
        moveDir = input;


        float step = speed * Time.deltaTime;

        Vector3 p = transform.position;
        PlayerMoveStep.Step(p.x, p.z, moveDir.x, moveDir.z, step, radius, MapGrid.Blocked, out float nx, out float nz);
        transform.position = new Vector3(nx, p.y, nz);

        if (faceLock > 0f) faceLock -= Time.deltaTime;


        if (moveDir.sqrMagnitude > 0.01f && faceLock <= 0f)
        {
            Quaternion target = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target,
                                                  rotateSpeed * Time.deltaTime);
        }

        //动画
        if (animator == null) return;
        animator.SetFloat(Speed, moveDir.magnitude);
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class MoveJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform handle;      // 拖入摇杆头
    public float deadzone = 0.45f;    // 死区，可调

    RectTransform bg;
    float radius;
    Vector2 dir;

    public Vector2 Dir { get { return dir; } }

    public Action OnRelease;   // 松手事件：在 dir 清零【前】触发，监听方可读到最后方向

    void Awake()
    {
        bg = GetComponent<RectTransform>();
        radius = bg.rect.width * 0.5f;
    }

    // 按下和拖动逻辑一样，共用一个方法
    public void OnPointerDown(PointerEventData e) { MoveHandle(e); }
    public void OnDrag(PointerEventData e) { MoveHandle(e); }

    void MoveHandle(PointerEventData e)
    {
        // 屏幕坐标 → 底盘本地坐标
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bg, e.position, e.pressEventCamera, out local);

        if (local.magnitude > radius)          // 拖出底盘就锁在边缘
            local = local.normalized * radius;

        handle.anchoredPosition = local;       // 摇杆头跟手
        dir = local / radius;                  // 归一化 0~1

        if (dir.magnitude < deadzone)          // 死区只作用输出，头已跟手不抖
            dir = Vector2.zero;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (OnRelease != null) OnRelease();       // 先通知（dir 还是最后瞄准值）
        dir = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;   // 头回中心
    }
}

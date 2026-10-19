using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;                                // 要跟随的角色
    public Vector3 offset = new Vector3(0, 23.7f, -6.6f);   // 沿用场景里调好的偏移
    public float smooth = 5f;                               // 跟随平滑度

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 want = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, want, smooth * Time.deltaTime);
    }
}

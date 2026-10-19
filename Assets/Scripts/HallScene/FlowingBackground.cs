using UnityEngine;
using UnityEngine.UI;

public class FlowingBackground : MonoBehaviour
{
    [SerializeField] private RawImage background;
    [SerializeField] private Vector2 speed = new Vector2(0.015f, 0.005f);

    private Material runtimeMaterial;
    private Vector2 offset;

    private void Awake()
    {
        if (background == null)
        {
            background = GetComponent<RawImage>();
        }

        // 创建材质副本，避免修改项目里的原始材质
        runtimeMaterial = new Material(background.material);
        background.material = runtimeMaterial;
    }

    private void Update()
    {
        offset += speed * Time.deltaTime;
        runtimeMaterial.mainTextureOffset = offset;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}
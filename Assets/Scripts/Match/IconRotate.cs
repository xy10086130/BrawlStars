using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IconRotate : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 80f;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        // 使用 unscaledDeltaTime，即使游戏暂停也可以继续转
        rectTransform.Rotate(
            0f,
            0f,
            rotateSpeed * Time.unscaledDeltaTime
        );
    }
}
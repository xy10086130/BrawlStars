using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 飘字提示：屏幕中央飘起淡出，静态调用 FlyText.Show("内容")
// 自带 DontDestroyOnLoad 画布，跨场景可用
public static class FlyText
{
    private static Canvas m_Canvas;
    private static Font m_Font;

    public static void Show(string msg)
    {
        EnsureCanvas();

        GameObject go = new GameObject("FlyText");
        go.transform.SetParent(m_Canvas.transform, false);

        Text text = go.AddComponent<Text>();
        text.text = msg;
        text.font = m_Font;
        text.fontSize = 44;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800, 100);
        rt.anchoredPosition = new Vector2(0, -150);

        CanvasGroup cg = go.AddComponent<CanvasGroup>();

        rt.DOAnchorPos(new Vector2(0, 80), 1.2f).SetEase(Ease.OutCubic);
        cg.DOFade(0, 1.2f)
            .SetDelay(0.3f)
            .OnComplete(() => Object.Destroy(go));
    }

    private static void EnsureCanvas()
    {
        if (m_Canvas != null) return;

        GameObject canvasGo = new GameObject("FlyTextCanvas");
        Object.DontDestroyOnLoad(canvasGo);

        m_Canvas = canvasGo.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_Canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        m_Font = Resources.Load<Font>("font/ShanHaiZhongXiaYeWuYuW-2");
    }
}

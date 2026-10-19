using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class TransitionBg : MonoBehaviour
{

    private CanvasGroup background;
    private float fadeDuration = 1.0f;

    // 从大厅"退出登录"退回登录界面前置位：开屏图不再放一遍 —— 那是菜单跳菜单，不是冷启动。
    // 静态字段跨场景活着，正好当一次性信箱（同 MainPanel.s_RoomCode 的做法）
    public static bool s_SkipFade;

    private void Awake()
    {

    }

    void Start()
    {
        background = GetComponent<CanvasGroup>();

        if (s_SkipFade)
        {
            s_SkipFade = false;
            background.alpha = 0f;   // 必须直接落到淡出后的状态，否则开屏图会一直盖着登录界面
            return;
        }

        FadeOut();
    }



    public void FadeOut()
    {
        background.DOKill();

        background
            .DOFade(0f, fadeDuration)
            .SetEase(Ease.Linear);
    }

    void Update()
    {

    }
}

using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BasePanel : MonoBehaviour
{
    protected CanvasGroup canvasGroup;
    protected Transform closeTrans;
    protected Button closeButton;
    protected Transform homeTrans;
    protected Button homeButton;

    //动画参数
    [SerializeField] protected float enterDuration = 0.3f;
    [SerializeField] protected float exitDuration = 0.2f;
    [SerializeField] protected Ease enterEase = Ease.OutBack;
    [SerializeField] protected Ease exitEase = Ease.InBack;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }


    // 预制体上不挂面板脚本（UIManager.AddScript 运行时加）→ public Button xxx 这种字段在
    // Inspector 里根本没得拖，永远是 null。所以面板一律用这个按节点名抓。
    // 抓不到就报明确错误并返回 null，让调用处判空跳过 —— 千万别让 Awake 里 NRE：
    // 异常会从 SpawnPanel 抛出去，面板既不进 panelDic 也不跑 OnOpen，屏幕上留个点不动的僵尸壳
    protected T Bind<T>(string nodeName) where T : Component
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name != nodeName) continue;
            T c = t.GetComponent<T>();
            if (c == null) Debug.LogError($"[{GetType().Name}] 节点 {nodeName} 上没有 {typeof(T).Name}");
            return c;
        }
        Debug.LogError($"[{GetType().Name}] 预制体里找不到节点：{nodeName}");
        return null;
    }

    public virtual void OnOpen()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (closeTrans == null)
        {
            closeTrans = transform.Find("closeBtn");
            if (closeTrans != null)
            {
                closeButton = closeTrans.GetComponent<Button>();
                closeButton.onClick.AddListener(OnClickCloseBtn);
            }
        }

        // homeBtn：一键关闭所有面板回大厅（判空防重复绑定）
        if (homeTrans == null)
        {
            homeTrans = transform.Find("homeBtn");
            if (homeTrans != null)
            {
                homeButton = homeTrans.GetComponent<Button>();
                homeButton.onClick.AddListener(OnClickHomeBtn);
            }
        }

        // 打断上一次动画
        transform.DOKill(true);
        canvasGroup.DOKill(true);

        // 初始状态
        canvasGroup.alpha = 0;
        transform.localScale = Vector3.one * 0.8f;   // 不用 originalScale，写死基准值
        canvasGroup.blocksRaycasts = true;

        // 进场动画
        transform.DOScale(Vector3.one, enterDuration).SetEase(enterEase);
        canvasGroup.DOFade(1f, enterDuration).SetEase(enterEase);
    }

    public virtual void OnClickCloseBtn()
    {
        UIManager.Instance.PopPanel();
    }

    // 面板里的 homeBtn：一键关掉所有面板回大厅
    public virtual void OnClickHomeBtn()
    {
        UIManager.Instance.CloseAll();
    }

    public virtual void OnClose()
    {
        if (canvasGroup == null) return;

        transform.DOKill(true);
        canvasGroup.DOKill(true);

        canvasGroup.blocksRaycasts = false;

        transform.DOScale(Vector3.one * 0.8f, exitDuration).SetEase(exitEase);
        canvasGroup.DOFade(0f, exitDuration).SetEase(exitEase);
    }


    //场景切换后，LoadingScene 的 Canvas 和面板全销毁了，但 UIManager.panelStack 里还留着引用。PushPanel
    //对新场景里的第一个面板调 OnPause 时，发现 canvasGroup 对象已销毁

    public virtual void OnPause()
    {
        if (canvasGroup == null) return;
        canvasGroup.blocksRaycasts = false;
    }

    public virtual void OnResume()
    {
        if (canvasGroup == null) return;
        canvasGroup.blocksRaycasts = true;
    }
}
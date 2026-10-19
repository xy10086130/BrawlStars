using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 设置面板：整块从右侧滑入，按钮按视觉从上到下依次跟进；关闭时顺序倒过来（最后进的先走）
public class SettingPanel : BasePanel
{
    [SerializeField] float slideDuration = 0.28f;
    [SerializeField] float stagger = 0.05f;

    RectTransform bgRt;
    RectTransform[] items;      // 除关闭键外的按钮，排序后 = exitLoginBtn → newsBtn
    Vector2[] itemHome;
    Vector2 bgHome;
    Canvas gearCanvas;          // MainPanel 上 settingBtn 自带的嵌套 Canvas
    Button exitLoginBtn;

    protected override void Awake()
    {
        base.Awake();

        bgRt = transform.Find("bg") as RectTransform;
        bgHome = bgRt.anchoredPosition;

        // 按屏幕 y 从高到低排 = 视觉上的从上到下（层级顺序不等于视觉顺序）
        var list = new List<RectTransform>();
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.name == "closeBtn") continue;
            if (b.name == "exitLoginBtn") exitLoginBtn = b;   // 按名字抓，省得动预制体加序列化字段
            list.Add((RectTransform)b.transform);
        }
        if (exitLoginBtn != null) exitLoginBtn.onClick.AddListener(ExitToLogin);
        list.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));

        items = list.ToArray();
        itemHome = new Vector2[items.Length];
        for (int i = 0; i < items.Length; i++) itemHome[i] = items[i].anchoredPosition;
    }

    // 面板和按钮统一推这么远：bg 宽 416 > 最左按钮（newsBtn，左边 582.77）出屏所需的 378
    float SlideDistance { get { return bgRt.rect.width; } }

    public override void OnOpen()
    {
        base.OnOpen();              // 必须调：closeBtn / homeBtn 是基类里绑的
        transform.DOKill(true);     // 基类的缩放对全屏容器不适用，毙掉
        canvasGroup.DOKill(true);
        transform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;

        float dist = SlideDistance;

        bgRt.DOKill();
        bgRt.anchoredPosition = bgHome + Vector2.right * dist;
        bgRt.DOAnchorPos(bgHome, slideDuration).SetEase(Ease.OutCubic);

        for (int i = 0; i < items.Length; i++)
        {
            items[i].DOKill();
            items[i].anchoredPosition = itemHome[i] + Vector2.right * dist;
            items[i].DOAnchorPos(itemHome[i], slideDuration)
                    .SetEase(Ease.OutCubic)
                    .SetDelay(i * stagger);
        }

        SetGearTop(true);   // 本面板压着 MainPanel，只有这时才让齿轮浮上来
    }

    public override void OnClose()
    {
        transform.DOKill(true);
        canvasGroup.DOKill(true);
        canvasGroup.blocksRaycasts = false;
        SetGearTop(false);

        float dist = SlideDistance;
        int last = items.Length - 1;

        // 按钮倒序先走（最后进来的先走），面板压在最后 —— 正好是 OnOpen 的时间镜像。
        // 面板不延迟的话 0.28s 就走了，而顶上的 exitLoginBtn 要 0.30s 才开始动，
        // 中间那 0.3 秒就是"面板关了、按钮还在往回撤"。
        bgRt.DOKill();
        bgRt.DOAnchorPos(bgHome + Vector2.right * dist, slideDuration)
            .SetEase(Ease.InCubic)
            .SetDelay(last * stagger);

        for (int i = 0; i < items.Length; i++)
        {
            items[i].DOKill();
            items[i].DOAnchorPos(itemHome[i] + Vector2.right * dist, slideDuration)
                    .SetEase(Ease.InCubic)
                    .SetDelay((last - i) * stagger);   // 顺序倒过来：最后进来的先走
        }
    }

    // 别的面板压到本面板上面（点好友/英雄/邀请…）→ 齿轮让位，否则它会浮在那个面板上面
    public override void OnPause()
    {
        base.OnPause();
        SetGearTop(false);
    }

    // 上面的面板关掉，又轮到本面板 → 齿轮再浮上来
    public override void OnResume()
    {
        base.OnResume();
        SetGearTop(true);
    }

    // 退回登录界面：先退房（房主退 = 服务端连房一起销毁，别把同伴晾在房里），再清客户端房态。
    // ServerRoot 是 DontDestroyOnLoad 的，socket 跨场景活着，登录界面再登一次直接复用这条连接。
    void ExitToLogin()
    {
        if (!string.IsNullOrEmpty(MainPanel.s_RoomCode))
            ProtoManager.CSRoomExit(null);   // 只推不等回包：场景马上就换了，回包也没人听
        MainPanel.ClearRoomState();
        AudioManager.StopBgm();              // LoadingScene 从不 PlayBgm，不关的话大厅音乐一直循环
        TransitionMgr.pendingTarget = TargetScene.LoadingScene;   // 走过渡场景：直跳 LoadingScene 会先露一段空镜头
        SceneManager.LoadScene("TransitionScene");
    }

    // 齿轮的嵌套 Canvas 是"全局最上层"开关：overrideSorting=true 时压过根 Canvas 上的
    // 所有面板（不只是本面板），所以必须跟着面板栈的栈顶状态开关，不能一挂上就开着。
    // 关掉 overrideSorting 后 sortingOrder 失效，齿轮退回 MainPanel 子树的正常层级 ——
    // 渲染和点击都被后面 push 的面板盖住，正是想要的。
    void SetGearTop(bool on)
    {
        if (gearCanvas == null && MainPanel.Instance != null && MainPanel.Instance.settingBtn != null)
            gearCanvas = MainPanel.Instance.settingBtn.GetComponent<Canvas>();

        if (gearCanvas != null) gearCanvas.overrideSorting = on;
    }
}

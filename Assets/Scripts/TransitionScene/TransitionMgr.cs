using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum TargetScene
{
    HallScene = 0,
    BattleScene = 1,
    LoadingScene = 2,   // 大厅退出登录回登录页。名字必须等于场景名（Load 里拿 ToString 当场景名用）
}

public class TransitionMgr : MonoBehaviour
{
    public static TransitionMgr Instance;
    public Text progressText;
    public Image progressImage;
    public Image bgImage;
    public TargetScene targetScene;


    public Sprite transitionToHallBg;
    public Sprite transitionToBattleBg;
    public Sprite transitionToLoginBg;   // 退出登录回登录页用（LS_BattleKatanaKingdom）

    private float barDuration = 1.2f;

    public static TargetScene? pendingTarget;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 42 开战广播：双方就绪 → 放行，同时进场
        ProtoManager.AddBeginBattleListener((msg) =>
        {
            beginReceived = true;
        });
        beginReceived = false;

        if (pendingTarget.HasValue)
        {
            Transition(pendingTarget.Value);
            pendingTarget = null;
        }
    }

    // 42 到达标记（战斗场景同步门：收齐双方 41 服务端才广播）
    bool beginReceived;

    public void Transition(TargetScene target)
    {
        targetScene = target;
        bgImage.sprite = PickBg(target);
        StartCoroutine(Load(targetScene));
    }

    Sprite PickBg(TargetScene target)
    {
        // 回登录页那条：用 Inspector 配的回登录图。没拖引用也不能白屏 → 退回 Hall 那张
        if (target == TargetScene.LoadingScene)
            return transitionToLoginBg != null ? transitionToLoginBg : transitionToHallBg;

        Sprite self = target == TargetScene.BattleScene ? transitionToBattleBg : transitionToHallBg;

        // 包里有同名热更图（transitionBg）就用包里的：ApplyTo 先换上，这里后跑，不查一下会被自带图盖回去
        Sprite hot = HotUpdateManager.GetAsset<Sprite>(bgImage.gameObject.name);
        return hot != null ? hot : self;
    }

    private IEnumerator Load(TargetScene targetScene)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(targetScene.ToString());
        op.allowSceneActivation = false;

        float elapsed = 0f;

        while (elapsed < barDuration || op.progress < 0.9f)
        {
            elapsed += Time.deltaTime;

            float showProgress =
                Mathf.Clamp01(elapsed / barDuration);

            float realProgress =
                Mathf.Clamp01(op.progress / 0.9f);

            float p = Mathf.Min(showProgress, realProgress);
            if (progressImage != null) progressImage.fillAmount = p;
            if (progressText != null) progressText.text = (int)(p * 10) * 10 + "%";

            yield return null;
        }

        if (progressImage != null) progressImage.fillAmount = 1f;
        if (progressText != null) progressText.text = "100%";

        // 匹配对战同步门：本地加载完 = 我方就绪 → 发 41，等双方都就绪（42）再同时进场
        if (targetScene == TargetScene.BattleScene)
        {
            if (progressText != null) progressText.text = "等待对手加入…";
            ProtoManager.CSBattleReady(null);
            while (!beginReceived)
                yield return null;
        }

        op.allowSceneActivation = true;
    }


    void Update()
    {

    }
}

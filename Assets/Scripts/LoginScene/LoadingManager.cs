using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// 登录场景总控。登录/注册本身搬去了 LoginAndRegisterPanel —— 面板改成预制体后实例是运行时
// Instantiate 的，原来的 nameIpt/passwordIpt/loginBtn/registerBtn 在 Inspector 里根本拖不到。
public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;

    void Awake()
    {
        Instance = this;

        // 视频第一帧准备好之前屏幕上什么都没有 → 摄像机默认清成天空盒，就是那个「unity 默认场景」。
        // 背景色本来就是黑的，改成纯色后这段时间看着就是一块黑，不是默认场景
        if (Camera.main != null) Camera.main.clearFlags = CameraClearFlags.SolidColor;

        // 开屏视频的起播统一走这里（场景里 Play On Awake 已关）：先把 clip 定下来，再播。
        // 不这么干就是「自带的 start0 先开播 → 被热更换成包里的那份 → 掐掉重来」，
        // 中间露出的那一帧就是「闪过一个画面再播视频」
        VideoPlayer vp = FindObjectOfType<VideoPlayer>(true);
        if (vp == null) return;

        VideoClip hot = HotUpdateManager.GetVideoClip();   // 没包/包里没视频 → null → 播自带那份
        if (hot != null) vp.clip = hot;

        // RT 跨场景留着：里面还是上一轮播到的画面，不清就会先闪一下它
        RenderTexture rt = vp.targetTexture as RenderTexture;
        if (rt != null)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = prev;
        }

        vp.Play();
    }

    IEnumerator Start()
    {
        // 跟开屏视频并行：HEAD 最长 5s，串行会让登录页白白晚出来
        Coroutine check = StartCoroutine(HotUpdateManager.Check());

        // 开屏视频先露 2 秒
        yield return new WaitForSeconds(2f);
        yield return check;

        // 有新版 → 更新页（它自己收尾弹登录）；没有直接进登录
        UIManager.Instance.PushPanel(HotUpdateManager.HasUpdate
            ? UIPanelType.UpdatePanel
            : UIPanelType.LoginAndRegisterPanel);
    }

    // 登录/注册成功 → 走过渡场景进大厅（LoginAndRegisterPanel 延迟 1 秒后调）
    public void EnterTransition()
    {
        TransitionMgr.pendingTarget = TargetScene.HallScene;
        SceneManager.LoadScene("TransitionScene");
    }
}

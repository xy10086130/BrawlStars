using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 热更页。面板是缓存的，OnOpen 会跑第二次 → 监听先摘再挂
public class UpdatePanel : BasePanel
{
    public Text title;
    public Image progress;
    public Text progressText;
    public Button updateBtn;
    public Button exitBtn;

    bool m_Busy;

    public override void OnOpen()
    {
        base.OnOpen();

        if (updateBtn == null || exitBtn == null)
        {
            Debug.LogError("[UpdatePanel] updateBtn/exitBtn 没在预制体里拖上 → 这个页面点不动");
            return;
        }

        updateBtn.onClick.RemoveListener(OnClickUpdate);
        updateBtn.onClick.AddListener(OnClickUpdate);
        exitBtn.onClick.RemoveListener(OnClickExit);
        exitBtn.onClick.AddListener(OnClickExit);

        SetTip("新版本");
        SetProgress(0f);
        SetBusy(false);
    }

    void OnClickUpdate()
    {
        if (m_Busy) return;

        SetBusy(true);          // 下载中把「跳过」也锁上：下到一半退出，tmp 删不删都别扭
        SetTip("下载中…");
        StartCoroutine(DownloadRoutine());
    }

    IEnumerator DownloadRoutine()
    {
        bool ok = false;
        yield return HotUpdateManager.Download(SetProgress, r => ok = r);

        // ⚠ 只有下成功了才试装：反着写会在「下载失败但磁盘上还留着上一版的包」时判成成功
        if (ok) ok = HotUpdateManager.Load();

        // 装上了就当场换开屏视频（不换要等下次启动）。不并进 ok：没视频可换 ≠ 热更失败
        if (ok) HotUpdateManager.ApplyVideo();

        if (!ok)
        {
            SetTip("下载失败，点重试或跳过");
            SetBusy(false);
            yield break;
        }

        SetProgress(1f);
        yield return new WaitForSeconds(0.3f);   // 让 100% 露一下再关
        Finish();
    }

    void OnClickExit()   // 跳过：不替换，界面就是旧的
    {
        if (m_Busy) return;
        Finish();
    }

    // 自己收尾：推登录面板，LoadingManager 不管这事
    void Finish()
    {
        UIManager.Instance.PopPanel(this);                          // 淡出自己
        UIManager.Instance.PushPanel(UIPanelType.LoginAndRegisterPanel);
    }

    void SetBusy(bool busy)
    {
        m_Busy = busy;
        if (updateBtn != null) updateBtn.interactable = !busy;
        if (exitBtn != null) exitBtn.interactable = !busy;
    }

    public void SetProgress(float p)
    {
        p = Mathf.Clamp01(p);
        if (progress != null) progress.fillAmount = p;
        if (progressText != null) progressText.text = Mathf.RoundToInt(p * 100f) + "%";
    }

    public void SetTip(string msg)
    {
        if (title != null) title.text = msg;
    }
}

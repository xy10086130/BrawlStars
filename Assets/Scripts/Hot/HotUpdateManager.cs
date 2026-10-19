using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// 热更总入口（方案 §12）：检查 / 下载 / 装载 / 应用。
// 零场景改动零拖引用 —— 应用靠两条钩子自己挂上：sceneLoaded（场景里那些）+ UIManager.PushPanel（预制体里的面板）。
// 版本号用 HTTP 的 Last-Modified，不用 version.txt：重打包 mtime 自己就变，没人需要记得改
public static class HotUpdateManager
{
    // 跟游戏服同一台机器，只是端口不同（8011 / 8080）→ 只认 ServerRoot 那一个 IP，别另养一个地址
    // （本机双开时它就是 127.0.0.1，行为跟写死一样；局域网时跟 ServerIP 一起改一处就够）
    static string BaseUrl { get { return "http://" + ServerRoot.ServerIP + ":8080/"; } }
    const string BundleName = "updatesource";             // 包名 == HTTP 文件名 == assetBundleName

    public static bool HasUpdate { get; private set; }

    static readonly string Dir        = Path.Combine(Application.persistentDataPath, "hot");
    static readonly string BundlePath = Path.Combine(Dir, BundleName);
    static readonly string MetaPath   = Path.Combine(Dir, BundleName + ".meta");
    static string BundleUrl { get { return BaseUrl + BundleName; } }

    static AssetBundle m_Bundle;    // 一直持有：Unload 之后已经加载出来的资源会跟着失效
    static readonly Dictionary<string, Sprite> m_Sprites = new Dictionary<string, Sprite>();
    static string m_RemoteVersion;  // 服务端那个 Last-Modified 串，下完写进 .meta
    static bool m_LoadFailed;       // 读包失败只报一次，别每次进场景刷一行

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // ── ① 检查 ────────────────────────────────────────────────────────
    // 两条判据缺一不可：本地包文件不在（清了应用数据就是「账上有货没」）或 .meta 串跟服务端不等
    public static IEnumerator Check()
    {
        HasUpdate = false;

        bool hasLocal = File.Exists(BundlePath);
        string localVer = null;
        if (hasLocal && File.Exists(MetaPath)) localVer = File.ReadAllText(MetaPath).Trim();

        UnityWebRequest req = UnityWebRequest.Head(BundleUrl);
        req.timeout = 5;   // 必须设：默认不超时，服务端没起就永远卡在这
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            // 连不上 / 超时 / 404 都算无更新，是正常路径
            Debug.LogWarning("[热更] 检查失败，按无更新处理：" + req.error + "   " + BundleUrl);
            req.Dispose();
            yield break;
        }

        string ver = req.GetResponseHeader("Last-Modified");
        req.Dispose();

        if (string.IsNullOrEmpty(ver))
        {
            Debug.LogWarning("[热更] 服务端没给 Last-Modified，按无更新处理");
            yield break;
        }

        m_RemoteVersion = ver.Trim();
        HasUpdate = !hasLocal || localVer != m_RemoteVersion;
    }

    // ── ② 下载 ────────────────────────────────────────────────────────
    // 自己下字节，不用 UnityWebRequestAssetBundle —— 那个按 URL 缓存，URL 不变就永远拿到第一次那份
    public static IEnumerator Download(Action<float> onProgress, Action<bool> onDone)
    {
        Directory.CreateDirectory(Dir);

        string tmp = BundlePath + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);   // 上次下崩留下的半截

        UnloadBundle();   // 覆盖之前先卸：Windows 上被 LoadFromFile 映射着的文件删不干净

        UnityWebRequest req = UnityWebRequest.Get(BundleUrl);
        req.timeout = 60;
        req.downloadHandler = new DownloadHandlerFile(tmp);   // 几十 MB 走文件，别走内存
        UnityWebRequestAsyncOperation op = req.SendWebRequest();

        while (!op.isDone)
        {
            if (onProgress != null) onProgress(req.downloadProgress);
            yield return null;
        }
        if (onProgress != null) onProgress(req.downloadProgress);

        // ⚠ 失败不抛异常，必须自己判 result —— 不管它的症状是「加载成功但图是白的」
        bool ok = req.result == UnityWebRequest.Result.Success && File.Exists(tmp);
        if (!ok) Debug.LogError("[热更] 下载失败：" + req.error + "   " + BundleUrl);
        req.Dispose();

        if (ok)
        {
            try
            {
                // 写 tmp 再 Move：下到一半断网不会留个半截文件冒充完整的包
                if (File.Exists(BundlePath)) File.Delete(BundlePath);
                File.Move(tmp, BundlePath);
                File.WriteAllText(MetaPath, m_RemoteVersion);
                m_LoadFailed = false;   // 新包到手，之前那次「读不出来」作废（不清就一局都装不上）
            }
            catch (Exception e)
            {
                ok = false;
                Debug.LogError("[热更] 落盘失败（旧包可能还映射着，重启再试）：" + e.Message);
            }
        }

        if (!ok && File.Exists(tmp)) File.Delete(tmp);

        if (onDone != null) onDone(ok);
    }

    static void UnloadBundle()
    {
        if (m_Bundle == null) return;

        // 传 false：传 true 会把已加载出来的资源一起销毁 → 屏幕上当场变白
        m_Bundle.Unload(false);
        m_Bundle = null;
        m_Sprites.Clear();
    }

    // ── ③ 装载 ────────────────────────────────────────────────────────
    public static bool Load()
    {
        if (m_Bundle != null) return true;
        if (m_LoadFailed) return false;
        if (!File.Exists(BundlePath)) return false;   // 没有包 = 什么都不替换，静默

        m_Bundle = AssetBundle.LoadFromFile(BundlePath);
        if (m_Bundle == null)
        {
            m_LoadFailed = true;
            Debug.LogError("[热更] 包读不出来：" + BundlePath +
                           "\n（LZMA 全压缩的包 LoadFromFile 拿到 null —— 打包要用 ChunkBasedCompression）");
            return false;
        }

        AudioManager.ClearCache();   // 包一到手，之前回退到 Resources 拿的 clip 全过期了

        m_Sprites.Clear();
        foreach (Sprite s in m_Bundle.LoadAllAssets<Sprite>()) m_Sprites[s.name] = s;

        return true;
    }

    // 按名字取包内资源，给那些「按名字加载」的地方用（AudioManager 的音频就靠它）
    public static T GetAsset<T>(string name) where T : UnityEngine.Object
    {
        if (!Load()) return null;   // 没包/读不出来 → null → 调用方回退自带那份

        foreach (T a in m_Bundle.LoadAllAssets<T>())
            if (a.name == name) return a;
        return null;
    }

    // ── ④ 应用 ────────────────────────────────────────────────────────
    // 匹配规则只有一条：包内短名 == 场景物体名。⚠ 匹配不上是静默的：不换、不报错
    public static int ApplyTo(Transform root)
    {
        if (m_Bundle == null || root == null) return 0;

        int n = 0;
        foreach (Image img in root.GetComponentsInChildren<Image>(true))
        {
            Sprite s;
            if (!m_Sprites.TryGetValue(img.gameObject.name, out s)) continue;
            if (img.sprite == s) continue;

            img.sprite = s;
            n++;
        }
        return n;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Load()) return;

        foreach (GameObject go in scene.GetRootGameObjects()) ApplyTo(go.transform);
        ApplyVideo();
    }

    // 包里只有一个视频，不用名字对
    public static VideoClip GetVideoClip()
    {
        if (!Load()) return null;
        foreach (VideoClip c in m_Bundle.LoadAllAssets<VideoClip>()) return c;
        return null;
    }

    // 把正在播的开屏视频换成包里的（下载完当场换，UpdatePanel 调）。场景加载那次也调：
    // clip 已经是包里那份就什么都不做 —— 重启一遍等于白准备一次，还会闪一下旧画面
    public static bool ApplyVideo()
    {
        VideoClip clip = GetVideoClip();
        if (clip == null) return false;

        bool done = false;
        foreach (VideoPlayer vp in UnityEngine.Object.FindObjectsOfType<VideoPlayer>(true))
        {
            if (vp.clip == clip) continue;

            vp.clip = clip;
            vp.Play();   // clip 是场景 load 完才换的，playOnAwake 那一下已经过了
            done = true;
        }
        return done;
    }
}

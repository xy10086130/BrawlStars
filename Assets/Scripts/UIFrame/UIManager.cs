using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager
{
    private static UIManager instance;
    private Transform canvasTrans;

    // 一个场景里可能有好几个根画布，FindObjectsOfType 的顺序又不保证 —— 不认名字就会随机进错那个。
    // LoadingScene 有 OptionCanvas（主界面）+ VideoCanvas（视频容器），进错的表现是面板位置/层级全不对。
    // 按这个顺序认名字，靠前的优先
    static readonly string[] MainCanvasNames = { "OptionCanvas", "Canvas" };
    public bool IsAnyPanelOpen
    {
        get { return panelStack.Count > 1; }
    }
    private UIManager()
    {
        ParseJson();
    }

    public Transform CanvasTrans
    {
        get
        {
            if (canvasTrans != null)
            {
                return canvasTrans;
            }

            // 1. 获取场景中所有的 Canvas
            Canvas[] allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            Canvas sceneCanvas = null;
            Canvas fallback = null;
            int best = int.MaxValue;   // 命中名字在 MainCanvasNames 里的下标，越小越优先

            foreach (Canvas canvas in allCanvases)
            {
                // 只认根画布：
                //  - isRootCanvas 排除嵌套 Canvas —— 有人给某个子物体挂了 Canvas（提层级之类），
                //    它也是 ScreenSpaceOverlay、也在场景里，不加这条就会被选中，
                //    之后 SpawnPanel 把面板全挂到那个子物体的小 Rect 底下，位置和点击全废
                //  - 跳过 DontDestroyOnLoad 的全局画布（如 FlyTextCanvas），只认场景内的 2D 画布
                if (!canvas.isRootCanvas ||
                    canvas.renderMode == RenderMode.WorldSpace ||
                    canvas.gameObject.scene.name == "DontDestroyOnLoad") continue;

                if (fallback == null) fallback = canvas;

                int rank = Array.IndexOf(MainCanvasNames, canvas.gameObject.name);
                if (rank < 0 || rank >= best) continue;
                best = rank;
                sceneCanvas = canvas;
            }

            // 名字一个都没对上 → 退回第一个符合条件的，老场景至少不会比以前更差
            if (sceneCanvas == null) sceneCanvas = fallback;

            if (sceneCanvas != null)
            {
                canvasTrans = sceneCanvas.transform;
                return canvasTrans;
            }

            GameObject canvasPrefab = Resources.Load<GameObject>("UIPrefabs/Canvas");

            if (canvasPrefab == null)
            {
                Debug.LogError(
                    "找不到 Canvas 预制体，请确认文件位于：\n" +
                    "Assets/Resources/UIPrefabs/Canvas.prefab"
                );
                return null;
            }

            GameObject instantiatedCanvas = GameObject.Instantiate(canvasPrefab);
            canvasTrans = instantiatedCanvas.transform;

            return canvasTrans;
        }
    }

    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new UIManager();
            }
            return instance;
        }
    }

    private Dictionary<UIPanelType, string> panelInfoDic = new Dictionary<UIPanelType, string>();
    private Dictionary<UIPanelType, BasePanel> panelDic = new Dictionary<UIPanelType, BasePanel>();
    private Stack<BasePanel> panelStack = new Stack<BasePanel>();

    public void ParseJson()
    {
        TextAsset ta = Resources.Load<TextAsset>("Config/UIPanelType");
        UIPanelTypeJson jsonObj = JsonUtility.FromJson<UIPanelTypeJson>(ta.text);
        foreach (UIPanelInfo info in jsonObj.PanelData)
        {
            panelInfoDic.Add(info.panelType, info.path);
        }
    }


    private BasePanel SpawnPanel(UIPanelType panelType)
    {
        BasePanel panel;

        if (panelDic.TryGetValue(panelType, out panel))
        {
            // 场景切换后 GameObject 可能已被销毁，但字典引用还在
            if (panel == null)
            {
                panelDic.Remove(panelType);
            }
            else
            {
                return panel;
            }
        }

        if (!panelInfoDic.TryGetValue(panelType, out string path))
        {
            Debug.LogError($"没有找到面板配置：{panelType}");
            return null;
        }

        GameObject prefab = Resources.Load<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError(
                $"加载面板预制体失败：{path}\n" +
                $"请确认文件位于 Assets/Resources/{path}.prefab"
            );
            return null;
        }

        GameObject instantiatePanel = GameObject.Instantiate(prefab, CanvasTrans);

        AddScript(panelType, instantiatePanel);

        panel = instantiatePanel.GetComponent<BasePanel>();

        if (panel == null)
        {
            Debug.LogError(
                $"预制体 {path} 上没有成功添加 BasePanel 脚本"
            );

            GameObject.Destroy(instantiatePanel);
            return null;
        }

        panelDic.Add(panelType, panel);
        return panel;
    }

    public void AddScript(UIPanelType panelType, GameObject go)
    {
        string panelName = Enum.GetName(typeof(UIPanelType), panelType);

        Type scriptType = Type.GetType(panelName);

        if (!go.GetComponent(scriptType))
        {
            go.AddComponent(scriptType);
        }
        GetAndAddComponent<CanvasGroup>(go);
    }

    public T GetAndAddComponent<T>(GameObject obj) where T : Component
    {
        // 检查物体身上是否有 T 组件（如 CanvasGroup），没有就自动添加一个
        if (!obj.GetComponent<T>())
        {
            obj.AddComponent<T>();
        }
        return obj.GetComponent<T>();
    }

    public void PushPanel(UIPanelType panelType)
    {
        while (panelStack.Count > 0 && panelStack.Peek() == null)
            panelStack.Pop();

        BasePanel panel = SpawnPanel(panelType);
        if (panel == null)
        {
            Debug.LogError($"无法打开面板：{panelType}");
            return;
        }

        //防止同一个面板创建多次
        if (panelStack.Count > 0 && panelStack.Peek() == panel)
            return;

        if (panelStack.Count > 0)
        {
            panelStack.Peek().OnPause();
        }

        panel.OnOpen();
        AudioManager.BindClicks(panel.transform);   // 必须在 OnOpen 之后：列表行是 OnOpen 里才生成的
        HotUpdateManager.ApplyTo(panel.transform);  // 同 BindClicks：HallBg 在预制体里，场景加载时还没有它
        panelStack.Push(panel);
    }

    public void PopPanel()
    {

        if (panelStack.Count <= 0)
        {
            return;
        }

        BasePanel topPanel = panelStack.Pop();
        AudioManager.PlaySfx(AudioManager.SfxMenuClose);
        topPanel.OnClose();


        if (panelStack.Count <= 0)
        {
            return;
        }

        BasePanel newTopPanel = panelStack.Peek();
        newTopPanel.OnResume();
    }

    public void PopPanel(BasePanel panel)
    {
        if (panelStack.Count <= 0)
        {
            return;
        }

        if (panelStack.Peek() != panel)
        {
            return;
        }

        PopPanel();

    }

 
    public void CloseAll()
    {
        while (panelStack.Count > 0)
        {
            BasePanel panel = panelStack.Pop();
            if (panel != null) panel.OnClose();
        }
    }


}
using UnityEngine;
using UnityEngine.UI;

// 被邀请弹窗：收到 54 弹出（对方点名邀请我组队）
// 确认 = 发 39 加入对方的房（和"输号加入"同一条路，房主那边靠现成的 51 刷新）；拒绝 = 什么都不发
public class InvitedPanel : BasePanel
{
    static InvitedPanel s_Instance;   // 已经弹着又收到一条 54 → 只换内容，不重开
    static string s_Nick;
    static string s_Room;

    string roomCode;                  // 本弹窗当前这条邀请对应的房号

    public Text title;
    public Text inviterName;
    public Button submitBtn;
    public Button refuseBtn;

    protected override void Awake()
    {
        base.Awake();

        // 预制体上没挂本脚本 → 四个字段在 Inspector 里根本没法拖，全靠这里兜底（节点名已在预制体固定）
        if (title == null) title = transform.Find("title").GetComponent<Text>();
        if (inviterName == null) inviterName = transform.Find("inviterName").GetComponent<Text>();
        if (submitBtn == null) submitBtn = transform.Find("submitBtn").GetComponent<Button>();
        if (refuseBtn == null) refuseBtn = transform.Find("refuseBtn").GetComponent<Button>();

        submitBtn.onClick.AddListener(OnClickSubmit);
        refuseBtn.onClick.AddListener(() => UIManager.Instance.PopPanel());
    }

    void OnDestroy()
    {
        if (s_Instance == this) s_Instance = null;
    }

    // 收到 54 的入口（MainPanel 常驻监听里调）。只推无答，不看当前开着哪个面板
    public static void Show(string nickName, string room)
    {
        s_Nick = nickName;
        s_Room = room ?? "";

        if (s_Instance != null)
        {
            s_Instance.Fill();
            return;
        }
        UIManager.Instance.PushPanel(UIPanelType.InvitedPanel);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        // 面板被 UIManager 缓存复用（PopPanel 只淡出、不销毁，下次 Push 不再走 Awake）
        // → s_Instance 得在每次打开时登记，否则"关掉后再被邀请"那一轮是 null，接受后关不掉
        s_Instance = this;
        Fill();
    }

    public override void OnClose()
    {
        s_Instance = null;
        base.OnClose();
    }

    void Fill()
    {
        roomCode = s_Room;
        if (inviterName != null) inviterName.text = s_Nick;
        if (title != null) title.text = s_Nick + "想和您组队游戏！";
    }

    void OnClickSubmit()
    {
        // 捕获局部量：回包是异步的，这中间弹窗可能已经关了
        string code = roomCode;
        int heroId = HallManager.Instance != null ? HallManager.Instance.curIndex : 0;

        ProtoManager.CSRoomJoin(code, heroId, msg =>
        {
            if (msg.Result == (int)LobbyRoomResult.Success)
            {
                MainPanel.EnterRoom(msg.RoomCode, false, -1);
                MainPanel.RefreshFromMembers(msg.RoomCode, msg.Members);   // 定 IsHost + 对手模型出场
                if (s_Instance != null) UIManager.Instance.PopPanel();
            }
            else
            {
                FlyText.Show(InvitePanel.ResultText(msg.Result));   // 房可能已经散了，飘字留弹窗
            }
        });
    }
}

using UnityEngine;
using UnityEngine.UI;

// 输号加入约战房：输入 7 位房号 → 39 → 成功进房态（对手模型同台）关面板；失败飘字留面板
public class InputTeamIDPanel : BasePanel
{
    public InputField ipt;
    public Button joinBtn;

    protected override void Awake()
    {
        base.Awake();
        joinBtn.onClick.AddListener(OnClickJoin);
    }

    void OnClickJoin()
    {
        string code = ipt.text.Trim().ToUpperInvariant();   // 房号 7 位大写字母，容错小写输入
        if (code.Length == 0) return;

        ProtoManager.CSRoomJoin(code, HallManager.Instance.curIndex, msg =>
        {
            if (msg.Result == (int)LobbyRoomResult.Success)
            {
                MainPanel.EnterRoom(msg.RoomCode, false, -1);
                MainPanel.RefreshFromMembers(msg.RoomCode, msg.Members);  // 定 IsHost + 对手模型出场
                UIManager.Instance.PopPanel();      // 关自己
                UIManager.Instance.PopPanel();      // 关 InvitePanel
            }
            else
            {
                FlyText.Show(InvitePanel.ResultText(msg.Result));   // 失败飘字留面板，可改房号重输
            }
        });
    }
}

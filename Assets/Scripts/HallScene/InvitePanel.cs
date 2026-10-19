using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InvitePanel : BasePanel
{
    private RectTransform rect;

    public GameObject tips;
    public Button tipsConfirmBtn;

    public Button createTeamBtn;
    public Button joinTeamBtn;

    public Transform onlineFriendList;              // 在线好友列表容器（预制体里叫 onlineFriendsList）
    public GameObject onlineFriendSlotPrefab;       // UIPrefabs/onlineFriendSlot

    private const float OpenX = 970f;
    private const float ClosedX = 1779f;

    protected override void Awake()
    {
        base.Awake();
        rect = GetComponent<RectTransform>();

        // 没在 Inspector 拖也能跑（节点名已在预制体固定）
        if (onlineFriendList == null) onlineFriendList = transform.Find("onlineFriendsList");
        if (onlineFriendSlotPrefab == null)
            onlineFriendSlotPrefab = Resources.Load<GameObject>("UIPrefabs/onlineFriendSlot");

        createTeamBtn.onClick.AddListener(OnClickCreate);
        joinTeamBtn.onClick.AddListener(() => tips.SetActive(true));
        tipsConfirmBtn.onClick.AddListener(() =>
        {
            tips.SetActive(false);
            UIManager.Instance.PushPanel(UIPanelType.InputTeamIDPanel);
        });
    }

    private void OnClickCreate()
    {
        // 英雄 id = 大厅当前选中（HallManager.curIndex 与 heroId 同序）
        int heroId = HallManager.Instance.curIndex;
        ProtoManager.CSRoomCreate(heroId, (msg) =>
        {
            if (msg.Result == (int)LobbyRoomResult.Success)
                OnCreateSuccess(msg.RoomCode);   // 进房间态 + 显示房号
            else
                FlyText.Show(ResultText(msg.Result));  // 失败飘字
        });
    }

    public static string ResultText(int result)
    {
        switch (result)
        {
            case (int)LobbyRoomResult.NotLogin: return "未登录";
            case (int)LobbyRoomResult.RoomNotExist: return "房间不存在";
            case (int)LobbyRoomResult.RoomFull: return "房间已满";
            case (int)LobbyRoomResult.NotHost: return "只有房主可以开始对战";
            case (int)LobbyRoomResult.NotEnough: return "至少需要 2 人才能开始";
            case (int)LobbyRoomResult.AlreadyInRoom: return "已在房间或对战中";
            default: return "操作失败";
        }
    }

    private void OnCreateSuccess(string roomCode)
    {
        // 进房态（房号上条 + MainPanel 收起邀请按钮/显示 exitBtn），关闭本面板
        MainPanel.EnterRoom(roomCode, true, -1);
        UIManager.Instance.PopPanel();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        transform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;

        // 在线状态是服务端算好随 33 下发的（没有上下线广播），每次开面板主动要一次最新的
        FriendController.Ensure();
        FriendController.Instance.OnListChanged += RefreshOnlineFriends;
        FriendController.Instance.SendList();
        RefreshOnlineFriends();

        StopAllCoroutines();
        rect.anchoredPosition = new Vector2(ClosedX, rect.anchoredPosition.y);
        StartCoroutine(SlideTo(OpenX, 0.3f));
    }

    public override void OnClose()
    {
        base.OnClose();

        if (FriendController.Instance != null)
            FriendController.Instance.OnListChanged -= RefreshOnlineFriends;

        transform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;

        StopAllCoroutines();
        StartCoroutine(SlideTo(ClosedX, 0.2f));
    }

    // 在线好友列表：只列 Online 的，点头像旁边按钮 = 一键邀请
    void RefreshOnlineFriends()
    {
        if (onlineFriendList == null || onlineFriendSlotPrefab == null) return;

        for (int i = onlineFriendList.childCount - 1; i >= 0; i--)
            Destroy(onlineFriendList.GetChild(i).gameObject);

        foreach (FriendInfo fi in FriendController.Instance.Friends)
        {
            if (!fi.Online) continue;

            GameObject go = Instantiate(onlineFriendSlotPrefab, onlineFriendList);
            Transform nameNode = go.transform.Find("name");
            if (nameNode != null) nameNode.GetComponent<Text>().text = fi.NickName;

            Button inviteBtn = go.transform.Find("inviteBtn").GetComponent<Button>();
            FriendInfo target = fi;   // 闭包捕获：别直接用循环变量 fi
            inviteBtn.onClick.AddListener(() => OnClickInvite(target));
        }
    }

    void OnClickInvite(FriendInfo target)
    {
        // 自己还没有房 → 服务端现建一个，回包里带回房号（同"创建队伍"的收尾）
        ProtoManager.CSTeamInvite(target.UserID, HallManager.Instance.curIndex, msg =>
        {
            if (msg.Result == (int)TeamInviteResult.Success)
            {
                FlyText.Show("已邀请 " + target.NickName);
                OnCreateSuccess(msg.RoomCode);
            }
            else
            {
                FlyText.Show(InviteResultText(msg.Result));
            }
        });
    }

    static string InviteResultText(int result)
    {
        switch (result)
        {
            case (int)TeamInviteResult.NotLogin: return "未登录";
            case (int)TeamInviteResult.Offline: return "对方不在线";
            case (int)TeamInviteResult.TargetBusy: return "对方正在房间或对战中";
            case (int)TeamInviteResult.SelfBusy: return "你正在房间或对战中";
            case (int)TeamInviteResult.RoomFull: return "队伍已满，无法再邀请";
            default: return "邀请失败";
        }
    }

    // 把面板 X 从当前位置插值滑到 targetX
    IEnumerator SlideTo(float targetX, float duration)
    {
        float from = rect.anchoredPosition.x;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(from, targetX, t / duration);
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            yield return null;
        }
        rect.anchoredPosition = new Vector2(targetX, rect.anchoredPosition.y);
    }
}

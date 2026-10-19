using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class MainPanel : BasePanel
{
    public static string s_RoomCode = "";   // 约战房房号；非空 = 在房内
    public static bool s_IsHost;            // 是否房主
    public static int s_GuestHeroId = -1;   // 对手英雄 id（-1 = 无对手）

    // 单人匹配发 46 用的服务端模式号（默认 = 第一张图 = 荒野决斗 = 4 人混战）
    public static int s_Mode = BattleRoster.ModeSolo4;

    // 模式图序号 → 服务端模式号。sprites 是 Inspector 里配的：0=荒野决斗(4 人) 1=宝石争霸 2=赏金猎人
    // 后两个玩法还没写 → ModeNone：点开始只提示，不发 46（否则服务端 ModeInvalid 回一个看不懂的失败）
    static readonly int[] SpriteModes = { BattleRoster.ModeSolo4, BattleRoster.ModeNone, BattleRoster.ModeNone };

    public static MainPanel Instance;

    static bool s_netHooksReady;
    static int s_prevModeIdx = -1;          // 进房前的模式序号（退房恢复用，-1 = 本实例没进过房）

    public Sprite[] sprites;
    public Button SelectCharacterBtn;
    public Button FriendBtn;
    public Button InviteBtn1;
    public Button InviteBtn2;
    public Button modelSelectBtn;
    public Button PlayBtn;
    public Button settingBtn;
    public Text nickName;

    public RectTransform currencyGold;      // 房态时上滑隐藏
    public RectTransform currencyGemstone;

    public GameObject teamID;
    public Text teamIDValue;
    public Button exitBtn;

    Image modeImg;
    int index = 0;
    bool roomUiOn;          // 房态 UI 是否生效（进出房沿触发主模型让位一次）

    RectTransform[] currency;
    Vector2[] currencyHome;
    bool currencyUp;        // 币图标是否已上滑隐藏（防 ApplyRoomUI 重入反复 tween）

    protected override void Awake()
    {
        base.Awake();
        Instance = this;

        // 编辑器拖了引用就直接用；没拖按路径兜底（节点名已在预制体固定）
        if (teamID == null) teamID = transform.Find("teamID").gameObject;
        if (teamIDValue == null) teamIDValue = transform.Find("teamID/teamIDValue").GetComponent<Text>();
        if (exitBtn == null) exitBtn = transform.Find("exitBtn").GetComponent<Button>();

        // settingBtn 的嵌套 Canvas 是"全局最上层"开关，但预制体上勾了 Override Sorting
        // 就等于开局第一帧起就压过所有面板 —— 那时还没人打开过设置面板，
        // SettingPanel 的 OnPause/OnClose 一次都没跑过，没人来关它。
        // 这里强制归零，之后统一交给 SettingPanel 跟着面板栈开关（预制体上勾没勾都不算数）。
        Canvas gearCanvas = settingBtn.GetComponent<Canvas>();
        if (gearCanvas != null) gearCanvas.overrideSorting = false;

        // 上滑的基准位：anchoredPosition 是 RectTransform 里序列化的值，不等布局就能取
        currency = new[] { currencyGold, currencyGemstone };
        currencyHome = new Vector2[currency.Length];
        for (int i = 0; i < currency.Length; i++)
            if (currency[i] != null) currencyHome[i] = currency[i].anchoredPosition;
    }

    void Start()
    {
        Instance = this;
        AudioManager.PlayBgm(AudioManager.BgmHall);

        // 先给 PlayBtn 定成自己的音，再批量挂普通的（BindClicks 会跳过已挂过的）
        PlayBtn.gameObject.AddComponent<ButtonSfx>().Sfx = AudioManager.SfxStart;
        AudioManager.BindClicks(transform);

        nickName.text = PlayerInfo.Instance.NickName;
        modeImg = modelSelectBtn.GetComponent<Image>();

        SelectCharacterBtn.onClick.AddListener(() => UIManager.Instance.PushPanel(UIPanelType.CharacterPanel));
        FriendBtn.onClick.AddListener(() => UIManager.Instance.PushPanel(UIPanelType.FriendPanel));
        InviteBtn1.onClick.AddListener(() => UIManager.Instance.PushPanel(UIPanelType.InvitePanel));
        InviteBtn2.onClick.AddListener(() => UIManager.Instance.PushPanel(UIPanelType.InvitePanel));
        modelSelectBtn.onClick.AddListener(SelectModel);
        PlayBtn.onClick.AddListener(OnClickPlay);
        settingBtn.onClick.AddListener(() => UIManager.Instance.PushPanel(UIPanelType.SettingPanel));
        exitBtn.onClick.AddListener(() => ProtoManager.CSRoomExit(msg => ClearRoomState()));

        // 48/51/52/54 常驻监听（均只推无答，一次性回包不冲突）：只挂一次
        if (!s_netHooksReady)
        {
            s_netHooksReady = true;
            ProtoManager.AddMatchSuccessListener(OnMatchSuccess);
            ProtoManager.AddRoomStateListener(OnRoomStatePush);
            ProtoManager.AddRoomCanceledListener(OnRoomCanceled);
            ProtoManager.AddTeamInvitedListener(OnTeamInvited);
        }

        ApplyRoomUI();  // 进场按静态状态恢复（房态刷新/单人默认）
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- 静态入口（各面板调）----

    // 建房/加入成功 → 进房态（guestHeroId 可先 -1，随后 RefreshFromMembers 覆盖）
    public static void EnterRoom(string code, bool isHost, int guestHeroId)
    {
        s_RoomCode = code ?? "";
        s_IsHost = isHost;
        s_GuestHeroId = guestHeroId;
        if (Instance != null) Instance.ApplyRoomUI();
    }

    // 退出/解散/开战 → 回单人态
    public static void ClearRoomState()
    {
        if (string.IsNullOrEmpty(s_RoomCode)) return;
        s_RoomCode = "";
        s_IsHost = false;
        s_GuestHeroId = -1;
        if (Instance != null) Instance.ApplyRoomUI();
    }

    // 51 推送 / 38-39 回包共用：Members 找自己定 IsHost，找另一人定对手模型
    public static void RefreshFromMembers(string roomCode, List<RoomMemberInfo> members)
    {
        if (members == null) return;
        if (!string.IsNullOrEmpty(roomCode)) s_RoomCode = roomCode;

        bool sawSelf = false;
        int guest = -1;
        foreach (RoomMemberInfo m in members)
        {
            if (m.UserID == PlayerInfo.Instance.UID)
            {
                s_IsHost = m.IsHost;
                sawSelf = true;
            }
            else
            {
                guest = m.HeroId;
            }
        }
        if (!sawSelf) return;   // 列表里没自己 = 异常数据，不动

        s_GuestHeroId = guest;
        if (Instance != null) Instance.ApplyRoomUI();
    }

    // 48 常驻：组队 49 开战/随机匹配成功都在这里进过渡场景（MatchPanel 已删注册，见 MatchScene）
    // 逐行同旧 MatchPanel.OnMatchSuccess
    static void OnMatchSuccess(MsgMatchSuccess msg)
    {
        if (msg.Result == 0)
        {
            MatchPanel.OpponentNickName = msg.OpponentNickName;
            MatchPanel.OpponentHeroId = msg.OpponentHeroId;
            // 己方英雄快照：组队 49 流不经 MatchScene，48 到达时人在大厅 → 现场补存（BattleEntry 靠它实例化）
            // 单人随机流此值进 MatchScene 前已写，此处 HallManager 已销毁不会覆盖
            if (HallManager.Instance != null)
                MatchPanel.HeroId = HallManager.Instance.curIndex;
            // 名单必须在 ClearRoomState 之前取（clear 会清 s_RoomCode）；组队房恒 1v1，单人匹配用选中的模式
            BattleRoster.Set(string.IsNullOrEmpty(s_RoomCode) ? s_Mode : BattleRoster.ModeDuel1v1, msg.Members);
            if (!string.IsNullOrEmpty(s_RoomCode)) ClearRoomState();    // 组队房开战收尾：房已被服务端销毁
            TransitionMgr.pendingTarget = TargetScene.BattleScene;
            SceneManager.LoadScene("TransitionScene");
        }
        else
        {
            // 48{Result:1} 匹配被取消（Loading 掉线/超时）→ 回大厅
            FlyText.Show("匹配被取消");
            ClearRoomState();
            SceneManager.LoadScene("HallScene");
        }
    }

    // 51 常驻推送：成员状态变化（对方加入/换英雄/退出）→ 刷新 IsHost/对手模型
    static void OnRoomStatePush(MsgRoomState msg)
    {
        RefreshFromMembers(msg.RoomCode, msg.Members);
    }

    // 52 常驻推送：房间销毁（同伴掉线/房主退）→ 回单人态
    static void OnRoomCanceled(MsgRoomCanceled msg)
    {
        FlyText.Show("房间已解散");
        ClearRoomState();
    }

    // 54 常驻推送：有人邀请我组队 → 弹确认框（确认后才发 39 进对方的房）
    static void OnTeamInvited(MsgTeamInvited msg)
    {
        InvitedPanel.Show(msg.InviterNickName, msg.RoomCode);
    }


    static int ModeOf(int i)
    {
        return (i >= 0 && i < SpriteModes.Length) ? SpriteModes[i] : BattleRoster.ModeNone;
    }


    void OnClickPlay()
    {
        if (string.IsNullOrEmpty(s_RoomCode))
        {
            if (s_Mode == BattleRoster.ModeNone) { FlyText.Show("该模式还没做"); return; }
            MatchPanel.HeroId = HallManager.Instance.curIndex;
            SceneManager.LoadScene("MatchScene");
        }
        else
        {

            ProtoManager.CSRoomStart(msg =>
            {
                if (msg.Result != (int)LobbyRoomResult.Success)
                    FlyText.Show(InvitePanel.ResultText(msg.Result));
            });
        }
    }

    void SelectModel()
    {
        if (!string.IsNullOrEmpty(s_RoomCode)) return;
        index = (index + 1) % sprites.Length;
        modeImg.sprite = sprites[index];
        s_Mode = ModeOf(index);
    }

    // 房态：币图标上滑出屏；单人态滑回。同 HallManager.SetOwnRoomShift 的做法
    void ShiftCurrency(bool up)
    {
        if (up == currencyUp) return;
        currencyUp = up;
        for (int i = 0; i < currency.Length; i++)
        {
            RectTransform rt = currency[i];
            if (rt == null) continue;

            // y=500 的图标再上移一个自身高度（90.4+20）就整个越过 1080 画布顶边
            // 老版 DOTween（1.0.x）没有 DOAnchorPosY，只有 Vector2 版的 DOAnchorPos，x 原样带回
            float y = currencyHome[i].y + (up ? rt.rect.height + 20f : 0f);
            rt.DOKill();
            rt.DOAnchorPos(new Vector2(currencyHome[i].x, y), 0.25f)
              .SetEase(up ? Ease.InBack : Ease.OutBack);
        }
    }

    void ApplyRoomUI()
    {
        bool inRoom = !string.IsNullOrEmpty(s_RoomCode);

        ShiftCurrency(inRoom);
        teamID.SetActive(inRoom);
        if (inRoom) teamIDValue.text = s_RoomCode;
        exitBtn.gameObject.SetActive(inRoom);
        InviteBtn1.gameObject.SetActive(!inRoom);
        InviteBtn2.gameObject.SetActive(!inRoom);

        bool modeChanged = (inRoom && s_prevModeIdx < 0) || (!inRoom && s_prevModeIdx >= 0);
        if (modeChanged)
        {
            if (inRoom)
            {
                s_prevModeIdx = index;
                index = Mathf.Min(2, sprites.Length - 1);   // 房间态 = sprites[2]
            }
            else
            {
                index = s_prevModeIdx;
                s_prevModeIdx = -1;
            }
            modeImg.sprite = sprites[index];
        }

        // 房间态 = 1v1（49 那条线服务端恒 1v1，图是"约战"观感）；单人态 = 当前选中的图
        s_Mode = inRoom ? BattleRoster.ModeDuel1v1 : ModeOf(index);

        if (roomUiOn != inRoom)
        {
            roomUiOn = inRoom;
            if (HallManager.Instance != null) HallManager.Instance.SetOwnRoomShift(inRoom);
        }

        if (HallManager.Instance != null)
        {
            if (s_GuestHeroId >= 0) HallManager.Instance.ShowGuest(s_GuestHeroId);
            else HallManager.Instance.HideGuest();
        }
    }
}

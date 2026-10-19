using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FriendPanel : BasePanel
{
    public Text m_UID;
    public InputField searchIpt;
    public Button addBtn;

    public Button TabMyFriendBtn;
    public Button TabRequestBtn;

    public Text TipText;

    public ScrollRect scrollView;
    public Transform myFriendListContent;
    public Transform friendRequestContent;

    public Text nullRequestText;

    public GameObject myFriendSlotPrefab;
    public GameObject friendRequestSlotPrefab;

    Sprite defaultTabSprite;
    Sprite selectedTabSprite;
    Sprite addBtnNormal;
    Sprite addBtnActive;

    void Start()
    {
        m_UID.text = PlayerInfo.Instance.UID;

        defaultTabSprite = TabMyFriendBtn.GetComponent<Image>().sprite;
        selectedTabSprite = Resources.Load<Sprite>("friend/MOBAbutton");
        addBtnNormal = addBtn.GetComponent<Image>().sprite;
        addBtnActive = Resources.Load<Sprite>("friend/btn_gold");
        addBtn.interactable = false;   // 未搜索到前不可点

        TabMyFriendBtn.onClick.AddListener(() => SwitchTab(true));
        TabRequestBtn.onClick.AddListener(() => SwitchTab(false));
        addBtn.onClick.AddListener(OnClickAdd);
        searchIpt.onEndEdit.AddListener(OnSearchEnd);

        SwitchTab(true);
    }

    public override void OnOpen()
    {
        base.OnOpen();

        FriendController.Ensure();

        FriendController.Instance.OnListChanged += RefreshLists;
        FriendController.Instance.OnSearchChanged += RefreshSearch;
        FriendController.Instance.OnToast += FlyText.Show;
        FriendController.Instance.SendList();
    }

    public override void OnClose()
    {
        FriendController.Instance.OnListChanged -= RefreshLists;
        FriendController.Instance.OnSearchChanged -= RefreshSearch;
        FriendController.Instance.OnToast -= FlyText.Show;
        // 重置面板状态
        searchIpt.text = "";
        addBtn.GetComponent<Image>().sprite = addBtnNormal;
        addBtn.interactable = false;
        SwitchTab(true);
        base.OnClose();
    }

    void SwitchTab(bool myFriend)
    {
        TabMyFriendBtn.GetComponent<Image>().sprite = myFriend ? selectedTabSprite : defaultTabSprite;
        TabRequestBtn.GetComponent<Image>().sprite = myFriend ? defaultTabSprite : selectedTabSprite;
        myFriendListContent.gameObject.SetActive(myFriend);
        friendRequestContent.gameObject.SetActive(!myFriend);
        scrollView.content = (myFriend ? myFriendListContent : friendRequestContent) as RectTransform;
        TipText.text = myFriend ? "好友" : "待处理的好友请求";
    }

    // 搜索（输入回车触发
    void OnSearchEnd(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        FriendController.Instance.Search(text);
    }

    // 搜索结果UI变更
    void RefreshSearch()
    {
        List<FriendInfo> result = FriendController.Instance.SearchResult;
        if (result.Count == 0)
        {
            addBtn.GetComponent<Image>().sprite = addBtnNormal;
            addBtn.interactable = false;
            return;
        }
        addBtn.GetComponent<Image>().sprite = addBtnActive;
        addBtn.interactable = true;
    }


    void OnClickAdd()
    {
        List<FriendInfo> result = FriendController.Instance.SearchResult;
        if (result.Count == 0)
            return;
        FriendController.Instance.Apply(result[0].UserID);
    }


    void RefreshLists()
    {
        FillList(myFriendListContent, myFriendSlotPrefab, FriendController.Instance.Friends);
        FillList(friendRequestContent, friendRequestSlotPrefab, FriendController.Instance.Requests);
        // 无申请时显示"暂无"提示，有申请隐藏
        if (nullRequestText != null)
            nullRequestText.gameObject.SetActive(FriendController.Instance.Requests.Count == 0);
    }

    void FillList(Transform content, GameObject prefab, List<FriendInfo> infos)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        foreach (FriendInfo fi in infos)
        {
            GameObject go = Instantiate(prefab, content);

            ApplySlot apply = go.GetComponent<ApplySlot>();
            if (apply != null)
            {
                apply.Init(fi);
                continue;
            }
            MyFriendSlot friend = go.GetComponent<MyFriendSlot>();
            if (friend != null)
                friend.Init(fi);
        }
    }
}

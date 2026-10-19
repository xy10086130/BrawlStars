using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class MyFriendSlot : MonoBehaviour
{
    public Text nickName;

    public Transform ShowclickContent;
    public Button deleteBtn;

    string friendID;
    static MyFriendSlot currentOpened;   // 当前展开的行，点其他行自动收起

    void Start()
    {
        Button selfBtn = GetComponent<Button>();
        if (selfBtn != null)
            selfBtn.onClick.AddListener(OnClickSelf);
        if (deleteBtn != null) deleteBtn.onClick.AddListener(() => FriendController.Instance.Delete(friendID));
    }

    void OnClickSelf()
    {
        if (ShowclickContent == null) return;
        if (currentOpened != null && currentOpened != this)
            currentOpened.ShowclickContent.gameObject.SetActive(false);
        currentOpened = this;
        ShowclickContent.gameObject.SetActive(true);
    }

    void OnDestroy()
    {
        if (currentOpened == this)
            currentOpened = null;
    }

    void Update()
    {
        // 只在展开状态下检测：点击行外任意处 → 隐藏
        if (ShowclickContent == null || !ShowclickContent.gameObject.activeSelf)
            return;
        if (!Input.GetMouseButtonDown(0))
            return;
        PointerEventData ped = new PointerEventData(EventSystem.current);
        ped.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        foreach (RaycastResult r in results)
        {
            if (r.gameObject == gameObject || r.gameObject.transform.IsChildOf(transform))
                return;   // 点在本行内，不隐藏
        }
        ShowclickContent.gameObject.SetActive(false);
        if (currentOpened == this)
            currentOpened = null;
    }

    public void Init(FriendInfo fi)
    {
        friendID = fi.UserID;
        if (nickName != null) nickName.text = fi.NickName;
        //if (lastLoginTimeText != null) lastLoginTimeText.text = fi.Online ? "在线" : "离线";
    }
}

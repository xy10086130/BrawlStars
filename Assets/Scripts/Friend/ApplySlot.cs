using UnityEngine;
using UnityEngine.UI;

public class ApplySlot : MonoBehaviour
{
    public Text nickName;
    public Button submitBtn;
    public Button refuseBtn;

    string applicantID;

    void Start()
    {
        if (submitBtn != null) submitBtn.onClick.AddListener(() => FriendController.Instance.HandleApply(applicantID, true));
        if (refuseBtn != null) refuseBtn.onClick.AddListener(() => FriendController.Instance.HandleApply(applicantID, false));
    }

    public void Init(FriendInfo fi)
    {
        applicantID = fi.UserID;
        if (nickName != null) nickName.text = fi.NickName;
    }
}

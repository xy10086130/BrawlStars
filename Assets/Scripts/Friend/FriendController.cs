using System;
using System.Collections.Generic;
using UnityEngine;

public class FriendController : MonoBehaviour
{
    public static FriendController Instance;

    public event Action<string> OnToast;   //View 飘字
    public event Action OnListChanged;     //View 订阅，转发自 Model   好友变更或好友申请变更
    public event Action OnSearchChanged;  //搜索结果 

    public List<FriendInfo> Friends => FriendModel.Instance.Friends;
    public List<FriendInfo> Requests => FriendModel.Instance.Requests;
    public List<FriendInfo> SearchResult => FriendModel.Instance.SearchResult;

    bool isApplying;     // Apply 防重发
    bool lastAgree;      // 最近一次 HandleApply 的同意/拒绝

    // 懒创建：面板打开时调（谁先开谁建，之后共用这一个常驻监听）
    public static FriendController Ensure()
    {
        if (Instance == null)
            new GameObject("FriendController").AddComponent<FriendController>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        FriendModel.Instance.Init();                    //常驻监听
        FriendModel.Instance.OnOpResult += OnOpResult;
        FriendModel.Instance.OnListChanged += OnModelListChanged;
        FriendModel.Instance.OnSearchChanged += OnModelSearchChanged;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            FriendModel.Instance.OnOpResult -= OnOpResult;
            FriendModel.Instance.OnListChanged -= OnModelListChanged;
            FriendModel.Instance.OnSearchChanged -= OnModelSearchChanged;
        }
    }

    void OnModelListChanged() { OnListChanged?.Invoke(); }
    void OnModelSearchChanged() { OnSearchChanged?.Invoke(); }

    public void SendList() { FriendModel.Instance.SendList(); }

    public void Search(string text) { FriendModel.Instance.Search(text); }

    public void Apply(string friendID)
    {
        if (isApplying) return;
        isApplying = true;
        FriendModel.Instance.Apply(friendID);
    }

    public void HandleApply(string applicantID, bool agree)
    {
        lastAgree = agree;
        FriendModel.Instance.HandleApply(applicantID, agree);
    }

    public void Delete(string friendID)
    {
        FriendModel.Instance.Delete(friendID);
    }

    void OnOpResult(FriendOp op, FriendResult result)
    {
        switch (op)
        {
            case FriendOp.Apply:
                isApplying = false;
                switch (result)
                {
                    case FriendResult.Success:
                        Toast("申请已发送");
                        FriendModel.Instance.ClearSearch();   // 清搜索态，addBtn 回默认
                        break;
                    case FriendResult.AlreadyFriend:
                        Toast("你们已经是好友了");
                        break;
                    case FriendResult.AlreadyApplied:
                        Toast("已申请过，等待对方同意");
                        break;
                    default:
                        Toast("申请失败");
                        break;
                }
                break;
            case FriendOp.HandleApply:
                Toast(result == FriendResult.Success ? (lastAgree ? "已同意" : "已拒绝") : "操作失败");
                if (result == FriendResult.Success)
                    FriendModel.Instance.SendList();   // 服务端只推申请人，自己主动刷
                break;
            case FriendOp.Delete:
                Toast(result == FriendResult.Success ? "已删除" : "删除失败");
                if (result == FriendResult.Success)
                    FriendModel.Instance.SendList();   // 同上，自己主动刷
                break;
        }
    }

    void Toast(string text)
    {
        OnToast?.Invoke(text);
    }
}

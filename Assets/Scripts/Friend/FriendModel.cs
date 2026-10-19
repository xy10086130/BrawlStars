using System;
using System.Collections.Generic;

// 好友操作类型（35/36/37 回包区分来源）
public enum FriendOp
{
    Apply,
    HandleApply,
    Delete
}

public class FriendModel
{
    public static FriendModel Instance = new FriendModel();

    public List<FriendInfo> Friends = new List<FriendInfo>();
    public List<FriendInfo> Requests = new List<FriendInfo>();
    public List<FriendInfo> SearchResult = new List<FriendInfo>();

    public event Action OnListChanged;                    // Friends/Requests 变
    public event Action OnSearchChanged;                  // SearchResult 变更
    public event Action<FriendOp, FriendResult> OnOpResult;   // 给controller  显示 操作结果

    bool inited;

    /// <summary>
    /// 开始监听
    /// </summary>
    public void Init()
    {
        if (inited) return;
        inited = true;
        ProtoManager.AddFriendListListener(OnFriendList);
    }

    /// <summary>
    /// 请求列表
    /// </summary>
    public void SendList()
    {
        ProtoManager.CSFriendList();
    }

    /// <summary>
    ///  搜索好友
    /// </summary>
    /// <param name="text"></param>
    public void Search(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        ProtoManager.CSFriendSearch(text, OnSearch);
    }

    /// <summary>
    /// 申请加好友
    /// </summary>
    /// <param name="friendID"></param>
    public void Apply(string friendID)
    {
        ProtoManager.CSFriendApply(friendID, msg =>
        {
            OnOpResult?.Invoke(FriendOp.Apply, (FriendResult)msg.Result);
        });
    }

    /// <summary>
    ///  同意/拒绝申请
    /// </summary>
    /// <param name="applicantID"></param>
    /// <param name="agree"></param>
    public void HandleApply(string applicantID, bool agree)
    {
        ProtoManager.CSFriendApplyResult(applicantID, agree, msg =>
        {
            OnOpResult?.Invoke(FriendOp.HandleApply, (FriendResult)msg.Result);
        });
    }

    /// <summary>
    /// 删除好友
    /// </summary>
    /// <param name="friendID"></param>
    public void Delete(string friendID)
    {
        ProtoManager.CSFriendDelete(friendID, msg =>
        {
            OnOpResult?.Invoke(FriendOp.Delete, (FriendResult)msg.Result);
        });
    }

    /// <summary>
    /// 清空搜索结果  申请成功后 addBtn 恢复默认态
    /// </summary>
    public void ClearSearch()
    {
        SearchResult.Clear();
        OnSearchChanged?.Invoke();
    }

    /// <summary>
    /// 监听发服务端推送的消息  好友列表和申请列表 
    /// </summary>
    /// <param name="msg"></param>
    void OnFriendList(MsgFriendList msg)
    {
        if (msg.Result != 0) return;
        Friends = msg.Friends ?? new List<FriendInfo>();
        Requests = msg.Requests ?? new List<FriendInfo>();
        OnListChanged?.Invoke();
    }


    /// <summary>
    /// 搜索的回调函数
    /// </summary>
    /// <param name="msg"></param>
    void OnSearch(MsgFriendSearch msg)
    {
        SearchResult = (msg.Result == 0 && msg.ResultList != null)
            ? msg.ResultList
            : new List<FriendInfo>();
        OnSearchChanged?.Invoke();
    }
}

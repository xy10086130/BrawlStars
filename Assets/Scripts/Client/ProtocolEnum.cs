using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


public enum ProtocolEnum
{
    None = 0,
    MsgSecret = 1, //密钥协议
    MsgPing = 2, //心跳包协议
    MsgRegister = 3,//注册协议
    MsgLogin = 4,//登录协议
    MsgFriendList = 33,
    MsgFriendSearch = 34,
    MsgFriendApply = 35,
    MsgFriendApplyResult = 36,
    MsgFriendDelete = 37,

    MsgRoomCreate = 38,     // 组队房间：建房（幂等=房主换英雄）
    MsgRoomJoin = 39,       // 组队房间：输号加入（幂等=换英雄，纯回包；房间变化推送走 51/52）

    MsgMatchCount = 40,     // 匹配队列人数推送（服务端推）

    MsgBattleReady = 41,
    MsgBeginBattle = 42,
    MsgBattleInput = 43,    // 战斗：客户端意图上报（方向+序号，20Hz，只推无答）
    MsgBattleState = 44,    // 战斗：服务端权威位置快照（15Hz = 每 3 tick）
    MsgBattleResult = 45,   // 战斗：结算（先 KillTarget 杀胜，只推无答）
    MsgMatch = 46,
    MsgMatchCancel = 47,
    MsgMatchSuccess = 48,
    MsgRoomStart = 49,      // 组队房间：房主点开始对战
    MsgRoomExit = 50,       // 组队房间：退出
    MsgRoomState = 51,      // 组队房间状态更新（服务端推送成员状态变化，只推无答）
    MsgRoomCanceled = 52,   // 组队房间已销毁（服务端推送，存活方回大厅，只推无答）

    MsgTeamInvite = 53,     // 一键邀请组队：邀请方发（回包 RoomCode，成功即进房态）
    MsgTeamInvited = 54     // 被邀请通知（服务端推，弹窗确认，只推无答）
}
public enum TeamInviteResult
{
    Success = 0,
    NotLogin = 1,       // 未登录
    Offline = 2,        // 对方不在线
    TargetBusy = 3,     // 对方在房间/对战中
    SelfBusy = 4,       // 自己在别人的房间/对战中
    RoomFull = 5        // 自己的房已满 2 人
}
public enum RegisterResult
{
    Success,
    Failed,
    AlreadyExist,//账号存在
    WrongCode,//验证码错误
    Forbidden
}
public enum LoginResult
{
    Success,
    Failed,
    WrongPwd,
    UserNotExist,
    TimeoutToken
}
public enum FriendResult
{
    Success = 0,        // 申请已发出 / 操作成功
    Failed = 1,         // 失败（申请自己 / 查无此人 / 未登录 / 处理失败）
    AlreadyFriend = 2,  // 已是好友
    AlreadyApplied = 3  // 已申请过
}
public enum FriendRelState
{
    None = 0,           // 无关，可申请
    Friend = 1,         // 已是好友
    Applied = 2,        // 我申请过对方
    RequestedMe = 3     // 对方申请我
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    MsgRoomCreate = 38,     // 组队房间：建房
    MsgRoomJoin = 39,       // 组队房间：输号加入

    MsgMatchCount = 40,     // 匹配队列人数推送

    MsgBattleReady = 41,
    MsgBeginBattle = 42,
    MsgBattleInput = 43,    // 战斗：客户端意图上报（方向+序号，20Hz，只推无答）
    MsgBattleState = 44,    // 战斗：服务端权威位置快照（15Hz = 每 3 tick）
    MsgBattleResult = 45,   // 战斗：结算（先 KillTarget 杀胜，只推无答）
    MsgMatch = 46,
    MsgMatchCancel = 47,
    MsgMatchSuccess = 48,

    MsgRoomStart = 49,      // 组队房间：房主点开始对战
    MsgRoomExit = 50,       // 组队房间：退出（房主退=销毁整房 / 成员退=只踢自己）
    MsgRoomState = 51,      // 组队房间状态更新
    MsgRoomCanceled = 52,   // 组队房间已销毁

    MsgTeamInvite = 53,     // 一键邀请组队：邀请方发（回包 RoomCode）
    MsgTeamInvited = 54     // 被邀请通知：服务端推给被邀请方，弹窗确认（只推无答）
}


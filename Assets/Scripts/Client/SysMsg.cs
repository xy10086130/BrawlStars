using ProtoBuf;
using SimpleServer.Proto;
using System.Collections.Generic;

[ProtoContract]
public class MsgSecret : MsgBase
{
    public MsgSecret()
    {
        ProtocolType = ProtocolEnum.MsgSecret;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }
    [ProtoMember(2)]
    public string Secret;
}
[ProtoContract]
public class MsgPing : MsgBase
{
    public MsgPing()
    {
        ProtocolType = ProtocolEnum.MsgPing;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }
}

[ProtoContract]
public class MsgRegister : MsgBase
{
    public MsgRegister()
    {
        ProtocolType = ProtocolEnum.MsgRegister;
    }

    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string NickName { get; set; }

    [ProtoMember(3)]
    public string Password { get; set; }

    [ProtoMember(4)]
    public RegisterResult Result { get; set; }

    [ProtoMember(5)]
    public string UserID { get; set; }
}
[ProtoContract]
public class MsgLogin : MsgBase
{
    public MsgLogin()
    {
        ProtocolType = ProtocolEnum.MsgLogin;
    }

    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string NickName { get; set; }

    [ProtoMember(3)]
    public string Password { get; set; }

    [ProtoMember(4)]
    public LoginResult Result { get; set; }

    [ProtoMember(5)]
    public string UserID { get; set; }
}

// 好友信息
[ProtoContract]
public class FriendInfo
{
    [ProtoMember(1)]
    public string UserID { get; set; }

    [ProtoMember(2)]
    public string NickName { get; set; }

    [ProtoMember(3)]
    public bool Online { get; set; }

    [ProtoMember(4)]
    public int RelState { get; set; }   // 0=无关 1=好友 2=我申请过对方 3=对方申请我
}

// 好友列表（请求/推送通用，客户端收到即刷新）
[ProtoContract]
public class MsgFriendList : MsgBase
{
    public MsgFriendList()
    {
        ProtocolType = ProtocolEnum.MsgFriendList;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public List<FriendInfo> Friends { get; set; }

    [ProtoMember(3)]
    public List<FriendInfo> Requests { get; set; }

    [ProtoMember(4)]
    public int Result { get; set; }     // 0成功 1未登录
}

// 好友搜索
[ProtoContract]
public class MsgFriendSearch : MsgBase
{
    public MsgFriendSearch()
    {
        ProtocolType = ProtocolEnum.MsgFriendSearch;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string SearchText { get; set; }

    [ProtoMember(3)]
    public List<FriendInfo> ResultList { get; set; }

    [ProtoMember(4)]
    public int Result { get; set; }     // 0成功 1未登录
}

// 好友申请
[ProtoContract]
public class MsgFriendApply : MsgBase
{
    public MsgFriendApply()
    {
        ProtocolType = ProtocolEnum.MsgFriendApply;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string FriendID { get; set; }    // 目标 UserID

    [ProtoMember(3)]
    public int Result { get; set; }         // 0成功 1失败 2已是好友 3已申请过
}

// 处理申请（同意/拒绝）
[ProtoContract]
public class MsgFriendApplyResult : MsgBase
{
    public MsgFriendApplyResult()
    {
        ProtocolType = ProtocolEnum.MsgFriendApplyResult;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string FriendID { get; set; }    // 申请人 UserID

    [ProtoMember(3)]
    public bool Agree { get; set; }         // true同意 false拒绝

    [ProtoMember(4)]
    public int Result { get; set; }         // 0成功 1失败
}

// 删除好友
[ProtoContract]
public class MsgFriendDelete : MsgBase
{
    public MsgFriendDelete()
    {
        ProtocolType = ProtocolEnum.MsgFriendDelete;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string FriendID { get; set; }

    [ProtoMember(3)]
    public int Result { get; set; }         // 0成功 1失败
}


// 匹配入队：先选英雄再匹配，服务端按模式分队列
[ProtoContract]
public class MsgMatch : MsgBase
{
    public MsgMatch()
    {
        ProtocolType = ProtocolEnum.MsgMatch;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Mode { get; set; }           // 1=1v1

    [ProtoMember(3)]
    public int HeroId { get; set; }

    [ProtoMember(4)]
    public int Result { get; set; }         // 0入队 1未登录 2已在房间 3模式无效
}

// 取消匹配
[ProtoContract]
public class MsgMatchCancel : MsgBase
{
    public MsgMatchCancel()
    {
        ProtocolType = ProtocolEnum.MsgMatchCancel;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Result { get; set; }         // 0成功 1失败
}

// 匹配结果（服务端广播）：0=匹配成功（带对手信息）；1=匹配被取消（Loading掉线/超时，存活方回大厅）
[ProtoContract]
public class MsgMatchSuccess : MsgBase
{
    public MsgMatchSuccess()
    {
        ProtocolType = ProtocolEnum.MsgMatchSuccess;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Result { get; set; }

    [ProtoMember(3)]
    public string OpponentUserID { get; set; }

    [ProtoMember(4)]
    public string OpponentNickName { get; set; }

    [ProtoMember(5)]
    public int OpponentHeroId { get; set; }

    // 全员名单（含自己）。1v1 也填，但老客户端只读上面三个"对手"字段，不受影响
    [ProtoMember(6)]
    public List<BattleMember> Members { get; set; }
}

// 48 里的一个座位：谁、什么英雄、出生在哪
[ProtoContract]
public class BattleMember
{
    [ProtoMember(1)]
    public string UID { get; set; }

    [ProtoMember(2)]
    public string NickName { get; set; }

    [ProtoMember(3)]
    public int HeroId { get; set; }

    [ProtoMember(4)]
    public float X { get; set; }            // 出生点（服务端建房时抽签定死，客户端照摆）

    [ProtoMember(5)]
    public float Z { get; set; }
}

// 场景加载完成（双方各发一次，服务端计数收齐 2 个）
[ProtoContract]
public class MsgBattleReady : MsgBase
{
    public MsgBattleReady()
    {
        ProtocolType = ProtocolEnum.MsgBattleReady;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Result { get; set; }         // 0成功 1未登录 2不在房间
}

// 开战广播（收齐 41 后发出，双方开始输入；服务端同时建 BattleInstance 开始 20Hz tick）
[ProtoContract]
public class MsgBeginBattle : MsgBase
{
    public MsgBeginBattle()
    {
        ProtocolType = ProtocolEnum.MsgBeginBattle;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Result { get; set; }         // 0成功 1未登录 2不在房间
}

// 匹配队列人数推送（服务端推：入队/取消/配对后，给该模式队列剩余所有人）
[ProtoContract]
public class MsgMatchCount : MsgBase
{
    public MsgMatchCount()
    {
        ProtocolType = ProtocolEnum.MsgMatchCount;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Count { get; set; }          // 当前队列人数
}

// 38/39/49/50 组队房间结果码：0成功 1未登录 2房不存在 3房满 4非房主 5不足2人
// 6已在房间（战斗房 Loading 中 / 在别的约战房）
public enum LobbyRoomResult
{
    Success = 0,
    NotLogin = 1,
    RoomNotExist = 2,
    RoomFull = 3,
    NotHost = 4,
    NotEnough = 5,
    AlreadyInRoom = 6
}

// 房内成员信息（38/39 回包与 51 推送的 Members 列表元素，含昵称，IsHost 房主标记）
[ProtoContract]
public class RoomMemberInfo
{
    [ProtoMember(1)]
    public string UserID { get; set; }

    [ProtoMember(2)]
    public string NickName { get; set; }

    [ProtoMember(3)]
    public int HeroId { get; set; }

    [ProtoMember(4)]
    public bool IsHost { get; set; }
}

// 38 建房：客户端带 HeroId 发；回包填 RoomCode/Result/Members
// 幂等：房主已建房再发 38 = 换英雄（服务端刷新 Members 回包 + 51 推成员）
[ProtoContract]
public class MsgRoomCreate : MsgBase
{
    public MsgRoomCreate()
    {
        ProtocolType = ProtocolEnum.MsgRoomCreate;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string RoomCode { get; set; }    // 7 位大写字母房号

    [ProtoMember(3)]
    public int HeroId { get; set; }         // 请求：自己的英雄

    [ProtoMember(4)]
    public int Result { get; set; }         // 0成功 其余见 LobbyRoomResult

    [ProtoMember(5)]
    public List<RoomMemberInfo> Members { get; set; }
}

// 39 加入：客户端带 RoomCode+HeroId 发；回包填 Result/Members（纯回包，不兼推流）
// 幂等：已在此房再发 39 = 换英雄；房间变化推送走 51 MsgRoomState / 52 MsgRoomCanceled
[ProtoContract]
public class MsgRoomJoin : MsgBase
{
    public MsgRoomJoin()
    {
        ProtocolType = ProtocolEnum.MsgRoomJoin;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string RoomCode { get; set; }    // 请求：要加入的房号

    [ProtoMember(3)]
    public int HeroId { get; set; }         // 请求：自己的英雄

    [ProtoMember(4)]
    public int Result { get; set; }         // 0成功 其余见 LobbyRoomResult

    [ProtoMember(5)]
    public List<RoomMemberInfo> Members { get; set; }
}

// 49 房主点"开始对战"：仅房主 + 满 2 人有效；服务端先回 49{Success}
// 再销毁约战房 → RoomManager.BeginPairing 走随机匹配同路（48/41/42 老流程）
[ProtoContract]
public class MsgRoomStart : MsgBase
{
    public MsgRoomStart()
    {
        ProtocolType = ProtocolEnum.MsgRoomStart;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Result { get; set; }         // 0成功 其余见 LobbyRoomResult
}

// 50 退出房间：恒回 50{Success}（幂等：不在房也空回成功，无操作）
// 房主退 = 整房销毁（推成员 52 MsgRoomCanceled）；成员退 = 只移出自己（推房主 51 刷新，房继续等新成员）
[ProtoContract]
public class MsgRoomExit : MsgBase
{
    public MsgRoomExit()
    {
        ProtocolType = ProtocolEnum.MsgRoomExit;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Result { get; set; }         // 0成功 其余见 LobbyRoomResult
}

// 51 房间状态推送（服务端 → 存活方，只推无答）：房内成员变化后推"另一方"刷新
// 触发：新成员加入 / 成员换英雄 / 成员退出；顶替旧 39{Success} 推流
[ProtoContract]
public class MsgRoomState : MsgBase
{
    public MsgRoomState()
    {
        ProtocolType = ProtocolEnum.MsgRoomState;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string RoomCode { get; set; }

    [ProtoMember(3)]
    public List<RoomMemberInfo> Members { get; set; }
}

// 52 房间销毁通知（服务端 → 存活方，只推无答）：房主退 / 同伴掉线，整房销毁回大厅
// 顶替旧 39{Canceled} 推流
[ProtoContract]
public class MsgRoomCanceled : MsgBase
{
    public MsgRoomCanceled()
    {
        ProtocolType = ProtocolEnum.MsgRoomCanceled;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }
}

// ---- 战斗：位置同步第一步（43/44），攻击/血量/结算的字段后面追加 ProtoMember 编号 ----

// 43 客户端 → 服务端，20Hz：只报意图（方向+序号），不报位置；服务端不回包
[ProtoContract]
public class MsgBattleInput : MsgBase
{
    public MsgBattleInput()
    {
        ProtocolType = ProtocolEnum.MsgBattleInput;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public int Seq { get; set; }            // 递增序号，服务端 ≤lastSeq 丢弃（防重放）

    [ProtoMember(3)]
    public float MoveX { get; set; }        // 移动方向（单位向量），0,0 = 不动

    [ProtoMember(4)]
    public float MoveZ { get; set; }

    [ProtoMember(5)]
    public float AimX { get; set; }         // 瞄准方向（单位向量），0,0 = 没在瞄准（朝向退回移动方向）

    [ProtoMember(6)]
    public float AimZ { get; set; }

    [ProtoMember(7)]
    public int Buttons { get; set; }        // 位掩码：1=普攻（按住即持续，服务端靠攻间隔限流）
}

// 单个玩家的权威状态（客户端按 UID 认出自己那条）
[ProtoContract]
public class BattleHeroState
{
    [ProtoMember(1)]
    public string UID { get; set; }

    [ProtoMember(2)]
    public float X { get; set; }

    [ProtoMember(3)]
    public float Z { get; set; }

    [ProtoMember(4)]
    public float Facing { get; set; }       // 朝向角度（度）

    [ProtoMember(5)]
    public float MoveMag { get; set; }      // 移动意图强度 0~1：客户端播走动画用，不能靠"位置有没有变"推

    [ProtoMember(6)]
    public int HeroId { get; set; }

    [ProtoMember(7)]
    public int Hp { get; set; }

    [ProtoMember(8)]
    public int MaxHp { get; set; }

    [ProtoMember(9)]
    public bool Alive { get; set; }

    [ProtoMember(10)]
    public int Kills { get; set; }

    [ProtoMember(11)]
    public int RespawnMs { get; set; }      // 剩余复活时间（>0 = 正在等复活）

    [ProtoMember(12)]
    public int AttackSeq { get; set; }      // 每次挥击 +1：客户端拿它播挥击动画，不能靠"掉没掉血"推

    // 13 是草丛加的（2026-09-13）：对【收到这条消息的人】而言这个英雄可不可见。
    // ⚠ 服务端从此每人一份包，两端这个字段必须同名同号，漏一边会静默错位
    [ProtoMember(13)]
    public bool CanSee { get; set; }        // false = 在草丛里躲着，X/Z 是"最后一次被看见的位置"，不是真坐标

    // 14 是弹药装的（2026-09-15）：0..3 的【连续值】，不是整数格数。
    // 可用格数 = (int)Ammo，正在充的那格进度 = Ammo - (int)Ammo —— 弹药条全靠这个小数部分。
    // ⚠ 必须与【服务端】 Proto/SysMsg.cs 的 14 同名同号，错一个就是静默错位
    [ProtoMember(14)]
    public float Ammo { get; set; }
}

// 44 服务端 → 客户端，20Hz（每 tick，StateEveryTicks=1）：双方权威位置，只推无答
[ProtoContract]
public class MsgBattleState : MsgBase
{
    public MsgBattleState()
    {
        ProtocolType = ProtocolEnum.MsgBattleState;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public List<BattleHeroState> Players { get; set; }

    [ProtoMember(3)]
    public List<BattleHitEvent> Events { get; set; }   // 本帧内发生的命中，发完即清（客户端拿去飘字/播特效）
}

// 45 服务端 → 双方，一局只发一次：谁赢靠 WinnerUID 和自己 UID 比对。
// 击杀数直接从这带出来，不去读最后一份 44 —— 45 和 44 谁先到不保证，两边的数字可能差一个
[ProtoContract]
public class MsgBattleResult : MsgBase
{
    public MsgBattleResult()
    {
        ProtocolType = ProtocolEnum.MsgBattleResult;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string WinnerUID { get; set; }

    [ProtoMember(3)]
    public int WinnerKills { get; set; }

    [ProtoMember(4)]
    public int LoserKills { get; set; }

    [ProtoMember(5)]
    public int Mode { get; set; }

    // 名次表（4 人用）。1v1 也填，只是老客户端不读
    [ProtoMember(6)]
    public List<BattleResultEntry> Entries { get; set; }
}

// 45 名次表的一行：Rank 1 = 赢家，其余按死亡顺序倒序（最后死的第 2 名）
[ProtoContract]
public class BattleResultEntry
{
    [ProtoMember(1)]
    public string UID { get; set; }

    [ProtoMember(2)]
    public int Rank { get; set; }

    [ProtoMember(3)]
    public int Kills { get; set; }

    [ProtoMember(4)]
    public int HeroId { get; set; }
}

// 一次命中（44 的 Events 元素）
[ProtoContract]
public class BattleHitEvent
{
    [ProtoMember(1)]
    public string AttackerUID { get; set; }

    [ProtoMember(2)]
    public string TargetUID { get; set; }

    [ProtoMember(3)]
    public int Damage { get; set; }

    [ProtoMember(4)]
    public int HpLeft { get; set; }

    [ProtoMember(5)]
    public bool Died { get; set; }

    [ProtoMember(6)]
    public float X { get; set; }            // 命中点（受击者位置），客户端放飘字/特效

    [ProtoMember(7)]
    public float Z { get; set; }
}

// 53 一键邀请组队：客户端带目标 UID + 自己英雄发；回包 RoomCode（成功 = 直接进房态）
// ⚠ 必须与【服务端】 Proto/SysMsg.cs 的 53 同号同名
[ProtoContract]
public class MsgTeamInvite : MsgBase
{
    public MsgTeamInvite()
    {
        ProtocolType = ProtocolEnum.MsgTeamInvite;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string TargetUID { get; set; }

    [ProtoMember(3)]
    public int HeroId { get; set; }

    [ProtoMember(4)]
    public int Result { get; set; }

    [ProtoMember(5)]
    public string RoomCode { get; set; }
}

// 54 被邀请通知（服务端 → 被邀请方，只推无答）：弹窗确认，接受 = 自己发 39 加入
[ProtoContract]
public class MsgTeamInvited : MsgBase
{
    public MsgTeamInvited()
    {
        ProtocolType = ProtocolEnum.MsgTeamInvited;
    }
    [ProtoMember(1)]
    public override ProtocolEnum ProtocolType { get; set; }

    [ProtoMember(2)]
    public string InviterUID { get; set; }

    [ProtoMember(3)]
    public string InviterNickName { get; set; }

    [ProtoMember(4)]
    public string RoomCode { get; set; }
}


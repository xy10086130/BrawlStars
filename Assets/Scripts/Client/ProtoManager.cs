using SimpleServer.Proto;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProtoManager
{
    //请求密钥
    public static void CSSecret()
    {
        Debug.Log("请求密钥");
        MsgSecret msg = new MsgSecret();
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgSecret, NetManager.Instance.RecSecret);
        NetManager.Instance.SendMessage(msg);
    }

    //请求注册
    public static void CSRegister(string nickName, string password, Action<RegisterResult, string> callback)
    {
        MsgRegister msg = new MsgRegister
        {
            NickName = nickName,
            Password = password
        };

        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgRegister, (resMsg) =>
        {
            MsgRegister resultMsg = (MsgRegister)resMsg;
            callback(resultMsg.Result, resultMsg.UserID);
        });
        NetManager.Instance.SendMessage(msg);
    }

    //请求登录
    public static void CSLogin(string nickName, string password,
     Action<LoginResult, string> callback)
    {
        MsgLogin msg = new MsgLogin { NickName = nickName, Password = password };

        NetManager.Instance.AddProtoListener(
            ProtocolEnum.MsgLogin,
            (resMsg) =>
            {
                MsgLogin resultMsg = (MsgLogin)resMsg;
                callback(resultMsg.Result, resultMsg.UserID);
            });

        NetManager.Instance.SendMessage(msg);
    }

    //常驻监听：请求回包 + 服务端推送共用，收到即回调
    public static void AddFriendListListener(Action<MsgFriendList> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgFriendList, (resMsg) =>
        {
            callback((MsgFriendList)resMsg);
        });
    }

    // 请求好友列表
    public static void CSFriendList()
    {
        NetManager.Instance.SendMessage(new MsgFriendList());
    }

    // 搜索好友
    public static void CSFriendSearch(string searchText, Action<MsgFriendSearch> callback)
    {
        MsgFriendSearch msg = new MsgFriendSearch { SearchText = searchText };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgFriendSearch, (resMsg) =>
        {
            callback((MsgFriendSearch)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 申请好友
    public static void CSFriendApply(string friendID, Action<MsgFriendApply> callback)
    {
        MsgFriendApply msg = new MsgFriendApply { FriendID = friendID };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgFriendApply, (resMsg) =>
        {
            callback((MsgFriendApply)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 处理申请（同意/拒绝）
    public static void CSFriendApplyResult(string friendID, bool agree, Action<MsgFriendApplyResult> callback)
    {
        MsgFriendApplyResult msg = new MsgFriendApplyResult { FriendID = friendID, Agree = agree };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgFriendApplyResult, (resMsg) =>
        {
            callback((MsgFriendApplyResult)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 删除好友
    public static void CSFriendDelete(string friendID, Action<MsgFriendDelete> callback)
    {
        MsgFriendDelete msg = new MsgFriendDelete { FriendID = friendID };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgFriendDelete, (resMsg) =>
        {
            callback((MsgFriendDelete)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }


    // 入队：mode=1(1v1)，heroId 为当前选中英雄
    public static void CSMatch(int mode, int heroId, Action<MsgMatch> callback)
    {
        MsgMatch msg = new MsgMatch { Mode = mode, HeroId = heroId };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgMatch, (resMsg) =>
        {
            if (callback != null)
                callback((MsgMatch)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 取消匹配
    public static void CSMatchCancel(Action<MsgMatchCancel> callback)
    {
        MsgMatchCancel msg = new MsgMatchCancel();
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgMatchCancel, (resMsg) =>
        {
            if (callback != null)
                callback((MsgMatchCancel)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 常驻监听：匹配结果广播
    public static void AddMatchSuccessListener(Action<MsgMatchSuccess> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgMatchSuccess, (resMsg) =>
        {
            callback((MsgMatchSuccess)resMsg);
        });
    }

    // 常驻监听：队列人数推送
    public static void AddMatchCountListener(Action<MsgMatchCount> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgMatchCount, (resMsg) =>
        {
            callback((MsgMatchCount)resMsg);
        });
    }

    // 就绪上报：战斗场景加载完成 → 41（服务端收齐 2 个才广播 42）
    public static void CSBattleReady(Action<MsgBattleReady> callback)
    {
        MsgBattleReady msg = new MsgBattleReady();
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgBattleReady, (resMsg) =>
        {
            if (callback != null)
                callback((MsgBattleReady)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 常驻监听：42 开战广播（服务端推 = 双方就绪，客户端此时才激活战斗场景同时进场）
    public static void AddBeginBattleListener(Action<MsgBeginBattle> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgBeginBattle, (resMsg) =>
        {
            callback((MsgBeginBattle)resMsg);
        });
    }

    // 43 战斗意图上报：20Hz，只推无答（服务端不回包，所以不注册监听）
    // seq 单调递增（服务端 ≤lastSeq 丢弃）；方向是单位向量，停手时发 (0,0) —— 不发会被判挂机
    public static void CSBattleInput(int seq, float moveX, float moveZ, float aimX, float aimZ, int buttons)
    {
        MsgBattleInput msg = new MsgBattleInput
        {
            Seq = seq, MoveX = moveX, MoveZ = moveZ,
            AimX = aimX, AimZ = aimZ, Buttons = buttons
        };
        NetManager.Instance.SendMessage(msg);
    }

    // 常驻监听：44 权威位置快照（15Hz 推送，只推无答）
    public static void AddBattleStateListener(Action<MsgBattleState> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgBattleState, (resMsg) =>
        {
            callback((MsgBattleState)resMsg);
        });
    }

    // 常驻监听：45 战斗结算（一局只推一次，只推无答）。收到就该弹结算面板 ——
    // 之后不会再有 44，画面就冻在最后一帧了，别指望靠"44 停了"来推结束
    public static void AddBattleResultListener(Action<MsgBattleResult> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgBattleResult, (resMsg) =>
        {
            callback((MsgBattleResult)resMsg);
        });
    }

    // 建房：38，heroId 为当前大厅英雄；回包 RoomCode = 服务端生成的 7 位大写字母房号
    public static void CSRoomCreate(int heroId, Action<MsgRoomCreate> callback)
    {
        MsgRoomCreate msg = new MsgRoomCreate { HeroId = heroId };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgRoomCreate, (resMsg) =>
        {
            if (callback != null)
                callback((MsgRoomCreate)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 输号加入：39，roomCode 为要加入的房号 已在房内重发 = 换英雄
    public static void CSRoomJoin(string roomCode, int heroId, Action<MsgRoomJoin> callback)
    {
        MsgRoomJoin msg = new MsgRoomJoin { RoomCode = roomCode, HeroId = heroId };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgRoomJoin, (resMsg) =>
        {
            if (callback != null)
                callback((MsgRoomJoin)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 常驻监听：51 服务端推送成员状态变化（对方加入 / 换英雄 / 退出 → 刷新 Members）
    // 51/52 只推无答：客户端不主动发，一次性回包永不覆盖此监听（旧 39 兼推流的 Rehook 补挂已不需要）
    public static void AddRoomStateListener(Action<MsgRoomState> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgRoomState, (resMsg) =>
        {
            callback((MsgRoomState)resMsg);
        });
    }

    // 常驻监听：52 服务端通知房间已销毁（房主退 / 同伴掉线 → 回大厅）
    public static void AddRoomCanceledListener(Action<MsgRoomCanceled> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgRoomCanceled, (resMsg) =>
        {
            callback((MsgRoomCanceled)resMsg);
        });
    }

    // 房主点开始对战：49（服务端回包后双方走 48/41/42 老流程）
    public static void CSRoomStart(Action<MsgRoomStart> callback)
    {
        MsgRoomStart msg = new MsgRoomStart();
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgRoomStart, (resMsg) =>
        {
            if (callback != null)
                callback((MsgRoomStart)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 退出房间：50（恒回成功；房主退=整房销毁后收 52 的成员已另走 AddRoomCanceledListener）
    public static void CSRoomExit(Action<MsgRoomExit> callback)
    {
        MsgRoomExit msg = new MsgRoomExit();
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgRoomExit, (resMsg) =>
        {
            if (callback != null)
                callback((MsgRoomExit)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 一键邀请组队：53，回包成功 = 自己已建房（或用了已有的房），拿 RoomCode 进房态
    public static void CSTeamInvite(string targetUID, int heroId, Action<MsgTeamInvite> callback)
    {
        MsgTeamInvite msg = new MsgTeamInvite { TargetUID = targetUID, HeroId = heroId };
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgTeamInvite, (resMsg) =>
        {
            if (callback != null)
                callback((MsgTeamInvite)resMsg);
        });
        NetManager.Instance.SendMessage(msg);
    }

    // 常驻监听：54 被邀请通知（只推无答，和 53 的回包分开挂，别合成一条）
    public static void AddTeamInvitedListener(Action<MsgTeamInvited> callback)
    {
        NetManager.Instance.AddProtoListener(ProtocolEnum.MsgTeamInvited, (resMsg) =>
        {
            callback((MsgTeamInvited)resMsg);
        });
    }
}
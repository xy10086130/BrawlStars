using SimpleServer.Game;
using SimpleServer.Net;
using SimpleServer.Proto;
using System;

namespace SimpleServer.Business
{
    // 匹配与对战组织业务（协议壳，照 FriendService 模式；逻辑在 Game/MatchManager）
    public class MatchService
    {
        public static void MsgMatch(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgMatch msg = (MsgMatch)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)MatchResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            msg.Result = (int)MatchManager.Instance.Enqueue(clientSocket.UserID, msg.Mode, msg.HeroId);
            ServerSocket.Instance.Send(clientSocket, msg);
        }

        public static void MsgMatchCancel(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgMatchCancel msg = (MsgMatchCancel)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)MatchResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            MatchManager.Instance.Cancel(clientSocket.UserID);
            msg.Result = (int)MatchResult.Success;
            ServerSocket.Instance.Send(clientSocket, msg);
        }

        public static void MsgBattleReady(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgBattleReady msg = (MsgBattleReady)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)BattleReadyResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            Room room = RoomManager.Instance.GetRoomByPlayer(clientSocket.UserID);
            if (room == null || room.State != RoomState.Loading)
            {
                msg.Result = (int)BattleReadyResult.NotInRoom;   // 不在房间（Loading 外阶段收到的 41 一律拒）
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            room.ReadyCount++;
            msg.Result = (int)BattleReadyResult.Success;
            ServerSocket.Instance.Send(clientSocket, msg);

            // 收齐全员 41 → 开战：广播 42，再建战斗实例（42 必须先到，否则 44 抢在进战斗前到客户端）
            if (room.ReadyCount >= room.Players.Length)
            {
                room.State = RoomState.BattleStarted;
                MsgBeginBattle begin = new MsgBeginBattle { Result = (int)BattleReadyResult.Success };
                foreach (RoomPlayer p in room.Players)
                {
                    ClientSocket cs = ServerSocket.Instance.FindByUserID(p.UID);
                    if (cs != null)
                        ServerSocket.Instance.Send(cs, begin);
                }
                room.Battle = new BattleInstance(room, BattleService.Send);
                Debug.Log("房间{0}双方就绪，开战", room.RoomId);
            }
        }

        // 38 建房：逻辑在 LobbyRoomManager（回包/推流都在管理器内完成，仿 OnPaired）
        public static void MsgRoomCreate(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgRoomCreate msg = (MsgRoomCreate)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)LobbyRoomResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            LobbyRoomManager.Instance.Create(clientSocket, msg);
        }

        // 39 输号加入（幂等=换英雄）
        public static void MsgRoomJoin(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgRoomJoin msg = (MsgRoomJoin)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)LobbyRoomResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            LobbyRoomManager.Instance.Join(clientSocket, msg);
        }

        // 49 房主点开始：直接走 BeginPairing（48/41/42 老流程）
        public static void MsgRoomStart(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgRoomStart msg = (MsgRoomStart)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)LobbyRoomResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            LobbyRoomManager.Instance.Start(clientSocket, msg);
        }

        // 50 退出房间：房主退=销毁整房（成员收 52）/ 成员退=只踢自己（房主收 51 刷新）
        public static void MsgRoomExit(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgRoomExit msg = (MsgRoomExit)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)LobbyRoomResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            LobbyRoomManager.Instance.Exit(clientSocket, msg);
        }

        // 53 一键邀请组队（回包 + 推 54 都在 LobbyRoomManager 内完成）
        public static void MsgTeamInvite(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgTeamInvite msg = (MsgTeamInvite)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)TeamInviteResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            LobbyRoomManager.Instance.Invite(clientSocket, msg);
        }

        public static void MsgBeginBattle(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgBeginBattle msg = (MsgBeginBattle)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)BattleReadyResult.NotLogin;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            msg.Result = (int)BattleReadyResult.NotInRoom;   // 不在房间（42 本应由服务端广播，客户端不应主动发）
            ServerSocket.Instance.Send(clientSocket, msg);
        }
    }
}

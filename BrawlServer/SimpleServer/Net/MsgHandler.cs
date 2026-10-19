using MySQL;
using SimpleServer.Business;
using SimpleServer.Proto;
using System;
using System.Collections.Generic;
using System.Linq;


namespace SimpleServer.Net
{
    public class MsgHandler
    {
        public static void MsgSecret(ClientSocket clientSocket, MsgBase msgBase)
        {
            Debug.LogInfo("收到密钥请求");
            MsgSecret msg = (MsgSecret)msgBase;
            msg.Secret = ServerSocket.Instance.SecretKey;
            ServerSocket.Instance.Send(clientSocket, msg);
            Debug.LogInfo("成功下发密钥");
        }
        public static void MsgPing(ClientSocket clientSocket, MsgBase msgBase)
        {
            // 心跳（每 5 秒 × N 人）本身没必要每条都打，但"谁还在心跳"是定位半开连接的关键：
            // 客户端主线程卡死时 43 会停、心跳线程还在发 → 只有这里能看出这人还活着。30 秒打一条
            long now = ServerSocket.Instance.GetTimeStamp();
            clientSocket.LastPingTime = now;
            if (now - clientSocket.LastPingLogTime >= 30)
            {
                clientSocket.LastPingLogTime = now;
                Debug.LogInfo("收到心跳包 UID=" + (string.IsNullOrEmpty(clientSocket.UserID) ? "(未登录)" : clientSocket.UserID));
            }
            MsgPing msg = new MsgPing();
            ServerSocket.Instance.Send(clientSocket, msg);
        }
        public static void MsgRegister(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgRegister msgRegister = (MsgRegister)msgBase;

            RegisterResult result = UserManager.Instance.Register(msgRegister.NickName, msgRegister.Password, out string userID);

            msgRegister.Result = result;
            msgRegister.UserID = userID;
            if (result == RegisterResult.Success)
                clientSocket.UserID = userID;   // 注册即登录

            ServerSocket.Instance.Send(clientSocket, msgRegister);
        }
        public static void MsgLogin(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgLogin msgLogin = (MsgLogin)msgBase;

            LoginResult result = UserManager.Instance.Login(msgLogin.NickName, msgLogin.Password, out User user);

            msgLogin.Result = result;
            if (result == LoginResult.Success)
            {
                clientSocket.UserID = user.UserID;
                msgLogin.UserID = user.UserID;
            }
            ServerSocket.Instance.Send(clientSocket, msgLogin);
        }



        public static void MsgFriendList(ClientSocket clientSocket, MsgBase msgBase)
        {
            FriendService.MsgFriendList(clientSocket, msgBase);
        }

        public static void MsgFriendSearch(ClientSocket clientSocket, MsgBase msgBase)
        {
            FriendService.MsgFriendSearch(clientSocket, msgBase);
        }

        public static void MsgFriendApply(ClientSocket clientSocket, MsgBase msgBase)
        {
            FriendService.MsgFriendApply(clientSocket, msgBase);
        }

        public static void MsgFriendApplyResult(ClientSocket clientSocket, MsgBase msgBase)
        {
            FriendService.MsgFriendApplyResult(clientSocket, msgBase);
        }

        public static void MsgFriendDelete(ClientSocket clientSocket, MsgBase msgBase)
        {
            FriendService.MsgFriendDelete(clientSocket, msgBase);
        }

        public static void MsgMatch(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgMatch(clientSocket, msgBase);
        }

        public static void MsgMatchCancel(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgMatchCancel(clientSocket, msgBase);
        }

        public static void MsgBattleReady(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgBattleReady(clientSocket, msgBase);
        }

        public static void MsgBeginBattle(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgBeginBattle(clientSocket, msgBase);
        }

        public static void MsgBattleInput(ClientSocket clientSocket, MsgBase msgBase)
        {
            BattleService.MsgBattleInput(clientSocket, msgBase);
        }

        public static void MsgRoomCreate(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgRoomCreate(clientSocket, msgBase);
        }

        public static void MsgRoomJoin(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgRoomJoin(clientSocket, msgBase);
        }

        public static void MsgRoomStart(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgRoomStart(clientSocket, msgBase);
        }

        public static void MsgRoomExit(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgRoomExit(clientSocket, msgBase);
        }

        public static void MsgTeamInvite(ClientSocket clientSocket, MsgBase msgBase)
        {
            MatchService.MsgTeamInvite(clientSocket, msgBase);
        }
    }
}

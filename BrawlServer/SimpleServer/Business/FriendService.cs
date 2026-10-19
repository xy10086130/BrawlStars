using MySQL;
using SimpleServer.Net;
using SimpleServer.Proto;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleServer.Business
{
    // 好友模块业务
    public class FriendService
    {
        public static void MsgFriendList(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgFriendList msg = (MsgFriendList)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)FriendResult.Failed;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            FillFriendList(msg, clientSocket.UserID);
            msg.Result = (int)FriendResult.Success;
            ServerSocket.Instance.Send(clientSocket, msg);
        }

        public static void MsgFriendSearch(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgFriendSearch msg = (MsgFriendSearch)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)FriendResult.Failed;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            string me = clientSocket.UserID;
            msg.ResultList = new List<FriendInfo>();
            if (!string.IsNullOrWhiteSpace(msg.SearchText))
            {
                List<User> users = UserManager.Instance.SearchUsers(msg.SearchText, me);
                foreach (User u in users)
                {
                    FriendInfo fi = new FriendInfo();
                    fi.UserID = u.UserID;
                    fi.NickName = u.NickName;
                    fi.Online = ServerSocket.Instance.FindByUserID(u.UserID) != null;
                    fi.RelState = (int)GetRelState(me, u.UserID);
                    msg.ResultList.Add(fi);
                }
            }
            msg.Result = (int)FriendResult.Success;
            ServerSocket.Instance.Send(clientSocket, msg);
        }

        public static void MsgFriendApply(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgFriendApply msg = (MsgFriendApply)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)FriendResult.Failed;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            string me = clientSocket.UserID;
            if (me == msg.FriendID || UserManager.Instance.GetUser(msg.FriendID) == null)
            {
                msg.Result = (int)FriendResult.Failed;   // 申请自己 / 查无此人
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            msg.Result = (int)FriendManager.Instance.Apply(me, msg.FriendID);
            if (msg.Result == (int)FriendResult.Success)
            {
                // 普通申请：对方 Requests 变了；互赞成好友：双方 Friends 都变了
                // 统一都推一次，客户端收到即刷新，多余刷新无害
                PushFriendList(me);
                PushFriendList(msg.FriendID);
            }
            ServerSocket.Instance.Send(clientSocket, msg);
        }

        public static void MsgFriendApplyResult(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgFriendApplyResult msg = (MsgFriendApplyResult)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)FriendResult.Failed;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            bool ok = FriendManager.Instance.HandleApply(clientSocket.UserID, msg.FriendID, msg.Agree);
            msg.Result = ok ? (int)FriendResult.Success : (int)FriendResult.Failed;
            if (ok)
                PushFriendList(msg.FriendID);   // 申请人刷新：申请消失 / 变好友
            ServerSocket.Instance.Send(clientSocket, msg);
        }

        public static void MsgFriendDelete(ClientSocket clientSocket, MsgBase msgBase)
        {
            MsgFriendDelete msg = (MsgFriendDelete)msgBase;
            if (string.IsNullOrEmpty(clientSocket.UserID))
            {
                msg.Result = (int)FriendResult.Failed;
                ServerSocket.Instance.Send(clientSocket, msg);
                return;
            }
            bool ok = FriendManager.Instance.DeleteRelation(clientSocket.UserID, msg.FriendID);
            msg.Result = ok ? (int)FriendResult.Success : (int)FriendResult.Failed;
            if (ok)
                PushFriendList(msg.FriendID);   // 对方刷新：好友消失
            ServerSocket.Instance.Send(clientSocket, msg);
        }



        // 组装全量列表数据（好友 + 别人申请我）
        static void FillFriendList(MsgFriendList msg, string me)
        {
            msg.Friends = ToFriendInfos(FriendManager.Instance.GetFriends(me), me);
            msg.Requests = ToFriendInfos(FriendManager.Instance.GetRequests(me), me);
            msg.Result = (int)FriendResult.Success;
        }

        // Friend 行 → FriendInfo（补昵称 + 在线状态）
        static List<FriendInfo> ToFriendInfos(List<Friend> rows, string me)
        {
            List<FriendInfo> list = new List<FriendInfo>();
            foreach (Friend row in rows)
            {
                string other = row.Uid == me ? row.FriendUid : row.Uid;
                User u = UserManager.Instance.GetUser(other);
                if (u == null)
                    continue;
                FriendInfo fi = new FriendInfo();
                fi.UserID = u.UserID;
                fi.NickName = u.NickName;
                fi.Online = ServerSocket.Instance.FindByUserID(u.UserID) != null;
                list.Add(fi);
            }
            return list;
        }

        // 关系状态：见 RelState 枚举
        static RelState GetRelState(string me, string other)
        {
            Friend row = FriendManager.Instance.FindRelation(me, other);
            if (row == null)
                return RelState.None;
            if (row.Status == 0)
                return RelState.Friend;
            if (row.Uid == me)
                return RelState.Applied;
            return RelState.AppliedByOther;
        }

        // 推全量列表给对方（在线才推）
        static void PushFriendList(string uid)
        {
            ClientSocket cs = ServerSocket.Instance.FindByUserID(uid);
            if (cs == null)
                return;
            MsgFriendList msg = new MsgFriendList();
            FillFriendList(msg, uid);
            ServerSocket.Instance.Send(cs, msg);
        }
    }
}

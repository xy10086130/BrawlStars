using MySQL;
using SimpleServer.Net;
using SimpleServer.Proto;
using System;
using System.Collections.Generic;

namespace SimpleServer.Game
{

    public class LobbyRoomManager : Singleton<LobbyRoomManager>
    {
        Dictionary<string, LobbyRoom> m_Rooms = new Dictionary<string, LobbyRoom>();   // 房号 → 房
        Dictionary<string, string> m_PlayerRoom = new Dictionary<string, string>();    // uid → 房号

        Random m_Random = new Random();

        public LobbyRoom GetRoomByPlayer(string uid)
        {
            if (m_PlayerRoom.TryGetValue(uid, out string code))
                return m_Rooms.TryGetValue(code, out LobbyRoom room) ? room : null;
            return null;
        }


        public void Create(ClientSocket cs, MsgRoomCreate msg)
        {
            string uid = cs.UserID;

            LobbyRoom room = GetRoomByPlayer(uid);//检查是否在组队房间里
            if (room == null)
            {
                room = new LobbyRoom { Code = GenCode(), HostUID = uid };
                room.Players.Add(new LobbyPlayer { UID = uid, HeroId = msg.HeroId });
                m_Rooms[room.Code] = room;
                m_PlayerRoom[uid] = room.Code;
                Debug.Log("创建组队房间{0}：房主{1}(英雄{2})", room.Code, uid, msg.HeroId);
            }
            else if (room.HostUID != uid)//玩家已经在别的组队房间了
            {
                msg.Result = (int)LobbyRoomResult.AlreadyInRoom;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }
            else//已经在房间了，并且是房主  这次请求是换英雄
            {
                room.Players[0].HeroId = msg.HeroId;    // 房主换英雄
                Debug.Log("房主{0}换英雄{1}（房间{2}）", uid, msg.HeroId, room.Code);
            }

            msg.Result = (int)LobbyRoomResult.Success;
            msg.RoomCode = room.Code;
            msg.Members = BuildMembers(room);
            ServerSocket.Instance.Send(cs, msg);
            if (room.Players.Count >= 2)
                PushOther(room, uid);   // 房主换英雄 → 推成员刷新
        }


        public void Join(ClientSocket cs, MsgRoomJoin msg)
        {
            string uid = cs.UserID;

            LobbyRoom room = GetRoomByPlayer(uid);
            if (room != null)
            {
                if (room.Code != msg.RoomCode)
                {
                    msg.Result = (int)LobbyRoomResult.AlreadyInRoom;
                    ServerSocket.Instance.Send(cs, msg);
                    return;
                }

                LobbyPlayer me = room.Players.Find(p => p.UID == uid);
                me.HeroId = msg.HeroId;             // 成员换英雄
                Debug.Log("成员{0}换英雄{1}（房间{2}）", uid, msg.HeroId, room.Code);
                msg.Result = (int)LobbyRoomResult.Success;
                msg.RoomCode = room.Code;
                msg.Members = BuildMembers(room);
                ServerSocket.Instance.Send(cs, msg);
                PushOther(room, uid);   // 换英雄 → 推对方刷新
                return;
            }

            if (!m_Rooms.TryGetValue(msg.RoomCode, out room))
            {
                msg.Result = (int)LobbyRoomResult.RoomNotExist;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }

            if (room.Players.Count >= 2)
            {
                msg.Result = (int)LobbyRoomResult.RoomFull;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }

            room.Players.Add(new LobbyPlayer { UID = uid, HeroId = msg.HeroId });
            m_PlayerRoom[uid] = room.Code;
            Debug.Log("玩家{0}(英雄{1})加入组队房间{2}", uid, msg.HeroId, room.Code);

            msg.Result = (int)LobbyRoomResult.Success;
            msg.RoomCode = room.Code;
            msg.Members = BuildMembers(room);
            ServerSocket.Instance.Send(cs, msg);
            PushOther(room, uid);
        }


        // 53 一键邀请组队：校验双方 → 自己没房就先建房 → 回包邀请方 + 推 54 给被邀请方
        // 只"通知"，不预占位：对方不接，房就空着等下一个（接受走现成的 39 Join，房主那边有 51 刷新）
        public void Invite(ClientSocket cs, MsgTeamInvite msg)
        {
            string uid = cs.UserID;

            // 自己：在别人的房里 / 战斗中 / 匹配队列里 → 不能邀请（房间上限 2 人，塞不下第三个）
            LobbyRoom room = GetRoomByPlayer(uid);
            if ((room != null && room.HostUID != uid)
                || RoomManager.Instance.GetRoomByPlayer(uid) != null
                || MatchManager.Instance.IsQueued(uid))
            {
                msg.Result = (int)TeamInviteResult.SelfBusy;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }

            // 对方：离线 / 约战房 / 战斗房 / 匹配队列 → 各自给个准话
            ClientSocket target = ServerSocket.Instance.FindByUserID(msg.TargetUID);
            if (target == null)
            {
                msg.Result = (int)TeamInviteResult.Offline;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }
            if (GetRoomByPlayer(msg.TargetUID) != null
                || RoomManager.Instance.GetRoomByPlayer(msg.TargetUID) != null
                || MatchManager.Instance.IsQueued(msg.TargetUID))
            {
                msg.Result = (int)TeamInviteResult.TargetBusy;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }

            // 自己还没房 → 现建一个（不走 Create：那是 38 的回包，这次回的是 53）
            if (room == null)
            {
                room = new LobbyRoom { Code = GenCode(), HostUID = uid };
                room.Players.Add(new LobbyPlayer { UID = uid, HeroId = msg.HeroId });
                m_Rooms[room.Code] = room;
                m_PlayerRoom[uid] = room.Code;
                Debug.Log("邀请建房{0}：房主{1}(英雄{2})", room.Code, uid, msg.HeroId);
            }
            if (room.Players.Count >= 2)// 到这只可能是本来就带着队友的房（上面刚建的只有 1 人）
            {
                msg.Result = (int)TeamInviteResult.RoomFull;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }

            msg.Result = (int)TeamInviteResult.Success;
            msg.RoomCode = room.Code;
            ServerSocket.Instance.Send(cs, msg);

            User u = UserManager.Instance.GetUser(uid);
            ServerSocket.Instance.Send(target, new MsgTeamInvited
            {
                InviterUID = uid,
                InviterNickName = u != null ? u.NickName : "",
                RoomCode = room.Code
            });
            Debug.Log("玩家{0}邀请{1}加入组队房间{2}", uid, msg.TargetUID, room.Code);
        }


        public void Start(ClientSocket cs, MsgRoomStart msg)
        {
            string uid = cs.UserID;
            LobbyRoom room = GetRoomByPlayer(uid);
            if (room == null || room.HostUID != uid)
            {
                msg.Result = (int)LobbyRoomResult.NotHost;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }
            if (room.Players.Count < 2)
            {
                msg.Result = (int)LobbyRoomResult.NotEnough;
                ServerSocket.Instance.Send(cs, msg);
                return;
            }

            msg.Result = (int)LobbyRoomResult.Success;
            ServerSocket.Instance.Send(cs, msg);
            Debug.Log("房主{0}点开始，组队房间{1}开战", uid, room.Code);

            // 组队约战房恒 1v1（多人只走单人匹配那条线）
            RoomPlayer[] players = new RoomPlayer[2];
            for (int i = 0; i < 2; i++)
                players[i] = new RoomPlayer { UID = room.Players[i].UID, HeroId = room.Players[i].HeroId };
            RemoveRoom(room.Code);
            RoomManager.Instance.BeginPairing(players, BattleModes.Duel1v1);
        }


        public void Exit(ClientSocket cs, MsgRoomExit msg)
        {
            string uid = cs.UserID;
            msg.Result = (int)LobbyRoomResult.Success;
            ServerSocket.Instance.Send(cs, msg);

            LobbyRoom room = GetRoomByPlayer(uid);
            if (room == null) return;

            if (room.HostUID == uid)
            {
                Debug.Log("房主{0}退出，销毁组队房间{1}", uid, room.Code);
                foreach (LobbyPlayer p in room.Players)
                {
                    if (p.UID == uid) continue;
                    ClientSocket other = ServerSocket.Instance.FindByUserID(p.UID);
                    if (other != null)
                        ServerSocket.Instance.Send(other, new MsgRoomCanceled());
                }
                RemoveRoom(room.Code);
            }
            else
            {
                Debug.Log("成员{0}退出组队房间{1}，房间继续等", uid, room.Code);
                room.Players.RemoveAll(p => p.UID == uid);
                m_PlayerRoom.Remove(uid);
                PushOther(room, uid);
            }
        }

        public void OnDisconnect(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            LobbyRoom room = GetRoomByPlayer(uid);
            if (room == null) return;

            LobbyPlayer other = null;
            foreach (LobbyPlayer p in room.Players)
                if (p.UID != uid) { other = p; break; }
            RemoveRoom(room.Code);
            Debug.Log("玩家{0}断线，销毁组队房间{1}", uid, room.Code);

            if (other != null)
            {
                ClientSocket cs = ServerSocket.Instance.FindByUserID(other.UID);
                if (cs != null)
                    ServerSocket.Instance.Send(cs, new MsgRoomCanceled());
            }
        }

        // 组 Members
        List<RoomMemberInfo> BuildMembers(LobbyRoom room)
        {
            List<RoomMemberInfo> list = new List<RoomMemberInfo>();
            foreach (LobbyPlayer p in room.Players)
            {
                User u = UserManager.Instance.GetUser(p.UID);
                list.Add(new RoomMemberInfo
                {
                    UserID = p.UID,
                    NickName = u != null ? u.NickName : "",
                    HeroId = p.HeroId,
                    IsHost = p.UID == room.HostUID
                });
            }
            return list;
        }

        // 房间状态更新
        void PushOther(LobbyRoom room, string exceptUid)
        {
            foreach (LobbyPlayer p in room.Players)
            {
                if (p.UID == exceptUid) continue;
                ClientSocket cs = ServerSocket.Instance.FindByUserID(p.UID);
                if (cs != null)
                    ServerSocket.Instance.Send(cs, new MsgRoomState
                    {
                        RoomCode = room.Code,
                        Members = BuildMembers(room)
                    });
            }
        }

        // 销毁房间
        void RemoveRoom(string code)
        {
            if (m_Rooms.TryGetValue(code, out LobbyRoom room))
            {
                foreach (LobbyPlayer p in room.Players)
                    m_PlayerRoom.Remove(p.UID);
                m_Rooms.Remove(code);
            }
        }

        // 7 位随机大写字母房号
        string GenCode()
        {
            string code;
            do
            {
                char[] chars = new char[7];
                for (int i = 0; i < chars.Length; i++)
                    chars[i] = (char)('A' + m_Random.Next(26));
                code = new string(chars);
            }
            while (m_Rooms.ContainsKey(code));
            return code;
        }
    }
}

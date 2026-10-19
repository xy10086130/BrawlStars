using MySQL;
using SimpleServer.Net;
using SimpleServer.Proto;
using System;
using System.Collections.Generic;

namespace SimpleServer.Game
{
    public class RoomManager : Singleton<RoomManager>
    {
        Dictionary<string, Room> m_Rooms = new Dictionary<string, Room>();        // roomId(=房主uid) → Room
        Dictionary<string, string> m_PlayerRoom = new Dictionary<string, string>(); // uid → roomId

        public const int LoadingTimeoutSec = 10;    

        // 配对成功 → 建房 + 全员登记
        public Room CreateRoom(RoomPlayer[] players, int mode)
        {
            // 建房前先把这些人所在的旧房间踢掉：房间进了 BattleStarted 就没有出口
            //（Finished 没有实现，掉线也只是挂机），旧战斗会一直被主循环 tick + 广播 44
            // → 客户端同一秒收到两套权威坐标（一套会动、一套冻在上一局），人被反复拉回去
            foreach (RoomPlayer p in players)
                KickFromOldRoom(p.UID);

            // 出生点在这里抽（48 马上要带出去，见 RoomPlayer.SpawnX 注释）
            SpawnTable.FillSpawns(players);

            Room room = new Room(players[0].UID, players, mode);
            m_Rooms[room.RoomId] = room;
            foreach (RoomPlayer p in players)
                m_PlayerRoom[p.UID] = room.RoomId;
            Debug.Log("创建战斗房间{0}（模式{1}，{2}人）", room.RoomId, mode, players.Length);
            return room;
        }

        // 该 uid 若还挂在某个房间里就先销毁它：一局一间，重新匹配就是新的一局
        void KickFromOldRoom(string uid)
        {
            if (m_PlayerRoom.TryGetValue(uid, out string roomId))
                RemoveRoom(roomId);
        }

        // 双入口汇合点：随机匹配 OnPaired 与约战房 49 共用的后半段
        // 建房 → Loading → 查库昵称 → 逐人推 48{0}（此后等 41/42 老流程）
        public void BeginPairing(RoomPlayer[] players, int mode)
        {
            Room room = CreateRoom(players, mode);
            room.State = RoomState.Loading;
            room.LoadingStart = DateTime.Now;

            List<BattleMember> members = new List<BattleMember>();
            foreach (RoomPlayer p in players)
            {
                User u = UserManager.Instance.GetUser(p.UID);
                members.Add(new BattleMember
                {
                    UID = p.UID,
                    NickName = u != null ? u.NickName : "",
                    HeroId = p.HeroId,
                    X = p.SpawnX,
                    Z = p.SpawnZ
                });
            }

            foreach (RoomPlayer self in players)
            {
                ClientSocket cs = ServerSocket.Instance.FindByUserID(self.UID);
                if (cs == null) continue;

                // 旧的"对手"三字段留给 1v1 老客户端（零改动）；多人一律看 Members
                RoomPlayer opp = null;
                foreach (RoomPlayer p in players)
                    if (p != self) { opp = p; break; }

                ServerSocket.Instance.Send(cs, new MsgMatchSuccess
                {
                    Result = (int)MatchSuccessResult.Success,
                    OpponentUserID = opp != null ? opp.UID : "",
                    OpponentNickName = opp != null ? members[IndexOf(players, opp)].NickName : "",
                    OpponentHeroId = opp != null ? opp.HeroId : 0,
                    Members = members
                });
            }
            Debug.Log("配对成功：{0}，房间{1}进Loading（{2}人，模式{3}）",
                string.Join(",", Array.ConvertAll(players, p => p.UID + "(英雄" + p.HeroId + ")")),
                room.RoomId, players.Length, mode);
        }

        static int IndexOf(RoomPlayer[] players, RoomPlayer p)
        {
            for (int i = 0; i < players.Length; i++)
                if (players[i] == p) return i;
            return 0;
        }

        // uid → 所在房间（41/掉线/战斗中定位用）
        public Room GetRoomByPlayer(string uid)
        {
            if (m_PlayerRoom.TryGetValue(uid, out string roomId))
                return m_Rooms.TryGetValue(roomId, out Room room) ? room : null;
            return null;
        }

        public Room GetRoom(string roomId)
        {
            m_Rooms.TryGetValue(roomId, out Room room);
            return room;
        }


        List<Room> m_Snapshot = new List<Room>();
        public List<Room> SnapshotRooms()
        {
            m_Snapshot.Clear();
            m_Snapshot.AddRange(m_Rooms.Values);
            return m_Snapshot;
        }

        /// <summary>
        /// 销毁房间
        /// </summary>
        /// <param name="roomId"></param>
        public void RemoveRoom(string roomId)
        {
            if (m_Rooms.TryGetValue(roomId, out Room room))
            {
                foreach (RoomPlayer p in room.Players)
                    m_PlayerRoom.Remove(p.UID);
                m_Rooms.Remove(roomId);
                Debug.Log("销毁房间{0}", roomId);
            }
        }


        // Loading（等 41）掉线 → 推存活方 48{1} 回大厅 + 销毁房间
        //（4 人也是收齐 4 才开，掉一个就整房取消，不做补位）
        // BattleStarted 掉线 → 角色挂机不处理（BattleInstance 管，角色不再收输入），
        //   **但全员都掉线就得销毁**：没人再发输入 → 这局永远走不到 Finished → 主循环一直 tick 它 + 广播 44，
        //   而且 RoomId = 房主 uid，房主下次匹配会撞出同名房、把上一局的 m_PlayerRoom 残条错挂到新房上
        public void OnDisconnect(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            Room room = GetRoomByPlayer(uid);
            if (room == null) return;

            if (room.State == RoomState.BattleStarted)
            {
                // 只判"别人还在不在"：自己这条连接此刻还没从 m_ClientDic 摘掉
                //（CloseClient 是先跑掉线钩子、后 Remove），拿 FindByUserID(uid) 判会永远为真
                foreach (RoomPlayer p in room.Players)
                {
                    if (p.UID == uid) continue;
                    if (ServerSocket.Instance.FindByUserID(p.UID) != null) return;
                }
                RemoveRoom(room.RoomId);
                return;
            }

            if (room.State == RoomState.Loading)
            {
                foreach (RoomPlayer p in room.Players)
                {
                    if (p.UID == uid) continue;
                    ClientSocket cs = ServerSocket.Instance.FindByUserID(p.UID);
                    if (cs != null)
                        ServerSocket.Instance.Send(cs, new MsgMatchSuccess { Result = (int)MatchSuccessResult.Canceled });
                }
                RemoveRoom(room.RoomId);
            }
        }

        // Loading 超时检查
        public void CheckLoadingTimeout()
        {
            List<Room> timeout = null;
            foreach (Room room in m_Rooms.Values)
            {
                if (room.State == RoomState.Loading
                    && (DateTime.Now - room.LoadingStart).TotalSeconds >= LoadingTimeoutSec)
                {
                    if (timeout == null) timeout = new List<Room>();
                    timeout.Add(room);
                }
            }
            if (timeout == null) return;
            foreach (Room room in timeout)
            {
                foreach (RoomPlayer p in room.Players)
                {
                    ClientSocket cs = ServerSocket.Instance.FindByUserID(p.UID);
                    if (cs != null)
                        ServerSocket.Instance.Send(cs, new MsgMatchSuccess { Result = (int)MatchSuccessResult.Canceled });
                }
                RemoveRoom(room.RoomId);
            }
        }
    }
}

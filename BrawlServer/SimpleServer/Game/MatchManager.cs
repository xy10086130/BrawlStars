using SimpleServer.Net;
using SimpleServer.Proto;
using System;
using System.Collections.Generic;

namespace SimpleServer.Game
{
    // 队列里的一方
    public class MatchPlayer
    {
        public string UID;
        public int HeroId;
    }

    // 匹配队列：按模式分队列，进满该模式的人数就配对（人数在 BattleModes 表里）
    public class MatchManager : Singleton<MatchManager>
    {
        // 按模式分队列（List 而非 Queue：取消匹配要删中间元素）
        Dictionary<int, List<MatchPlayer>> m_Queues = new Dictionary<int, List<MatchPlayer>>();

        // uid → 所在模式队列，快速定位/防重入
        Dictionary<string, int> m_PlayerMode = new Dictionary<string, int>();

        // 入队结果
        public MatchResult Enqueue(string uid, int mode, int heroId)
        {
            // 防串门：已在约战房（38/39 进来的）不能再入随机匹配
            if (LobbyRoomManager.Instance.GetRoomByPlayer(uid) != null)
                return MatchResult.InRoom;

            if (!BattleModes.IsValid(mode))
                return MatchResult.ModeInvalid;

            if (!m_Queues.TryGetValue(mode, out List<MatchPlayer> queue))
            {
                queue = new List<MatchPlayer>();
                m_Queues[mode] = queue;
            }
            queue.Add(new MatchPlayer { UID = uid, HeroId = heroId });
            m_PlayerMode[uid] = mode;
            Debug.Log("玩家{0}进入匹配队列（模式{1}，队列{2}人）", uid, mode, queue.Count);

            // 满员 → 头部 N 人配对
            int need = BattleModes.Get(mode).PlayerCount;
            if (queue.Count >= need)
            {
                MatchPlayer[] picked = new MatchPlayer[need];
                for (int i = 0; i < need; i++)
                {
                    picked[i] = queue[i];
                    m_PlayerMode.Remove(picked[i].UID);
                }
                queue.RemoveRange(0, need);

                // 配对瞬间：给这几个人也推一次满员人数（否则入队即配对，收不到 40 推送，界面停在初始值）
                MsgMatchCount full = new MsgMatchCount { Count = need };
                foreach (MatchPlayer p in picked)
                {
                    ClientSocket cs = ServerSocket.Instance.FindByUserID(p.UID);
                    if (cs != null) ServerSocket.Instance.Send(cs, full);
                }
                OnPaired(picked, mode);
            }
            // 队列变化后，给剩余所有人推人数
            PushQueueCount(mode);
            return MatchResult.Success;
        }

        // 取消/掉线统一移出队列
        public MatchResult Cancel(string uid)
        {
            if (!m_PlayerMode.TryGetValue(uid, out int mode))
                return MatchResult.Success;
            List<MatchPlayer> queue = m_Queues[mode];
            queue.RemoveAll(p => p.UID == uid);
            m_PlayerMode.Remove(uid);
            Debug.Log("玩家{0}离开匹配队列（模式{1}，队列剩{2}人）", uid, mode, queue.Count);
            PushQueueCount(mode);
            return MatchResult.Success;
        }


        public void OnDisconnect(string uid)
        {
            if (!string.IsNullOrEmpty(uid))
                Cancel(uid);
        }

        // 是否在队列里（53 邀请的占用检查：排队中的人不该被拉进约战房，反之亦然）
        public bool IsQueued(string uid)
        {
            return !string.IsNullOrEmpty(uid) && m_PlayerMode.ContainsKey(uid);
        }

        // 配对成功回调：建房与约战房 49 的汇合逻辑已抽到 RoomManager.BeginPairing
        void OnPaired(MatchPlayer[] picked, int mode)
        {
            RoomPlayer[] players = new RoomPlayer[picked.Length];
            for (int i = 0; i < picked.Length; i++)
                players[i] = new RoomPlayer { UID = picked[i].UID, HeroId = picked[i].HeroId };
            RoomManager.Instance.BeginPairing(players, mode);
        }

        // 给该模式队列里剩余所有人推当前人数
        void PushQueueCount(int mode)
        {
            if (!m_Queues.TryGetValue(mode, out List<MatchPlayer> queue))
                return;
            MsgMatchCount msg = new MsgMatchCount { Count = queue.Count };
            foreach (MatchPlayer p in queue)
            {
                ClientSocket cs = ServerSocket.Instance.FindByUserID(p.UID);
                if (cs != null)
                    ServerSocket.Instance.Send(cs, msg);
            }
        }
    }
}

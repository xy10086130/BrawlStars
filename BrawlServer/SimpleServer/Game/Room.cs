using System;

namespace SimpleServer.Game
{
    // 房间内的一方
    public class RoomPlayer
    {
        public string UID;
        public int HeroId;
        public float SpawnX, SpawnZ;    // 建房抽签时定死（48 要带出去，BattleInstance 只读不算）
    }

    // 房间状态机：Created → Loading → BattleStarted → Finished
    public enum RoomState
    {
        Created,
        Loading,        // 已配对，等全员发 41（场景加载完成）
        BattleStarted,  // 收齐 41 广播 42
        Finished
    }

    // 一局房间：内存态，不落库。人数由 Mode 决定（1v1 = 2 人，单人匹配 = 4 人）
    public class Room
    {
        public string RoomId;              // 复用房主uid
        public RoomPlayer[] Players;
        public int Mode = BattleModes.Duel1v1;
        public RoomState State = RoomState.Created;
        public int ReadyCount;          // 41 计数
        public DateTime LoadingStart;   // 广播 48 时置，超时从这里起算
        public BattleInstance Battle;   // 收齐 41 建（42 广播之后），战斗中每 tick 由主循环驱动

        public Room(string roomId, RoomPlayer[] players, int mode)
        {
            RoomId = roomId;
            Players = players;
            Mode = mode;
        }
    }
}

using SimpleServer.Game;
using SimpleServer.Net;
using SimpleServer.Proto;
using System;

namespace SimpleServer.Business
{
    public class BattleService
    {
        public static void MsgBattleInput(ClientSocket clientSocket, MsgBase msgBase)
        {
            if (string.IsNullOrEmpty(clientSocket.UserID)) return;
            Room room = RoomManager.Instance.GetRoomByPlayer(clientSocket.UserID);
            if (room == null || room.Battle == null) return;
            room.Battle.PushInput(clientSocket.UserID, (MsgBattleInput)msgBase, ServerSocket.NowMs);
        }

        public static void Send(string uid, MsgBase msg)
        {
            ClientSocket cs = ServerSocket.Instance.FindByUserID(uid);
            if (cs != null) ServerSocket.Instance.Send(cs, msg);
        }
    }
}

using System.Collections.Generic;

namespace SimpleServer.Game
{

    public class LobbyPlayer
    {
        public string UID;
        public int HeroId;
    }

    public class LobbyRoom
    {
        public string Code;              // 7 位大写字母房号
        public string HostUID;
        public List<LobbyPlayer> Players = new List<LobbyPlayer>();   // ≤ 2 人
    }
}

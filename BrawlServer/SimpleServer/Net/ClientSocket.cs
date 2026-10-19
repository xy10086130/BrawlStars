using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SimpleServer.Net
{
    public class ClientSocket
    {
        public string UserID;
        public Socket Socket { get; set; }
        public long LastPingTime { get; set; }
        public long LastPingLogTime { get; set; }   // 心跳那条日志的限流（心跳 5 秒一次 × N 人会刷屏）
        public int BadMsgCount;                     // 连续解不开的包数：满 5 条才断开（一条坏包不该踢人）
        public ByteArray byteArray = new ByteArray();
    }
}

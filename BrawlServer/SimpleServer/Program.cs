using SimpleServer.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySQL;

namespace SimpleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            // 自测模式：不连库不联网，跑完直接退出（返回码 0 = 全过）
            if (args.Length > 0 && args[0] == "--bottest")
            {
                Environment.Exit(BattleBotTest.Run());
            }

            if (!MySqlManager.Instance.Init())
            {
                Debug.LogError("数据库不可用，服务端停止启动");
                return;
            }

            ServerSocket.Instance.Init();
        }
    }
}

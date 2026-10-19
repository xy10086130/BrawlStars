using SimpleServer.Game;
using SimpleServer.Proto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
/*
 * 公钥：
 * 密钥：
 */
namespace SimpleServer.Net
{
    public class ServerSocket : Singleton<ServerSocket>
    {
        public string PublicKey = "abc123";
        public string SecretKey = "123abc";
        public Socket m_ListenSocket;//服务端的socket
        // 0.0.0.0 = 监听本机所有网卡 —— 局域网/手机热点里别的机器才连得进来。
        // 原来写 127.0.0.1 是回环地址，Bind 上去只监听本机内部接口，外部连接根本到不了这个 socket。
        // 注意：绑 0.0.0.0 后下面那句日志会打出 "0.0.0.0:8011"，那是通配符不是可连接地址 ——
        // 客户端要填的是真实网卡地址，所以紧接着把本机地址一并打出来（见 Init）
        private string ipStr = "0.0.0.0";
        private int port = 8011;
        private List<Socket> m_CheckReadList = new List<Socket>();//临时存储所有客户端的socket的集合
        public Dictionary<Socket, ClientSocket> m_ClientDic = new Dictionary<Socket, ClientSocket>();//存储所有的客户端
        private List<ClientSocket> m_TempList = new List<ClientSocket>();//存储所有心跳包超时的客户端
        private long m_PingInterval = 300;//心跳包时间间隔
        private long m_LastRoomCheck = 0;   // 上次房间 Loading 超时检查（秒级时间戳）

        // 战斗 tick 用的毫秒时钟（GetTimeStamp 只有秒精度，跑不了 20Hz）
        static readonly Stopwatch s_Clock = Stopwatch.StartNew();
        public static int NowMs { get { return (int)s_Clock.ElapsedMilliseconds; } }
        public void Init()
        {
            IPAddress ip = IPAddress.Parse(ipStr);
            IPEndPoint iPEndPoint = new IPEndPoint(ip, port);
            m_ListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_ListenSocket.Bind(iPEndPoint);
            m_ListenSocket.Listen(100);
            Debug.Log("服务器启动监听{0}成功！", m_ListenSocket.LocalEndPoint.ToString());

            // 绑 0.0.0.0 时上面那句只会打 "0.0.0.0:8011"，那是通配符，客户端填不得。
            // 联机时客户端要填的是下面这些真实网卡地址里的一个（热点/局域网那个网段的）
            if (ipStr == "0.0.0.0")
            {
                // 遍历网卡而不是 Dns.GetHostAddresses：后者只返回在 DNS 里注册过的地址，
                // 移动热点那张虚拟网卡（Microsoft Wi-Fi Direct Virtual Adapter / 192.168.137.1）
                // 不在里面 —— 联机时最想看的那个地址恰好打不出来。谁的都跑不掉才省事。
                try
                {
                    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up) continue;
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                        foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                            if (ua.Address.ToString().StartsWith("169.254.")) continue;   // APIPA 兜底地址，连不上
                            Debug.Log("  [{0}] {1}:{2}", ni.Name, ua.Address, port);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("查本机地址失败，手动 ipconfig 看一下：" + e.Message);
                }
            }


            while (true)
            {
                ResetCheckRead();
                try
                {
                    Socket.Select(m_CheckReadList, null, null, 15);   // 15ms：让主循环跑得动 20Hz 战斗 tick（原 1000ms 会卡死）
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
                for (int i = m_CheckReadList.Count - 1; i >= 0; i--)
                {
                    Socket s = m_CheckReadList[i];
                    if (s == m_ListenSocket)//处理链接
                    {
                        ReadListen(s);
                    }
                    else//处理已经链接成功的客户端的数据的
                    {
                        ReadClient(s);
                    }
                }
                //检测心跳包超时
                long timeNow = GetTimeStamp();
                m_TempList.Clear();
                foreach (ClientSocket clientSocket in m_ClientDic.Values)
                {
                    if (timeNow - clientSocket.LastPingTime > m_PingInterval * 4)
                    {
                        Debug.Log("心跳包超时！");
                        m_TempList.Add(clientSocket);
                    }
                }
                //对心跳包超时的客户端进行统一处理
                foreach (ClientSocket client in m_TempList)
                {
                    CloseClient(client);
                }
                m_TempList.Clear();

                // 房间 Loading 超时检查（每秒一次：广播 48 后 30s 未收齐 41 → 销毁推 48{1}）
                if (timeNow - m_LastRoomCheck >= 1)
                {
                    RoomManager.Instance.CheckLoadingTimeout();
                    m_LastRoomCheck = timeNow;
                }

                // 战斗 tick：20Hz 定步长 + 每 tick 广播 44（遍历快照，tick 里可能销毁房间）
                List<Room> rooms = RoomManager.Instance.SnapshotRooms();
                for (int i = 0; i < rooms.Count; i++)
                {
                    BattleInstance battle = rooms[i].Battle;
                    if (battle == null) continue;
                    battle.Tick(NowMs);
                    // 这一 tick 刚打完：45 已经在 CheckFinish 里发出去了，房间这才是真的有了出口
                    // （rooms 是快照副本，RemoveRoom 动的是 m_Rooms，边走边删安全）
                    if (battle.Finished) RoomManager.Instance.RemoveRoom(rooms[i].RoomId);
                }
            }
        }

        //检测哪些socket是可以读取
        public void ResetCheckRead()
        {
            m_CheckReadList.Clear();
            m_CheckReadList.Add(m_ListenSocket);
            foreach (Socket s in m_ClientDic.Keys)
            {
                m_CheckReadList.Add(s);
            }
        }

        //处理链接
        void ReadListen(Socket listen)
        {
            try
            {
                Socket client = listen.Accept();
                ClientSocket clientSocket = new ClientSocket();
                clientSocket.Socket = client;
                clientSocket.LastPingTime = GetTimeStamp();
                m_ClientDic.Add(client, clientSocket);
                Debug.Log("一个客户端链接{0}，当前{1}个客户端在线！", client.LocalEndPoint.ToString(), m_ClientDic.Count);
            }
            catch (Exception e)
            {
                Debug.LogError("建立链接异常！" + e);
            }
        }

        //处理已经链接的客户端的数据
        void ReadClient(Socket client)
        {
            ClientSocket clientSocket = m_ClientDic[client];
            ByteArray byteArray = clientSocket.byteArray;//获取到对应客户端的缓冲区
            //接收数据
            int count = 0;
            //扩容
            if (byteArray.Remain <= 0)
            {
                //对接收到的数据进行解析处理
                OnReceiveData(clientSocket);
                byteArray.CheckAndMoveBytes();
                while (byteArray.Remain <= 0)
                {
                    int expandSize = byteArray.Length < ByteArray.DefaultSize ? ByteArray.DefaultSize : byteArray.Length;
                    byteArray.ReSize(2 * expandSize);
                }
            }
            try
            {
                count = client.Receive(byteArray.Bytes, byteArray.writeIdx, byteArray.Remain, 0);
            }
            catch (SocketException e)
            {
                Debug.LogError("接收数据异常！" + e);
                CloseClient(clientSocket);
            }
            if (count <= 0)
            {
                CloseClient(clientSocket);
                return;
            }
            byteArray.writeIdx += count;
            //对接收到的数据进行解析处理
            OnReceiveData(clientSocket);
            //刷新缓冲区
            byteArray.CheckAndMoveBytes();
        }

        //对接收到的数据进行解析处理
        void OnReceiveData(ClientSocket clientSocket)
        {
            ByteArray byteArray = clientSocket.byteArray;//获取接收到的客户端的数据
            if (byteArray.Length <= 4 || byteArray.readIdx < 0)
            {
                return;
            }
            int readIdx = byteArray.readIdx;
            byte[] bytes = byteArray.Bytes;
            int bodyLength = BitConverter.ToInt32(bytes, readIdx);//计算出包名和包内容的长度
            if (byteArray.Length < bodyLength + 4)
            {
                return;
            }
            //对完整的消息进行处理
            byteArray.readIdx += 4;
            //解析协议名
            int nameCount = 0;
            ProtocolEnum protocolEnum = ProtocolEnum.None;
            try
            {
                protocolEnum = MsgBase.DecodeName(byteArray.Bytes, byteArray.readIdx, out nameCount);
            }
            catch (Exception e)
            {
                Debug.LogError("解析协议名异常！" + e);
                CloseClient(clientSocket);
            }
            if (protocolEnum == ProtocolEnum.None)
            {
                Debug.LogError("解析协议名出错！");
                CloseClient(clientSocket);
                return;
            }
            //协议名解析成功
            byteArray.readIdx += nameCount;
            int bodyCount = bodyLength - nameCount;//协议内容长度
            MsgBase msgBase = null;
            try
            {
                msgBase = MsgBase.Decode(protocolEnum, byteArray.Bytes, byteArray.readIdx, bodyCount);
            }
            catch (Exception e)
            {
                Debug.LogError("解析协议内容异常！" + e);
                CloseClient(clientSocket);
                return;
            }
            if (msgBase == null)
            {
                // 和客户端同一条策略：一条坏包不再直接踢连接（踢了对方就是"敌人不动、自己能走不能打"）。
                // 长度前缀和协议名都可信，按 bodyCount 跳过这一帧接着收；真错位会每帧都坏，连续 5 条才断。
                // 是哪条协议、两把钥匙都试过 —— Decode 里已经打了
                byteArray.readIdx += bodyCount;
                byteArray.CheckAndMoveBytes();
                if (++clientSocket.BadMsgCount >= 5)
                {
                    Debug.LogError("连续 " + clientSocket.BadMsgCount + " 条包解不开，判定收包流出错，断开 UID="
                                   + (string.IsNullOrEmpty(clientSocket.UserID) ? "(未登录)" : clientSocket.UserID));
                    CloseClient(clientSocket);
                    return;
                }
                if (byteArray.Length > 4)
                {
                    OnReceiveData(clientSocket);   // 后面还粘着别的包，接着解
                }
                return;
            }
            clientSocket.BadMsgCount = 0;   // 正常解出一条就清零
            //一条完整的消息解析完成
            byteArray.readIdx += bodyCount;
            byteArray.CheckAndMoveBytes();
            //通过消息机制处理，反射
            // 这里原来有一行 LogInfo 打每条消息的协议名 —— 删了：43 输入是 20Hz×N 人，
            // 而这段跑在服务端主循环上（跟 battle.Tick 同一个线程），每秒几十次同步控制台写
            // 就是给战斗 tick 加 IO 抖动。而且它只说明"消息到了"，真出问题下面有 LogError。
            MethodInfo methodInfo = typeof(MsgHandler).GetMethod(protocolEnum.ToString());
            object[] obj = { clientSocket, msgBase };
            if (methodInfo != null)
            {
                methodInfo.Invoke(null, obj);
            }
            else
            {
                Debug.LogError("解析协议后进行消息分发错误：" + protocolEnum.ToString());
            }

            //粘包处理
            if (byteArray.Length > 4)
            {
                OnReceiveData(clientSocket);
            }
        }
        //发送数据
        public void Send(ClientSocket clientSocket, MsgBase msgBase)
        {
            if (clientSocket == null || !clientSocket.Socket.Connected)
            {
                return;
            }
            try
            {
                byte[] nameBytes = MsgBase.EncodeName(msgBase);
                byte[] bodyBytes = MsgBase.Encode(msgBase);
                int len = nameBytes.Length + bodyBytes.Length;
                byte[] headBytes = BitConverter.GetBytes(len);
                byte[] sendBytes = new byte[headBytes.Length + len];
                Array.Copy(headBytes, 0, sendBytes, 0, headBytes.Length);
                Array.Copy(nameBytes, 0, sendBytes, headBytes.Length, nameBytes.Length);
                Array.Copy(bodyBytes, 0, sendBytes, headBytes.Length + nameBytes.Length, bodyBytes.Length);
                try
                {
                    clientSocket.Socket.BeginSend(sendBytes, 0, sendBytes.Length, 0, null, null);
                }
                catch (SocketException e)
                {
                    Debug.LogError("Send数据异常！" + e);
                }
            }
            catch (SocketException e)
            {
                Debug.LogError("发送数据异常！" + e);
            }
        }


        //获取当前时间戳
        public long GetTimeStamp()
        {
            TimeSpan timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(timeSpan.TotalSeconds);
        }

        //关闭链接
        public void CloseClient(ClientSocket client)
        {
            if (client == null) return;
            // 幂等：ReadClient / OnReceiveData 有几条路径会连着调两次（count<=0 再加 catch 各一次），
            // 而下面是掉线钩子（推 52 / 推对手 48{1} / 销毁房间），跑两遍就会重复推包
            if (!m_ClientDic.ContainsKey(client.Socket)) return;
            // 掉线钩子：匹配队列移出；约战房整房销毁推 52；战斗房间 Loading 掉线推对手 48{1} + 销毁（战斗中挂机，第 6 步管）
            string uid = client.UserID;
            if (!string.IsNullOrEmpty(uid))
            {
                MatchManager.Instance.OnDisconnect(uid);
                LobbyRoomManager.Instance.OnDisconnect(uid);
                RoomManager.Instance.OnDisconnect(uid);
            }
            client.Socket.Close();
            m_ClientDic.Remove(client.Socket);
            // UID 一定要打：不然只有连接数，日志里没法跟"转入挂机/退出挂机"对上是谁
            Debug.Log("客户端断开连接 UID={0}，当前总连接数{1}",
                      string.IsNullOrEmpty(uid) ? "(未登录)" : uid, m_ClientDic.Count);
        }
        public ClientSocket FindByUserID(string userID)
        {
            foreach (ClientSocket cs in m_ClientDic.Values)
                if (cs.UserID == userID) return cs;
            return null;
        }
    }
}

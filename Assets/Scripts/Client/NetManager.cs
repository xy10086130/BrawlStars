using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using SimpleServer.Net;
using SimpleServer.Proto;
using UnityEngine;

public class NetManager : Singleton<NetManager>
{
    public string SecretKey;
    public string PublicKey = "abc123";
    private Socket m_Socket;
    private ByteArray m_ReadBuffer;
    private List<MsgBase> m_MsgList; //存储解析完成的消息
    private List<MsgBase> m_UnityList; //存储在unity中需要使用到的消息
    private int m_MsgCount; //存储消息的个数
    private Thread m_MsgThread; //消息处理线程
    private Queue<ByteArray> m_WriteQueue; //存储待发送的消息

    private long lastPingTime; //上一次发送心跳包的时间
    private long lastPongTime; //上一次收到心跳包的时间
    private Thread m_HeartThread; //心跳包处理线程
    // 心跳包间隔。⚠ 单位是**秒**（GetTimeStamp() 返回秒），不是毫秒。
    // 原来写 300 = 5 分钟一次心跳、超时 ×4 = 20 分钟才认死 —— 半开连接（丢包但没人发 FIN）
    // 20 分钟内两边都发现不了：表现就是"刚进去能动，过一会敌人不动了、我也打不了"
    // （同一时刻服务端会把我打成挂机，两边都以为对面死了，谁也不报错）
    private long m_HeartInterval = 5;
    private int m_LastDropWarnMs;  // "链接断了还在发"那条告警的限流
    private string m_LastPongClock = "(无)";   // 末次回包的墙上时间，只给"心跳超时"那行日志用
    private int m_BadMsgCount;     // 连续解不开的包数：满 5 条才判定收包流废了

    public delegate void ProtoListener(MsgBase msgBase);

    private Dictionary<ProtocolEnum, ProtoListener> m_ProtoDic = new Dictionary<ProtocolEnum, ProtoListener>(); //协议监听,每条协议对应一个监听即每条协议对应一个处理方法

    public void Init(string ip, int port)
    {
        if (m_Socket != null && m_Socket.Connected)
        {
            Debug.LogError("链接已存在!");
            return;
        }

        InitState();
        m_Socket.NoDelay = true;
        m_Socket.BeginConnect(ip, port, ConnectCallback, m_Socket);
    }

    //初始化状态
    void InitState()
    {
        m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        m_ReadBuffer = new ByteArray();
        m_MsgCount = 0;
        m_MsgList = new List<MsgBase>();
        m_UnityList = new List<MsgBase>();
        m_WriteQueue = new Queue<ByteArray>();
        lastPingTime = GetTimeStamp();
        lastPongTime = GetTimeStamp();
    }

    //链接回调
    void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndConnect(ar);
            m_MsgThread = new Thread(MsgThread);
            m_MsgThread.IsBackground = true;
            m_MsgThread.Start();
            m_HeartThread = new Thread(HeartThread);
            m_HeartThread.IsBackground = true;
            m_HeartThread.Start();
            ProtoManager.CSSecret();
            Debug.Log("链接成功！");
            m_Socket.BeginReceive(m_ReadBuffer.Bytes, m_ReadBuffer.writeIdx, m_ReadBuffer.Remain, 0, ReceiveCallback,
                socket);
        }
        catch (SocketException e)
        {
            Debug.LogError("链接回调异常！" + e);
            CloseClient();
        }
    }

    //心跳包处理线程回调
    private void HeartThread()
    {
        while (m_Socket != null && m_Socket.Connected)
        {
            long timeNow = GetTimeStamp();
            if (timeNow - lastPingTime > m_HeartInterval)
            {
                MsgPing msg = new MsgPing();
                SendMessage(msg);
                lastPingTime = GetTimeStamp();
            }

            if (timeNow - lastPongTime > m_HeartInterval * 4)
            {
                // Player.log 本身没有时间戳（也没有跨机器的共同基准），直接用墙上时间打，
                // 才能跟服务端日志（16:14:48,359 那种）逐行对齐。DateTime 是线程安全的，Time.* 不是
                Debug.LogError("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] 心跳超时：" + (m_HeartInterval * 4)
                               + " 秒没收到回包（末次回包 " + m_LastPongClock + "），判定掉线，关闭链接！");
                CloseClient();
                return;             // CloseClient 已经 Abort 掉本线程，别接着转圈
            }

            Thread.Sleep(100);      // 没有 sleep 会空转烧满一个核（MsgThread 里同样的坑，注释也在那边）
        }
    }

    //消息线程回调
    void MsgThread()
    {
        while (m_Socket != null && m_Socket.Connected)
        {
            if (m_MsgList.Count <= 0)
            {
                Thread.Sleep(10);   // 没消息时让出 CPU：忙等会占满一个核心，双开时帧率互相掐架
                continue;
            }

            MsgBase msgBase = null;
            lock (m_MsgList)
            {
                if (m_MsgList.Count > 0)
                {
                    msgBase = m_MsgList[0];
                    m_MsgList.RemoveAt(0);
                }
            }

            if (msgBase != null) //根据不同的消息做不同的处理
            {
                if (msgBase is MsgPing)
                {
                    lastPongTime = GetTimeStamp();
                    m_LastPongClock = DateTime.Now.ToString("HH:mm:ss.fff");   // 只给日志用
                    m_MsgCount--;
                }
                else
                {
                    lock (m_UnityList)
                    {
                        m_UnityList.Add(msgBase);
                    }
                }
            }
            else
            {
                CloseClient();
                break;
            }
        }
    }

    //处理unity中的消息数据
    public void MsgUpdate()
    {
        if (m_Socket != null && m_Socket.Connected)
        {
            // 每帧清空积压：高频协议（移动同步等）同帧到多条，逐条处理 = 逐帧延迟
            int n = 0;
            while (m_MsgCount > 0 && n++ < 64)   // 上限防一帧处理太多卡死
            {
                MsgBase msgBase = null;
                lock (m_UnityList)
                {
                    if (m_UnityList.Count > 0)
                    {
                        msgBase = m_UnityList[0];
                        m_UnityList.RemoveAt(0);
                        m_MsgCount--;
                    }
                    else break;
                }

                //执行协议处理
                if (msgBase != null)
                {
                    ExcuteProto(msgBase.ProtocolType, msgBase);
                }
            }
        }
    }


    //接收数据回调
    void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            int count = socket.EndReceive(ar);
            if (count <= 0)
            {
                CloseClient();
                return;
            }

            m_ReadBuffer.writeIdx += count;
            //对接受到的数据进行处理
            ReceiveData();
            // 腾出下一次 Receive 的空间。不能写成 ReSize(Length * 2)：ReSize 内部有
            // `if (size < initSize) return;` 的短路，Length 小于 512 时它直接返回、什么都没做，
            // 而 Remain 一旦是 0，下面 BeginReceive 会抛 ArgumentOutOfRange —— 它不是 SocketException，
            // 外层 catch 接不住，接收链就此断掉，之后一条消息都收不到（症状：位置不动、攻击动画不播）。
            // 本机双开时每次 Receive 基本刚好一条消息，readIdx 跟得上，碰不到；跨机粘包才暴露。
            // 和服务端 ReadClient 同一套写法。
            if (m_ReadBuffer.Remain < 8)
            {
                m_ReadBuffer.CheckAndMoveBytes();
                while (m_ReadBuffer.Remain < 8)
                {
                    int expandSize = m_ReadBuffer.Length < ByteArray.DefaultSize
                        ? ByteArray.DefaultSize
                        : m_ReadBuffer.Length;
                    m_ReadBuffer.ReSize(2 * expandSize);
                }
            }

            socket.BeginReceive(m_ReadBuffer.Bytes, m_ReadBuffer.writeIdx, m_ReadBuffer.Remain, 0, ReceiveCallback,
                socket);
        }
        // 接 Exception 而不只是 SocketException：缓冲区参数越界之类的问题不是 SocketException，
        // 漏出去会让接收链无声断掉（异常打在别的线程上，症状只剩"什么都不动了"，极难查）
        catch (Exception e)
        {
            Debug.LogError("接收数据回调异常！" + e);
            CloseClient();
        }
    }

    //对接受到的数据进行处理
    void ReceiveData()
    {
        if (m_ReadBuffer.Length <= 4 || m_ReadBuffer.readIdx < 0)
        {
            return;
        }

        int readIdx = m_ReadBuffer.readIdx;
        byte[] bytes = m_ReadBuffer.Bytes;
        int bodyLength = BitConverter.ToInt32(bytes, readIdx); //计算出包名和包内容的长度
        if (m_ReadBuffer.Length < bodyLength + 4)
        {
            return;
        }

        //对完整的消息进行处理
        m_ReadBuffer.readIdx += 4;
        //解析协议名
        int nameCount = 0;
        ProtocolEnum protocolEnum = ProtocolEnum.None;
        try
        {
            protocolEnum = MsgBase.DecodeName(m_ReadBuffer.Bytes, m_ReadBuffer.readIdx, out nameCount);
        }
        catch (Exception e)
        {
            Debug.LogError("解析协议名异常！" + e);
            CloseClient();
        }

        if (protocolEnum == ProtocolEnum.None)
        {
            Debug.LogError("解析协议名出错！");
            CloseClient();
            return;
        }

        //协议名解析成功
        m_ReadBuffer.readIdx += nameCount;
        int bodyCount = bodyLength - nameCount; //协议内容长度
        MsgBase msgBase = null;
        try
        {
            msgBase = MsgBase.Decode(protocolEnum, m_ReadBuffer.Bytes, m_ReadBuffer.readIdx, bodyCount);
        }
        catch (Exception e)
        {
            Debug.LogError("解析协议内容异常！" + e);
            CloseClient();
            return;
        }

        if (msgBase == null)
        {
            // 一条坏包不再直接把连接掐死（掐死 = 对面看你挂机、你看对面不动，两边都不报错，最难查的那种）。
            // 长度前缀和协议名都是可信的（名字都读出来了），按 bodyCount 跳过这一帧接着收；
            // 真错位的话每帧都会坏，连续坏满 5 条才判定收包流废了。
            // 具体是哪条协议、多大，Decode 里已经打出来了（"协议 X 解密失败/用钥匙[..]解不开"）
            m_ReadBuffer.readIdx += bodyCount;
            m_ReadBuffer.CheckAndMoveBytes();
            if (++m_BadMsgCount >= 5)
            {
                Debug.LogError("连续 " + m_BadMsgCount + " 条包解不开，判定收包流出错，关闭链接！");
                CloseClient();
                return;
            }
            if (m_ReadBuffer.Length > 4)
            {
                ReceiveData();   // 后面还粘着别的包，接着解
            }
            return;
        }
        m_BadMsgCount = 0;   // 正常解出一条就清零

        //一条完整的消息解析完成
        m_ReadBuffer.readIdx += bodyCount;
        m_ReadBuffer.CheckAndMoveBytes();
        //将解析完成的消息加入列表（高频协议不打日志，打日志会卡主线程）
        lock (m_MsgList)
        {
            m_MsgList.Add(msgBase);
            m_MsgCount++;
        }

        //粘包处理
        if (m_ReadBuffer.Length > 4)
        {
            ReceiveData();
        }
    }

    //关闭链接
    void CloseClient()
    {
        if (m_Socket == null)
        {
            return;
        }

        SecretKey = "";
        m_Socket.Close();
        if (m_MsgThread != null && m_MsgThread.IsAlive)
        {
            m_MsgThread.Abort();
            m_MsgThread = null;
        }
        if (m_HeartThread != null && m_HeartThread.IsAlive)
        {
            m_HeartThread.Abort();
            m_HeartThread = null;
        }
        Debug.Log("关闭链接！");
    }

    //协议监听 一条协议对应一个方法
    public void AddProtoListener(ProtocolEnum protocolEnum, ProtoListener protoListener)
    {
        m_ProtoDic[protocolEnum] = protoListener;
    }

    // 移除协议监听：每次发请求前清掉旧的，保证回包回调进"当前这次请求"
    public void RemoveProtoListener(ProtocolEnum protocolEnum)
    {
        if (m_ProtoDic.ContainsKey(protocolEnum))
        {
            m_ProtoDic.Remove(protocolEnum);
        }
    }

    //执行协议处理
    void ExcuteProto(ProtocolEnum protocolEnum, MsgBase msgBase)
    {
        if (m_ProtoDic.ContainsKey(protocolEnum))
        {
            m_ProtoDic[protocolEnum](msgBase);
        }
    }

    //发送信息
    public void SendMessage(MsgBase msgBase)
    {
        if (m_Socket == null || !m_Socket.Connected)
        {
            // 这里静默丢弃最坑：本机看着一切正常（本地预测还在动），服务端却一条都收不到 →
            // 对面看你原地站桩、你收不到 44 → "敌人不动、我打不了"。限流打一条，别刷屏
            int nowTick = Environment.TickCount;
            if (nowTick - m_LastDropWarnMs > 2000)
            {
                m_LastDropWarnMs = nowTick;
                Debug.LogWarning("链接已断开，消息被丢弃：" + msgBase.ProtocolType);
            }
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
            ByteArray ba = new ByteArray(sendBytes);
            int count = 0;
            lock (m_WriteQueue)
            {
                m_WriteQueue.Enqueue(ba);
                count = m_WriteQueue.Count;
            }

            if (count == 1)
            {
                m_Socket.BeginSend(sendBytes, 0, sendBytes.Length, 0, SendCallback, m_Socket);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError("发送数据异常！" + e);
        }
    }

    //发送回调
    void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            if (socket == null || !socket.Connected)
            {
                // ⚠ 这里静默 return = **永久停发**：队列里剩下的包再也没人推链
                //（SendMessage 只在入队后 count==1 时才起链，队列非空就永远轮不到它）。
                // 表现和服务端"半开"一模一样（服务端收不到你的 43，把你判挂机），
                // 但客户端主线程一切正常、连接也没断 —— 两端都不报错，最难查。
                int left;
                lock (m_WriteQueue) left = m_WriteQueue.Count;
                Debug.LogError("发送链断了：socket 不可用，队列里还剩 " + left + " 条没发出去（之后一个包都发不出去）");
                return;
            }

            int count = socket.EndSend(ar); //发送成功返回发送的字节数
            ByteArray ba;
            lock (m_WriteQueue)
            {
                ba = m_WriteQueue.First();
            }

            ba.readIdx += count;
            if (ba.Length == 0) //发送完成
            {
                lock (m_WriteQueue)
                {
                    m_WriteQueue.Dequeue();
                    if (m_WriteQueue.Count > 0)
                    {
                        ba = m_WriteQueue.First();
                    }
                    else
                    {
                        ba = null;
                    }
                }
            }

            if (ba != null)
            {
                socket.BeginSend(ba.Bytes, ba.readIdx, ba.Length, 0, SendCallback, socket);
            }
        }
        // 和收包那边同理：只接 SocketException 会漏掉 ObjectDisposedException / InvalidOperationException
        // （socket 被别的线程关了、队列被清空后 First() 抛），异常打在别的线程上，发送链无声断掉
        catch (Exception e)
        {
            Debug.LogError("发送数据回调异常！" + e);
            CloseClient();
        }
    }

    //获取当前时间戳
    public long GetTimeStamp()
    {
        TimeSpan timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(timeSpan.TotalSeconds);
    }


    //请求密钥的回调函数
    public void RecSecret(MsgBase msgBase)
    {
        NetManager.Instance.SecretKey = ((MsgSecret)msgBase).Secret;
        Debug.Log("收到密钥：" + NetManager.Instance.SecretKey);
    }
}
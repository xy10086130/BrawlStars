using ProtoBuf;
using SimpleServer.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleServer.Proto
{
    //消息基类
    public class MsgBase
    {
        public virtual ProtocolEnum ProtocolType { get; set; }
        //编码协议名
        public static byte[] EncodeName(MsgBase msgBase)
        {
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(msgBase.ProtocolType.ToString());
            Int16 len = (Int16)nameBytes.Length;
            byte[] bytes = new byte[2 + len];
            bytes[0] = (byte)(len % 256);
            bytes[1] = (byte)(len / 256);
            Array.Copy(nameBytes, 0, bytes, 2, len);
            return bytes;
        }
        //解码协议名
        public static ProtocolEnum DecodeName(byte[] bytes, int offset, out int count)
        {
            count = 0;
            if (offset + 2 > bytes.Length)
            {
                return ProtocolEnum.None;
            }
            Int16 len = (Int16)(bytes[offset + 1] << 8 | bytes[offset]);
            if (offset + 2 + len > bytes.Length)
            {
                return ProtocolEnum.None;
            }
            count = 2 + len;
            try
            {
                string name = System.Text.Encoding.UTF8.GetString(bytes, offset + 2, len);
                return (ProtocolEnum)System.Enum.Parse(typeof(ProtocolEnum), name);
            }
            catch (Exception e)
            {
                Debug.LogError("不存在协议：" + e.ToString());
                return ProtocolEnum.None;
            }
        }
        //协议内容序列化和加密
        public static byte[] Encode(MsgBase msgBase)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                Serializer.Serialize(memory, msgBase);
                byte[] bytes = memory.ToArray();
                string secret = ServerSocket.Instance.SecretKey;
                if (msgBase is MsgSecret)//当前的消息是请求密钥的时候
                {
                    secret = ServerSocket.Instance.PublicKey;
                }
                bytes = AES.AESEncrypt(bytes, secret);
                return bytes;
            }
        }
        //协议内容解密和反序列化
        public static MsgBase Decode(ProtocolEnum protocol, byte[] bytes, int offset, int count)
        {
            if (count <= 0)
            {
                Debug.LogError("协议解密错误，数据长度为0");
                return null;
            }

            byte[] cipher = new byte[count];
            Array.Copy(bytes, offset, cipher, 0, count);

            // ⚠ 一把钥匙解不开就换另一把兜底（2026-09-24）：客户端那把钥匙是在它**主线程**才落地的
            //（MsgUpdate → RecSecret），而它收完密钥之后可能马上又发一条请求 —— 那条还带着公钥出来，
            // 服务端拿私钥解就"填充无效"→ 老代码直接把这条连接踢了（日志 16:13:26 那次的真实原因）。
            // 全项目就两把钥匙，两把都试一遍，这套时机问题就不存在了。
            string secret = ServerSocket.Instance.SecretKey;//默认是密钥解密
            if (protocol == ProtocolEnum.MsgSecret)//请求密钥，用公钥解密
            {
                secret = ServerSocket.Instance.PublicKey;
            }
            string other = secret == ServerSocket.Instance.PublicKey
                ? ServerSocket.Instance.SecretKey
                : ServerSocket.Instance.PublicKey;

            MsgBase msg = TryDecode(protocol, cipher, secret);
            if (msg == null && !string.IsNullOrEmpty(other) && other != secret)
            {
                msg = TryDecode(protocol, cipher, other);
                if (msg != null)
                {
                    Debug.LogWarn("协议 " + protocol + " 是用另一把钥匙发的（两端换钥匙的时机错开了），已兜底解开");
                }
            }
            if (msg == null)
            {
                Debug.LogError("协议 " + protocol + " 解密失败：两把钥匙都试过，密文 " + count + " 字节");
            }
            return msg;
        }

        // 用指定钥匙解一次：解不开（含反序列化失败）返回 null，不抛
        static MsgBase TryDecode(ProtocolEnum protocol, byte[] cipher, string key)
        {
            try
            {
                byte[] newBytes = AES.AESDecrypt(cipher, key);//AESDecrypt 不改入参，可以拿同一份密文重试
                //反序列化
                using (MemoryStream memory = new MemoryStream(newBytes, 0, newBytes.Length))
                {
                    Type type = System.Type.GetType(protocol.ToString());//根据协议名获取到对应的类类型
                    return (MsgBase)Serializer.NonGeneric.Deserialize(type, memory);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarn("协议 " + protocol + " 用钥匙[" + key + "]解不开：" + e.Message);
                return null;
            }
        }
    }
}

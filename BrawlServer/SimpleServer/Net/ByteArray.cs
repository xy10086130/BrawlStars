using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleServer.Net
{
    public class ByteArray
    {
        public const int DefaultSize = 1024;
        public int initSize;
        public int capacity;
        public byte[] Bytes;
        public int readIdx;
        public int writeIdx;
        public int Remain { get { return capacity - writeIdx; } }//剩余空间
        public int Length { get { return writeIdx - readIdx; } }//数据长度

        public ByteArray()
        {
            Bytes = new byte[DefaultSize];
            capacity = DefaultSize;
            initSize = DefaultSize;
            readIdx = 0;
            writeIdx = 0;
        }

        //检测数据
        public void CheckAndMoveBytes()
        {
            if (Length < 8)
            {
                if (readIdx < 0)
                {
                    return;
                }
                Array.Copy(Bytes, readIdx, Bytes, 0, Length);
                writeIdx = Length;
                readIdx = 0;
            }
        }
        //缓冲区扩容
        public void ReSize(int size)
        {
            if (readIdx < 0)
            {
                return;
            }
            if (size < Length)
            {
                return;
            }
            if (size < initSize)
            {
                return;
            }
            int n = DefaultSize;
            while (n < size)
            {
                n *= 2;
            }
            capacity = n;
            byte[] newBytes = new byte[capacity];
            Array.Copy(Bytes, readIdx, newBytes, 0, Length);
            Bytes = newBytes;
            readIdx = 0;
            writeIdx = Length;
        }
    }
}

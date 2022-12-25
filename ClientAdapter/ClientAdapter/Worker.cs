using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ClientAdapter
{
    class Worker
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private Socket _sock = null;
        private string _secret = string.Empty;
        private int _timeout = 10;
        private int _waitLoopMaxCount = 100;
        private int _waitLoopCount = 0;

        private RemoteSocketInfo _remoteSocket = null;

        public Worker(Socket sock, string secret, int timeout)
        {
            this._sock = sock;
            this._secret = secret;
            this._timeout = timeout;

            this._waitLoopMaxCount = timeout * 10;
        }

        public async Task<bool> Authenticate(DateTime now)
        {
            var authData = new byte[36];

            var t = (long)((now - UnixEpoch).TotalSeconds);
            var part2 = string.Format("{0:X12}", t);
            var part3 = string.Format("{0:X4}", this._timeout);

            var s = part2 + part3;
            var buf = Encoding.ASCII.GetBytes(s);
            Array.Copy(buf, 0, authData, 20, buf.Length);

            s = this._secret + "\n" + part2;
            var hash = Encoding.ASCII.GetBytes(s);
            using (var engine = SHA1.Create())
            {
                hash = engine.ComputeHash(hash);
            }
            Array.Copy(hash, 0, authData, 0, hash.Length);

            await this.SendAll(authData, 0, authData.Length);
            return true;
        }

        public Socket InnerSock
        {
            get
            {
                return this._sock;
            }
        }

        public RemoteSocketInfo RemoteSocket
        {
            get
            {
                return this._remoteSocket;
            }
        }

        public class RemoteSocketInfo
        {
            private int _addrFamily = 0;
            private int _remotePort = 0;
            private string _remoteAddr = string.Empty;

            public RemoteSocketInfo(int addrFamily, int remotePort, string remoteAddr)
            {
                this._addrFamily = addrFamily;
                this._remotePort = remotePort;
                this._remoteAddr = remoteAddr;
            }

            public override string ToString()
            {
                return this._remoteAddr;
            }

            public int AddrFamily { get { return this._addrFamily; } }
            public int RemotePort { get { return this._remotePort; } }
            public string RemoteAddr { get { return this._remoteAddr; } }
        }

        public async Task<bool> WaitForSocket()
        {
            var buf = new byte[4];
            var ok = await this.RecvAllWithWorkerTimeout(buf, 0, 2);
            if (!ok)
            {
                return false;
            }

            var s = Encoding.ASCII.GetString(buf, 0, 2);
            var n = int.Parse(s, System.Globalization.NumberStyles.AllowHexSpecifier);
            buf = new byte[n];
            ok = await this.RecvAllWithWorkerTimeout(buf, 0, n);
            if (!ok)
            {
                return false;
            }

            s = Encoding.ASCII.GetString(buf, 1, n - 1);
            var arr = s.Split('\n');
            var addrFamily = int.Parse(arr[0], System.Globalization.NumberStyles.AllowHexSpecifier);
            var remotePort = int.Parse(arr[1], System.Globalization.NumberStyles.AllowHexSpecifier);
            var remoteAddr = arr[2];

            this._remoteSocket = new RemoteSocketInfo(addrFamily, remotePort, remoteAddr);
            return true;
        }

        private async Task<int> RecvWithWorkerTimeout(byte[] buf, int offset, int size)
        {
            int result = 0;
            var task = this._sock.ReceiveAsync(new ArraySegment<byte>(buf, offset, size), SocketFlags.None);
            while (this._waitLoopCount < this._waitLoopMaxCount)
            {
                if (task.IsCompleted || task.IsCanceled || !this._sock.Connected)
                {
                    try
                    {
                        result = task.Result;
                    }
                    catch (Exception) { }
                    break;
                }

                await Task.Delay(100);
                ++this._waitLoopCount;
            }
            return result;
        }

        private async Task<bool> RecvAllWithWorkerTimeout(byte[] buf, int offset, int size)
        {
            int actualSize = 0;
            while (actualSize < size)
            {
                var n = await this.RecvWithWorkerTimeout(buf, offset + actualSize, size - actualSize);
                if (n <= 0)
                {
                    break;
                }
                actualSize += n;
            }
            return (actualSize == size);
        }


        private async Task<bool> SendAll(byte[] buf, int offset, int size)
        {
            int actualSize = 0;
            while (actualSize < size)
            {
                var segBuf = new ArraySegment<byte>(buf, offset + actualSize, size - actualSize);
                var n = await this._sock.SendAsync(segBuf, SocketFlags.None);
                if (n <= 0)
                {
                    break;
                }
                actualSize += n;
            }
            return (actualSize == size);
        }

        //
    }
}

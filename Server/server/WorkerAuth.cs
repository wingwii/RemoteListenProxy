using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;

namespace server
{
    class WorkerAuth
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private long _maxDeltaSeconds = 3600;

        private Socket _socket = null;
        private string _secret = null;
        private int _aliveTime = 60;

        public WorkerAuth(Socket sock, string secret, long maxDeltaSeconds)
        {
            this._socket = sock;
            this._secret = secret;
            this._maxDeltaSeconds = maxDeltaSeconds;
        }

        public int AliveTime
        {
            get
            {
                return this._aliveTime;
            }
        }

        public async Task<bool> BeginAccept(DateTime now)
        {
            var buf = new byte[36];
            var ok = await this.RecvAll(buf, 0, buf.Length);
            if (!ok)
            {
                return false;
            }

            var s = Encoding.ASCII.GetString(buf, 32, 4);
            this._aliveTime = int.Parse(s, System.Globalization.NumberStyles.AllowHexSpecifier);

            s = Encoding.ASCII.GetString(buf, 20, 12);
            var num = long.Parse(s, System.Globalization.NumberStyles.AllowHexSpecifier);

            var t1 = UnixEpoch.AddSeconds(num);
            var dt = now - t1;
            num = (long)Math.Abs(dt.TotalSeconds);
            if (num > this._maxDeltaSeconds)
            {
                return false;
            }

            s = this._secret + "\n" + s;
            var hash = Encoding.ASCII.GetBytes(s);
            using (var engine = SHA1.Create())
            {
                hash = engine.ComputeHash(hash);
            }

            for (int i = 0; i < 20; ++i)
            {
                if (hash[i] != buf[i])
                {
                    return false;
                }
            }

            return true;
        }

        public async Task EndAccept(Socket sock)
        {
            var remoteEndPoint = sock.RemoteEndPoint as IPEndPoint;
            var s = string.Empty;
            s += "\n" + ((int)remoteEndPoint.AddressFamily).ToString("X");
            s += "\n" + remoteEndPoint.Port.ToString("X");
            s += "\n" + remoteEndPoint.Address.ToString();

            var s2 = string.Format("{0:X2}", s.Length) + s;
            var buf = Encoding.ASCII.GetBytes(s2);

            await this.SendAll(buf, 0, buf.Length);
        }

        private async Task<bool> RecvAll(byte[] buf, int offset, int size)
        {
            int actualSize = 0;
            while (actualSize < size)
            {
                var segBuf = new ArraySegment<byte>(buf, offset + actualSize, size - actualSize);
                var n = await this._socket.ReceiveAsync(segBuf, SocketFlags.None);
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
                var n = await this._socket.SendAsync(segBuf, SocketFlags.None);
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

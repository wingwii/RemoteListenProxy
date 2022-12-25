using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace RemoteListenProxy
{
    public class Transfer
    {
        private bool _primary = false;
        private byte[] _buffer = null;
        private Socket _socket1 = null;
        private Socket _socket2 = null;

        public Transfer(Socket socket1, Socket socket2, int bufferSize, bool primary)
        {
            this._primary = primary;
            this._socket1 = socket1;
            this._socket2 = socket2;
            this._buffer = new byte[bufferSize];
        }

        public async Task Run()
        {
            var segment = new ArraySegment<byte>(this._buffer, 0, this._buffer.Length);
            while (true)
            {
                var r = await this._socket1.ReceiveAsync(segment, SocketFlags.None);
                if (r <= 0)
                {
                    break;
                }

                var w = await this.Forward(r);
                if (w != r)
                {
                    break;
                }
            }
            if (!this._primary)
            {
                ShutdownSocket(this._socket1);
                ShutdownSocket(this._socket2);
            }
            //
        }

        private async Task<int> Forward(int size)
        {
            int actualSize = 0;
            while (actualSize < size)
            {
                var segment = new ArraySegment<byte>(this._buffer, actualSize, size - actualSize);
                var partSize = await this._socket2.SendAsync(segment, SocketFlags.None);
                if (partSize <= 0)
                {
                    break;
                }
                actualSize += partSize;
            }
            return actualSize;
        }

        private static void ShutdownSocket(Socket socket)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception) { }
        }

        //
    }
}

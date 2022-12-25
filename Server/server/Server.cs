using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace server
{
    class Server
    {
        private string _config = null;
        private string _workerAuthSecret = string.Empty;
        private int _workerPort = 0;
        private int _clientPort = 0;

        private long _workerAuthMaxDeltaSeconds = 3600;
        private int _proxyBufferSize = 0x4000;

        private ConcurrentQueue<Worker> _quWorkers = new ConcurrentQueue<Worker>();
        

        public Server(string config)
        {
            this._config = config;
        }

        public void Run()
        {
            var args = this._config.Split(' ');

            this._clientPort = int.Parse(args[0]);
            this._workerPort = int.Parse(args[1]);
            this._workerAuthSecret = args[2];

            var task = this.RunClientListener();
            task = this.RunWorkerListener();
            task.Wait();
        }

        private async Task RunWorkerListener()
        {
            var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Any, this._workerPort));
            listenSocket.Listen(8);
            while (true)
            {
                var socket = await listenSocket.AcceptAsync();
                var task = this.HandleAdminSock(socket);
            }
        }

        private class Worker
        {
            public DateTime createdTime = DateTime.MinValue;
            public Socket socket = null;
            public WorkerAuth auth = null;
        }

        private async Task HandleAdminSock(Socket socket)
        {
            var t0 = DateTime.Now;
            var ok = false;
            try
            {
                socket.NoDelay = true;

                var auth = new WorkerAuth(socket, this._workerAuthSecret, this._workerAuthMaxDeltaSeconds);
                ok = await auth.BeginAccept(t0);
                if (ok)
                {
                    var worker = new Worker();
                    worker.createdTime = t0;
                    worker.socket = socket;
                    worker.auth = auth;

                    this._quWorkers.Enqueue(worker);
                }
            }
            catch (Exception) { }
            if (!ok)
            {
                socket.Close();
            }
        }

        private async Task RunClientListener()
        {
            var listenSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSock.Bind(new IPEndPoint(IPAddress.Any, this._clientPort));
            listenSock.Listen(8);
            while (true)
            {
                var socket = await listenSock.AcceptAsync();
                var task = this.HandleClientSockSafely(socket);
            }
        }

        private async Task HandleClientSockSafely(Socket socket)
        {
            var t1 = DateTime.Now;
            try
            {
                await this.HandleClientSock(socket, t1);
            }
            catch (Exception) { }
            socket.Close();
        }

        private Worker SelectAdminWorker(DateTime t1)
        {
            while (true)
            {
                Worker worker = null;
                if (this._quWorkers.TryDequeue(out worker))
                {
                    var dt = t1 - worker.createdTime;
                    if (dt.TotalSeconds <= worker.auth.AliveTime)
                    {
                        return worker;
                    }
                }
                if (worker != null)
                {
                    worker.socket.Close();
                }
                if (this._quWorkers.IsEmpty)
                {
                    break;
                }
            }
            return null;
        }

        private async Task HandleClientSock(Socket socket2, DateTime t1)
        {
            var worker = this.SelectAdminWorker(t1);
            if (null == worker)
            {
                return;
            }

            var socket1 = worker.socket;
            try
            {
                socket2.NoDelay = true;
                await worker.auth.EndAccept(socket2);
                await this.RunProxy(socket1, socket2);
            }
            catch (Exception) { }
            socket2.Close();
        }

        private async Task RunProxy(Socket socket1, Socket socket2)
        {
            var tr1 = new RemoteListenProxy.Transfer(socket1, socket2, this._proxyBufferSize, true);
            var tr2 = new RemoteListenProxy.Transfer(socket2, socket1, this._proxyBufferSize, false);

            var task = tr2.Run();
            await tr1.Run();
        }

        //
    }
}

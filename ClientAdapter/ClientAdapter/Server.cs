using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ClientAdapter
{
    class Server
    {
        private string _config = null;

        private int _workerCount = 1;
        private string _workerAuthSecret = string.Empty;
        private int _workerPort = 0;
        private int _workerWaitTimeout = 30;
        private string _remoteServer = string.Empty;
        private int _dstPort = 0;
        private string _dstHost = string.Empty;
        private int _proxyBufferSize = 0x4000;

        public Server(string config)
        {
            this._config = config;
        }

        public void Run()
        {
            this.ParseUserConfig();

            Task task = null;
            for (int i = 1; i < this._workerCount; ++i)
            {
                task = this.RunWorker();
            }

            task = this.RunWorker();
            task.Wait();
        }

        private void ParseUserConfig()
        {
            var cfg = UserConfig.Parse(this._config);

            this._workerPort = cfg.workerPort;
            this._workerAuthSecret = cfg.workerSecret;

            foreach (var kv in cfg.optionalKVs)
            {
                var key = kv.Key;
                var value = kv.Value;
                if (key.Equals("server", StringComparison.Ordinal))
                {
                    this._remoteServer = value;
                }
                else if (key.Equals("worker", StringComparison.Ordinal))
                {
                    this._workerCount = int.Parse(value);
                }
                else if (key.Equals("dstport", StringComparison.Ordinal))
                {
                    this._dstPort = int.Parse(value);
                }
                else if (key.Equals("dsthost", StringComparison.Ordinal))
                {
                    this._dstHost = value;
                }
            }
        }

        private async Task RunWorker()
        {
            while (true)
            {
                await this.SafelyMaintainWorker();
                await Task.Delay(10);
            }
        }

        private async Task SafelyMaintainWorker()
        {
            var ok = false;
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                ok = await this.MaintainWorker(socket);
            }
            catch (Exception) { }
            if (!ok)
            {
                socket.Close();
            }
        }

        private async Task<bool> MaintainWorker(Socket socket)
        {
            var now = DateTime.Now;
            await socket.ConnectAsync(this._remoteServer, this._workerPort);
            socket.NoDelay = true;

            var worker = new Worker(socket, this._workerAuthSecret, this._workerWaitTimeout);
            await worker.Authenticate(now);
            
            await worker.WaitForSocket();
            var remoteSocket = worker.RemoteSocket;
            if (null == remoteSocket)
            {
                return false;
            }

            var task = this.StartProxySafely(worker);
            return true;
        }

        private async Task StartProxySafely(Worker worker)
        {
            var socket1 = worker.InnerSock;
            var socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await this.StartProxy(worker, socket2);
            }
            catch (Exception) { }
            socket1.Close();
            socket2.Close();
        }

        private async Task StartProxy(Worker worker, Socket socket2)
        {
            await socket2.ConnectAsync(this._dstHost, this._dstPort);
            socket2.NoDelay = true;

            var socket1 = worker.InnerSock;
            var tr1 = new RemoteListenProxy.Transfer(socket1, socket2, this._proxyBufferSize, true);
            var tr2 = new RemoteListenProxy.Transfer(socket2, socket1, this._proxyBufferSize, false);

            var task = tr2.Run();
            await tr1.Run();
        }

        //
    }
}

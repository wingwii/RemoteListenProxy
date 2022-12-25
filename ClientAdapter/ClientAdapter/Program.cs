using System;
using System.IO;
using System.Threading;

namespace ClientAdapter
{
    class Program
    {
        static void Main(string[] args)
        {
            Server firstServer = null;

            var configs = File.ReadAllLines(args[0]);
            foreach (var config in configs)
            {
                if (string.IsNullOrEmpty(config))
                {
                    continue;
                }
                if (config.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var server = new Server(config);
                if (null == firstServer)
                {
                    firstServer = server;
                }
                else
                {
                    var thread = new Thread(ThreadRunServer);
                    thread.IsBackground = true;
                    thread.Start(server);
                }
            }

            if (firstServer != null)
            {
                firstServer.Run();
            }
        }

        private static void ThreadRunServer(object threadParam)
        {
            var server = threadParam as Server;
            server.Run();
        }

        //
    }
}

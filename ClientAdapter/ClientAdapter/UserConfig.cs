using System;
using System.Collections.Generic;
using System.Text;

namespace ClientAdapter
{
    class UserConfig
    {
        public class Config
        {
            public int clientPort = 0;
            public int workerPort = 0;
            public string workerSecret = null;
            public KeyValuePair<string, string>[] optionalKVs = null;
        }

        public static Config Parse(string config)
        {
            var result = new Config();
            var optKVs = new List<KeyValuePair<string, string>>();

            var args = config.Split(' ');
            var argc = args.Length;

            result.clientPort = int.Parse(args[0]);
            result.workerPort = int.Parse(args[1]);
            result.workerSecret = args[2];

            int i = 3;
            while (i < argc)
            {
                var key = args[i++];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                if (i >= argc)
                {
                    break;
                }

                var value = args[i++];
                key = key.Substring(2).ToLower();

                optKVs.Add(new KeyValuePair<string, string>(key, value));
            }

            result.optionalKVs = optKVs.ToArray();
            return result;
        }

        //
    }
}

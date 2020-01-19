using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WebSocketSharp.Server;

namespace StateFlux.Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar+ "appsettings.json", true, true)
            .Build();

            string url = config["url"];
            if (url == null)
            {
                Console.WriteLine("Missing setting: url");
                return;
            }

            var wssv = new WebSocketServer(url);
            wssv.AddWebSocketService<AppWebSocketBehavior>("/Service");
            wssv.Start();
            Console.WriteLine($"{DateTime.Now}, Starting Service @ {url}");

            // wait forever...
            using(var evt = new ManualResetEvent(false))
            {
                evt.WaitOne();
            }
            wssv.Stop();
        }
    }
}
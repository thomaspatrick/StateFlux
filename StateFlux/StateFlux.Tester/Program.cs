using System;
using WebSocketSharp;
using WebSocketSharp.Net;
using StateFlux.Client;
using StateFlux.Model;
using System.Threading;
using Newtonsoft.Json;

namespace StateFlux.Client
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            //var app = new App();
            //app.Run();
            var client = new Client();
            client.SessionSaveFilename = "cheese.json";
            client.RequestedUsername = "happyboi";
            client.Start();

            bool once = true;
            int idx = 0;
            bool shouldStop = false;
            while (!shouldStop)
            {
                if(client.SocketOpenWithIdentity && idx % 10 == 0)
                {
                    if (once) { Console.WriteLine("Socket Open!"); once = false; }
                    ChatSayMessage request = new ChatSayMessage
                    {
                        say = "Hi!"
                    };
                    Console.WriteLine("adding ChatSayMessage to queue");
                    client.SendRequest(request);
      //              var request2 = new GameInstanceListMessage();
        //            Console.WriteLine("adding GameInstanceList to queue");
          //          client.SendRequest(request2);
//                    var request3 = new CreateGameInstanceMessage() { GameName = "AssetCollapse", InstanceName = "GameInstance#" + idx };
  //                  Console.WriteLine("creating game instance to queue");
    //                client.SendRequest(request3);

                }

                Message response = client.ReceiveResponse();
                if(response != null)
                {
                    Console.WriteLine("Received response message: " + response.MessageType);
                    if (response.MessageType == MessageTypeNames.ChatSaid)
                    {
                        var msg = (ChatSaidMessage)response;
                        Console.WriteLine(msg.Say);
                    }
                    else if (response.MessageType == MessageTypeNames.GameInstanceListing)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(response));
                    }
                }

                Thread.Sleep(100);
                idx++;
            }
        }
    }
}

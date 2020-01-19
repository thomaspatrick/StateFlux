using System;
using StateFlux.Model;

namespace StateFlux.Service
{
    public class MessageHandler
    {
        public AppWebSocketBehavior _websocket { get; set; }
        public Server _server;

        public MessageHandler(AppWebSocketBehavior serviceBehavior)
        {
            _websocket = serviceBehavior;
            _server = Server.Instance;
        }
    }
}

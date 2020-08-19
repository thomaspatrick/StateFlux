using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using StateFlux.Model;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace StateFlux.Service
{
    public class MessageHandlerBinding
    {
        public MessageHandler objectTarget;
        public MethodInfo methodTarget;
    }

    public class AppWebSocketBehavior : WebSocketBehavior
    {
        static protected List<Type> _messageTypes = FindAssignableFrom(typeof(Message));
        static protected List<Type> _handlerTypes = FindAssignableFrom(typeof(MessageHandler));
        protected Dictionary<Type, MessageHandlerBinding> _handlerMap;
        static protected Dictionary<string, string> _tokens = new Dictionary<string, string>();
        private const string _messageClassPrefix = "StateFlux.Model.";
        private const string _messageClassSuffix = "Message, StateFlux.Model";
        private const string _dateFormat = "yyyy-MM-ddTHH:mm:ss:ffff";

        public AppWebSocketBehavior() : base()
        {
            _handlerMap = BuildHandlerMap();
        }

        public string GetSessionCookieValue()
        {
            string value = null;
            if(Context != null && Context.CookieCollection != null)
            {
                foreach (Cookie cookie in Context.CookieCollection)
                {
                    if (cookie.Name.Equals(MessageConstants.SessionCookieName))
                    {
                        value = cookie.Value;
                        break;
                    }
                }
            }
            return value;
        }

        public Player GetCurrentSessionPlayer(bool attemptLoadIfNoSession = true)
        {
            string sessionCookieValue = GetSessionCookieValue();
            if (string.IsNullOrEmpty(sessionCookieValue)) return null;

            Player player = Server.Instance.Players.FirstOrDefault(p => p.SessionData.SessionId == sessionCookieValue);
            if(attemptLoadIfNoSession && player == null)
            {
                // not an active player - check database?
                LogMessage($"Session cookie '{sessionCookieValue}' not found in player list", false);
                if (sessionCookieValue != null)
                {
                    Player found = Server.Instance.playerRepository.GetAllPlayers().FirstOrDefault(p => p.SessionData?.SessionId == sessionCookieValue);
                    if (found != null)
                    {
                        // add player to active list
                        LogMessage($"Activating player {found.Name} from database that matches session cookie '{sessionCookieValue}'", false);
                        player = found;
                        player.GameInstanceRef = null;
                        Server.Instance.Players.Add(player);
                    }
                    else
                    {
                        string msg = $"Connected user provided an unknown session cookie '{sessionCookieValue}' (not found in database)!";
                        throw new Exception(msg);
                    }
                }
            }
            return player;
        }

        public Player CreatePlayerSession(string playerName)
        {
            Player player = new Player
            {
                Name = playerName,
                SessionData = new PlayerSessionData { SessionId = Guid.NewGuid().ToString(), WebsocketSessionId = this.ID },
            };
            Server.Instance.playerRepository.InsertPlayer(player);
            Server.Instance.Players.Add(player);
            LogMessage($"Created player {player.Name} with session '{player.SessionData.SessionId}'.  Added to players & saved to database");
            return player;
        }

        public void Respond(Message message)
        {
            try
            {
                if (State == WebSocketState.Open)
                {
                    Send(JsonConvert.SerializeObject(message));
                }
            }
            catch (Exception e)
            {
                LogMessage(e.Message);
            }
        }

        public void Broadcast(Message message, GameInstanceRef gameInstanceRef, bool meToo)
        {
            try
            {
                Player currentPlayer = GetCurrentSessionPlayer();
                string msg = JsonConvert.SerializeObject(message);
                WebSocketSessionManager sessionManager = this.Sessions;
                foreach (Player player in Server.Instance.Players)
                {
                    GameInstance playerGameInstance = FindPlayerGameInstance(player);
                    if (gameInstanceRef == null || gameInstanceRef.Id == playerGameInstance.Id)
                    {
                        IWebSocketSession session;
                        if (sessionManager.TryGetSession(player.SessionData.WebsocketSessionId, out session))
                        {
                            if (player.Id == this.GetCurrentSessionPlayer().Id && !meToo) continue;
                            //LogMessage($"Sending to {player.Name} msg={JsonConvert.SerializeObject(msg)}");
                            session.Context.WebSocket.Send(msg);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                LogMessage(e.Message);
            }
        }

        public void Send(Message message, Guid playerId)
        {
            try
            {
                string msg = JsonConvert.SerializeObject(message);
                Player toPlayer = Server.Instance.Players.FirstOrDefault(p => p.Id == playerId);
                if (this.Sessions.TryGetSession(toPlayer.SessionData.WebsocketSessionId, out IWebSocketSession session))
                {
                    session.Context.WebSocket.Send(msg);
                }
                else
                {
                    LogMessage($"Attempted to send {message.MessageType} to {playerId}, who was not found.");
                }
            }
            catch (Exception e)
            {
                LogMessage(e.Message);
            }
        }

        public GameInstance FindPlayerGameInstance(Player player)
        {
            foreach (Game game in Server.Instance.Games)
            {
                foreach (GameInstance gameInstance in game.Instances)
                {
                    if (gameInstance.Players.Contains(player))
                    {
                        return gameInstance;
                    }
                }
            }
            return null;
        }

        public void LogMessage(string message, bool showPlayerIdentity = true)
        {
            Player currentPlayer = null;
            try
            {
                currentPlayer = showPlayerIdentity? GetCurrentSessionPlayer(attemptLoadIfNoSession: false) : null;
            }
            catch(Exception)
            {
            }

            string playerName = currentPlayer != null ? currentPlayer.Name : "unknown";
            Console.WriteLine($"{DateTime.Now.ToString(_dateFormat)},{playerName},{message}");
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            try
            {
                Player currentPlayer = GetCurrentSessionPlayer();
                LogMessage($"Connection opened");
                if (currentPlayer != null)
                {
                    currentPlayer.SessionData.WebsocketSessionId = this.ID;
                }
                Server.Instance.SessionManager = this.Sessions;
            }
            catch (Exception e)
            {
                LogMessage(e.Message);
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            LogMessage($"Connection error { e.Message }");
            base.OnError(e);
        }

        protected override void OnClose(CloseEventArgs closeEventArgs)
        {
            try
            {
                Player currentPlayer = GetCurrentSessionPlayer();
                if (currentPlayer == null) return; 

                LogMessage($"Connection closed");

                IEnumerable<Player> players = Server.Instance.Players;

                var playerListingMessage = new PlayerListingMessage
                {
                    Players = Server.Instance.Players.Where(p => p.SessionData.SessionId != currentPlayer.SessionData.SessionId).ToList()
                };
                Broadcast(playerListingMessage, null, true);

                if(currentPlayer.GameInstanceRef != null)
                {
                    // player is in a game instance!
                    var gameInstanceStoppedMessage = new GameInstanceStoppedMessage
                    {
                        GameInstance = currentPlayer.GameInstanceRef,
                        Host = Server.Instance.LookupInstance(currentPlayer.GameInstanceRef.Id).HostPlayer
                    };
                    Broadcast(gameInstanceStoppedMessage, null, true);
                }

                Server.Instance.RemoveGameInstance(currentPlayer);
                Server.Instance.Players.Remove(currentPlayer);
            }
            finally
            {
                base.OnClose(closeEventArgs);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                Message message = DeserializeMessage(e.Data);

                MessageHandlerBinding binding = _handlerMap[message.GetType()];
                MessageHandler handler = binding.objectTarget;
                MethodInfo method = binding.methodTarget;

                Player player = this.GetCurrentSessionPlayer();
                string pname = (player != null) ? player.Name : "unknown";
                if (message.MessageType != MessageTypeNames.HostStateChange && message.MessageType != MessageTypeNames.GuestStateChange)
                {
                    LogMessage($"OnMessage.Processing {message.MessageType} from {pname}");
                }
                Message responseMessage = (Message)method.Invoke(handler, new object[] { message });
                if(responseMessage != null)
                {
                    Respond(responseMessage);
                }
                if(message.MessageType != MessageTypeNames.HostStateChange && message.MessageType != MessageTypeNames.GuestStateChange)
                {
                    LogMessage($"OnMessage.Processed {message.MessageType} from {pname}");
                }
            }
            catch (Exception exception)
            {
                string msg = (exception.InnerException == null) ? exception.Message : exception.InnerException.Message;
                LogMessage($"{msg}");
                ServerErrorMessage error = new ServerErrorMessage()
                {
                    Error = msg
                };
                Send(JsonConvert.SerializeObject(error));
            }
        }

        private Dictionary<Type, MessageHandlerBinding> BuildHandlerMap()
        {
            var dictionary = new Dictionary<Type, MessageHandlerBinding>();
            foreach (Type messageType in _messageTypes)
            {
                foreach (Type handlerType in _handlerTypes)
                {
                    var methodInfoFiltered =
                        handlerType.GetMethods().Where(mi => mi.IsPublic && mi.GetParameters().Any(p => messageType == p.ParameterType));
                    MethodInfo target = methodInfoFiltered.FirstOrDefault();
                    if (target != null)
                    {
                        ConstructorInfo info = handlerType.GetConstructors().FirstOrDefault();
                        MessageHandler messageHandler = (MessageHandler)info.Invoke(new object[] { this });
                        MessageHandlerBinding binding = new MessageHandlerBinding
                        {
                            objectTarget = messageHandler,
                            methodTarget = target
                        };
                        dictionary.Add(messageType, binding);
                    }
                }
            }
            return dictionary;
        }

        private Type ExtractTypeFromMessage(string messageAsString)
        {
            // all serialized messages are type "Message"
            // so first deserialize the string info a Message and check the messageType
            Message peekMessage = JsonConvert.DeserializeObject<Message>(messageAsString);
            string fullTargetTypeName =  _messageClassPrefix + peekMessage.MessageType + _messageClassSuffix;
            return Type.GetType(fullTargetTypeName);
        }

        private Message DeserializeMessage(string messageAsString)
        {
            Type targetType = ExtractTypeFromMessage(messageAsString);
            return (Message)JsonConvert.DeserializeObject(messageAsString, targetType);
        }

        static private List<Type> FindAssignableFrom(Type t)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
            .Where(x => t!=x && t.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract).ToList();
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StateFlux.Model;
using WebSocketSharp;
using WebSocketSharp.Net;

#if (UNITY_STANDALONE || UNITY_EDITOR)
using UnityEngine;
#endif

namespace StateFlux.Client
{
    public class StateFluxConnection
    {
        private PlayerClientInfo _currentPlayer;
        private WebSocket _webSocket;
        private Task _task;
        private bool ShouldExit { get; set; }
        private readonly ConcurrentQueue<Message> _requests = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<Message> _responses = new ConcurrentQueue<Message>();

        public string UserName { get; set; }
        public string RequestedUsername { get; set; }
        public string SessionSaveFilename { get; set; }
        public string Endpoint { get; set; }

        public WebSocketState ReadyState {
            get {
                lock(this) {
                    return _webSocket != null ? _webSocket.ReadyState : WebSocketState.Closed;
                }
            }
        }
        public PlayerClientInfo CurrentPlayer
        {
            get => _currentPlayer;
        }

        public bool SocketOpenWithIdentity
        {
            get
            {
                lock (this)
                {
                    return ReadyState == WebSocketState.Open && _currentPlayer != null;
                }
            }
        }

        public void SendRequest(Message message)
        {
            _requests.Enqueue(message);
        }

        public Message ReceiveResponse()
        {
            if(_responses.TryDequeue(out Message message))
            {
                return message;
            }
            return null;
        }

        public void Start()
        {
            _task = new Task(MainAction);
            _task.Start();
        }

        public void Stop()
        {
            lock(this)
            {
                ShouldExit = true;
            }
            Log("Stop called...");
        }

        private void MainAction()
        {
            while(!ShouldExit)
            {
                bool needsAuth = (_currentPlayer == null);

                if (needsAuth)
                {
                    if (HasSavedSession())
                    {
                        _currentPlayer = LoadSession();
                        UserName = _currentPlayer.Name;
                    }
                    else
                    {
                        Authenticate();
                    }
                }
                else
                {
                    ProcessMessages();
                }
            }
        }

        private void ProcessMessages()
        {
            Log($"Starting ProcessMessages as {_currentPlayer.Name} with sessionId = {_currentPlayer.SessionId}");

            WebSocketSharp.ErrorEventArgs errorEventArgs = null;
            try
            {
                lock(this)
                {
                    _webSocket = new WebSocket(Endpoint);
                    _webSocket.OnOpen += (object source, EventArgs e) =>
                    {
                        Log("Websocket.OnOpen");
                    };
                    _webSocket.OnMessage += ProcessMessagesHandler;
                    _webSocket.OnError += (object source, WebSocketSharp.ErrorEventArgs e) =>
                    {
                        errorEventArgs = e;
                        Log($"Websocket.OnError - {e.Message}");
                        if (e.Message.Contains("requires a user session"))
                        {
                            Log("websocket OnError - resetting saved session and reconnecting");
                            lock(this)
                            {
                                ResetSavedSession();
                                _webSocket.Close();
                                ShouldExit = true;
                            }
                        }
                    };
                    _webSocket.OnClose += (object source, CloseEventArgs e) =>
                    {
                        Log($"Websocket.OnClose - WasClean: {e.WasClean}, Reason: {e.Reason}, Code: {e.Code}");
                        lock (this)
                        {
                            if (!e.WasClean) ResetSavedSession();
                            ShouldExit = true;
                        }
                    };

                    _webSocket.SetCookie(new Cookie(MessageConstants.SessionCookieName, _currentPlayer.SessionId));
                    _webSocket.Connect();
                }
            }
            catch (Exception e)
            {
                ResetSavedSession();
                Log(e.Message);
                return;
            }

            Log("Waiting for socket to open...");
            int tries = 40;
            while (ReadyState != WebSocketState.Open && tries>0)
            {
                Thread.Sleep(25);
                tries--;
            }

            if(ReadyState != WebSocketState.Open)
                Log("Gave up waiting for socket to open...");
            else
            {
                Log("Socket open! Working from request queue...");
            }


            while (!ShouldExit && ReadyState == WebSocketState.Open)
            {

                try
                {
                    if (_requests.TryDequeue(out Message message))
                    {
                        string serializedMessage = JsonConvert.SerializeObject(message);
                        if (message.MessageType != MessageTypeNames.HostStateChange && message.MessageType != MessageTypeNames.GuestInputChange)
                        {
                            Log($"Sending message to server '{serializedMessage}'"); // enable for debug only
                        }
                        _webSocket.Send(serializedMessage);
                    }
                }
                catch (Exception e)
                {
                    Log(e.Message);
                }
            }
            Log("Stopped working from request queue...");

            try
            {
                if (ReadyState == WebSocketState.Open)
                {
                    Log("Closing an open socket");

                    lock(this)
                    {
                        _webSocket.Close();
                        _webSocket = null;
                    }
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
            }
            Log("Exiting ProcessMessages");
        }

        private void ProcessMessagesHandler(object source, MessageEventArgs evnt)
        {
            Message mappedMessage = null;
            try
            {
                string msgTxt = evnt.Data.ToString();
                Message responseMessage = JsonConvert.DeserializeObject<Message>(msgTxt);

                if (responseMessage.MessageType == MessageTypeNames.ChatSaid)
                {
                    mappedMessage = JsonConvert.DeserializeObject<ChatSaidMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.PlayerListing)
                {
                    mappedMessage = JsonConvert.DeserializeObject<PlayerListingMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GameInstanceCreated)
                {
                    Debug.Log("GameInstanceCreated message");
                    mappedMessage = JsonConvert.DeserializeObject<GameInstanceCreatedMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GameInstanceJoined)
                {
                    mappedMessage = JsonConvert.DeserializeObject<GameInstanceJoinedMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GameInstanceListing)
                {
                    mappedMessage = JsonConvert.DeserializeObject<GameInstanceListingMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GameInstanceStart)
                {
                    mappedMessage = JsonConvert.DeserializeObject<GameInstanceStartMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GameInstanceStopped)
                {
                    mappedMessage = JsonConvert.DeserializeObject<GameInstanceStoppedMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GameInstanceLeft)
                {
                    mappedMessage = JsonConvert.DeserializeObject<GameInstanceLeftMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GuestRequestFullState)
                {
                    mappedMessage = JsonConvert.DeserializeObject<GuestRequestFullStateMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.HostStateChanged)
                {
                    //Debug.Log($"Receiving HostStateChanged: {msgTxt}");
                    mappedMessage = JsonConvert.DeserializeObject<HostStateChangedMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.GuestInputChanged)
                {
                    //Debug.Log($"Receiving GuestInputChanged: {msgTxt}");
                    mappedMessage = JsonConvert.DeserializeObject<GuestInputChangedMessage>(msgTxt);
                }
                else if (responseMessage.MessageType == MessageTypeNames.ServerError)
                {
                    ServerErrorMessage error = JsonConvert.DeserializeObject<ServerErrorMessage>(msgTxt);
                    OnServerError(error);
                    mappedMessage = error;

                }
            }
            catch (Exception e)
            {
                ServerErrorMessage errorMessage = new ServerErrorMessage { Error = $"Failed to deserialize message. (server/client protocol mismatch?) {e}" };
                OnServerError(errorMessage);
                mappedMessage = errorMessage;
            }

            if (mappedMessage != null)
            {
                _responses.Enqueue(mappedMessage);
            }
        }

        private void OnServerError(ServerErrorMessage serverErrorMessage)
        {
            Log($"{DateTime.Now}: server error '{serverErrorMessage.Error}'");
            lock (this)
            {
                if (serverErrorMessage.Error.Contains("requires a user session") ||
                   serverErrorMessage.Error.Contains("Failed to deserialize") ||
                   serverErrorMessage.Error.Contains("unknown session cookie")
                   )
                {
                    Log($"{DateTime.Now}: resetting saved session due to error");
                    ResetSavedSession();
                    Log($"{DateTime.Now}: closing socket due to error");
                    if (_webSocket != null) _webSocket.Close();
                }
            }
        }

        private bool Authenticate()
        {
            try
            {
                lock(this)
                {
                    if(string.IsNullOrWhiteSpace(RequestedUsername))
                    {
                        return false;
                    }
                    _webSocket = new WebSocket(Endpoint);
                    _webSocket.OnMessage += HandleAuthResponseMessage;
                    _webSocket.Connect();
                }

                int tries = 40; // wait for 10 seconds
                while(ReadyState != WebSocketState.Open && tries > 0)
                {
                    Thread.Sleep(250);
                    tries--;
                }

                if (ReadyState != WebSocketState.Open)
                {
                    throw new Exception("Websocket Timeout");
                }

                AuthenticateMessage requestMessage = new AuthenticateMessage
                {
                    PlayerName = RequestedUsername
                };
                string msg = JsonConvert.SerializeObject(requestMessage);
                lock(this)
                {
                    if(_webSocket != null)
                    {
                        _webSocket.Send(msg);
                    }
                }

                // wait for the delegate function to set _currentPlayer
                tries = 10;
                while (_currentPlayer == null && tries > 0)
                {
                    Thread.Sleep(1000);
                    tries--;
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
                ResetSavedSession();
            }

            lock(this)
            {
                _webSocket.Close();
                _webSocket = null;
            }

            return (_currentPlayer != null);
        }

        private void HandleAuthResponseMessage(object source, MessageEventArgs e)
        {
            Message message = JsonConvert.DeserializeObject<Message>(e.Data.ToString());
            if(message.MessageType != MessageTypeNames.Authenticated)
            {
                // sometimes we can be sent a push message if the timing is right, so ignore it
                var err = $"unrecognized message preceeding authenticate response, msg = { e.Data.ToString()}";
                Log(err);
                return;
            }

            AuthenticatedMessage authenticated = JsonConvert.DeserializeObject<AuthenticatedMessage>(e.Data.ToString());
            if (authenticated.Status != AuthenticationStatus.Authenticated)
            {
                string err = $"Player login rejected ({authenticated.Status}), message= '{authenticated.StatusMessage}'";
                Log(err);
                ResetSavedSession();
                OnServerError(new ServerErrorMessage { Error = err });
                ShouldExit = true;
                throw new Exception(err);
            }

            _currentPlayer = new PlayerClientInfo
            {
                Name = authenticated.PlayerName,
                SessionId = authenticated.SessionId
            };
            UserName = _currentPlayer.Name;

            SaveSession();
            Log($"Player is authenticated as {_currentPlayer.Name} with sessionId = {_currentPlayer.SessionId}");
        }

        private void SaveSession()
        {
            Log($"Saving session to {SessionSaveFilename} as {JsonConvert.SerializeObject(_currentPlayer)}");
            File.WriteAllText(SessionSaveFilename, JsonConvert.SerializeObject(_currentPlayer, Formatting.Indented));
        }

        private bool HasSavedSession()
        {
            return File.Exists(SessionSaveFilename);
        }

        private PlayerClientInfo LoadSession()
        {
            string serializedSession = File.ReadAllText(SessionSaveFilename);
            Log($"Loading session from {SessionSaveFilename} as {serializedSession}");
            return JsonConvert.DeserializeObject<PlayerClientInfo>(serializedSession);
        }

        private void ResetSavedSession()
        {
            if(!string.IsNullOrEmpty(SessionSaveFilename)) File.Delete(SessionSaveFilename);
            _currentPlayer = null;
            UserName = "";
        }

        private void Log(string msg)
        {
#if (UNITY_EDITOR || UNITY_STANDALONE)
            Debug.Log(msg);
#else
            Console.WriteLine(msg);
#endif
        }
    }
}

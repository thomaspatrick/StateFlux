using UnityEngine;
using System;
using System.IO;
using StateFlux.Model;
using StateFlux.Client;
using System.Collections;
using System.Collections.Generic;
using System.Net.Configuration;

public class StateFluxClient : MonoBehaviour
{
    // --- Singleton ---
    private static StateFluxClient _instance;
    public static StateFluxClient Instance {
        get 
        {
            //if (_instance == null) throw new Exception("StateFluxClient accessed before ready");
            return _instance; 
        } 
    }

    private StateFluxConnection connection;

    [HideInInspector]
    public string userName;
    public StateFlux.Model.Color requestedPlayerColor;
    [HideInInspector]
    public bool connected;
    public bool openWithIdentity;
    public string endpoint;
    public string otherEndpoint;
    [HideInInspector]
    public bool hasSavedSession;
    [HideInInspector]
    public string clientId;
    [HideInInspector]
    public string sessionFile;
    [HideInInspector]
    public bool isInitialized;
    [HideInInspector]
    public bool isHosting;

    private readonly List<IStateFluxListener> listeners = new List<IStateFluxListener>();

    public void Start()
    {
        connected = false;
        isInitialized = false;
        //clientId = null;
        sessionFile = Application.persistentDataPath;
        if (Application.isEditor)
            sessionFile += "/currentplayer-editor.json";
        else {
            sessionFile += "/currentPlayer.json";
        }
        hasSavedSession = File.Exists(sessionFile);
        if (string.IsNullOrEmpty(endpoint)) endpoint = "ws://play.pixelkingsoftware.com/Service";
    }
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.Log("StateFluxClient blocking double instantiate");
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    private void OnDestroy()
    {
        Debug.Log("StateFluxClient OnDestroy");
        listeners.Clear();
    }

    public void AddListener(IStateFluxListener listener)
    {
        listeners.Add(listener);
    }

    public void RemoveListener(IStateFluxListener listener)
    {
        listeners.Remove(listener);
    }

    public void Initialize()
    {
        if (hasSavedSession && connection == null)
        {
            InitializeClient(null); // start StateFlux.Client with no username (uses saved session)
        }
    }

    public void Login()
    {
        ClearSavedSessionFile();
        InitializeClient(userName); // start StateFlux.Client with a username (performs authentication using the name)
    }

    private void InitializeClient(string userName)
    {
        if(connection != null)
        {
            connection.Shutdown();
        }

        connection = new StateFluxConnection
        {
            SessionSaveFilename = sessionFile,
            Endpoint = endpoint,
            RequestedUsername = userName,
            RequestedPlayerColor = this.requestedPlayerColor
        };

        connection.AuthAttemptEvent += OnAuthAttemptEvent;
        connection.AuthSuccessEvent += OnAuthSuccessEvent;
        connection.AuthFailureEvent += OnAuthFailureEvent;
        connection.AuthTimeoutEvent += OnAuthTimeoutEvent;
        connection.ConnectAttemptEvent += OnConnectAttemptEvent;
        connection.ConnectSuccessEvent += OnConnectSuccess;
        connection.ConnectFailureEvent += OnConnectFailureEvent;
        connection.ConnectTimeoutEvent += OnConnectTimeoutEvent;
        connection.Start();
        if (!isInitialized)
        {
            isInitialized = true;
            foreach (var listener in listeners) listener.OnStateFluxInitialize();
            StartCoroutine(ReceiveAndDispatchMessages());
        }
    }

    private void OnConnectSuccess(string playerName, string sessionId, string playerId)
    {
        clientId = playerId;
        Debug.Log($"OnConnectSuccessEvent,{playerName},{sessionId},{playerId}");
        Debug.Log($"Initializing id to be {clientId}");
    }

    private void OnAuthAttemptEvent(string username, string endpoint)
    {
        Debug.Log($"OnAuthAttemptEvent,{username},{endpoint}");
    }

    private void OnAuthSuccessEvent(string playerName, string sessionId, string playerId)
    {
        clientId = playerId;
        Debug.Log($"OnAuthSuccessEvent,{playerName},{sessionId},{playerId}");
        Debug.Log($"Initializing id to be {clientId}");
    }

    private void OnAuthFailureEvent(string message)
    {
        Debug.Log($"OnAuthFailureEvent,{message}");
    }

    private void OnAuthTimeoutEvent()
    {
        Debug.Log($"OnAuthTimeoutEvent");
    }
    private void OnConnectAttemptEvent(string username, string endpoint)
    {
        Debug.Log($"OnConnectAttemptEvent,{username},{endpoint}");
    }

    private void OnConnectFailureEvent(string message)
    {
        Debug.Log($"OnConnectFailureEvent,{message}");
    }

    private void OnConnectTimeoutEvent()
    {
        Debug.Log($"OnConnectTimeoutEvent");
    }


    public void Logout()
    {
        ClearSavedSessionFile();
        if (connection != null)
        {
            Debug.Log($"Shuttind down connection loop");
            connection.Shutdown();
        }
    }

    public void ClearSavedSessionFile()
    {
        Debug.Log($"Deleting session file {sessionFile}");
        File.Delete(sessionFile);
    }

    public void OnApplicationQuit()
    {
        StopAllCoroutines();
        if (connection != null) connection.Shutdown();
    }

    public void SendRequest(StateFlux.Model.Message message)
    {
        connection.SendRequest(message);
    }

    IEnumerator ReceiveAndDispatchMessages()
    {
        while (!connection.SocketOpenWithIdentity)
        {
            foreach (var listener in listeners) listener.OnStateFluxWaitingToConnect();
            yield return new WaitForSeconds(1);
        }

        while(true)
        {
            openWithIdentity = connection.SocketOpenWithIdentity;
            if(!connected && openWithIdentity)
            {
                connected = true;
                userName = connection.UserName;
                foreach (var listener in listeners) listener.OnStateFluxConnect();
            }
            else if(connected && !openWithIdentity)
            {
                connected = false;
                userName = "";
                foreach (var listener in listeners) listener.OnStateFluxDisconnect();
            }

            bool draining = true;
            int drainingCount = 50;
            while(draining && (drainingCount--) > 0)
            {
                StateFlux.Model.Message message = connection.ReceiveResponse();
                draining = (message != null);
                if (draining)
                {
                    if (message.MessageType == MessageTypeNames.HostStateChanged)
                    {
                        // FIXME: convert all the others to linq method ForEach
                        listeners.ForEach(l => l.OnStateFluxHostStateChanged((HostStateChangedMessage)message));
                    }
                    else if (message.MessageType == MessageTypeNames.MiceChanged)
                    {
                        MiceChangedMessage msg = (MiceChangedMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxMiceChanged(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.HostCommandChanged)
                    {
                        HostCommandChangedMessage msg = (HostCommandChangedMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxHostCommandChanged(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GuestCommandChanged)
                    {
                        GuestCommandChangedMessage msg = (GuestCommandChangedMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxGuestCommandChanged(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GuestInputChanged)
                    {
                        GuestInputChangedMessage msg = (GuestInputChangedMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxGuestInputChanged(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.PlayerListing)
                    {
                        PlayerListingMessage msg = (PlayerListingMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxPlayerListing(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GameInstanceCreated)
                    {
                        GameInstanceCreatedMessage msg = (GameInstanceCreatedMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxGameInstanceCreated(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GameInstanceListing)
                    {
                        GameInstanceListingMessage msg = (GameInstanceListingMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxGameInstanceListing(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GameInstanceJoined)
                    {
                        GameInstanceJoinedMessage msg = (GameInstanceJoinedMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxGameInstanceJoined(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GameInstanceStart)
                    {
                        GameInstanceStartMessage msg = (GameInstanceStartMessage)message;
                        Debug.Log($"Game start message, host is {msg.Host.Name}");
                        Debug.Log($"Current player name is {connection.CurrentPlayer.Name}");
                        isHosting = (msg.Host.Name == connection.CurrentPlayer.Name);
                        foreach (var listener in listeners) listener.OnStateFluxGameInstanceStart(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GameInstanceStopped)
                    {
                        GameInstanceStoppedMessage msg = (GameInstanceStoppedMessage)message;
                        Debug.Log($"Game stopped message, host is {msg.Host.Name}");
                        Debug.Log($"Current player name is {connection.CurrentPlayer.Name}");
                        isHosting = (msg.Host.Name == connection.CurrentPlayer.Name);
                        foreach (var listener in listeners) listener.OnStateFluxGameInstanceStopped(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GameInstanceLeft)
                    {
                        GameInstanceLeftMessage msg = (GameInstanceLeftMessage)message;
                        Debug.Log($"Player {msg.Player.Name} left {msg.GameName}:{msg.InstanceName}");
                        Debug.Log($"Current player name is {connection.CurrentPlayer.Name}");
                        foreach (var listener in listeners) listener.OnStateFluxGameInstanceLeft(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.ChatSaid)
                    {
                        ChatSaidMessage msg = (ChatSaidMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxChatSaid(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.ServerError)
                    {
                        ServerErrorMessage msg = (ServerErrorMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxServerError(msg);
                    }
                    else
                    {
                        foreach (var listener in listeners) listener.OnStateFluxOtherMessage(message);
                    }
                }
            }

            yield return null;
        }
    }
}

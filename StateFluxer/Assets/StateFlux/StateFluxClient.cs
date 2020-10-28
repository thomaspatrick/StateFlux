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
            if (_instance == null) throw new Exception("StateFluxClient accessed before ready");
            return _instance; 
        } 
    }

    private StateFluxConnection connection;

    [HideInInspector]
    public string userName;
    [HideInInspector]
    public bool connected;
    public bool openWithIdentity;
    public string endpoint;
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
        clientId = Guid.NewGuid().ToString();
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
            Debug.Log("StateFluxClient is Awake");
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
            Debug.Log("StateFluxClient Initializing");
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
            connection.Stop();
        }

        connection = new StateFluxConnection
        {
            SessionSaveFilename = sessionFile,
            Endpoint = endpoint,
            RequestedUsername = userName
        };
        connection.Start();
        if (!isInitialized)
        {
            isInitialized = true;
            foreach (var listener in listeners) listener.OnStateFluxInitialize();
            StartCoroutine(ReceiveAndDispatchMessages());
        }
    }

    public void Logout()
    {
        if(connection != null) connection.Stop();
        ClearSavedSessionFile();
    }

    public void ClearSavedSessionFile()
    {
        Debug.Log($"Deleting session file {sessionFile}");
        File.Delete(sessionFile);
    }

    public void OnApplicationQuit()
    {
        StopAllCoroutines();
        if (connection != null) connection.Stop();
    }

    public void SendRequest(StateFlux.Model.Message message)
    {
        connection.SendRequest(message);
    }

    IEnumerator ReceiveAndDispatchMessages()
    {
        while (!connection.SocketOpenWithIdentity)
        {
            Debug.Log("Waiting for a connection");
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

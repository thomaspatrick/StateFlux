using UnityEngine;
using System;
using System.IO;
using StateFlux.Model;
using StateFlux.Client;
using System.Collections;
using System.Collections.Generic;

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

    private Client client;

    [HideInInspector]
    public string userName;
    [HideInInspector]
    public bool connected;
    public string endpoint;
    [HideInInspector]
    public bool hasSavedSession;
    [HideInInspector]
    public string clientId;
    [HideInInspector]
    public string sessionFile;
    [HideInInspector]
    public bool isInitialized;

    private List<IStateFluxListener> listeners = new List<IStateFluxListener>();

    public void Start()
    {
        connected = false;
        isInitialized = false;
        clientId = Guid.NewGuid().ToString();
        sessionFile = Application.persistentDataPath;
        if (Application.isEditor)
            sessionFile += "\\currentplayer-editor.json";
        else {
            sessionFile += "\\currentPlayer.json";
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
        if (hasSavedSession && client == null)
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

    private void InitializeClient(string userName) // 
    {
        if(client != null)
        {
            client.Stop();
        }

        client = new Client
        {
            SessionSaveFilename = sessionFile,
            Endpoint = endpoint,
            RequestedUsername = userName
        };
        client.Start();
        if (!isInitialized)
        {
            isInitialized = true;
            foreach (var listener in listeners) listener.OnStateFluxInitialize();
                StartCoroutine(ReceiveAndDispatchMessages());
        }
    }

    public void Logout()
    {
        if(client != null) client.Stop();
        ClearSavedSessionFile();
    }

    public void ClearSavedSessionFile()
    {
        File.Delete(sessionFile);
    }

    public void OnApplicationQuit()
    {
        StopAllCoroutines();
        if (client != null) client.Stop();
    }

    public void SendRequest(StateFlux.Model.Message message)
    {
        client.SendRequest(message);
    }

    IEnumerator ReceiveAndDispatchMessages()
    {
        float sec = Time.time;
        while (!client.SocketOpenWithIdentity)
        {
            Debug.Log("Waiting for a connection");
            foreach (var listener in listeners) listener.OnStateFluxWaitingToConnect();
            yield return new WaitForSeconds(1);
        }

        while(true)
        {
            bool openWithIdentity = client.SocketOpenWithIdentity;
            if(!connected && openWithIdentity)
            {
                connected = true;
                userName = client.UserName;
                foreach (var listener in listeners) listener.OnStateFluxConnect();
            }
            else if(connected && !openWithIdentity)
            {
                connected = false;
                userName = "";
                foreach (var listener in listeners) listener.OnStateFluxDisconnect();
            }

            StateFlux.Model.Message message = null;
            bool draining = true;
            while(draining)
            {
                message = client.ReceiveResponse();
                draining = (message != null);
                if (draining)
                {
                    if (message.MessageType == MessageTypeNames.StateChanged)
                    {
                        StateChangedMessage msg = (StateChangedMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxStateChanged(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.PlayerListing)
                    {
                        PlayerListingMessage msg = (PlayerListingMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxPlayerListing(msg);
                    }
                    else if (message.MessageType == MessageTypeNames.GameInstanceListing)
                    {
                        GameInstanceListingMessage msg = (GameInstanceListingMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxGameInstanceListing(msg);
                    }
                    else if(message.MessageType == MessageTypeNames.ChatSaid)
                    {
                        ChatSaidMessage msg = (ChatSaidMessage)message;
                        foreach (var listener in listeners) listener.OnStateFluxChatSaid(msg);
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

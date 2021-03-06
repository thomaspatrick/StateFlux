﻿using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using StateFlux.Model;
using StateFlux.Client;
using System.Text;
using System.Linq;
using UnityEngine.SceneManagement;
using System;
using StateFlux.Unity;

public class LobbyManager : MonoBehaviour, IStateFluxListener
{
    // --- Singleton ---
    private static LobbyManager _instance;
    public static LobbyManager Instance { get { return _instance; } }

    private void Awake()
    {
        DebugLog("Lobby Awake");

        if (StateFluxClient.Instance == null)
        {
            SceneManager.LoadScene("_preload");
            return;
        }

        if (_instance != null && _instance != this)
        {
            StartCoroutine(Initialize());
//            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
            StartCoroutine(Initialize());
        }
    }
   
    // --- -------- ---

    public GameObject playerRowPrefab;
    public GameObject gameRowPrefab;

    [HideInInspector]
    public bool Initialized;
    private List<Player> _players;
    private List<GameInstance> _games;
    private List<ChatSaidMessage> _said;
    private string _lastUsernameSaveFile;
    private GameObject _login;
    private GameObject _lobby;
    private GameObject _connection;
    private GameObject _userNameInputField;
    private GameObject _userNameInputText;
    private GameObject _userNameInputPlaceholder;
    private GameObject _chatField;
    private GameObject _chatScrollView;
    private GameObject _newGamePanel;
    private GameObject _creatingGamePanel;
    private GameObject _errorPanel;
    private GameObject _modalBackgroundPanel;
    private GameObject _debugPanel;
    private GameObject _playersListView;
    private GameObject _gameInstanceListView;
    private GameObject _joinGamePanel;
    private GameObject _joinGamePanelText;
    private GameObject _leaveGamePanel;
    private GameObject _leaveGamePanelText;
    private GameObject _gamesPanelContent;
    private GameObject _playersPanelContent;
    private GameObject _loginButton;
    private GameInstance _maybeJoinThisGameInstance;
    private GameObject _currentShowingPanel;
    private bool _hostingGame;

    public Player hostPlayer = null;
    public Dictionary<string, Player> players = new Dictionary<string, Player>();


    public string LastUsername
    {
        get 
        {
            string tmp = null;
            if (File.Exists(_lastUsernameSaveFile))
            {
                tmp = File.ReadAllText(_lastUsernameSaveFile);
                //DebugLog($"read '{tmp}' from {_lastUsernameSaveFile}");
            }
            else
            {
                DebugLog($"{_lastUsernameSaveFile} does not exist");
            }
            return tmp;
        }
        set
        {
            DebugLog($"write '{value}' to {_lastUsernameSaveFile}");
            File.WriteAllText(_lastUsernameSaveFile, value);
        }
    }

    public IEnumerator Initialize()
    {
        yield return new WaitForEndOfFrame();
        _players = new List<Player>();
        _games = new List<GameInstance>();
        _said = new List<ChatSaidMessage>();
        _lastUsernameSaveFile = Application.persistentDataPath + (Application.isEditor ? "/lastUsername-editor.txt" : "/lastUsername.txt");
        FindGameObjects();
        SceneManager.activeSceneChanged += ChangedActiveScene;

        StateFluxClient client = StateFluxClient.Instance;
        client.AddListener(this);
        StateFluxClient.Instance.Initialize();

        StartCoroutine(StateFluxClient.Instance.hasSavedSession ? ActivateLobbyPanel() : ActivateLoginPanel());
        Initialized = true;
    }

    private void ChangedActiveScene(Scene current, Scene next)
    {
        //DebugLog($"Changed scene to {next.name}");

        //GameObject gameCam = GameObject.Find("Game Camera");
        //GameObject lobbyCam = GameObject.Find("Lobby Camera");

        //if (next.name == "LobbyScene")
        //{
        //    gameCam?.SetActive(false);
        //    lobbyCam?.SetActive(true);
        //    Initialize();
        //    ShowPanel(_lobby, null, true);
        //}
        //if (next.name == "DemoGame")
        //{
        //    gameCam?.SetActive(true);
        //    lobbyCam?.SetActive(false);
        //}

        //DebugLog($"Changed scene from {current.name} to {next.name}");
    }

    public void OnSceneAwake()
    {
        DebugLog("OnSceneAwake");
        GameObject gameCam = GameObject.Find("Game Camera");
        GameObject lobbyCam = GameObject.Find("Lobby Camera");

        gameCam?.SetActive(false);
        lobbyCam?.SetActive(true);
        Initialize();
        ShowPanel(_lobby, null, true);
    }

    private void FindGameObjects()
    {
        _login = GameObject.Find("Login Panel");
        _lobby = GameObject.Find("Lobby Panel");
        _connection = GameObject.Find("ConnectionPanel");
        _userNameInputField = GameObject.Find("InputField");
        _userNameInputText = GameObject.Find("InputField/Text");
        _userNameInputPlaceholder = GameObject.Find("InputField/Placeholder");
        _chatScrollView = GameObject.Find("Chat/Scroll View");
        _chatField = GameObject.Find("Content/Text");
        _newGamePanel = GameObject.Find("New Game Panel");
        _creatingGamePanel = GameObject.Find("Creating Game Panel");
        _errorPanel = GameObject.Find("ErrorPanel");
        _modalBackgroundPanel = GameObject.Find("ModalBackdrop");
        _joinGamePanel = GameObject.Find("Join Game Panel");
        _joinGamePanelText = GameObject.Find("Join Game Panel/Text");
        _leaveGamePanel = GameObject.Find("Leave Game Panel");
        _leaveGamePanelText = GameObject.Find("Leave Game Panel/Text");
        _gamesPanelContent = GameObject.Find("Lobby Panel/GamesPanel/Games/Scroll View/Viewport/Content");
        _playersPanelContent = GameObject.Find("Lobby Panel/PlayersPanel/Players/Scroll View/Viewport/Content");
        _debugPanel = GameObject.Find("DebugPanel");
        _playersListView = GameObject.Find("Lobby Panel/PlayersPanel/Players/Scroll View/Viewport/Content");
        _gameInstanceListView = GameObject.Find("Lobby Panel/GamesPanel/Games/Scroll View/Viewport/Content");
        _loginButton = GameObject.Find("LoginButton");
    }

    public void Update()
    {
        if (Initialized && _userNameInputField != null && Input.GetKey(KeyCode.Return))
        {
            var field = _userNameInputField.GetComponent<InputField>();
            if (field.isFocused && field.text != "")
            {
                OnClickToConnect();
            }
        }
    }

    // ------------------------------------
    // controlling the ui

    IEnumerator ActivateConnectionPanel()
    {
        yield return null;
        ShowPanel(_connection, _modalBackgroundPanel, true);
    }

    IEnumerator HideConnectionPanel()
    {
        yield return null;
        ShowPanel(_connection, _modalBackgroundPanel, false);
    }

    IEnumerator ActivateLobbyPanel()
    {
        yield return null;
        ShowPanel(_lobby, _modalBackgroundPanel, true);
        ShowPanel(_login, _modalBackgroundPanel, false);
    }

    IEnumerator ActivateLoginPanel()
    {
        yield return null;
        ShowPanel(_lobby, _modalBackgroundPanel, false);
        ShowPanel(_connection, _modalBackgroundPanel, false);
        ShowPanel(_login, null, true);
        var field = _userNameInputField.GetComponent<InputField>();
        field.text = LastUsername;
        EventSystem.current.SetSelectedGameObject(_userNameInputField);
        field.ActivateInputField();
    }

    void ShowPanel(GameObject obj, GameObject backdrop, bool show)
    {
        //DebugLog($"{(show ? "Showing" : "Hiding")} panel {obj.name}");
        _currentShowingPanel = show ? obj : null;

        var canvasGroup = (obj != null) ? obj.GetComponent<CanvasGroup>() : null;
        if(canvasGroup != null)
        {
            canvasGroup.alpha = show ? 1 : 0;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
        }
        
        if(backdrop != null)
        {
            canvasGroup = (backdrop != null) ? backdrop.GetComponent<CanvasGroup>() : null;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = show ? 1 : 0;
                canvasGroup.interactable = show;
                canvasGroup.blocksRaycasts = show;
            }
        }
    }

    private void ClearPlayerListView()
    {
        _players.Clear();
        if (_playersListView == null) return;
        foreach (Transform child in _playersListView.transform) GameObject.Destroy(child.gameObject);
    }

    private void ClearGameInstanceListView()
    {
        _games.Clear();
        if (_gameInstanceListView == null) return;
        foreach (Transform child in _gameInstanceListView.transform) GameObject.Destroy(child.gameObject);
    }
    private void ClearChatListView()
    {
        _said.Clear();
        _chatField.GetComponent<TextMeshProUGUI>().text = "";
    }


    // --------------------------------------------------
    // UI message handlers

    public void OnClickToConnect()
    {
        LastUsername = StateFluxClient.Instance.userName = GetLoginUsername();
        StateFluxClient.Instance.requestedPlayerColor = ColorSequence.Next();
        if(String.IsNullOrWhiteSpace(LastUsername))
        {
            DebugLog($"LobbyManager.OnClickToConnect no username!");
        }
        else
        {
            DebugLog($"LobbyManager.OnClickToConnect as {LastUsername}");
            StateFluxClient.Instance.Login();
        }
    }

    public void OnClickToCreateGame()
    {
        ShowPanel(_newGamePanel, _modalBackgroundPanel, true);
        InputField inputField = _newGamePanel.GetComponentInChildren<InputField>();
        if (inputField != null)
        {
            DebugLog("found input field");
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    public void OnClickConfirmCreateGame()
    {
        ShowPanel(_newGamePanel, _modalBackgroundPanel, false);
        ShowPanel(_creatingGamePanel, _modalBackgroundPanel, true);
        InputField inputField = _newGamePanel.GetComponentInChildren<InputField>();
        if (inputField != null)
        {
            DebugLog("found input field");
            inputField.Select();
            inputField.ActivateInputField();
        }
        var message = new CreateGameInstanceMessage
        {
            GameName = "Stellendency",
            InstanceName = inputField.text,
            MaxPlayers = 3,
            MinPlayers = 2
        };
        StateFluxClient.Instance.SendRequest(message);
    }

    public void OnClickToLogout()
    {
        DebugLog($"LobbyManager.OnClickToLogout");

        StateFluxClient.Instance.Logout();
        ClearPlayerListView();
        ClearChatListView();
        ClearGameInstanceListView();
        StartCoroutine(ActivateLoginPanel());
    }

    public void OnChatInputSubmit(string value)
    {
        StateFluxClient.Instance.SendRequest(new ChatSayMessage { Say = value });
    }


    public void OnUsernameChanged(string newValue)
    {
        _loginButton.GetComponent<Button>().interactable = !string.IsNullOrEmpty(newValue);
    }

    public void OnClickedModalBackdrop()
    {
        DebugLog("clicked modal backdrop!");
        if(_currentShowingPanel)
        {
            if(_currentShowingPanel.name != "Login Panel")
                ShowPanel(_currentShowingPanel, _modalBackgroundPanel, false);
        }
    }

    public string GetLoginUsername()
    {
        string placeholderText = _userNameInputPlaceholder.GetComponent<Text>().text;
        //DebugLog($"PlaceholderText = {placeholderText}");
        string userNameText = _userNameInputText.GetComponent<Text>().text;
        //DebugLog($"UserNameText = {userNameText}");
        return (userNameText == placeholderText) ? null : userNameText;
    }

    // --------------------------------------------------
    // StateFluxListener interface methods

    public void OnStateFluxInitialize()
    {
        DebugLog("OnStateFluxInitialize");
    }

    public void OnStateFluxWaitingToConnect()
    {
        DebugLog("OnStateFluxWaitingToConnect");
        StartCoroutine(ActivateConnectionPanel());
    }

    public void OnStateFluxConnect()
    {
        DebugLog("OnStateFluxConnect!");
        StartCoroutine(OnStateFluxConnectCoroutine());
    }

    public IEnumerator OnStateFluxConnectCoroutine()
    {
        yield return new WaitForEndOfFrame();
        yield return ActivateLobbyPanel();
        yield return HideConnectionPanel();
        StateFluxClient.Instance.SendRequest(new PlayerListMessage());
        StateFluxClient.Instance.SendRequest(new GameInstanceListMessage());
    }

    public void OnStateFluxDisconnect()
    {
        DebugLog("OnStateFluxDisconnect!");
        
        //StartCoroutine(ActivateConnectionPanel());
    }

    public void OnStateFluxPlayerListing(PlayerListingMessage message)
    {
        ClearPlayerListView();
        if (_playersPanelContent == null) return;
        foreach (Player p in message.Players)
        {
            _players.Add(p);
            GameObject row = GameObject.Instantiate(playerRowPrefab, _playersPanelContent.transform);
            var textMeshPro = row.GetComponentInChildren<TextMeshProUGUI>();
            textMeshPro.text = p.Name;
            if(p.Color != null)
            {
                textMeshPro.color = StateFluxTypeConvert.Convert(p.Color);
            }
        }
    }

    public void OnStateFluxChatSaid(ChatSaidMessage message)
    {
        _said.Add(message);
        string userName = StateFluxClient.Instance.userName;
        var chatText = _chatField.GetComponent<TextMeshProUGUI>();
        StringBuilder builder = new StringBuilder();
        builder.Append("<color=#339933>");
        foreach (var msg in _said)
        {
            if(msg.PlayerName == userName)
            {
                builder.Append("<color=#996633>");
            }
            builder.AppendLine($"<b>{msg.PlayerName}:</b>  {msg.Say}");
            if (msg.PlayerName == userName)
            {
                builder.Append("</color>");
            }
        }
        builder.Append("</color>");
        chatText.SetText(builder.ToString());
        _chatScrollView.GetComponent<ScrollRect>().normalizedPosition = new Vector2(0, 0); // scroll to bottom
    }

    public void OnStateFluxGameInstanceCreated(GameInstanceCreatedMessage message)
    {
        DebugLog($"Server says {message.GameInstance.HostPlayer.Name} began hosting a new game instance: {message.GameInstance.Name}.  (I am {this.LastUsername})");
        if (message.GameInstance.HostPlayer.Name == LastUsername )
        {
            DebugLog($"Current user is hosting a game");
            _hostingGame = true;
        }
    }
    public void OnStateFluxGameInstanceLeft(GameInstanceLeftMessage message)
    {
        DebugLog($"Player {message.Player.Name} left {message.GameName}:{message.InstanceName}");
        if(message.Player.Name == LastUsername)
        {
            // we are no longer hosting a game, because we left it
            _hostingGame = false;
        }
    }

    public void OnStateFluxGameInstanceListing(GameInstanceListingMessage message)
    {
        if(_hostingGame)
        {
            ShowPanel(_creatingGamePanel, _modalBackgroundPanel, false);
        }

        ClearGameInstanceListView();
        if (_gamesPanelContent == null) return;
        foreach (GameInstance g in message.GameInstances)
        {
            bool first = true;
            StringBuilder builder = new StringBuilder();
            builder.Append($"<b>{g.Name}</b>({g.Game.Name})\t\t<i><size=-4>({g.State})</size></i>\n Players: ");
            foreach(Player p in g.Players)
            {
                if (!first) builder.Append(",");
                first = false;
                bool host = p.Name == g.HostPlayer.Name;
                if(host)
                {
                    builder.Append("<i>");
                }
                builder.Append(p.Name);
                if(host)
                {
                    builder.Append("</i>");
                }
            }
            _games.Add(g);
            GameObject row = GameObject.Instantiate(gameRowPrefab, _gamesPanelContent.transform);
            var button = row.GetComponent<Button>();
            button.name = "GameInstance." + g.Id;
            button.onClick.AddListener(delegate { OnClickGameInstance(button.name); });
            var textMeshPro = row.GetComponentInChildren<TextMeshProUGUI>();
            textMeshPro.text = builder.ToString();
        }
    }

    public void OnStateFluxGameInstanceJoined(GameInstanceJoinedMessage message)
    {
        DebugLog("OnStateFluxGameInstanceJoined!");
    }

    public void OnStateFluxGameInstanceStart(GameInstanceStartMessage message)
    {
        DebugLog("OnStateFluxGameInstanceStart!");
        players.Clear();
        hostPlayer = message.Host;
        players[message.Host.Id] = message.Host;
        foreach (Player p in message.Guests)
        {
            players[p.Id] = p;
        }
        if (message.GameInstance.GameName == "Stellendency")
        {
            ShowPanel(GameObject.Find("Canvas"), null, false);
            SceneManager.LoadScene("DemoGame");
        }
        else
        {
            DebugLog($"Started unknown game instance: {message.GameInstance.GameName}:{message.GameInstance.Name}");
        }
    }

    public void OnClickGameInstance(string buttonName)
    {
        GameObject panel = _hostingGame ? _leaveGamePanel : _joinGamePanel;
        GameObject panelText = _hostingGame ? _leaveGamePanelText : _joinGamePanelText;
        string action = _hostingGame ? "Leave" : "Join";

        string[] parts = buttonName.Split(new char[] { '.' });
        string gameInstanceId = parts[1];
        GameInstance gameInstance = _games.FirstOrDefault(g => g.Id.ToString() == gameInstanceId);
        ShowPanel(panel, null, true);
        var txt = panelText.GetComponent<Text>();
        txt.text = $"{action} {gameInstance.Name}?";
        _maybeJoinThisGameInstance = gameInstance;
    }

    public void OnClickJoinGame()
    {
        if(!_hostingGame)
        {
            ShowPanel(_joinGamePanel, null, false);
            DebugLog($"Requested join game instance {_maybeJoinThisGameInstance.Name}");
            StateFluxClient.Instance.SendRequest(new JoinGameInstanceMessage { GameName = _maybeJoinThisGameInstance.Game.Name, InstanceName = _maybeJoinThisGameInstance.Name });
        }
        else
        {
            DebugLog("Join game request supressed because you are hosting a game");
        }
    }

    public void OnClickDismissJoinGame()
    {
        ShowPanel(_joinGamePanel, null, false);
    }

    public void OnClickLeaveGame()
    {
        DebugLog("OnClickLeaveGame");
        StateFluxClient.Instance.SendRequest(new LeaveGameInstanceMessage { GameName = _maybeJoinThisGameInstance.Game.Name, InstanceName = _maybeJoinThisGameInstance.Name });
        StateFluxClient.Instance.SendRequest(new GameInstanceListMessage());
        _hostingGame = false;

        OnClickDismissLeaveGame();
    }
    public void OnClickDismissLeaveGame()
    {
        ShowPanel(_leaveGamePanel, null, false);
    }

    public void OnStateFluxServerError(ServerErrorMessage message)
    {
        DebugLog($"OnStateFluxServerError - {message.Error}!");

        StartCoroutine(nameof(ActivateLoginPanel));

        if(!message.Error.Contains("requires a user session"))
            _errorPanel.SendMessage("OnStateFluxError", message.Error);
    }

    private void DebugLog(string msg)
    {
        Debug.Log(msg);
        if(_debugPanel)
        {
            var textComponent = _debugPanel.GetComponentInChildren<UnityEngine.UI.Text>();
            if (textComponent.text.Length > 1000) textComponent.text = textComponent.text.Substring(1000,textComponent.text.Length - 1000);
            textComponent.text = msg + "\n" + textComponent.text;
        }
    }

    public void OnStateFluxGameInstanceStopped(GameInstanceStoppedMessage message)
    {
        //  ShowPanel(GameObject.Find("Canvas"), null, true);
    }

    public void OnStateFluxHostCommandChanged(HostCommandChangedMessage message)
    {
    }

    public void OnStateFluxGuestCommandChanged(GuestCommandChangedMessage message)
    {
    }

    public void OnStateFluxMiceChanged(MiceChangedMessage msg)
    {
    }

    public void OnStateFluxHostStateChanged(HostStateChangedMessage message)
    {
    }

    public void OnStateFluxGuestInputChanged(GuestInputChangedMessage message)
    {
    }

    public void OnStateFluxOtherMessage(Message message)
    {
        DebugLog($"OnStateFluxOtherMessage - {message.MessageType}!");
    }
}

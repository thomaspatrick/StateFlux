using System.IO;
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

public class LobbyManager : MonoBehaviour, IStateFluxListener
{
    // --- Singleton ---
    private static LobbyManager _instance;
    public static LobbyManager Instance { get { return _instance; } }

    private void Awake()
    {

        if (StateFluxClient.Instance == null)
        {
            SceneManager.LoadScene("_preload");
            return;
        }

        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            //DontDestroyOnLoad(this.gameObject);
            StartCoroutine(Initialize());
        }
    }
    // --- -------- ---

    public GameObject playerRowPrefab;
    public GameObject gameRowPrefab;
    public GameObject login;
    public GameObject lobby;
    public GameObject connection;

    [HideInInspector]
    public bool Initialized;
    private List<Player> _players;
    private List<GameInstance> _games;
    private List<ChatSaidMessage> _said;
    private string _lastUsernameSaveFile;
    private GameObject _userNameInputField;
    private GameObject _userNameInputText;
    private GameObject _userNameInputPlaceholder;
    private GameObject _chatField;
    private GameObject _chatScrollView;
    private GameObject _newGamePanel;
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

        StateFluxClient.Instance.AddListener(this);
        StateFluxClient.Instance.Initialize();

        StartCoroutine(StateFluxClient.Instance.hasSavedSession ? ActivateLobbyPanel() : ActivateLoginPanel());
        Initialized = true;
    }

    private void ChangedActiveScene(Scene current, Scene next)
    {
        DebugLog($"Changed scene from {current.name} to {next.name}");
    }

    private void FindGameObjects()
    {
        _userNameInputField = GameObject.Find("InputField");
        _userNameInputText = GameObject.Find("InputField/Text");
        _userNameInputPlaceholder = GameObject.Find("InputField/Placeholder");
        _chatScrollView = GameObject.Find("Chat/Scroll View");
        _chatField = GameObject.Find("Content/Text");
        _newGamePanel = GameObject.Find("New Game Panel");
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
        ShowPanel(connection, _modalBackgroundPanel, true);
    }

    IEnumerator HideConnectionPanel()
    {
        yield return null;
        ShowPanel(connection, _modalBackgroundPanel, false);
    }

    IEnumerator ActivateLobbyPanel()
    {
        yield return null;
        ShowPanel(lobby, _modalBackgroundPanel, true);
        ShowPanel(login, _modalBackgroundPanel, false);
    }

    IEnumerator ActivateLoginPanel()
    {
        yield return null;
        ShowPanel(lobby, _modalBackgroundPanel, false);
        ShowPanel(connection, _modalBackgroundPanel, false);
        ShowPanel(login, null, true);
        var field = _userNameInputField.GetComponent<InputField>();
        if (!string.IsNullOrEmpty(LastUsername)) field.text = LastUsername;
        else
        {
            LastUsername = "NameError" + new System.Random().Next();
            field.text = LastUsername;
        }
        EventSystem.current.SetSelectedGameObject(_userNameInputField);
        field.ActivateInputField();
    }

    void ShowPanel(GameObject obj, GameObject backdrop, bool show)
    {
        //DebugLog($"{(show ? "Showing" : "Hiding")} panel {obj.name}");
        _currentShowingPanel = show ? obj : null;

        var canvasGroup = obj.GetComponent<CanvasGroup>();
        if(canvasGroup != null)
        {
            canvasGroup.alpha = show ? 1 : 0;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
        }
        
        if(backdrop != null)
        {
            canvasGroup = backdrop.GetComponent<CanvasGroup>();
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
        DebugLog($"LobbyManager.OnClickToConnect as {LastUsername}");
        StateFluxClient.Instance.Login();
    }

    public void OnClickToCreateGame()
    {
        ShowPanel(_newGamePanel, _modalBackgroundPanel, true);
    }

    public void OnClickConfirmCreateGame()
    {
        ShowPanel(_newGamePanel, _modalBackgroundPanel, false);
        InputField inputField = _newGamePanel.GetComponentInChildren<InputField>();
        var message = new CreateGameInstanceMessage
        {
            GameName = "AssetCollapse",
            InstanceName = inputField.text
            
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
        //DebugLog("OnStateFluxWaitingToConnect");
        StartCoroutine(ActivateConnectionPanel());
    }

    public void OnStateFluxConnect()
    {
        DebugLog("OnStateFluxConnect!");
        StartCoroutine(ActivateLobbyPanel());
        StartCoroutine(HideConnectionPanel());
        StateFluxClient.Instance.SendRequest(new PlayerListMessage());
        StateFluxClient.Instance.SendRequest(new GameInstanceListMessage());
    }

    public void OnStateFluxDisconnect()
    {
        DebugLog("OnStateFluxDisconnect!");
        StartCoroutine(ActivateConnectionPanel());
    }

    public void OnStateFluxHostStateChanged(HostStateChangedMessage message)
    {
    }

    public void OnStateFluxGuestInputChanged(GuestInputChangedMessage message)
    {
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
        if (message.GameInstance.HostPlayer.Name == LastUsername)
        {
            DebugLog($"Current user is hosting a game");
            _hostingGame = true;
        }
    }
    public void OnStateFluxGameInstanceLeft(GameInstanceLeftMessage message)
    {
        DebugLog($"Player {message.Player.Name} left {message.GameName}:{message.InstanceName}");
    }

    public void OnStateFluxGameInstanceListing(GameInstanceListingMessage message)
    {
        ClearGameInstanceListView();
        if (_gamesPanelContent == null) return;
        foreach (GameInstance g in message.GameInstances)
        {
            bool first = true;
            StringBuilder builder = new StringBuilder();
            builder.Append($"<b>{g.Name}</b>\t\t<i><size=-14>({g.State})</size></i>\n Players: ");
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
        if (message.GameInstance.GameName == "AssetCollapse")
            SceneManager.LoadScene("PlaceholderGame");
        else
        {

        }
        SceneManager.LoadScene("DemoTile");
    }

    public void OnClickGameInstance(string buttonName)
    {
        if(!_hostingGame)
        {
            string[] parts = buttonName.Split(new char[] { '.' });
            string gameInstanceId = parts[1];
            GameInstance gameInstance = _games.FirstOrDefault(g => g.Id.ToString() == gameInstanceId);
            var canvasGroup = _joinGamePanel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            var txt = _joinGamePanelText.GetComponent<Text>();
            txt.text = $"Join {gameInstance.Name} hosted by {gameInstance.HostPlayer.Name}?";
            _maybeJoinThisGameInstance = gameInstance;
        }
        else
        {
            string[] parts = buttonName.Split(new char[] { '.' });
            string gameInstanceId = parts[1];
            GameInstance gameInstance = _games.FirstOrDefault(g => g.Id.ToString() == gameInstanceId);
            var canvasGroup = _leaveGamePanel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            var txt = _leaveGamePanelText.GetComponent<Text>();
            txt.text = $"Leave {gameInstance.Name} hosted by {gameInstance.HostPlayer.Name}?";
            _maybeJoinThisGameInstance = gameInstance;
        }
    }

    public void OnClickJoinGame()
    {
        if(!_hostingGame)
        {
            var canvasGroup = _joinGamePanel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
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
        var canvasGroup = _joinGamePanel.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnClickLeaveGame()
    {
        StateFluxClient.Instance.SendRequest(new LeaveGameInstanceMessage { GameName = _maybeJoinThisGameInstance.Game.Name, InstanceName = _maybeJoinThisGameInstance.Name });
        OnClickDismissLeaveGame();
    }
    public void OnClickDismissLeaveGame()
    {
        var canvasGroup = _leaveGamePanel.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnStateFluxOtherMessage(Message message)
    {
        DebugLog($"OnStateFluxOtherMessage - {message.MessageType}!");
    }

    public void OnStateFluxServerError(ServerErrorMessage message)
    {
        DebugLog($"OnStateFluxServerError - {message.Error}!");

        StartCoroutine(nameof(ActivateLoginPanel));
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
    }

    public void OnStateFluxHostCommandChanged(HostCommandChangedMessage message)
    {
    }

    public void OnStateFluxGuestCommandChanged(GuestCommandChangedMessage message)
    {
    }
}

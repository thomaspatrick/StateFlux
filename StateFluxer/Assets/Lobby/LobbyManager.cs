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

public class LobbyManager : MonoBehaviour, IStateFluxListener
{
    // --- Singleton ---
    private static LobbyManager _instance;
    public static LobbyManager Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
            StartCoroutine(Initialize());
        }
    }
    // --- -------- ---

    public GameObject login;
    public GameObject lobby;
    public GameObject playerRowPrefab;
    public GameObject gameRowPrefab;

    private List<Player> players;
    private List<GameInstance> games;
    private List<ChatSaidMessage> said;
    private string lastUsernameSaveFile;
    private GameObject userNameInputField;
    private GameObject userNameInputText;
    private GameObject userNameInputPlaceholder;
    private GameObject chatInputField;
    private GameObject chatField;
    private GameObject chatScrollView;
    private GameObject newGamePanel;
    private GameObject errorPanel;
    private GameObject modalBackgroundPanel;
    private GameInstance maybeJoinThisGameInstance;
    private GameObject currentShowingPanel;
    private bool hostingGame;

    public string lastUsername
    {
        get 
        {
            string value = null;
            if (File.Exists(lastUsernameSaveFile))
            {
                value = File.ReadAllText(lastUsernameSaveFile);
            }
            return value;
        }
        set
        {
            File.WriteAllText(lastUsernameSaveFile, value);
        }
    }


    [HideInInspector]
    public bool Initialized;
    public IEnumerator Initialize()
    {
        yield return new WaitForEndOfFrame();
        players = new List<Player>();
        games = new List<GameInstance>();
        said = new List<ChatSaidMessage>();
        userNameInputField = GameObject.Find("InputField");
        userNameInputText = GameObject.Find("InputField/Text");
        userNameInputPlaceholder = GameObject.Find("InputField/Placeholder");
        lastUsernameSaveFile = Application.persistentDataPath;
        if(Application.isEditor)
            lastUsernameSaveFile += "\\lastUsername-editor.txt";
        else
        {
            lastUsernameSaveFile += "\\lastUsername.txt";
        }

        chatScrollView = GameObject.Find("Chat/Scroll View");
        chatInputField = GameObject.Find("ChatInputField/Text");
        chatField = GameObject.Find("Content/Text");
        newGamePanel = GameObject.Find("New Game Panel");
        errorPanel = GameObject.Find("ErrorPanel");
        modalBackgroundPanel = GameObject.Find("ModalBackdrop");

        StateFluxClient.Instance.AddListener(this);
        StateFluxClient.Instance.Initialize();

        if (StateFluxClient.Instance.hasSavedSession)
        {
            StartCoroutine(ActivateLobbyPanel());
        }
        else
        {
            StartCoroutine(ActivateLoginPanel());
        }
        Initialized = true;
    }

    public void Update()
    {
        if (Initialized && userNameInputField != null && Input.GetKey(KeyCode.Return))
        {
            var field = userNameInputField.GetComponent<InputField>();
            if (field.isFocused && field.text != "")
            {
                OnClickToConnect();
            }
        }
    }

    IEnumerator PollLists()
    {
        yield return new WaitForSeconds(1);
        /* remove this method later - doing this on the server side now
        while(true)
        {
            if (StateFluxClient.Instance.openWithIdentity)
            {
                StateFluxClient.Instance.SendRequest(new PlayerListMessage());
                StateFluxClient.Instance.SendRequest(new GameInstanceListMessage());
            }
            yield return new WaitForSeconds(10);
        }
         */
    }

    // ------------------------------------
    // controlling the ui

    IEnumerator ActivateLobbyPanel()
    {
        yield return null;
        ShowPanel(lobby, modalBackgroundPanel, true);
        ShowPanel(login, modalBackgroundPanel, false);
    }

    IEnumerator ActivateLoginPanel()
    {
        yield return null;
        ShowPanel(lobby, modalBackgroundPanel, false);
        ShowPanel(login, null, true);
        var field = userNameInputField.GetComponent<InputField>();
        if (!string.IsNullOrEmpty(lastUsername)) field.text = lastUsername;
        EventSystem.current.SetSelectedGameObject(userNameInputField);
        field.ActivateInputField();
    }

    void ShowPanel(GameObject obj, GameObject backdrop, bool show)
    {
        Debug.Log($"{(show ? "Showing" : "Hiding")} panel {obj.name}");
        if(show)
        {
            currentShowingPanel = obj;
        }
        else
        {
            currentShowingPanel = null;
        }

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
        GameObject content = GameObject.Find("Lobby Panel/PlayersPanel/Players/Scroll View/Viewport/Content");
        players.Clear();
        if (content == null) return;
        foreach (Transform child in content.transform) GameObject.Destroy(child.gameObject);
    }

    private void ClearGameInstanceListView()
    {
        GameObject content = GameObject.Find("Lobby Panel/GamesPanel/Games/Scroll View/Viewport/Content");
        games.Clear();
        if (content == null) return;
        foreach (Transform child in content.transform) GameObject.Destroy(child.gameObject);
    }


    // --------------------------------------------------
    // UI message handlers

    public void OnClickToConnect()
    {
        lastUsername = StateFluxClient.Instance.userName = GetLoginUsername();
        Debug.Log($"LobbyManager.OnClickToConnect as {lastUsername}");
        StateFluxClient.Instance.Login();

    }

    public void OnClickToCreateGame()
    {
        ShowPanel(newGamePanel, modalBackgroundPanel, true);
        GameObject.Find("New Game Panel");
    }

    public void OnClickConfirmCreateGame()
    {
        ShowPanel(newGamePanel, modalBackgroundPanel, false);
        InputField inputField = newGamePanel.GetComponentInChildren<InputField>();
        var message = new CreateGameInstanceMessage
        {
            GameName = "AssetCollapse",
            InstanceName = inputField.text
            
        };
        StateFluxClient.Instance.SendRequest(message);
    }

    public void OnClickToLogout()
    {
        Debug.Log($"LobbyManager.OnClickToLogout");
        StateFluxClient.Instance.Logout();
        ClearPlayerListView();
        ClearGameInstanceListView();
        StartCoroutine(ActivateLoginPanel());
    }

    public void OnChatInputSubmit(string value)
    {
        StateFluxClient.Instance.SendRequest(new ChatSayMessage { say = value });
    }


    public void OnUsernameChanged(string newValue)
    {
        GameObject.Find("LoginButton").GetComponent<Button>().interactable = !string.IsNullOrEmpty(newValue);
    }

    public void OnClickedModalBackdrop()
    {
        Debug.Log("clicked modal backdrop!");
        if(currentShowingPanel)
        {
            if(currentShowingPanel.name != "Login Panel")
                ShowPanel(currentShowingPanel, modalBackgroundPanel, false);
        }
    }

    public string GetLoginUsername()
    {
        string placeholderText = userNameInputPlaceholder.GetComponent<Text>().text;
        string userNameText = userNameInputText.GetComponent<Text>().text;
        return (userNameText == placeholderText) ? null : userNameText;
    }

    // --------------------------------------------------
    // StateFluxListener interface methods

    public void OnStateFluxInitialize()
    {
        Debug.Log("OnStateFluxInitialize");
    }

    public void OnStateFluxWaitingToConnect()
    {
        Debug.Log("OnStateFluxWaitingToConnect");
    }

    public void OnStateFluxConnect()
    {
        Debug.Log("OnStateFluxConnect!");
        StartCoroutine(ActivateLobbyPanel());
        StartCoroutine(PollLists());
    }

    public void OnStateFluxDisconnect()
    {
        Debug.Log("OnStateFluxDisconnect!");
        StopCoroutine(PollLists());
    }

    public void OnStateFluxStateChanged(StateChangedMessage message)
    {
    }

    public void OnStateFluxPlayerListing(PlayerListingMessage message)
    {
        ClearPlayerListView();
        GameObject content = GameObject.Find("Lobby Panel/PlayersPanel/Players/Scroll View/Viewport/Content");
        if (content == null) return;
        foreach (Player p in message.Players)
        {
            players.Add(p);
            GameObject row = GameObject.Instantiate(playerRowPrefab, content.transform);
            var textMeshPro = row.GetComponentInChildren<TextMeshProUGUI>();
            textMeshPro.text = p.Name;

        }
    }

    public void OnStateFluxChatSaid(ChatSaidMessage message)
    {
        said.Add(message);
        string userName = StateFluxClient.Instance.userName;
        var chatText = chatField.GetComponent<TextMeshProUGUI>();
        StringBuilder builder = new StringBuilder();
        builder.Append("<color=#339933>");
        foreach (var msg in said)
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
        chatScrollView.GetComponent<ScrollRect>().normalizedPosition = new Vector2(0, 0); // scroll to bottom
    }

    public void OnStateFluxGameInstanceCreatedMessage(GameInstanceCreatedMessage message)
    {
        Debug.Log($"Server says {message.GameInstance.HostPlayer.Name} began hosting a new game instance: {message.GameInstance.Name}");
        Debug.Log($"I am {this.lastUsername}");
        if (message.GameInstance.HostPlayer.Name == lastUsername)
        {
            Debug.Log($"Current user is hosting a game!");
            hostingGame = true;
        }
    }

    public void OnStateFluxGameInstanceListing(GameInstanceListingMessage message)
    {
        ClearGameInstanceListView();
        GameObject content = GameObject.Find("Lobby Panel/GamesPanel/Games/Scroll View/Viewport/Content");
        if (content == null) return;
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
            games.Add(g);
            GameObject row = GameObject.Instantiate(gameRowPrefab, content.transform);
            var button = row.GetComponent<Button>();
            button.name = "GameInstance." + g.Id;
            button.onClick.AddListener(delegate { OnClickGameInstance(button.name); });
            var textMeshPro = row.GetComponentInChildren<TextMeshProUGUI>();
            textMeshPro.text = builder.ToString();
        }
    }
    public void OnClickGameInstance(string buttonName)
    {
        if(!hostingGame)
        {
            string[] parts = buttonName.Split(new char[] { '.' });
            string gameInstanceId = parts[1];
            GameInstance gameInstance = games.FirstOrDefault(g => g.Id.ToString() == gameInstanceId);
            var joinGamePanel = GameObject.Find("Join Game Panel");
            var canvasGroup = joinGamePanel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            var textPanel = GameObject.Find("Join Game Panel/Text");
            var txt = textPanel.GetComponent<Text>();
            txt.text = $"Join {gameInstance.Name} hosted by {gameInstance.HostPlayer.Name}?";
            maybeJoinThisGameInstance = gameInstance;
        }
        else
        {
            string[] parts = buttonName.Split(new char[] { '.' });
            string gameInstanceId = parts[1];
            GameInstance gameInstance = games.FirstOrDefault(g => g.Id.ToString() == gameInstanceId);
            var leaveGamePanel = GameObject.Find("Leave Game Panel");
            var canvasGroup = leaveGamePanel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            var textPanel = GameObject.Find("Leave Game Panel/Text");
            var txt = textPanel.GetComponent<Text>();
            txt.text = $"Leave {gameInstance.Name} hosted by {gameInstance.HostPlayer.Name}?";
            maybeJoinThisGameInstance = gameInstance;
        }
    }

    public void OnClickJoinGame()
    {
        if(!this.hostingGame)
        {
            var joinGamePanel = GameObject.Find("Join Game Panel");
            var canvasGroup = joinGamePanel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            Debug.Log($"Requested join game instance {maybeJoinThisGameInstance.Name}");
            StateFluxClient.Instance.SendRequest(new JoinGameInstanceMessage { GameName = maybeJoinThisGameInstance.Game.Name, InstanceName = maybeJoinThisGameInstance.Name });
        }
        else
        {
            Debug.Log("Join game request supressed because you are hosting a game");
        }

    }

    public void OnClickDismissJoinGame()
    {
        var joinGamePanel = GameObject.Find("Join Game Panel");
        var canvasGroup = joinGamePanel.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnClickLeaveGame()
    {
        StateFluxClient.Instance.SendRequest(new LeaveGameInstanceMessage { GameName = maybeJoinThisGameInstance.Game.Name, InstanceName = maybeJoinThisGameInstance.Name });
        OnClickDismissLeaveGame();
    }
    public void OnClickDismissLeaveGame()
    {
        var leaveGamePanel = GameObject.Find("Leave Game Panel");
        var canvasGroup = leaveGamePanel.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnStateFluxOtherMessage(Message message)
    {
        Debug.Log($"OnStateFluxOtherMessage - {message.MessageType}!");
    }

    public void OnStateFluxServerError(ServerErrorMessage message)
    {
        Debug.Log($"OnStateFluxServerError - {message.Error}!");
        errorPanel.SendMessage("OnStateFluxError", message.Error);
    }
}

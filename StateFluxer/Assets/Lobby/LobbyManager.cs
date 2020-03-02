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
        while(true)
        {
            if (StateFluxClient.Instance.connected)
            {
                StateFluxClient.Instance.SendRequest(new PlayerListMessage());
                StateFluxClient.Instance.SendRequest(new GameInstanceListMessage());
            }
            yield return new WaitForSeconds(10);
        }
    }

    // ------------------------------------
    // controlling the ui

    IEnumerator ActivateLobbyPanel()
    {
        yield return null;
        ShowPanel(lobby, true);
        ShowPanel(login, false);
    }

    IEnumerator ActivateLoginPanel()
    {
        yield return null;
        ShowPanel(lobby, false);
        ShowPanel(login, true);
        var field = userNameInputField.GetComponent<InputField>();
        if (!string.IsNullOrEmpty(lastUsername)) field.text = lastUsername;
        EventSystem.current.SetSelectedGameObject(userNameInputField);
        field.ActivateInputField();
    }

    void ShowPanel(GameObject obj, bool show)
    {
        Debug.Log($"{(show ? "Showing" : "Hiding")} panel {obj.name}");
        var canvasGroup = obj.GetComponent<CanvasGroup>();
        if(canvasGroup != null)
        {
            canvasGroup.alpha = show ? 1 : 0;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
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
        var message = new CreateGameInstanceMessage
        {
            GameName = "AssetCollapse",
            InstanceName = System.Guid.NewGuid().ToString()
        };
        StateFluxClient.Instance.SendRequest(message);
    }

    public void OnClickToLogout()
    {
        Debug.Log($"LobbyManager.OnClickToLogout");
        StateFluxClient.Instance.Logout();
        ClearPlayerListView();
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

    public void OnStateFluxGameInstanceListing(GameInstanceListingMessage message)
    {
        ClearGameInstanceListView();
        GameObject content = GameObject.Find("Lobby Panel/GamesPanel/Games/Scroll View/Viewport/Content");
        if (content == null) return;
        foreach (GameInstance g in message.GameInstances)
        {
            bool first = true;
            StringBuilder builder = new StringBuilder();
            builder.Append($"<b>{g.Name}</b>\nHost: {g?.HostPlayer?.Name}\n Player: ");
            foreach(Player p in g.Players)
            {
                if (!first) builder.Append(",");
                first = false;
                builder.Append(p.Name);
            }
            games.Add(g);
            GameObject row = GameObject.Instantiate(gameRowPrefab, content.transform);
            var textMeshPro = row.GetComponentInChildren<TextMeshProUGUI>();
            textMeshPro.text = builder.ToString();

        }
    }

    public void OnStateFluxOtherMessage(Message message)
    {
        Debug.Log($"OnStateFluxOtherMessage - {message.MessageType}!");
    }
}

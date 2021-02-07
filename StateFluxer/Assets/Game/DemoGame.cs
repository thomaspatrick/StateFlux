using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using StateFlux.Client;
using StateFlux.Model;
using StateFlux.Unity;

public class DemoGame : MonoBehaviour, IStateFluxListener
{
    // --- Singleton ---
    private static DemoGame _instance;
    public static DemoGame Instance
    {
        get
        {
            if (_instance == null) throw new Exception("GameManage accessed before ready");
            return _instance;
        }
    }

    // the stateflux player id
    private string playerId;

    private StateFluxClient stateFluxClient;

    // object tracking
    public static GameObjectTracker gameObjectTracker;

    // used when running as a guest to determine if changed input should be sent to the server
    private GuestInput lastGuestInput;

    // used by the host to keep track of multiple mouse information
    private MiceTracker miceTracker;
    private GameObject thisMouse, thatMouse;
    public GameObject mousePrefab;
    public GameObject jakePrefab;

    public GameObject circler;

    private Player thisPlayer = null;
    private Player thatPlayer = null;
    private Player hostPlayer = null;
    private Dictionary<string, Player> players = new Dictionary<string, Player>();

    private void Awake()
    {

        if (StateFluxClient.Instance == null)
        {
            SceneManager.LoadScene("_preload");
            return;
        }

        if (_instance != null && _instance != this)
        {
            Debug.Log("GameManage blocking double instantiate");
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
            Debug.Log("GameManage is Awake");
        }
    }

    void Start()
    {
        gameObjectTracker = new GameObjectTracker();
        lastGuestInput = new GuestInput();
        miceTracker = new MiceTracker();

        Cursor.visible = false;

        stateFluxClient = GameObject.Find("StateFlux").GetComponent<StateFluxClient>();
        if (stateFluxClient == null)
        {
            DebugLog("Failed to connect with StateFluxClient");
            return;
        }
        stateFluxClient.AddListener(this);

        // lobby makes sure we are logged in before starting a game
        playerId = stateFluxClient.clientId;

        // when the game instance starts, LobbyManager receives a game instance start notification and saves these
        // we copy them here for convenience
        hostPlayer = LobbyManager.Instance.hostPlayer;
        players = LobbyManager.Instance.players;
        thisPlayer = players[playerId];
        thatPlayer = players.Values.Where(p => p.Id != playerId).FirstOrDefault();

        thisMouse = GameObject.Instantiate(mousePrefab);
        SetObjectColor(thisMouse, thisPlayer.Color);
        thatMouse = GameObject.Instantiate(mousePrefab);
        SetObjectColor(thatMouse, thatPlayer.Color);

        if (stateFluxClient.isHosting)
        {
            GameObject.Find("State_IsGuest").SetActive(false);
            StartCoroutine(gameObjectTracker.SendStateAsHost());
        }
        else
        {
            GameObject.Find("State_IsHost").SetActive(false);
            StartCoroutine(nameof(SendInputAsGuest));
        }
    }

    void Update()
    {
        if (stateFluxClient.isHosting)
            UpdateAsHost();
        else
        {
            UpdateAsGuest();
        }
    }

    void OnGUI()
    {
        Event currentEvent = Event.current;

        //Camera cam = Camera.main;
        //Vector2 mousePos = new Vector2();
        //mousePos.x = currentEvent.mousePosition.x;
        //mousePos.y = cam.pixelHeight - currentEvent.mousePosition.y;
        //Vector3 point = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cam.nearClipPlane));

        GUILayout.BeginArea(new Rect(20, 20, 250, 520));
        GUILayout.Label($"Player Id: {playerId} {(stateFluxClient.isHosting ? "(Host)" : "(Guest)")}");
        //GUILayout.Label("Items in tracking map: " + trackingMap.Count);
        GUILayout.Label("Items in tracking map: " + gameObjectTracker.Count());
        //GUILayout.Label("Screen pixels: " + cam.pixelWidth + ":" + cam.pixelHeight);
        //GUILayout.Label("Mouse position: " + mousePos);
        //GUILayout.Label("World position: " + point.ToString("F3"));
        //GUILayout.Label("My Mouse GO: " + myMouse.transform.position.x + "," + myMouse.transform.position.y);
        //GUILayout.Label("Their Mouse GO: " + theirMouse.transform.position.x + "," + theirMouse.transform.position.y);
        // miceTracker.GUIDescribe();
        GUILayout.EndArea();
    }

    void UpdateAsHost()
    {
        Vector3 world = GetMousePoint();

        // move mouse cursor object to that position
        thisMouse.transform.position = world;

        // keep track of my mouse position in the list of mice sent to guests
        miceTracker.Track(playerId, new Vec2d { X = world.x, Y = world.y });

        // get a list of only mice that have changed position
        Mice mice = miceTracker.BuildMice();
        if(mice != null)
        {
            // send changed mice to guests
            MiceChangeMessage mcm = new MiceChangeMessage
            {
                Payload = mice
            };
            stateFluxClient.SendRequest(mcm);

            // move mouse cursor gameobjects
            foreach (Mouse m in mice.Items)
            {
                SetPlayerMouseDetails(m);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if(stateFluxClient.isHosting)
            {
                SendHostFloobieCommand();
            }
            ChangeTracker changeTracker = CreateJake(world, thisPlayer.Color, circler);
            gameObjectTracker.TrackCreate(changeTracker);
        }
    }

    // send a dummy, placeholder command, when operating as the host
    void SendHostFloobieCommand()
    {
        HostCommandChangeMessage message = new HostCommandChangeMessage()
        {
            Payload = new GameCommand
            {
                Name = "floobie",
                ObjectId = "test",
                Params = new Dictionary<string, string>()
            }
        };
        message.Payload.Params["foo"] = "bar";
        stateFluxClient.SendRequest(message);
    }

    void UpdateAsGuest()
    {
        SendInputAsGuest();
    }

    private ChangeTracker CreateJake(Vector3 mousePoint, StateFlux.Model.Color color, GameObject parent = null)
    {
        var change = new StateFlux.Model.Change2d
        {
            Event = ChangeEvent.Created,
            ObjectID = "jake" + ShortGuid.Generate(),
            TypeID = "jake",
            ParentID = parent?.name, 
            Transform = new Transform2d
            {
                Pos = mousePoint.Convert2d(),
                Vel = new Vec2d { X = 0, Y = 0 },
                Rot = 0, //UnityEngine.Random.value * 720.0f - 360.0f,
                RotV = UnityEngine.Random.value * 100.0f - 50.0f,
                Scale = UnityEngine.Random.value
            },
            Attributes = new StateFlux.Model.Attributes
            {
                Color = color
            }
        };
        GameObject jake = gameObjectTracker.StateCreateGameObject(change, stateFluxClient.isHosting);
        return new ChangeTracker { gameObject = jake, create = change };
    }

    // called when the game is guest (not hosting), gathers and sends user input to host
    private void SendInputAsGuest()
    {
        bool clicked = Input.GetMouseButtonDown(0);
        if(clicked)
        {
            SendGuestGroovyCommand();
            clicked = false; // eat the click if a command is sent
        }

        var input = new GuestInput
        {
            mPos = GetMousePoint().Convert2d(),
            mDown = Input.GetMouseButton(0),
            mClicked = Input.GetMouseButtonDown(0),
            hArrows = Input.GetAxis("Horizontal"),
            vArrows = Input.GetAxis("Vertical"),
            btn1 = Input.GetButtonDown("Fire1"),
            btn2 = Input.GetButtonDown("Fire2"),
            btn3 = Input.GetButtonDown("Jump")
        };
        if (lastGuestInput.mPos == null ||
            input.mPos.X != lastGuestInput.mPos.X ||
            input.mPos.Y != lastGuestInput.mPos.Y ||
            input.mDown != lastGuestInput.mDown ||
            input.hArrows != lastGuestInput.hArrows ||
            input.vArrows != lastGuestInput.vArrows ||
            input.btn1 != lastGuestInput.btn1 ||
            input.btn2 != lastGuestInput.btn2 ||
            input.btn3 != lastGuestInput.btn3)
        {
            var message = new GuestInputChangeMessage
            {
                Payload = input
            };
            //Debug.Log($"SendInputAsGuest: {JsonConvert.SerializeObject(message)}");
            stateFluxClient.SendRequest(message);
            lastGuestInput = input;
        }
    }

    // send a dummy, placeholder command, when operating as guest
    public void SendGuestGroovyCommand()
    {
        GuestCommandChangeMessage message = new GuestCommandChangeMessage()
        {
            Payload = new GameCommand
            {
                Name = "groovy",
                ObjectId = "testy",
                Params = new Dictionary<string, string>()
            }
        };
        message.Payload.Params["bin"] = "baz";
        stateFluxClient.SendRequest(message);
    }

    // called when the game is guest (not hosting), contains state changes broadcast from the host
    public void OnStateFluxHostStateChanged(HostStateChangedMessage message)
    {
        gameObjectTracker.OnHostStateChanged(message);
    }

    // called when the game is hosting, contains state updates sent from a guest
    public void OnStateFluxGuestInputChanged(GuestInputChangedMessage message)
    {
        string guestMouseId = message.Guest.ToString();
        miceTracker.Track(guestMouseId, message.Payload.mPos);

        if(message.Payload.mClicked)
        {
            ChangeTracker changeTracker = CreateJake(message.Payload.mPos.Convert3d(), thatPlayer.Color, circler);
            gameObjectTracker.TrackCreate(changeTracker);
        }
    }

    public void OnStateFluxInitialize()
    {
    }

    public void OnStateFluxWaitingToConnect()
    {
    }

    public void OnStateFluxConnect()
    {
    }

    public void OnStateFluxDisconnect()
    {
    }

    public void OnStateFluxServerError(ServerErrorMessage message)
    {
    }

    public void OnStateFluxPlayerListing(PlayerListingMessage message)
    {
    }

    public void OnStateFluxGameInstanceCreated(GameInstanceCreatedMessage message)
    {
    }

    public void OnStateFluxGameInstanceJoined(GameInstanceJoinedMessage message)
    {
    }

    public void OnStateFluxGameInstanceLeft(GameInstanceLeftMessage message)
    {
    }

    public void OnStateFluxGameInstanceListing(GameInstanceListingMessage message)
    {
    }

    public void OnStateFluxGameInstanceStart(GameInstanceStartMessage message)
    {
    }

    public void OnStateFluxGameInstanceStopped(GameInstanceStoppedMessage message)
    {
        Debug.Log("OnStateFluxGameInstanceStopped!");
        Cursor.visible = true;
        gameObjectTracker.Stop();
        SceneManager.LoadScene("LobbyScene");

    }

    public void OnStateFluxChatSaid(ChatSaidMessage message)
    {
    }

    public void OnStateFluxOtherMessage(Message message)
    {
    }

    static private void SetObjectColor(GameObject gameObject, StateFlux.Model.Color newColor)
    {
        UnityEngine.Color color = new UnityEngine.Color(newColor.Red, newColor.Green, newColor.Blue, newColor.Alpha);
        var spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        //var textMesh = gameObject.GetComponentInChildren<TextMesh>();
        if (spriteRenderer != null) spriteRenderer.color = color;
        //if (textMesh != null) textMesh.color = color;
    }

    static private void DebugLog(string msg, bool focus = false)
    {
        //if(focus)
        {
            Debug.Log(msg);
        }
    }

    static private Vector3 GetMousePoint()
    {
        // get mouse position
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.nearClipPlane;
        Vector3 point = Camera.main.ScreenToWorldPoint(mousePoint);
        point.z = 0;
        return point;
    }

    static private void SetObjectText(GameObject gameObject, string newText)
    {
        var textMesh = gameObject.GetComponentInChildren<TextMesh>();
        if (textMesh != null) textMesh.text = newText;
    }

    
    public void OnStateFluxHostCommandChanged(HostCommandChangedMessage message)
    {
        DebugLog(message.Payload.Params["foo"]);
    }

    public void OnStateFluxGuestCommandChanged(GuestCommandChangedMessage message)
    {
        DebugLog(message.Payload.Params["bin"]);
    }

    // called when running as guest - host is telling us where to move everybody's mouse cursors
    public void OnStateFluxMiceChanged(MiceChangedMessage message)
    {
        foreach(Mouse m in message.Payload.Items)
        {
            SetPlayerMouseDetails(m);
        }
    }

    private void SetPlayerMouseDetails(Mouse m)
    {
        if(m.PlayerId == playerId)
        {
            thisMouse.transform.position = m.Pos.Convert3d();
        }
        else
        {
            // assume 2 players
            thatMouse.transform.position = m.Pos.Convert3d();
        }
    }
}

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
    //private Dictionary<string, ChangeTracker> trackingMap;
    //private Queue<Change2d> changeQueue;
    private GameObjectTracker gameObjectTracker;

    // used when running as a guest to determine if changed input should be sent to the server
    private GuestInput lastGuestInput;

    // used by the host to keep track of multiple mouse information
    private MiceTracker miceTracker;
    private GameObject thisMouse, thatMouse;
    public GameObject mousePrefab;
    public GameObject jakePrefab;

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
        //trackingMap = new Dictionary<string, ChangeTracker>();
        //changeQueue = new Queue<Change2d>();
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
            //StartCoroutine(nameof(SendStateAsHost));
            StartCoroutine(gameObjectTracker.SendStateAsHost());
        }
        else
        {
            GameObject.Find("State_IsHost").SetActive(false);
            //StartCoroutine(gameObjectTracker.SweepStateAsGuest());
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

        if (Input.GetMouseButton(0))
        {
            if(stateFluxClient.isHosting)
            {
                HostCommandChangeMessage message = new HostCommandChangeMessage()
                {
                    Payload = new GameCommand
                    {
                        Name = "floobie",
                        ObjectId = "test",
                        Params = new Dictionary<string,string>()
                    }
                };
                message.Payload.Params["foo"] = "bar";
                stateFluxClient.SendRequest(message);
            }
            ChangeTracker changeTracker = CreateJake(world, thisPlayer.Color);
            //trackingMap.Add(changeTracker.change.ObjectID, changeTracker);
            //changeQueue.Enqueue(changeTracker.change);
            gameObjectTracker.TrackCreate(changeTracker);
        }
    }

    void UpdateAsGuest()
    {
        SendInputAsGuest();
    }

    private ChangeTracker CreateJake(Vector3 mousePoint, StateFlux.Model.Color color)
    {
        var change = new StateFlux.Model.Change2d
        {
            Event = ChangeEvent.Created,
            ObjectID = "jake" + ShortGuid.Generate(),
            TypeID = "jake",
            Transform = new Transform2d
            {
                Pos = mousePoint.Convert2d(),
                Vel = new Vec2d { X = 0, Y = 0 },
                Rot = 0, //UnityEngine.Random.value * 720.0f - 360.0f,
                RotV = UnityEngine.Random.value * 100.0f - 50.0f
            },
            Attributes = new StateFlux.Model.Attributes
            {
                Color = color
            }
        };
        GameObject jake = gameObjectTracker.StateCreateGameObject(change, stateFluxClient.isHosting);
        return new ChangeTracker { gameObject = jake, create = change };
    }


    //private void EnqueuePosChangeAsHost(string objectId, Vector3 pos)
    //{
    //    if(trackingMap.TryGetValue(objectId, out ChangeTracker tracker))
    //    {
    //        //Debug.Log($"EnqueuePosChangeAsHost: found tracker object for {objectId}");
    //        Change2d change = tracker.change;
    //        if(change.Transform.Pos.X != pos.x || change.Transform.Pos.Y != pos.y)
    //        {
    //            //DebugLog($"EnqueuePosChangeAsHost: position changed for {objectId}, so queueing...", true);
    //            change.Event = ChangeEvent.Updated;
    //            change.Transform.Pos = pos.Convert2d();
    //            changeQueue.Enqueue(change);
    //        }
    //    }
    //    else
    //    {
    //        Debug.Log($"EnqueuePosChangeAsHost: failed to track {objectId}");
    //    }
    //}

    //private IEnumerator SendStateAsHost()
    //{
    //    while(true)
    //    {
    //        // send changes to mouse pos and tracked objects to the server, every 50 milliseconds
    //        var changes = new List<Change2d>();
    //        while (changeQueue.Count > 0)
    //        {
    //            changes.Add(changeQueue.Dequeue());
    //        }

    //        if(changes.Count > 0)
    //        {
    //            // the server forwards host state change messages to all guests
    //            Message message = new HostStateChangeMessage()
    //            {
    //                Payload = new StateChange
    //                {
    //                    Changes = changes
    //                }
    //            };
    //            stateFluxClient.SendRequest(message);
    //        }

    //        yield return new WaitForSeconds(.01f);
    //    }
    //}

    // called when the game is guest (not hosting), gathers and sends user input to host
    private void SendInputAsGuest()
    {
        bool clicked = Input.GetMouseButtonDown(0);
        if(clicked)
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


    // called when the game is guest (not hosting), contains state changes broadcast from the host
    public void OnStateFluxHostStateChanged(HostStateChangedMessage message)
    {
        gameObjectTracker.OnHostStateChanged(message);
        //if (stateFluxClient.isHosting)
        //{
        //    DebugLog($"Host should not be receiving host state change messages! (Error)");
        //    return;
        //}

        //foreach (var change in message.Payload.Changes)
        //{
        //    bool found = trackingMap.TryGetValue(change.ObjectID, out ChangeTracker tracker);

        //    if (change.Event == ChangeEvent.Created)
        //    {
        //        if (found)
        //        {
        //            DebugLog($"Host has asked us to create object {change.ObjectID} that already exists. (Skipping)");
        //            continue;
        //        }

        //        var createdGameObject = StateCreateGameObject(change, false);
        //        trackingMap[change.ObjectID] = new ChangeTracker { gameObject = createdGameObject, change = change };
        //        DebugLog($"Created tracker for {change.TypeID} - {change.ObjectID}.", true);
        //    }
        //    else if (change.Event == ChangeEvent.Destroyed)
        //    {
        //        if (!found)
        //        {
        //            DebugLog($"Host has asked us to destroy object {change.ObjectID} but it does not exist. (Skipping)", true);
        //            continue;
        //        }


        //        if (tracker.gameObject == null) // unity supposedly overrides the behavior of == to return null for destroyed objects, even if they haven't been c# deleted yet
        //        {
        //            DebugLog($"Host has asked us to destroy object {change.ObjectID} but it has already been destroyed. (Skipping)");
        //        }
        //        else
        //        {
        //            DebugLog($"Destroying game object for {change.ObjectID}. (Should call OnTrackedObjectDestroy)");
        //            GameObject.Destroy(tracker.gameObject);
        //            DebugLog($"Destroyed game object for {change.ObjectID}. (Should have called OnTrackedObjectDestroy)");
        //        }
        //    }
        //    else if (change.Event == ChangeEvent.Updated)
        //    {
        //        if (!found)
        //        {
        //            var createdGameObject = StateCreateGameObject(change, false);
        //            tracker = trackingMap[change.ObjectID] = new ChangeTracker { gameObject = createdGameObject, change = change };
        //            DebugLog($"Created tracker for {change.TypeID} - {change.ObjectID}.", true);
        //            //DebugLog($"Host has asked us to update object {change.ObjectID} but it does not exist. (Skipping)", true);
        //            //continue;
        //        }

        //        if (change?.Transform?.Pos == null)
        //        {
        //            DebugLog($"Host has asked us to update object {change.ObjectID} but it's transform pos does not exist. (Skipping)");
        //            continue;
        //        }

        //        if (tracker.gameObject == null) // unity supposedly overrides the behavior of == to return null for destroyed objects, even if they haven't been c# deleted yet
        //        {
        //            DebugLog($"Host has asked us to update object {change.ObjectID} but it has already been destroyed. (Skipping)", true);
        //            continue;
        //        }

        //        tracker.gameObject.transform.position = change.Transform.Pos.Convert3d();
        //    }
        //}
    }

    // called when the game is hosting, contains state updates sent from a guest
    public void OnStateFluxGuestInputChanged(GuestInputChangedMessage message)
    {
        string guestMouseId = message.Guest.ToString();
        miceTracker.Track(guestMouseId, message.Payload.mPos);

        if(message.Payload.mClicked)
        {
            ChangeTracker changeTracker = CreateJake(message.Payload.mPos.Convert3d(), thatPlayer.Color);
            gameObjectTracker.TrackCreate(changeTracker);
            //trackingMap.Add(changeTracker.change.ObjectID, changeTracker);
            //changeQueue.Enqueue(changeTracker.change);
        }
    }

    //// loads the prefab named the same as change.TypeID, applies caption, color & position
    //private GameObject StateCreateGameObject(Change2d change, bool asHost)
    //{
    //    var prefabPath = $"{change.TypeID}";
    //    DebugLog($"StateCreateGameObject is attempting to instantiate prefab '{prefabPath}' for '{change.ObjectID}'", true);
    //    var obj = (GameObject)Instantiate(Resources.Load(prefabPath));
    //    if(obj == null)
    //    {
    //        DebugLog($"StateCreateGameObject failed to instantiate prefab '{prefabPath}'", true);
    //        return null;
    //    }
    //    obj.name = change.ObjectID;

    //    string side = asHost ? "hostObject" : "guestObject";

    //    if (change.Attributes.Color != null) SetObjectColor(obj, change.Attributes.Color);
    //    SetObjectText(obj, $"{side}:{change.ObjectID}");
    //    if (!asHost) 
    //    {
    //        var rigidbody =obj.GetComponent<Rigidbody2D>();
    //        if(rigidbody != null)
    //        {
    //            // guest objects don't feel gravity
    //            rigidbody.gravityScale = 0;
    //        }

    //        // guest objects don't destroy themselves, so remove KillMeOverTime
    //        var killMeOverTime = obj.GetComponent<KillMeOverTime>();
    //        if(killMeOverTime != null) Destroy(killMeOverTime);
    //    }
    //    obj.AddComponent<StateFluxTracked>();
    //    obj.transform.position = change.Transform.Pos.Convert3d();

    //    return obj;
    //}

    public void OnTrackedObjectChange(string name, Vector3 pos, Vector3 vel, Vector3 eulerAngles, float angularVelocity)
    {
        gameObjectTracker.OnTrackedObjectChange(name, pos, vel, eulerAngles, angularVelocity);
        //if(trackingMap.TryGetValue(name, out ChangeTracker tracker))
        //{
        //    // don't send guest state changes to the host, guest sends input and commands instead
        //    if(stateFluxClient.isHosting)
        //    {
        //        tracker.change.Event = ChangeEvent.Updated;
        //        tracker.change.Transform.Pos = pos.Convert2d();
        //        tracker.change.Transform.Vel = vel.Convert2d();
        //        changeQueue.Enqueue(tracker.change);
        //    }
        //}
    }

    public void OnTrackedObjectDestroy(string name)
    {
        gameObjectTracker.OnTrackedObjectDestroy(name);
        //if (trackingMap.TryGetValue(name, out ChangeTracker tracker))
        //{
        //    if(stateFluxClient.isHosting)
        //    {
        //        tracker.change.Event = ChangeEvent.Destroyed;
        //        changeQueue.Enqueue(tracker.change);
        //        DebugLog($"OnTrackedObjectDestroy, queued destroy change for '{name}'");
        //    }
        //    else
        //    {
        //        DebugLog($"OnTrackedObjectDestroy, removed tracker for '{name}'");
        //    }
        //    trackingMap.Remove(name);
        //    DebugLog($"Removed tracker for {name}.");

        //}
        //else
        //{
        //    DebugLog($"OnTrackedObjectDestroy, failed to look up tracking map for '{name}'");
        //}
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

    public void SetPlayerMouseDetails(Mouse m)
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

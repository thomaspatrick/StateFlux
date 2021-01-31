using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using StateFlux.Model;
using StateFlux.Client;
using StateFlux.Unity;
using Newtonsoft.Json;

public class GameManage : MonoBehaviour, IStateFluxListener
{
    // --- Singleton ---
    private static GameManage _instance;
    public static GameManage Instance
    {
        get
        {
            if (_instance == null) throw new Exception("GameManage accessed before ready");
            return _instance;
        }
    }

    private static readonly StateFlux.Model.Color hostColor = new StateFlux.Model.Color { Red = 1f, Green = 0f, Blue = 0f, Alpha = 1f };
    private static readonly StateFlux.Model.Color guestColor = new StateFlux.Model.Color { Red = 0f, Green = 0f, Blue = 1f, Alpha = 1f };

    private string id;
    private StateFluxClient stateFluxClient;
    private Dictionary<string, ChangeTracker> trackingMap;
    private Queue<Change2d> changeQueue;
    private GuestInput lastGuestInput;
    private MiceTracker miceTracker;
    private GameObject thisMouse, thatMouse;
    public GameObject myMousePrefab, theirMousePrefab;

    public Player thisPlayer = null;
    public Player thatPlayer = null;
    public Player hostPlayer = null;
    public Dictionary<string, Player> players = new Dictionary<string, Player>();


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
        trackingMap = new Dictionary<string, ChangeTracker>();
        changeQueue = new Queue<Change2d>();
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

        id = stateFluxClient.clientId;

        // when the game instance starts, LobbyManager receives a notification and sets these values
        // we copy them here for convenience
        hostPlayer = LobbyManager.Instance.hostPlayer;
        players = LobbyManager.Instance.players;
        thisPlayer = players[id];
        thatPlayer = players.Values.Where(p => p.Id != id).FirstOrDefault();

        thisMouse = GameObject.Instantiate(myMousePrefab);
        SetObjectColor(thisMouse, thisPlayer.Color);
        thatMouse = GameObject.Instantiate(theirMousePrefab);
        SetObjectColor(thatMouse, thatPlayer.Color);

        if (stateFluxClient.isHosting)
        {
            GameObject.Find("State_IsGuest").SetActive(false);
            StartCoroutine(nameof(SendStateAsHost));
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
        Camera cam = Camera.main;
        Event currentEvent = Event.current;


        Vector2 mousePos = new Vector2();
        mousePos.x = currentEvent.mousePosition.x;
        mousePos.y = cam.pixelHeight - currentEvent.mousePosition.y;

        Vector3 point = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cam.nearClipPlane));

        GUILayout.BeginArea(new Rect(20, 20, 250, 520));
        GUILayout.Label("Player Id: " + id);
        //GUILayout.Label("Screen pixels: " + cam.pixelWidth + ":" + cam.pixelHeight);
        //GUILayout.Label("Mouse position: " + mousePos);
        //GUILayout.Label("World position: " + point.ToString("F3"));
        //GUILayout.Label("My Mouse GO: " + myMouse.transform.position.x + "," + myMouse.transform.position.y);
        //GUILayout.Label("Their Mouse GO: " + theirMouse.transform.position.x + "," + theirMouse.transform.position.y);
        miceTracker.GUIDescribe();
        GUILayout.EndArea();
    }

    void UpdateAsHost()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.nearClipPlane;
        Vector3 world = Camera.main.ScreenToWorldPoint(mousePoint);
        thisMouse.transform.position = world;

        // host's mouse position
        miceTracker.Track(id, new Vec2d { X = world.x, Y = world.y });

        Mice mice = miceTracker.BuildMice();
        if(mice != null)
        {
            // send mice changes to guests
            MiceChangeMessage mcm = new MiceChangeMessage
            {
                Payload = mice
            };
            stateFluxClient.SendRequest(mcm);

            // show mice changes
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
            //ChangeTracker changeTracker = CreateBombAsHost(mousePoint);
            //trackingMap.Add(changeTracker.change.ObjectID, changeTracker);
            //changeQueue.Enqueue(changeTracker.change);
        }
    }

    void UpdateAsGuest()
    {
        SendInputAsGuest();
    }

    private ChangeTracker CreateBombAsHost(Vector3 mousePoint)
    {
        var change = new StateFlux.Model.Change2d
        {
            Event = ChangeEvent.Created,
            ObjectID = "bomb" + ShortGuid.Generate(),
            TypeID = "bomb",
            Transform = new Transform2d
            {
                Pos = mousePoint.Convert2d(),
                Vel = new Vec2d { X = 0, Y = 0 }
            },
            Attributes = new StateFlux.Model.Attributes
            {
                Color = CreateHostingColor()
            }
        };
        GameObject bomb = StateCreateGameObject(change, stateFluxClient.isHosting);
        return new ChangeTracker { gameObject = bomb, change = change };
    }


    private void EnqueuePosChangeAsHost(string objectId, Vector3 pos)
    {
        if(trackingMap.TryGetValue(objectId, out ChangeTracker tracker))
        {
            //Debug.Log($"EnqueuePosChangeAsHost: found tracker object for {objectId}");
            Change2d change = tracker.change;
            if(change.Transform.Pos.X != pos.x || change.Transform.Pos.Y != pos.y)
            {
                //DebugLog($"EnqueuePosChangeAsHost: position changed for {objectId}, so queueing...", true);
                change.Event = ChangeEvent.Updated;
                change.Transform.Pos = pos.Convert2d();
                changeQueue.Enqueue(change);
            }
        }
        else
        {
            Debug.Log($"EnqueuePosChangeAsHost: failed to track {objectId}");
        }
    }

    private IEnumerator SendStateAsHost()
    {
        while(true)
        {
            // send changes to mouse pos and tracked objects to the server, every 50 milliseconds
            var changes = new List<Change2d>();
            while (changeQueue.Count > 0)
            {
                changes.Add(changeQueue.Dequeue());
            }

            if(changes.Count > 0)
            {
                // the server forwards host state change messages to all guests
                Message message = new HostStateChangeMessage()
                {
                    Payload = new StateChange
                    {
                        Changes = changes
                    }
                };
                stateFluxClient.SendRequest(message);
            }

            yield return new WaitForSeconds(.05f);
        }
    }

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
        if (stateFluxClient.isHosting)
        {
            DebugLog($"Host should not be receiving host state change messages! (Error)");
            return;
        }

        foreach (var change in message.Payload.Changes)
        {
            bool found = trackingMap.TryGetValue(change.ObjectID, out ChangeTracker tracker);

            if (change.Event == ChangeEvent.Created)
            {
                if (found)
                {
                    DebugLog($"Host has asked us to create object {change.ObjectID} that already exists. (Skipping)");
                    continue;
                }

                var createdGameObject = StateCreateGameObject(change, false);
                trackingMap[change.ObjectID] = new ChangeTracker { gameObject = createdGameObject, change = change };
                DebugLog($"Created tracker for {change.TypeID} - {change.ObjectID}.", true);
            }
            else if (change.Event == ChangeEvent.Destroyed)
            {
                if (!found)
                {
                    DebugLog($"Host has asked us to destroy object {change.ObjectID} but it does not exist. (Skipping)", true);
                    continue;
                }


                if (tracker.gameObject == null) // unity supposedly overrides the behavior of == to return null for destroyed objects, even if they haven't been c# deleted yet
                {
                    DebugLog($"Host has asked us to destroy object {change.ObjectID} but it has already been destroyed. (Skipping)");
                }
                else
                {
                    DebugLog($"Destroying game object for {change.ObjectID}. (Should call OnTrackedObjectDestroy)");
                    GameObject.Destroy(tracker.gameObject);
                    DebugLog($"Destroyed game object for {change.ObjectID}. (Should have called OnTrackedObjectDestroy)");
                }
            }
            else if (change.Event == ChangeEvent.Updated)
            {
                if (!found)
                {
                    DebugLog($"Host has asked us to update object {change.ObjectID} but it does not exist. (Skipping)", true);
                    continue;
                }

                if (change?.Transform?.Pos == null)
                {
                    DebugLog($"Host has asked us to update object {change.ObjectID} but it's transform pos does not exist. (Skipping)");
                    continue;
                }

                if (tracker.gameObject == null) // unity supposedly overrides the behavior of == to return null for destroyed objects, even if they haven't been c# deleted yet
                {
                    DebugLog($"Host has asked us to update object {change.ObjectID} but it has already been destroyed. (Skipping)", true);
                    continue;
                }

                tracker.gameObject.transform.position = change.Transform.Pos.Convert3d();
            }
        }

    }

    // called when the game is hosting, contains state updates sent from a guest
    public void OnStateFluxGuestInputChanged(GuestInputChangedMessage message)
    {
        string guestMouseId = message.Guest.ToString();
        miceTracker.Track(guestMouseId, message.Payload.mPos);
    }

    // loads the prefab named the same as change.TypeID, applies caption, color & position
    private GameObject StateCreateGameObject(Change2d change, bool asHost)
    {
        var prefabPath = $"{change.TypeID}";
        DebugLog($"StateCreateGameObject is attempting to instantiate prefab '{prefabPath}' for '{change.ObjectID}'", true);
        var obj = (GameObject)Instantiate(Resources.Load(prefabPath));
        if(obj == null)
        {
            DebugLog($"StateCreateGameObject failed to instantiate prefab '{prefabPath}'", true);
            return null;
        }
        obj.name = change.ObjectID;

        string side = asHost ? "hostObject" : "guestObject";

        if (change.Attributes.Color != null) SetObjectColor(obj, change.Attributes.Color);
        SetObjectText(obj, $"{side}:{change.ObjectID}");
        if (!asHost) 
        {
            var rigidbody =obj.GetComponent<Rigidbody2D>();
            if(rigidbody != null)
            {
                // guest objects don't feel gravity
                rigidbody.gravityScale = 0;
            }

            // guest objects don't destroy themselves, so remove KillMeOverTime
            var killMeOverTime = obj.GetComponent<KillMeOverTime>();
            if(killMeOverTime != null) Destroy(killMeOverTime);
        }
        obj.AddComponent<StateFluxTracked>();
        obj.transform.position = change.Transform.Pos.Convert3d();

        return obj;
    }

    public void OnTrackedObjectChange(string name, Vector3 pos, Vector3 vel)
    {
        if(trackingMap.TryGetValue(name, out ChangeTracker tracker))
        {
            // don't send guest state changes to the host, guest sends input and commands instead
            if(stateFluxClient.isHosting)
            {
                tracker.change.Event = ChangeEvent.Updated;
                tracker.change.Transform.Pos = pos.Convert2d();
                tracker.change.Transform.Vel = vel.Convert2d();
                changeQueue.Enqueue(tracker.change);
            }
        }
    }

    public void OnTrackedObjectDestroy(string name)
    {
        if (trackingMap.TryGetValue(name, out ChangeTracker tracker))
        {
            tracker.change.Event = ChangeEvent.Destroyed;
            changeQueue.Enqueue(tracker.change);
            trackingMap.Remove(name);
            DebugLog($"Removed tracker for {name}.");

            DebugLog($"OnTrackedObjectDestroy, queued destroy change for '{name}'");
        }
        else
        {
            DebugLog($"OnTrackedObjectDestroy, failed to look up tracking map for '{name}'");
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
        UnityEngine.Color color = new UnityEngine.Color(newColor.Red, newColor.Green, newColor.Green, newColor.Alpha);
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

        Vector3 point = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        point.z = 0;
        return point;
    }

    static private void SetObjectText(GameObject gameObject, string newText)
    {
        var textMesh = gameObject.GetComponentInChildren<TextMesh>();
        if (textMesh != null) textMesh.text = newText;
    }

    private StateFlux.Model.Color CreateHostingColor()
    {
        return stateFluxClient.isHosting ? hostColor : guestColor;
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
        if(m.PlayerId == id)
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

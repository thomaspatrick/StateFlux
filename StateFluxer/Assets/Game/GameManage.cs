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


    private StateFluxClient stateFluxClient;

    private string id;

    private Dictionary<string, ChangeTracker> trackingMap;
    private Queue<Change2d> changeQueue;

    private GuestInput lastGuestInput;

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

        stateFluxClient = GameObject.Find("StateFlux").GetComponent<StateFluxClient>();
        if (stateFluxClient == null)
        {
            DebugLog("Failed to connect with StateFluxClient");
            return;
        }
        stateFluxClient.AddListener(this);

        id = stateFluxClient.clientId;// Guid.NewGuid().ToString(); //stateFluxClient.userName;
        DebugLog($"Initializing id to be {id}",true);

        if (stateFluxClient.isHosting)
        {
            GameObject.Find("State_IsGuest").SetActive(false);
            CreateMouseAsHost(id, GetMousePoint());
            StartCoroutine(nameof(SendStateAsHost));
        }
        else
        {
            GameObject.Find("State_IsHost").SetActive(false);
            StartCoroutine(nameof(SendInputAsGuest));
        }
    }

    private void CreateMouseAsHost(string mouseId, Vector3 mousePoint)
    {
        Change2d change = new Change2d
        {
            Event = ChangeEvent.Created,
            ObjectID = mouseId,
            TypeID = "mouse",
            Attributes = new Attributes
            {
                Color = CreateHostingColor()
            },
            Transform = new Transform2d
            {
                Pos = mousePoint.Convert3d()
            }
        };

        var prefab = $"{change.TypeID}";
        DebugLog($"CreateMouse is attempting to instantiate prefab '{prefab}' for id '{mouseId}'", true);
        var mousePointer = (GameObject)Instantiate(Resources.Load(prefab));
        if (mousePointer == null)
        {
            DebugLog($"StateCreateGameObject failed to instantiate prefab '{prefab}'");
        }

        string side = stateFluxClient.isHosting ? "hostObject" : "guestObject";

        SetObjectColor(mousePointer, change.Attributes.Color);
        SetObjectText(mousePointer, $"{side}:{mouseId}");
        mousePointer.transform.position = mousePoint;

        trackingMap[mouseId] = new ChangeTracker
        {
            change = change,
            gameObject = mousePointer
        };
        changeQueue.Enqueue(change);
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

    void UpdateAsHost()
    {
        Vector3 mousePoint = GetMousePoint();
        DebugLog($"Now changing mouse position as {id}", true);

        trackingMap[id].gameObject.transform.position = mousePoint;
        EnqueuePosChangeAsHost(id, mousePoint);

        if (Input.GetMouseButton(0))
        {
            ChangeTracker changeTracker = CreateBombAsHost(mousePoint);
            trackingMap.Add(changeTracker.change.ObjectID, changeTracker);
            changeQueue.Enqueue(changeTracker.change);
        }
    }

    void UpdateAsGuest()
    {
        SendInputAsGuest();
    }

    private ChangeTracker CreateFizzleAsHost(Vector3 mousePoint)
    {
        var change = new StateFlux.Model.Change2d
        {
            Event = ChangeEvent.Created,
            ObjectID = "fizzle" + Guid.NewGuid(),
            TypeID = "fizzle",
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
        GameObject fizzle = StateCreateGameObject(change, asHost: stateFluxClient.isHosting);
        ParticleSystem.MainModule particleSettings = fizzle.GetComponentInChildren<ParticleSystem>().main;
        particleSettings.startColor = new ParticleSystem.MinMaxGradient(change.Attributes.Color.Convert());
        return new ChangeTracker { gameObject = fizzle, change = change };
    }

    private ChangeTracker CreateBombAsHost(Vector3 mousePoint)
    {
        var change = new StateFlux.Model.Change2d
        {
            Event = ChangeEvent.Created,
            ObjectID = "bomb" + Guid.NewGuid(),
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
            Debug.Log($"EnqueuePosChangeAsHost: found tracker object for {objectId}");
            Change2d change = tracker.change;
            if(change.Transform.Pos.X != pos.x || change.Transform.Pos.Y != pos.y)
            {
                DebugLog($"EnqueuePosChangeAsHost: position changed for {objectId}, so queueing...", true);
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
                if(change.TypeID == "mouse")
                {
                    DebugLog($"Creating tracker for mouse! Calling it {change.ObjectID}", true);
                }
                trackingMap[change.ObjectID] = new ChangeTracker { gameObject = createdGameObject, change = change };
                DebugLog($"Created tracker for {change.TypeID} - {change.ObjectID}.", true);
            }
            else if (change.Event == ChangeEvent.Destroyed)
            {
                if (change.TypeID == "mouse")
                {
                    DebugLog($"Destroying tracker for mouse!", true);
                }

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
                if (change.TypeID == "mouse")
                {
                    DebugLog($"Updating tracker for mouse!", true);
                }

                if (!found)
                {
                    if (change.TypeID == "mouse")
                    {
                        var createdGameObject = StateCreateGameObject(change, false);
                        DebugLog($"Special Behavior: creating tracker for unknown updating mouse! Calling it {change.ObjectID}", true);
                        trackingMap[change.ObjectID] = new ChangeTracker { gameObject = createdGameObject, change = change };
                        DebugLog($"Created tracker for {change.TypeID} - {change.ObjectID}.", true);
                    }
                    else
                    {
                        DebugLog($"Host has asked us to update object {change.ObjectID} but it does not exist. (Skipping)", true);
                    }

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
                if(change.TypeID == "mouse") DebugLog($"Updated position for mouse {change.ObjectID} ({change.Transform.Pos.X},{change.Transform.Pos.X})");
            }
        }

    }

    // called when the game is hosting, contains state updates sent from a guest
    public void OnStateFluxGuestInputChanged(GuestInputChangedMessage message)
    {
        string guestMouseId = message.Guest.ToString();
        if(trackingMap.TryGetValue(guestMouseId, out ChangeTracker tracker))
        {
            tracker.change.Transform.Pos = message.Payload.mPos;
            tracker.gameObject.transform.position = message.Payload.mPos.Convert2d();
            DebugLog($"OnStateFluxGuestInputChanged - moving mouse from '{JsonConvert.SerializeObject(message)}'");

            if(message.Payload.mDown)
            {
                DebugLog($"OnStateFluxGuestInputChanged - guest input changed mouse down '{JsonConvert.SerializeObject(message)}'");
                CreateBombAsHost(message.Payload.mPos.Convert2d());
            }
        }
        else
        {
            CreateMouseAsHost(guestMouseId, message.Payload.mPos.Convert3d());
            DebugLog($"OnStateFluxGuestInputChanged - creating mouse from '{JsonConvert.SerializeObject(message)}'");
        }
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

    static private StateFlux.Model.Color CreateRandomColor()
    {
        return new StateFlux.Model.Color() { Red = Random.Range(0f, 1f), Green = Random.Range(0f, 1f), Blue = Random.Range(0f, 1f), Alpha = 1f };
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StateFlux.Model;
using StateFlux.Unity;
using UnityEngine;


public class GameObjectTracker
{
    private StateFluxClient stateFluxClient;
    private Dictionary<string, ChangeTracker> trackingMap;
    //private Queue<Change2d> changeQueue;

    public GameObjectTracker()
    {
        stateFluxClient = StateFluxClient.Instance;
        trackingMap = new Dictionary<string, ChangeTracker>();
        //changeQueue = new Queue<Change2d>();
    }

    // coroutine that sweeps up deleted trackers
    //public IEnumerator SweepStateAsGuest()
    //{
    //    List<string> ids = new List<string>();
    //    foreach (ChangeTracker changeTracker in trackingMap.Values.Where(t => t.destroy != null))
    //    {
    //        ids.Add(changeTracker.destroy.ObjectID);
    //    }

    //    foreach(string id in ids)
    //    {
    //        trackingMap.Remove(id);
    //    }

    //    yield return new WaitForSeconds(1f);
    //}

    // coroutine that sends out the HostStateChangeMessage messages
    public IEnumerator SendStateAsHost()
    {
        while (true)
        {
            // send changes to mouse pos and tracked objects to the server, every 50 milliseconds
            //IEnumerable<ChangeTracker> changes = trackingMap.Values.Where(t => t.dirty);

            List<Change2d> changes = new List<Change2d>();
            List<string> removeIds = new List<string>();
            foreach(ChangeTracker changeTracker in trackingMap.Values.Where(t=>t.dirty))
            {
                changeTracker.dirty = false;

                if (changeTracker.create != null)
                {
                    //DebugLog($"Adding change tracker for {changeTracker.create.ObjectID}");
                    changes.Add(changeTracker.create);
                    changeTracker.create = null;
                }
                else if(changeTracker.destroy != null)
                {
                    //DebugLog($"Removing change tracker for {changeTracker.destroy.ObjectID}");
                    changes.Add(changeTracker.destroy);
                    removeIds.Add(changeTracker.destroy.ObjectID);
                    changeTracker.destroy = null;
                }
                else
                {
                    //DebugLog($"Updating change tracker for {changeTracker.update.ObjectID}");
                    changes.Add(changeTracker.update);
                }
            }

            foreach(string objectId in removeIds)
            {
                trackingMap.Remove(objectId);
            }

            if (changes.Count > 0)
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

            yield return new WaitForSeconds(0.1f);
        }
    }

    public void TrackCreate(ChangeTracker changeTracker)
    {
        trackingMap.Add(changeTracker.create.ObjectID, changeTracker);
        //changeQueue.Enqueue(changeTracker.change);
    }

    public int Count()
    {
        return trackingMap.Count;
    }

    // loads the prefab named the same as change.TypeID, applies caption, color & position
    public GameObject StateCreateGameObject(Change2d change, bool asHost)
    {
        var prefabPath = $"{change.TypeID}";
        //DebugLog($"StateCreateGameObject is attempting to instantiate prefab '{prefabPath}' for '{change.ObjectID}'", true);
        var obj = (GameObject)GameObject.Instantiate(Resources.Load(prefabPath));
        if (obj == null)
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
            //var rigidbody = obj.GetComponent<Rigidbody2D>();
            //if (rigidbody != null)
            //{
            //    // guest objects don't feel gravity
            //    //rigidbody.gravityScale = 0;
            //}

            // guest objects don't destroy themselves, so remove KillMeOverTime
            var killMeOverTime = obj.GetComponent<KillMeOverTime>();
            if (killMeOverTime != null) GameObject.Destroy(killMeOverTime);
        }
        obj.AddComponent<StateFluxTracked>();
        obj.transform.position = change.Transform.Pos.Convert3d();
        obj.transform.eulerAngles = new Vector3(0,0,change.Transform.Rot);
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if(rb != null)
        {
            rb.velocity = change.Transform.Vel.Convert2d();
            rb.angularVelocity = change.Transform.RotV;
        }

        return obj;
    }


    // called when the game is guest (not hosting), contains state changes broadcast from the host
    public void OnHostStateChanged(HostStateChangedMessage message)
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
                trackingMap[change.ObjectID] = new ChangeTracker { gameObject = createdGameObject, create = change };
                //DebugLog($"Created tracker for {change.TypeID} - {change.ObjectID}.", true);
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
                    var createdGameObject = StateCreateGameObject(change, false);
                    tracker = trackingMap[change.ObjectID] = new ChangeTracker { gameObject = createdGameObject, update = change };
                    //DebugLog($"Created tracker for {change.TypeID} - {change.ObjectID}.", true);
                    //DebugLog($"Host has asked us to update object {change.ObjectID} but it does not exist. (Skipping)", true);
                    //continue;
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
                tracker.gameObject.transform.eulerAngles = new Vector3(0, 0, change.Transform.Rot);
                Rigidbody2D rb = tracker.gameObject.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = change.Transform.Vel.Convert2d();
                    rb.angularVelocity = change.Transform.RotV;
                }
            }
        }

    }

    public void OnTrackedObjectChange(string name, Vector3 pos, Vector3 vel, Vector3 eulerAngles, float angularVelocity)
    {
        if (trackingMap.TryGetValue(name, out ChangeTracker tracker))
        {
            // don't send guest state changes to the host, guest sends input and commands instead
            if (stateFluxClient.isHosting)
            {
                if(tracker.update == null)
                {
                    tracker.update = new Change2d();
                    tracker.update.ObjectID = name;
                    tracker.update.Transform = new Transform2d();
                }
                tracker.update.Event = ChangeEvent.Updated;
                tracker.update.Transform.Pos = pos.Convert2d();
                tracker.update.Transform.Vel = vel.Convert2d();
                tracker.update.Transform.Rot = eulerAngles.z;
                tracker.update.Transform.RotV = angularVelocity;
                tracker.dirty = true;
                //changeQueue.Enqueue(tracker.change);
            }
        }
    }

    public void OnTrackedObjectDestroy(string name)
    {
        if (trackingMap.TryGetValue(name, out ChangeTracker tracker))
        {
            if (stateFluxClient.isHosting)
            {
                tracker.destroy = new Change2d();
                tracker.destroy.ObjectID = name;
                tracker.destroy.Event = ChangeEvent.Destroyed;
                tracker.dirty = true;
                
//                changeQueue.Enqueue(tracker.change);
                //DebugLog($"OnTrackedObjectDestroy, queued destroy change for '{name}'");
            }
            else
            {            
                trackingMap.Remove(name);

                //DebugLog($"OnTrackedObjectDestroy, removed tracker for '{name}'");
            }
            //trackingMap.Remove(name); // place in remove queue?
            //DebugLog($"Removed tracker for {name}.");

        }
        else
        {
            DebugLog($"OnTrackedObjectDestroy, failed to look up tracking map for '{name}'");
        }
    }

    public void EnqueuePosChangeAsHost(string objectId, Vector3 pos)
    {
        if (trackingMap.TryGetValue(objectId, out ChangeTracker tracker))
        {
            //Debug.Log($"EnqueuePosChangeAsHost: found tracker object for {objectId}");
            Change2d change = tracker.update;
            if(tracker.update == null)
            {
                tracker.update = new Change2d();
                tracker.update.ObjectID = objectId;
                tracker.update.Transform = new Transform2d();
            }
            if (change.Transform.Pos.X != pos.x || change.Transform.Pos.Y != pos.y)
            {
                //DebugLog($"EnqueuePosChangeAsHost: position changeds for {objectId}, so queueing...", true);
                change.Event = ChangeEvent.Updated;
                change.Transform.Pos = pos.Convert2d();
                tracker.dirty = true;
                //changeQueue.Enqueue(change);
            }
        }
        else
        {
            Debug.Log($"EnqueuePosChangeAsHost: failed to track {objectId}");
        }
    }


    static private void SetObjectColor(GameObject gameObject, StateFlux.Model.Color newColor)
    {
        UnityEngine.Color color = new UnityEngine.Color(newColor.Red, newColor.Green, newColor.Blue, newColor.Alpha);
        var spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        //var textMesh = gameObject.GetComponentInChildren<TextMesh>();
        if (spriteRenderer != null) spriteRenderer.color = color;
        //if (textMesh != null) textMesh.color = color;
    }

    static private void SetObjectText(GameObject gameObject, string newText)
    {
        var textMesh = gameObject.GetComponentInChildren<TextMesh>();
        if (textMesh != null) textMesh.text = newText;
    }


    public void Stop()
    {
        foreach(ChangeTracker changeTracker in trackingMap.Values)
        {
            if(changeTracker.gameObject != null)
            {
                // we have to stop our script on these gameobjects - or else they will call back to notify they are destroyed
                var stateFluxTracked = changeTracker.gameObject.GetComponent<StateFluxTracked>();
                stateFluxTracked.enabled = false;
                GameObject.Destroy(changeTracker.gameObject);
            }
        }
    }

    private void DebugLog(string msg, bool focus = false)
    {
        Debug.Log(msg);
    }


}

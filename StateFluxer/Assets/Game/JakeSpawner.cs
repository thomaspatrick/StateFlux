using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateFlux.Client;
using StateFlux.Model;
using StateFlux.Unity;
public class JakeSpawner : MonoBehaviour
{
    public GameObject Prefab;
    public float TargetNum=200;
    void FixedUpdate()
    {
        if(StateFluxClient.Instance.isHosting)
        {
            if(this.transform.childCount<TargetNum)
            {
               ChangeTracker changeTracker=CreateJake(this.transform.position, new StateFlux.Model.Color() { Red = UnityEngine.Color.blue.r, Blue = UnityEngine.Color.blue.b, Green = UnityEngine.Color.blue.g, Alpha = 1f }, this.gameObject);
                DemoGame.gameObjectTracker.TrackCreate(changeTracker);
            }
        }
    }
    private ChangeTracker CreateJake(Vector3 mousePoint, StateFlux.Model.Color color, GameObject parent = null)
    {
        var change = new StateFlux.Model.Change2d
        {
            Event = ChangeEvent.Created,
            ObjectID = "liquid_jake" + ShortGuid.Generate(),
            TypeID = "liquid_jake",
            ParentID = parent?.name,
            Transform = new Transform2d
            {
                Pos = mousePoint.Convert2d(),
                Vel = new Vec2d { X = 0, Y = 0 },
                Rot = 0, //UnityEngine.Random.value * 720.0f - 360.0f,
                RotV = UnityEngine.Random.value * 100.0f - 50.0f,
                Scale = UnityEngine.Random.value/10f
            },
            Attributes = new StateFlux.Model.Attributes
            {
                Color = color
            }
        };
        GameObject jake = DemoGame.gameObjectTracker.StateCreateGameObject(change, StateFluxClient.Instance.isHosting);
        return new ChangeTracker { gameObject = jake, create = change };
    }
}

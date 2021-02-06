using UnityEngine;

namespace StateFlux.Unity
{
    public interface IGameObjectTrackingListener
    {
        void OnTrackedObjectChange(string name, Vector3 pos, Vector3 vel, Vector3 eulerAngles, float angularVelocity, float scale);
        void OnTrackedObjectDestroy(string name);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StateFlux.Unity
{
    class StateFluxTracked : MonoBehaviour
    {
        private Rigidbody2D _rigidBody;
        private IGameObjectTrackingListener _listener;

        public void SetListener(IGameObjectTrackingListener listener)
        {
            _listener = listener;
        }

        void Update()
        {
            if(_rigidBody == null)
            {
                _rigidBody = GetComponent<Rigidbody2D>();
            }

            if (transform.hasChanged)
            {
                Vector3 vel = (_rigidBody != null) ? _rigidBody.velocity : Vector2.zero;
                float angularVelocity = (_rigidBody != null) ? _rigidBody.angularVelocity : 0f;

                _listener?.OnTrackedObjectChange(name, transform.position, vel, transform.eulerAngles, angularVelocity );
                transform.hasChanged = false;
            }
        }

        void OnDestroy()
        {
            _listener?.OnTrackedObjectDestroy(name);
        }
    }
}

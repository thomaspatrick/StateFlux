using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.StateFlux.Unity
{
    class SceneAwakeNotifier : MonoBehaviour
    {
        public GameObject target;
        public void Awake()
        {
            LobbyManager.Instance.SendMessage("OnSceneAwake");
        }
    }
}

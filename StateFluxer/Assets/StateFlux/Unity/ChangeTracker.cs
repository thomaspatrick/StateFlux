using StateFlux.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StateFlux.Unity
{
    public enum ChangeTrackerSource { Host, Guest };
    class ChangeTracker
    {
        public ChangeTrackerSource source { get; set; }
        public Change2d change { get; set; }
        public GameObject gameObject { get; set; }
    }
}

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
    public class ChangeTracker
    {
        public ChangeTrackerSource source { get; set; }

        public Change2d create { get; set; }
        public Change2d update { get; set; }
        public Change2d destroy { get; set; }
        public GameObject gameObject { get; set; }
        public bool dirty { get; set; }
    }
}

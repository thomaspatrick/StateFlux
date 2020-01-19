using System.Collections.Generic;

namespace StateFlux.Model
{
    public class Vector2d
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class Transform2d
    {
        public Vector2d Pos { get; set; }
        public Vector2d Vel { get; set; }
        public double Rot { get; set; }
        public double Scale { get; set; }
    }

    public class Measures
    {
        public double Health { get; set; }
        public double Ammo { get; set; }
        public double Fuel { get; set; }
    }

    public enum ChangeEvent { Created, Updated, Destroyed }
    public class Change2d
    {
        public string ObjectID { get; set; }
        public ChangeEvent Event { get; set; }
        public Transform2d Transform { get; set; }
        public Measures Measures { get; set; }
    }

    public class StateChange
    {
        public List<Change2d> changes;
    }

}

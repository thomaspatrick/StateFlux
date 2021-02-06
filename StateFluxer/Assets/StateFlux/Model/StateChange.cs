using System.Collections.Generic;

namespace StateFlux.Model
{
    public class Vec2d
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class Transform2d
    {
        public Vec2d Pos { get; set; }
        public Vec2d Vel { get; set; }
        public float Rot { get; set; }
        public float RotV { get; set; }
        public float Scale { get; set; }
    }

    public class Measures
    {
        public float Health { get; set; }
        public float Ammo { get; set; }
        public float Fuel { get; set; }
    }

    public class Attributes
    {
        public Color Color { get; set; }
    }

    public class Color
    {
        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }
        public float Alpha { get; set; }
    }

    public enum ChangeEvent { Created, Updated, Destroyed }
    public class Change2d
    {
        public string ObjectID { get; set; }
        public string TypeID { get; set; }
        public ChangeEvent Event { get; set; }
        public Transform2d Transform { get; set; }
        public Measures Measures { get; set; }
        public Attributes Attributes { get; set; }
    }

    public class StateChange
    {
        public List<Change2d> Changes { get; set; }
    }

}

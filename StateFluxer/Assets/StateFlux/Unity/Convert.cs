using StateFlux.Model;
using UnityEngine;

namespace StateFlux.Unity
{
    public static class StateFluxTypeConvert
    {
        public static Vec2d Convert3d(this Vector3 vec)
        {
            return new Vec2d { X = vec.x, Y = vec.y };
        }
        public static Vec2d Convert2d(this UnityEngine.Vector3 vec)
        {
            return new Vec2d { X = vec.x, Y = vec.y };
        }
        public static Vec2d Convert2d(this UnityEngine.Vector2 vec)
        {
            return new Vec2d { X = vec.x, Y = vec.y };
        }
        public static Vector3 Convert3d(this Vec2d vec)
        {
            return new Vector3 { x = vec.X, y = vec.Y };
        }
        public static Vector2 Convert2d(this Vec2d vec)
        {
            return new Vector2 { x = vec.X, y = vec.Y };
        }
        public static UnityEngine.Color Convert(this StateFlux.Model.Color sfColor)
        {
            return new UnityEngine.Color(sfColor.Red, sfColor.Green, sfColor.Blue, sfColor.Alpha);
        }
        public static StateFlux.Model.Color Convert(this UnityEngine.Color uColor)
        {
            return new StateFlux.Model.Color { Red = uColor.r, Green = uColor.g, Blue = uColor.b, Alpha = uColor.a };
        }
    }
}

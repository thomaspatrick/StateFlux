using StateFlux.Model;
using StateFlux.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class MouseTracker
{
    public Mouse mouse;
    public GameObject gameObject;
    public bool dirty;
}

public class MiceTracker
{
    private Dictionary<string, MouseTracker> trackers = new Dictionary<string, MouseTracker>();

    public MiceTracker()
    {
    }

    public void GUIDescribe()
    {
        foreach (MouseTracker mt in trackers.Values.AsEnumerable())
        {
            GUILayout.Label("Mouse for PlayerId " + mt.mouse.PlayerId + ":");
            GUILayout.Label("\tPos: (" + mt.mouse.Pos.X + "," + mt.mouse.Pos.Y + ")");
            GUILayout.Label("\tDirty: " + mt.dirty);
        }
    }

    public Mouse Find(string playerId)
    {
        Mouse m = null;
        if (trackers.ContainsKey(playerId))
        {
            m = trackers[playerId].mouse;
        }
        return m;
    }

    public void Track(string playerId, Vec2d pos)
    {
        if(!trackers.ContainsKey(playerId))
        {
            trackers[playerId] = new MouseTracker
            {
                dirty = true,
                mouse = new Mouse
                {
                    PlayerId = playerId,
                    Pos = pos
                }
            };
        }
        else
        {
            MouseTracker mouseTracker = trackers[playerId];
            if(mouseTracker.mouse.Pos.X != pos.X || mouseTracker.mouse.Pos.Y != pos.Y)
            {
                mouseTracker.dirty = true;
                mouseTracker.mouse.Pos = pos;
            }
        }
    }

    public Mice BuildMice()
    {
        List<Mouse> items = null;
        foreach(MouseTracker mt in trackers.Values.AsEnumerable())
        {
            if(mt.dirty)
            {
                if (items == null) items = new List<Mouse>();
                items.Add(mt.mouse);
                mt.dirty = false;
            }
        }
        if(items==null) return null;
        return new Mice { Items = items };
    }
}

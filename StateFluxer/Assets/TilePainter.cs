using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
[System.Serializable]
public class TileInstance
{
    public TileInstance(Vector2Int Pos,Color Col)
    {
        Position = Pos;
        Color = Col;
    }
    public Vector2Int Position;
    public Color Color;
    public override bool Equals(object obj)
    {
        return Position.Equals(((TileInstance)obj).Position);
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode();
    }
}
public class TilePainter : MonoBehaviour
{
    private Tilemap map;
    public Tile GenericTile;
    public Color MyColor;
    public List<TileInstance> Tiles;
    private void Start()
    {
        map = this.GetComponent<Tilemap>();
        Tiles = new List<TileInstance>();
        map.ClearAllTiles();
    }
    private void FixedUpdate()
    {
        if (Input.GetMouseButton(0))
        {
            Vector2Int MousePos = toVector2Int(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            if (!Tiles.Contains(new TileInstance(MousePos, MyColor)))
            {
                SetTile(MousePos, MyColor);
            }
        } 
        if (Input.GetMouseButton(1))
        {

            Vector2Int MousePos = toVector2Int(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            if (Tiles.Contains(new TileInstance(MousePos, MyColor)))
            {
                RemoveTile(MousePos);
            }
        }
    }

    public void SetTile(Vector2Int Pos, Color Col)
    {

        Tiles.Remove(new TileInstance(Pos, MyColor));
        Tiles.Add(new TileInstance(Pos, MyColor));
        GenericTile.color = Col;
        Vector3Int Coords = new Vector3Int(Pos.x, Pos.y, 0);
        map.SetTile(Coords, null);
        map.SetTile(Coords, GenericTile);
    }   
    public void RemoveTile(Vector2Int Pos)
    {
        Tiles.Remove(new TileInstance(Pos, MyColor));
        Vector3Int Coords = new Vector3Int(Pos.x, Pos.y, 0);
        map.SetTile(Coords, null);
    }
    private Vector2Int toVector2Int(Vector2 Pos)
    {
        return new Vector2Int(Mathf.RoundToInt(Pos.x), Mathf.RoundToInt(Pos.y));
    }
    private Vector2Int toVector2Int(Vector3 Pos)
    {
        return new Vector2Int(Mathf.RoundToInt(Pos.x), Mathf.RoundToInt(Pos.y));
    }
}

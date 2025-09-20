using System;
using System.Collections.Generic;
using Game.Save;

[Serializable]
public class MapLayoutSnapshot
{
    public int Seed;
    public List<MapLayoutNodeSnapshot> Nodes = new();
}

[Serializable]
public class MapLayoutNodeSnapshot
{
    public int Floor;
    public int Index;
    public NodeType NodeType;
    public float PositionX;
    public float PositionY;
    public string EventIdOverride;
    public List<MapLayoutEdge> Children = new();
}

[Serializable]
public struct MapLayoutEdge
{
    public int Floor;
    public int Index;
}

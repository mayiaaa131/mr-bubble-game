using System;
using System.Collections.Generic;

/// <summary>
/// 3D变换数据（位置、旋转、缩放）
/// </summary>
[Serializable]
public class MapTransform
{
    public float x;
    public float y;
    public float z;
}

/// <summary>
/// 地图物体数据
/// </summary>
[Serializable]
public class MapGameObject
{
    public int prefabIndex;
    public string prefabName;
    public MapTransform position;
    public MapTransform rotation;
    public MapTransform scale;
}

/// <summary>
/// 地图信息
/// </summary>
[Serializable]
public class MapInfo
{
    public string type;              // "MapData"
    public string mapName;
    public string savedAt;
    public List<MapGameObject> objects;

    public MapInfo()
    {
        objects = new List<MapGameObject>();
    }
}

/// <summary>
/// 地图广播消息（服务端 → 客户端）
/// </summary>
[Serializable]
public class MapBroadcastMessage
{
    public string type;              // "MapData"
    public string roomId;
    public string mapName;
    public long timestamp;
    public List<MapGameObject> objects;

    public MapBroadcastMessage()
    {
        objects = new List<MapGameObject>();
    }
}

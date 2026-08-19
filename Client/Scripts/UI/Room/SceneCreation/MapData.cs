using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapData
{
    public string mapName;
    public string savedAt;
    public string type;  // ★ 新增：用于兼容编辑器导出的JSON
    public List<MapObject> objects = new List<MapObject>();
}

[System.Serializable]
public class MapObject
{
    public int prefabIndex;  // ★ 新增：Prefab 索引
    public string prefabName;  // ★ 改为 prefabName 而不是 prefabId
    public SerializableVector3 position = new SerializableVector3();
    public SerializableVector3 rotation = new SerializableVector3();
    public SerializableVector3 scale = new SerializableVector3 { x = 1, y = 1, z = 1 };
}

[System.Serializable]
public class SerializableVector3
{
    public float x, y, z;
    public Vector3 ToVector3( ) => new Vector3(x, y, z);
}

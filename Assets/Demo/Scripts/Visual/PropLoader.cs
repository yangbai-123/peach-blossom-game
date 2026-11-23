using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 开源道具（CC0，Kenney 3D Platformer Kit）。
///
/// GLB 已经在编辑器里烘成 prefab（见 PropImporter），运行时只做 Instantiate，
/// 不需要再解析 glTF —— 运行时解析在 IL2CPP 下会踩 AOT 裁剪的坑。
/// </summary>
public static class PropLoader
{
    static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

    static readonly string[] Names = { "cloud", "island", "grass", "grass_small", "flag" };

    public static bool Ready { get; private set; }

    public static void LoadAll(System.Action onReady)
    {
        Cache.Clear();

        foreach (var name in Names)
        {
            var prefab = Resources.Load<GameObject>("Props/" + name);
            if (prefab != null) Cache[name] = prefab;
            else Debug.LogWarning("[prop] 缺少 Resources/Props/" + name + "（先跑 Tools/桃夭/导入开源道具）");
        }

        Ready = true;
        Debug.Log($"[prop] 就绪：{Cache.Count}/{Names.Length}");
        onReady?.Invoke();
    }

    public static bool Has(string name) => Cache.ContainsKey(name) && Cache[name] != null;

    /// <summary>实例化一份道具。</summary>
    public static GameObject Spawn(string name, Vector3 position, float scale, float yaw = 0f)
    {
        if (!Cache.TryGetValue(name, out var prefab) || prefab == null) return null;

        var go = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f));
        go.name = "Prop_" + name;
        go.SetActive(true);
        go.transform.localScale = Vector3.one * scale;

        // 道具不参与碰撞（地图碰撞保持程序化几何，避免随机卡住玩家）
        foreach (var c in go.GetComponentsInChildren<Collider>())
            Object.Destroy(c);

        return go;
    }
}

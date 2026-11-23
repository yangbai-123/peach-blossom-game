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

        // 模型自带的贴图在 WebGL 下解码不出来（会变白），统一改成纯色，
        // 和场景现有的低模配色保持一致
        Color tint = name switch
        {
            "grass" => new Color(0.62f, 0.80f, 0.52f),
            "grass_small" => new Color(0.68f, 0.84f, 0.56f),
            "island" => new Color(0.70f, 0.86f, 0.62f),
            "flag" => new Color(0.98f, 0.86f, 0.55f),
            _ => new Color(0.96f, 0.97f, 1f),
        };

        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            var mat = renderer.material;   // 实例化材质，避免改动共享资源
            if (mat != null && mat.HasProperty("_Color")) mat.color = tint;
        }

        // 诊断：确认实例真的有可见网格
        if (Debug.isDebugBuild || Application.isEditor || true)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var filters = go.GetComponentsInChildren<MeshFilter>(true);

            string bounds = "无";
            var meshFilter = filters.Length > 0 ? filters[0] : null;
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var size = meshFilter.sharedMesh.bounds.size;
                bounds = $"{size.x:0.##}×{size.y:0.##}×{size.z:0.##}";
            }
            else if (renderers.Length > 0)
            {
                var b = renderers[0].bounds.size;
                bounds = $"r:{b.x:0.##}×{b.y:0.##}×{b.z:0.##}";
            }

            string shader = "无";
            if (renderers.Length > 0 && renderers[0].sharedMaterial != null)
                shader = renderers[0].sharedMaterial.shader != null ? renderers[0].sharedMaterial.shader.name : "null shader";

            Debug.Log($"[prop] 生成 {name} pos={position} scale={scale} 网格={filters.Length} 渲染器={renderers.Length} 尺寸={bounds} 着色器={shader}");
        }

        // 道具不参与碰撞（地图碰撞保持程序化几何，避免随机卡住玩家）
        foreach (var c in go.GetComponentsInChildren<Collider>())
            Object.Destroy(c);

        return go;
    }
}

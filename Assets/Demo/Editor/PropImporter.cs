#if UNITY_EDITOR
using System.IO;
using System.Threading.Tasks;
using UniGLTF;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把开源 GLB 道具在编辑器里烘成 prefab。
///
/// 运行时直接解析 GLB 在 IL2CPP 下会报 "IStorage.Get failed"（AOT 裁剪问题），
/// 编辑器里没有这个限制，所以提前转好，运行时只做 Instantiate。
/// 菜单：Tools / 桃夭 / 导入开源道具
/// 命令行：-executeMethod PropImporter.ImportAll
/// </summary>
public static class PropImporter
{
    const string SourceDir = "Assets/Demo/Resources/Models/props";
    const string TargetDir = "Assets/Demo/Resources/Props";

    static readonly string[] Names = { "cloud", "island", "grass", "grass_small", "flag" };

    [MenuItem("Tools/桃夭/导入开源道具")]
    public static void ImportAll()
    {
        if (!Directory.Exists(TargetDir)) Directory.CreateDirectory(TargetDir);

        int ok = 0;

        foreach (var name in Names)
        {
            var assetPath = $"{SourceDir}/{name}.bytes";
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);

            if (text == null)
            {
                Debug.LogWarning("[prop] 缺少 " + assetPath);
                continue;
            }

            var root = Bake(text.bytes, name);
            if (root == null) continue;

            var prefabPath = $"{TargetDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            ok++;
            Debug.Log("[prop] 已生成 " + prefabPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[prop] 导入完成：{ok}/{Names.Length}");

        if (Application.isBatchMode && ok == 0) EditorApplication.Exit(1);
    }

    /// <summary>把运行时生成的 mesh / material / texture 落成资产文件。</summary>
    static void PersistAssets(GameObject root, string name)
    {
        var sharedDir = $"{TargetDir}/Shared";
        if (!AssetDatabase.IsValidFolder(sharedDir))
            AssetDatabase.CreateFolder(TargetDir, "Shared");

        int index = 0;

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null || AssetDatabase.Contains(mesh)) continue;

            var path = $"{sharedDir}/{name}_mesh_{index++}.asset";
            AssetDatabase.CreateAsset(mesh, path);
        }

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                if (mat.mainTexture != null && !AssetDatabase.Contains(mat.mainTexture))
                {
                    var texPath = $"{sharedDir}/{name}_tex_{index++}.asset";
                    AssetDatabase.CreateAsset(mat.mainTexture, texPath);
                }

                if (!AssetDatabase.Contains(mat))
                {
                    var matPath = $"{sharedDir}/{name}_mat_{index++}.asset";
                    AssetDatabase.CreateAsset(mat, matPath);
                }
            }
        }

        AssetDatabase.SaveAssets();
    }

    static GameObject Bake(byte[] bytes, string name)
    {
        try
        {
            // GLB 里引用了外部贴图 Textures/colormap.png，
            // 必须放到同一个目录结构下，走文件路径接口才能解析
            var tempDir = Path.Combine(Application.temporaryCachePath, "propbake", name);
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.Combine(tempDir, "Textures"));

            var glbPath = Path.Combine(tempDir, name + ".glb");
            File.WriteAllBytes(glbPath, bytes);

            var texturePath = Path.Combine(SourceDir, "Textures", "colormap.png");
            if (File.Exists(texturePath))
                File.Copy(texturePath, Path.Combine(tempDir, "Textures", "colormap.png"), true);
            else
                Debug.LogWarning("[prop] 缺少 colormap.png，模型可能没有贴图");

            var generator = new UniGLTF.BuiltInGltfMaterialDescriptorGenerator();

            Task<RuntimeGltfInstance> task = GltfUtility.LoadAsync(glbPath, new ImmediateCaller());
            task.GetAwaiter().GetResult();

            var instance = task.Result;
            if (instance == null)
            {
                Debug.LogError($"[prop] {name} 解析结果为 null");
                return null;
            }

            instance.ShowMeshes();

            var root = instance.Root;
            root.name = name;

            foreach (var c in root.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c, true);

            // 关键：模型/材质/贴图此刻还是内存里的临时对象，
            // 不存成资源的话 prefab 里会变成空引用（材质丢失、网格丢失）
            PersistAssets(root, name);

            return root;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[prop] {name} 烘焙失败: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }
}
#endif

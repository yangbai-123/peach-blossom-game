#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成运行时依赖的资源资产：
///   - Resources/Materials/Base.mat：让 Standard 着色器一定被打进包（否则 WebGL 里全洋红）
/// 命令行：Unity.exe -batchmode -quit -executeMethod CreateAssets.Build
/// </summary>
public static class CreateAssets
{
    const string Dir = "Assets/Demo/Resources/Materials";
    const string Path = Dir + "/Base.mat";

    [MenuItem("Tools/桃夭/生成基础资源")]
    public static void Build()
    {
        if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

        var shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("[CreateAssets] 找不到 Standard 着色器");
            return;
        }

        var existing = AssetDatabase.LoadAssetAtPath<Material>(Path);
        if (existing != null)
        {
            existing.shader = shader;
            EditorUtility.SetDirty(existing);
        }
        else
        {
            var mat = new Material(shader) { name = "Base" };
            if (mat.HasProperty("_Color")) mat.color = Color.white;
            AssetDatabase.CreateAsset(mat, Path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateAssets] 基础材质已就绪：" + Path);
    }
}
#endif

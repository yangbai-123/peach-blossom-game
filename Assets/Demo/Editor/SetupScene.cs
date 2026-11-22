#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器脚本：自动生成主场景并加入 Build Settings。
/// 菜单：Tools / 桃夭 / 生成主场景
/// 命令行：Unity.exe -batchmode -quit -executeMethod SetupScene.Build
/// </summary>
public static class SetupScene
{
    const string SceneDir = "Assets/Demo/Scenes";
    const string ScenePath = SceneDir + "/Main.unity";

    [MenuItem("Tools/桃夭/生成主场景")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var game = new GameObject("Game");
        game.AddComponent<GameBootstrap>();

        if (!AssetDatabase.IsValidFolder(SceneDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Demo"))
                AssetDatabase.CreateFolder("Assets", "Demo");
            AssetDatabase.CreateFolder("Assets/Demo", "Scenes");
        }

        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SetupScene] 主场景已生成：" + ScenePath);
    }
}
#endif

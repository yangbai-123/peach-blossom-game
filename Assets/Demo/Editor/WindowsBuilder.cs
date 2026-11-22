#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows 独立版打包。
/// 菜单：Tools / 桃夭 / 导出 Windows
/// 命令行：Unity.exe -batchmode -quit -buildTarget Win64 -executeMethod WindowsBuilder.BuildWindows
/// </summary>
public static class WindowsBuilder
{
    const string Scene = "Assets/Demo/Scenes/Main.unity";
    const string OutputDir = "Build/Windows";
    const string ExeName = "PeachBlossom.exe";

    [MenuItem("Tools/桃夭/导出 Windows")]
    public static void BuildWindows()
    {
        CreateAssets.Build();

        // 交付版本的游戏化设置：窗口化、可缩放、后台运行
        PlayerSettings.productName = "PeachBlossom";
        PlayerSettings.companyName = "PeachStudio";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SplashScreen.show = false;

        // 关闭崩溃上报弹窗（本地运行更干净）
        PlayerSettings.usePlayerLog = true;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = $"{OutputDir}/{ExeName}",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        Debug.Log($"[Win] 开始打包 → {options.locationPathName}");
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[Win] 结果={summary.result}  大小={summary.totalSize / (1024f * 1024f):0.0} MB  " +
                  $"耗时={summary.totalTime}  错误={summary.totalErrors}");

        if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }
}
#endif

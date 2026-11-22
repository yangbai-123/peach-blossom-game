#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// WebGL 打包脚本。
/// 菜单：Tools / 桃夭 / 导出 WebGL
/// 命令行：Unity.exe -batchmode -quit -buildTarget WebGL -executeMethod WebGLBuilder.BuildWebGL
/// </summary>
public static class WebGLBuilder
{
    const string Scene = "Assets/Demo/Scenes/Main.unity";
    const string OutputDir = "Build/WebGL";

    [MenuItem("Tools/桃夭/导出 WebGL")]
    public static void BuildWebGL()
    {
        // 先确保运行时依赖的资源资产存在（Base.mat 决定着色器是否被打进包）
        CreateAssets.Build();

        // 关键：开启解压回退。
        // 否则 Unity 输出 .gz，需要服务器带 Content-Encoding: gzip 才能被浏览器解压；
        // 开了回退后由 Unity 自带 JS 解压，任何静态服务器（python -m http.server / npx serve）都能跑。
        PlayerSettings.WebGL.decompressionFallback = true;

        // 关键：关闭引擎代码剥离。
        // 碰撞体/音频/粒子都是运行时 AddComponent 出来的，静态分析看不到引用，
        // 剥离后会报 "class 'SphereCollider' doesn't exist"，导致挥剑判定完全失效。
        PlayerSettings.stripEngineCode = false;

        Debug.Log($"[WebGL] decompressionFallback={PlayerSettings.WebGL.decompressionFallback}, " +
                  $"stripEngineCode={PlayerSettings.stripEngineCode}");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        Debug.Log($"[WebGL] 开始打包，输出目录：{OutputDir}");
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[WebGL] 结果={summary.result}  大小={summary.totalSize / (1024f * 1024f):0.0} MB  " +
                  $"耗时={summary.totalTime}  错误={summary.totalErrors}");

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[WebGL] 打包失败");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
#endif

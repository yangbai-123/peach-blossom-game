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

        // MToon 是 VRM 模型用的着色器，运行时通过 Shader.Find 取，
        // 不放进去会被剥离，模型会变成洋红。
        EnsureShaderIncluded("VRM/MToon");

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

        if (summary.result == BuildResult.Succeeded)
            PatchIndexForMobile();

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[WebGL] 打包失败");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    /// <summary>把着色器加入 Always Included Shaders，避免运行时 Shader.Find 拿到 null。</summary>
    static void EnsureShaderIncluded(string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[WebGL] 找不到着色器：{shaderName}");
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (asset == null)
        {
            Debug.LogWarning("[WebGL] 读不到 GraphicsSettings.asset");
            return;
        }

        var so = new SerializedObject(asset);
        var array = so.FindProperty("m_AlwaysIncludedShaders");
        if (array == null) return;

        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
        }

        array.InsertArrayElementAtIndex(array.arraySize);
        array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.Log($"[WebGL] 已加入 Always Included Shaders：{shaderName}");
    }

    /// <summary>
    /// Unity 默认模板只在识别到手机 UA 时才让画布铺满屏幕，
    /// 桌面浏览器 / 某些横屏环境仍会保持 960×600。
    /// 这里直接注入 CSS 强制铺满，保证触屏坐标与屏幕一致。
    /// </summary>
    static void PatchIndexForMobile()
    {
        var indexPath = System.IO.Path.Combine(OutputDir, "index.html");
        if (!System.IO.File.Exists(indexPath)) return;

        var html = System.IO.File.ReadAllText(indexPath);
        if (html.Contains("dsh-fullscreen")) return;

        const string patch = @"
<style id=""dsh-fullscreen"">
  html, body { width: 100%; height: 100%; margin: 0; padding: 0; overflow: hidden; background: #0d1117; }
  #unity-container, #unity-fullscreen-container { width: 100% !important; height: 100% !important; }
  #unity-canvas { width: 100% !important; height: 100% !important; display: block; touch-action: none; }
  #unity-footer { display: none !important; }

  /* 竖屏时把画布转 90°，让游戏以横屏比例显示。
     不支持方向锁定的内置浏览器（比如从聊天软件里打开）靠这个兜底。
     C# 侧会用 MobileOrientation.IsRotated 还原触摸坐标。 */
  @media (orientation: portrait) {
    #unity-container {
      position: fixed !important;
      left: 50% !important;
      top: 50% !important;
      width: 100vh !important;
      height: 100vw !important;
      margin: 0 !important;
      transform: translate(-50%, -50%) rotate(-90deg) !important;
      transform-origin: center center !important;
    }
    #unity-fullscreen-container, #unity-canvas {
      width: 100% !important;
      height: 100% !important;
    }
  }
</style>
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover"">
</head>";

        html = html.Replace("</head>", patch);
        System.IO.File.WriteAllText(indexPath, html);
        Debug.Log("[WebGL] 已注入全屏 CSS（触屏坐标对齐）");
    }
}
#endif

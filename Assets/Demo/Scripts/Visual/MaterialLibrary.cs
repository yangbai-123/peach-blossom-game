using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 材质库。
/// 关键点：打包时 Shader.Find 拿不到没被引用的着色器（会被剥离，表现为全洋红），
/// 所以这里优先加载 Resources 里的材质资产，那个资产的着色器一定会被打进包里。
/// </summary>
public static class MaterialLibrary
{
    static Material _base;
    static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

    public static Material Base()
    {
        if (_base != null) return _base;

        _base = Resources.Load<Material>("Materials/Base");

        if (_base == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) _base = new Material(shader);
        }

        return _base;
    }

    /// <summary>按颜色取材质（颜色相同则复用同一个实例）。</summary>
    public static Material For(Color color)
    {
        var baseMat = Base();
        if (baseMat == null) return null;

        int key = ((Color32)color).GetHashCode();
        if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var mat = new Material(baseMat);
        if (mat.HasProperty("_Color")) mat.color = color;
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);

        Cache[key] = mat;
        return mat;
    }
}

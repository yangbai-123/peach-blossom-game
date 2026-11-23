using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 移动端强制横屏。
///
/// 浏览器只在全屏状态下允许锁定方向，所以第一次点击（用户手势）时
/// 请求全屏 + orientation.lock('landscape')。不支持的浏览器（如 iOS Safari）
/// 会退化成"提示用户手动旋转"，HUD 里会显示横屏提示。
/// </summary>
public static class MobileOrientation
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void DSH_EnterLandscape();
    [DllImport("__Internal")] static extern int DSH_IsPortrait();
    [DllImport("__Internal")] static extern int DSH_IsFullscreen();
#endif

    static bool _requested;
    static bool _logged;

    /// <summary>当前是不是竖屏。</summary>
    public static bool IsPortrait
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return DSH_IsPortrait() == 1; }
            catch { }
#endif
            return Screen.height > Screen.width;
        }
    }

    public static bool IsFullscreen
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return DSH_IsFullscreen() == 1; }
            catch { }
#endif
            return false;
        }
    }

    /// <summary>请求横屏（只在移动端、且只请求一次）。</summary>
    public static void RequestLandscape()
    {
        if (_requested) return;
        _requested = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            DSH_EnterLandscape();

            if (!_logged)
            {
                _logged = true;
                Debug.Log("[orientation] 已请求全屏 + 横屏锁定");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[orientation] 请求横屏失败: " + e.Message);
        }
#endif
    }
}

using UnityEngine;

/// <summary>
/// 战斗表现层：招架、必杀的瞬时反馈（屏幕特效 + 大字）。
/// HUD 每帧读这里的窗口时间，不需要事件订阅。
/// </summary>
public static class CombatFx
{
    static float _parryUntil;
    static float _superUntil;
    static float _superStartedAt;

    /// <summary>招架成功：金色闪光。</summary>
    public static void Parry()
    {
        _parryUntil = Time.unscaledTime + 0.45f;
    }

    /// <summary>必杀发动：切镜式的橙色横幅。</summary>
    public static void SuperCutin()
    {
        _superUntil = Time.unscaledTime + 1.0f;
        _superStartedAt = Time.unscaledTime;
    }

    public static float ParryAlpha =>
        Mathf.Clamp01((_parryUntil - Time.unscaledTime) / 0.45f);

    public static float SuperAlpha =>
        Mathf.Clamp01((_superUntil - Time.unscaledTime) / 1.0f);

    /// <summary>横幅从左右滑入的程度 0..1</summary>
    public static float SuperSlide
    {
        get
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _superStartedAt) / 0.22f);
            return t * t * (3f - 2f * t);   // smoothstep
        }
    }
}

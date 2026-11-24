using UnityEngine;

/// <summary>
/// 触屏输入中转站。
/// PlayerController / PlayerCombat / ThirdPersonCamera 都先读这里，
/// 没有触屏设备时 Enabled 为 false，完全走键鼠老逻辑。
/// </summary>
public static class MobileInput
{
    /// <summary>是否启用触屏控制（自动检测，也可按 T 手动切换）。</summary>
    public static bool Enabled;

    /// <summary>摇杆方向，-1..1。x 右为正，y 前为正（已处理好屏幕坐标翻转）。</summary>
    public static Vector2 Move;

    /// <summary>是否处于冲刺（点击冲刺键切换）。</summary>
    public static bool Sprint;

    /// <summary>格挡键按住状态（触屏按钮）</summary>
    public static bool BlockHeld;

    /// <summary>这一帧视角转动的角度（度）。x 是偏航，y 是俯仰。</summary>
    public static Vector2 LookDelta;

    static bool _jump;
    static bool _attack;
    static bool _viewToggle;

    public static void PressJump() => _jump = true;
    public static void PressAttack() => _attack = true;
    public static void PressViewToggle() => _viewToggle = true;

    public static void PressBlock() => BlockHeld = true;

    public static void ReleaseBlock() => BlockHeld = false;

    public static bool ConsumeJump()
    {
        if (!_jump) return false;
        _jump = false;
        return true;
    }

    public static bool ConsumeAttack()
    {
        if (!_attack) return false;
        _attack = false;
        return true;
    }

    public static bool ConsumeViewToggle()
    {
        if (!_viewToggle) return false;
        _viewToggle = false;
        return true;
    }

    public static void Reset()
    {
        Move = Vector2.zero;
        Sprint = false;
        LookDelta = Vector2.zero;
        _jump = false;
        _attack = false;
        _viewToggle = false;
    }
}

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

    /// <summary>摇杆方向，已归一化，-1..1。</summary>
    public static Vector2 Move;

    /// <summary>是否处于冲刺（点击冲刺键切换）。</summary>
    public static bool Sprint;

    static bool _jump;
    static bool _attack;
    public static Vector2 LookDelta;

    public static void PressJump() => _jump = true;
    public static void PressAttack() => _attack = true;

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

    public static void Reset()
    {
        Move = Vector2.zero;
        Sprint = false;
        _jump = false;
        _attack = false;
        LookDelta = Vector2.zero;
    }
}

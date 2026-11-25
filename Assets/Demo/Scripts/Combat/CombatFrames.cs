using UnityEngine;

/// <summary>招式类型。</summary>
public enum AttackKind
{
    Light,
    Heavy,
    Air,
    Super,
}

/// <summary>
/// 一帧的招式数据。字段含义照搬参考实现的 17 个字段里的核心部分：
/// 起手 / 判定 / 收招（帧）、伤害、射程、判定框上下沿、击退、硬直、顿帧。
///
/// 参考实现用像素坐标（角色高 160px），这里换算成米：
/// 1px ≈ 1.8m / 160 = 0.01125m，所以 reach 108px ≈ 1.215m。
/// </summary>
public struct AttackData
{
    public AttackKind kind;

    public int startup;     // 起手帧
    public int active;      // 判定帧
    public int recover;     // 收招帧

    public float dmg;
    public float reach;     // 判定框远端（米）
    public float y0;        // 判定框下沿（相对脚底）
    public float y1;        // 判定框上沿
    public float kb;        // 击退
    public int stop;        // 命中顿帧
    public int stun;        // 受击硬直帧
    public int bstun;       // 格挡硬直帧
    public int chain;       // 可接连段数
    public float meter;     // 命中回气
    public float lunge;     // 前冲

    public int TotalFrames => startup + active + recover;
    public int ActiveStart => startup + 1;                 // 与参考一致：t > startup 才判定
    public int ActiveEnd => startup + active;
}

/// <summary>帧表 + 帧数换算。</summary>
public static class CombatFrames
{
    public const float Fps = 60f;
    public const float Px = 1.8f / 160f;    // 参考实现 1 像素 ≈ 0.01125 米

    public static readonly AttackData Light = new AttackData
    {
        kind = AttackKind.Light,
        startup = 5, active = 4, recover = 12,
        dmg = 18f,
        reach = 108f * Px,
        y0 = 40f * Px, y1 = 150f * Px,
        kb = 5f, stop = 5, stun = 17, bstun = 9, chain = 4, meter = 7f, lunge = 0.6f,
    };

    public static readonly AttackData Heavy = new AttackData
    {
        kind = AttackKind.Heavy,
        startup = 11, active = 6, recover = 20,
        dmg = 32f,
        reach = 128f * Px,
        y0 = 20f * Px, y1 = 140f * Px,
        kb = 8f, stop = 9, stun = 24, bstun = 13, chain = 4, meter = 12f, lunge = 1.2f,
    };

    // 空中攻击：y0 为负，能打到比自身更低的目标
    public static readonly AttackData AirAttack = new AttackData
    {
        kind = AttackKind.Air,
        startup = 4, active = 12, recover = 8,
        dmg = 22f,
        reach = 128f * Px,
        y0 = -40f * Px, y1 = 120f * Px,
        kb = 6f, stop = 6, stun = 20, bstun = 11, chain = 1, meter = 8f, lunge = 0f,
    };

    /// <summary>
    /// 必杀「突进」：恒速冲刺，前 2 帧无敌，撞墙或命中即结束。
    /// len 同时是时长和距离（spd × len）。
    /// </summary>
    public static readonly AttackData Rush = new AttackData
    {
        kind = AttackKind.Super,
        startup = 6, active = 30, recover = 14,
        dmg = 46f,
        reach = 118f * Px,
        y0 = 10f * Px, y1 = 150f * Px,
        kb = 12f, stop = 12, stun = 34, bstun = 20, chain = 1, meter = 0f, lunge = 0f,
    };

    public const float RushSpeed = 9.5f;      // 米/秒，恒速
    public const int RushInvulnFrames = 2;

    public static AttackData Get(AttackKind kind) => kind switch
    {
        AttackKind.Heavy => Heavy,
        AttackKind.Air => AirAttack,
        _ => Light,
    };

    public static float ToSeconds(int frames) => frames / Fps;
}

/// <summary>
/// 全局顿帧。参考实现里 hitstop 是整场冻结：t 不推进，hitstop 自己每帧 -1。
/// 这里用"未缩放时间"做窗口，所有战斗角色在窗口内不推进自己的帧。
/// </summary>
public static class HitStop
{
    static float _until;

    public static bool Frozen => Time.unscaledTime < _until;

    public static void Freeze(int frames)
    {
        if (frames <= 0) return;
        float end = Time.unscaledTime + frames / CombatFrames.Fps;
        if (end > _until) _until = end;
    }

    public static void Reset() => _until = 0f;
}

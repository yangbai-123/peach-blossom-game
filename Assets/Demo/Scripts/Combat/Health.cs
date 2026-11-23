using UnityEngine;

/// <summary>
/// 通用生命值组件。玩家和敌人共用。
/// 自带一个无敌窗口，避免一次挥砍多帧重复命中。
/// </summary>
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;

    [Tooltip("受击后的无敌时间（秒）")]
    public float invulnerableTime = 0.35f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead => Current <= 0f;

    /// <summary>(当前血量, 最大血量)</summary>
    public event System.Action<float, float> Changed;

    /// <summary>受到伤害，参数为伤害来源位置</summary>
    public event System.Action<Vector3> Damaged;

    public event System.Action Died;

    float _immuneUntil;

    void Awake()
    {
        Current = maxHealth;
    }

    public void ResetHealth()
    {
        Current = maxHealth;
        _immuneUntil = 0f;
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>回血（医疗包等）。</summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>直接设置当前血量（联机同步远端血量用）。</summary>
    public void SetCurrent(float value)
    {
        Current = Mathf.Clamp(value, 0f, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>
    /// 扣血。返回是否真的生效（无敌中、已死亡会返回 false）。
    /// </summary>
    public bool TakeDamage(float amount, Vector3 fromPosition, float immuneWindow = -1f)
    {
        if (IsDead) return false;
        if (Time.time < _immuneUntil) return false;

        _immuneUntil = Time.time + (immuneWindow >= 0f ? immuneWindow : invulnerableTime);

        Current = Mathf.Max(0f, Current - amount);
        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke(fromPosition);

        if (Current <= 0f) Died?.Invoke();
        return true;
    }
}

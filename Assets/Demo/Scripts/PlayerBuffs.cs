using UnityEngine;

/// <summary>玩家身上的临时增益：医疗、攻击强化、疾行。</summary>
public class PlayerBuffs : MonoBehaviour
{
    PlayerCombat _combat;
    PlayerMotor _motor;
    Health _health;

    float _damageUntil;
    float _speedUntil;
    float _damageMultiplier = 1f;
    float _speedMultiplier = 1f;

    public float DamageLeft => Mathf.Max(0f, _damageUntil - Time.time);
    public float SpeedLeft => Mathf.Max(0f, _speedUntil - Time.time);

    void Awake()
    {
        _combat = GetComponent<PlayerCombat>();
        _motor = GetComponent<PlayerMotor>();
        _health = GetComponent<Health>();
    }

    public void Heal(float amount)
    {
        if (_health != null) _health.Heal(amount);
    }

    public void AddDamage(float multiplier, float duration)
    {
        _damageMultiplier = multiplier;
        _damageUntil = Time.time + duration;
        Apply();
    }

    public void AddSpeed(float multiplier, float duration)
    {
        _speedMultiplier = multiplier;
        _speedUntil = Time.time + duration;
        Apply();
    }

    void Update()
    {
        bool expired = false;

        if (_damageUntil > 0f && Time.time > _damageUntil) { _damageMultiplier = 1f; _damageUntil = 0f; expired = true; }
        if (_speedUntil > 0f && Time.time > _speedUntil) { _speedMultiplier = 1f; _speedUntil = 0f; expired = true; }

        if (expired) Apply();
    }

    void Apply()
    {
        if (_combat != null) _combat.damageMultiplier = _damageMultiplier;
        if (_motor != null) _motor.speedMultiplier = _speedMultiplier;
    }
}

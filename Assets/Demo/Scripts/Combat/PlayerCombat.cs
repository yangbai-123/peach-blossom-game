using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家战斗：三段连击，挥剑判定用球形检测，一次挥砍每个目标只吃一次伤害。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("连段")]
    [Tooltip("每一段的伤害，长度即连段数")]
    public float[] comboDamage = { 18f, 20f, 32f };

    [Tooltip("超过这个时间不攻击，连段重置")]
    public float comboResetTime = 0.85f;

    [Header("判定")]
    [Tooltip("判定球心到角色前方的距离")]
    public float reach = 1.5f;
    public float radius = 1.2f;
    public float knockback = 7f;

    [Header("节奏")]
    public float windupTime = 0.07f;
    public float activeTime = 0.13f;
    public float recoverTime = 0.2f;

    [Header("自动索敌")]
    [Tooltip("这个距离内挥剑会自动转身对准敌人")]
    public float autoAimRange = 4.5f;

    [Tooltip("正后方这个距离内也允许自动索敌（0 表示只朝前）")]
    public float autoAimCloseRange = 2.6f;

    public bool IsAttacking => _phase != Phase.Idle;

    /// <summary>当前这一挥的进度 0..1，给角色骨骼动画用</summary>
    public float SwingProgress { get; private set; }

    public int ComboIndex => _combo;

    /// <summary>每次开始挥砍（参数为连段序号）</summary>
    public event System.Action<int> Swung;

    /// <summary>命中任意目标</summary>
    public event System.Action HitLanded;

    enum Phase { Idle, Windup, Active, Recover }

    Phase _phase = Phase.Idle;
    float _timer;
    float _phaseDuration = 1f;
    float _lastSwingEnd = -99f;
    int _combo;
    readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.CanPlayerMove) return;

        if (_phase == Phase.Idle)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J)) BeginSwing();
            return;
        }

        _timer -= Time.deltaTime;
        SwingProgress = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _phaseDuration));

        switch (_phase)
        {
            case Phase.Windup:
                if (_timer <= 0f) Enter(Phase.Active, activeTime);
                break;

            case Phase.Active:
                CheckHits();
                if (_timer <= 0f) Enter(Phase.Recover, recoverTime);
                break;

            case Phase.Recover:
                if (_timer <= 0f)
                {
                    _phase = Phase.Idle;
                    _lastSwingEnd = Time.time;
                    _combo = (_combo + 1) % Mathf.Max(1, comboDamage.Length);
                    SwingProgress = 0f;
                }
                break;
        }
    }

    void BeginSwing()
    {
        if (Time.time - _lastSwingEnd > comboResetTime) _combo = 0;

        _hitThisSwing.Clear();

        // 自动索敌：挥剑前先对准最近的敌人，避免"明明贴身却砍空气"
        var motor = GetComponent<PlayerMotor>();
        if (motor != null && TryAutoAim(out Vector3 aimDir))
            motor.SnapFacing(aimDir, 0.3f);

        Enter(Phase.Windup, windupTime);

        Sfx.Play(Sfx.Swing, 0.75f, Random.Range(0.92f, 1.08f));
        Fx.SwingArc(transform.position + transform.forward * 0.5f, transform.forward,
            new Color(0.88f, 0.93f, 1f));

        Swung?.Invoke(_combo);
    }

    void Enter(Phase phase, float duration)
    {
        _phase = phase;
        _phaseDuration = Mathf.Max(0.01f, duration);
        _timer = _phaseDuration;
    }

    /// <summary>
    /// 找最近的可攻击敌人：前方 4.5m 内，或贴身 2.6m 内（允许背身）。
    /// </summary>
    bool TryAutoAim(out Vector3 direction)
    {
        direction = Vector3.zero;
        Vector3 forward = transform.forward;
        float bestDistance = float.MaxValue;

        foreach (var enemy in Enemy.All)
        {
            if (enemy == null || enemy.CurrentState == Enemy.State.Dead) continue;

            Vector3 to = enemy.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.001f || distance > autoAimRange) continue;

            bool inFront = Vector3.Dot(forward, to / distance) > -0.1f;
            bool veryClose = distance <= autoAimCloseRange;
            if (!inFront && !veryClose) continue;

            if (distance >= bestDistance) continue;
            bestDistance = distance;
            direction = to / distance;
        }

        return bestDistance < float.MaxValue;
    }

    void CheckHits()
    {
        Vector3 center = transform.position + transform.forward * reach + Vector3.up * 0.95f;

        var overlaps = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (var collider in overlaps)
        {
            var health = collider.GetComponentInParent<Health>();
            if (health == null) continue;
            if (health.transform.root == transform.root) continue;
            if (!_hitThisSwing.Add(health)) continue;

            float dmg = comboDamage[Mathf.Clamp(_combo, 0, comboDamage.Length - 1)];
            if (!health.TakeDamage(dmg, transform.position)) continue;

            // 便于在浏览器控制台验证战斗链路（Unity WebGL 会把 Debug.Log 转到 console）
            Debug.Log($"[combat] hit {health.name} dmg={dmg} combo={_combo}");

            Fx.HitStop(0.05f);
            Fx.Burst(collider.ClosestPoint(center), new Color(1f, 0.9f, 0.55f), 12, 5f, 0.45f, 0.09f);
            HitLanded?.Invoke();

            var enemy = health.GetComponent<Enemy>();
            if (enemy != null)
                enemy.ApplyKnockback(transform.forward, knockback);
        }
    }

    /// <summary>给 UI 用：当前连段伤害</summary>
    public float CurrentDamage => comboDamage[Mathf.Clamp(_combo, 0, comboDamage.Length - 1)];
}

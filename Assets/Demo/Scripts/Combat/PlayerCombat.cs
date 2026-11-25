using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家战斗：按 60Hz 帧驱动，规则对齐参考实现。
///
/// 一帧的顺序：
///   读输入 → 状态机推进 → 判定（AABB） → 结算（格挡 / 招架 / 命中） → 顿帧
///
/// 关键规则：
///   - 起手 startup 帧内不能判定，第 startup+1 帧起才是判定帧
///   - 取消连段只有"打中且没被格挡且在地面且 t > startup"才成立
///   - 连段上限 4，超了进入 16 帧疲劳（不能出招）
///   - 格挡吃 6% 伤害且打不死；招架是按下格挡后 10 帧内挨打，40 帧内连按会失效
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerCombat : MonoBehaviour
{
    [Header("连段")]
    [Tooltip("只用于 UI 显示当前段位的伤害")]
    public float[] comboDamage = { 18f, 20f, 32f };

    [Header("判定")]
    public float knockback = 5f;

    [Tooltip("临时伤害倍率（物资增益）")]
    public float damageMultiplier = 1f;

    [Tooltip("输入缓冲帧数：起手/收招期间按的键会留着")]
    public int bufferFrames = 8;

    [Tooltip("连段上限，超过进入疲劳")]
    public int chainLimit = 4;

    [Tooltip("疲劳帧数：这段时间内不能出招")]
    public int tiredFrames = 16;

    [Header("自动索敌")]
    public float autoAimRange = 4.5f;
    public float autoAimCloseRange = 2.6f;

    [Header("气条")]
    public float meter;
    public float meterMax = 100f;

    public bool IsAttacking => _phase != Phase.Idle;

    /// <summary>当前这一挥的进度 0..1，给骨骼动画用</summary>
    public float SwingProgress { get; private set; }

    public int ComboIndex => _chain;

    public bool IsGuarding { get; private set; }

    public float Meter => meter;

    public event System.Action<int> Swung;
    public event System.Action HitLanded;
    public event System.Action<Vector3> Blocked;
    public event System.Action<Vector3> Parried;

    enum Phase { Idle, Startup, Active, Recover, Super, GuardStun, Tired }

    enum Queued { None, Light, Heavy }

    Phase _phase = Phase.Idle;
    AttackData _attack;
    int _frame;                  // 当前招式内的帧
    int _phaseFrames;            // 非攻击状态的剩余帧
    int _chain;
    int _guardFrames;
    int _parryFrames;
    long _lastMashFrame = -999;
    long _tiredUntilFrame;
    bool _atkHit;
    bool _atkBlocked;
    bool _airUsed;

    int _bufferFrames;
    Queued _buffer = Queued.None;

    bool _attackPressed;
    bool _superPressed;
    int _hitstunFrames;
    bool _rushHitWall;
    int _counterDelay;
    bool _guardPressedEdge;
    bool _guardHeld;

    float _frameAccum;
    long _globalFrame;

    readonly HashSet<Health> _hitThisAttack = new HashSet<Health>();
    Health _health;
    PlayerMotor _motor;
    CharacterController _cc;

    void Awake()
    {
        _health = GetComponent<Health>();
        _motor = GetComponent<PlayerMotor>();
        _cc = GetComponent<CharacterController>();

        // 受击前先过一遍格挡/招架
        _health.DamageFilter = FilterIncomingDamage;
    }

    void OnDestroy()
    {
        if (_health != null) _health.DamageFilter = null;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.CanPlayerMove) return;

        // 输入在 Unity 帧里采一次，交给固定帧步进消费
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J) ||
            (MobileInput.Enabled && MobileInput.ConsumeAttack()))
            _attackPressed = true;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.K) ||
            (MobileInput.Enabled && MobileInput.ConsumeViewToggle()))
            _guardPressedEdge = true;

        _guardHeld = Input.GetMouseButton(1) || Input.GetKey(KeyCode.K) ||
                     (MobileInput.Enabled && MobileInput.BlockHeld);

        if (Input.GetKeyDown(KeyCode.Q) || (MobileInput.Enabled && MobileInput.ConsumeSuper()))
            _superPressed = true;

        // 顿帧期间不推进，也丢掉这一帧的输入（与参考实现一致）
        if (HitStop.Frozen)
        {
            _attackPressed = false;
            _guardPressedEdge = false;
            _superPressed = false;
            return;
        }

        _frameAccum += Time.deltaTime;

        int steps = 0;
        while (_frameAccum >= 1f / CombatFrames.Fps && steps < 4)
        {
            _frameAccum -= 1f / CombatFrames.Fps;
            StepFrame();
            steps++;

            _attackPressed = false;
            _guardPressedEdge = false;
            _superPressed = false;
        }
    }

    // ---------------------------------------------------------------- 帧步进

    void StepFrame()
    {
        _globalFrame++;

        // 攻击途中按下的键进缓冲：起手/判定/收招期间都能存，
        // 等"打中且未被格挡"的取消窗口打开时再消费
        if (_phase != Phase.Idle && _attackPressed && _buffer == Queued.None)
        {
            _buffer = (_chain >= 2) ? Queued.Heavy : Queued.Light;
            _bufferFrames = bufferFrames;
        }

        // 受击硬直：这段时间不能做任何动作
        if (_hitstunFrames > 0)
        {
            _hitstunFrames--;
            if (_phase != Phase.Idle && _phase != Phase.GuardStun) _phase = Phase.Idle;
            return;
        }

        if (_counterDelay > 0)
        {
            if (--_counterDelay == 0 && _phase == Phase.Idle)
            {
                StartAttackData(CombatFrames.Counter);
                Debug.Log("[combat] counter!");
                return;
            }
        }

        switch (_phase)
        {
            case Phase.Idle:
                TickGuard();
                if (TryStartSuper()) break;
                TryStartFromInput();
                TryConsumeBuffer();
                break;

            case Phase.Super:
                TickSuper();
                break;

            case Phase.Startup:
            case Phase.Active:
            case Phase.Recover:
                TickAttack();
                break;

            case Phase.GuardStun:
                TickGuard();
                if (--_phaseFrames <= 0) _phase = Phase.Idle;
                break;

            case Phase.Tired:
                if (--_phaseFrames <= 0) _phase = Phase.Idle;
                break;
        }

        // 缓冲计时
        if (_buffer != Queued.None && --_bufferFrames <= 0) _buffer = Queued.None;
    }

    void TickGuard()
    {
        bool grounded = Grounded;

        IsGuarding = _guardHeld && grounded && _phase == Phase.Idle;

        if (_guardPressedEdge)
        {
            _guardFrames = 14;

            // 40 帧内连按格挡：招架窗口直接归零（反连打）
            _parryFrames = (_globalFrame - _lastMashFrame >= 40) ? 10 : 0;
            _lastMashFrame = _globalFrame;
        }
        else
        {
            if (_guardFrames > 0) _guardFrames--;
            if (_parryFrames > 0) _parryFrames--;
        }
    }

    void TryStartFromInput()
    {
        if (!_attackPressed) return;

        // 空中：一次滞空只能打一次
        if (!Grounded)
        {
            if (_airUsed) return;
            _airUsed = true;
            StartAttack(AttackKind.Air);
            return;
        }

        StartAttack(NextGroundKind());
    }

    void TryConsumeBuffer()
    {
        if (_buffer == Queued.None || !Grounded || _phase != Phase.Idle) return;

        var kind = _buffer == Queued.Heavy ? AttackKind.Heavy : AttackKind.Light;
        _buffer = Queued.None;
        StartAttack(kind);
    }

    AttackKind NextGroundKind()
    {
        // 第三段是重击，前两段是轻击
        return (_chain >= 2) ? AttackKind.Heavy : AttackKind.Light;
    }

    void StartAttack(AttackKind kind) => StartAttackData(CombatFrames.Get(kind));

    void StartAttackData(AttackData data)
    {
        if (_tiredUntilFrame > _globalFrame) return;

        _attack = data;
        var kind = data.kind;
        _phase = Phase.Startup;
        _frame = 0;
        _atkHit = false;
        _atkBlocked = false;
        _hitThisAttack.Clear();

        IsGuarding = false;

        // 自动索敌：挥剑前对准最近的敌人
        if (kind != AttackKind.Air && _motor != null && TryAutoAim(out Vector3 aimDir))
            _motor.SnapFacing(aimDir, 0.3f);

        // 前冲
        if (_attack.lunge > 0f && _motor != null) _motor.AddImpulse(_attack.lunge * 0.35f);

        Sfx.Play(Sfx.Swing, 0.75f, Random.Range(0.92f, 1.08f));
        Fx.SwingArc(transform.position + transform.forward * 0.5f, transform.forward,
            new Color(0.88f, 0.93f, 1f));

        Swung?.Invoke(_chain);
    }

    // ---------------------------------------------------------------- 必杀

    /// <summary>气满时按 Q / 触屏「必杀」发动突进。</summary>
    bool TryStartSuper()
    {
        if (!_superPressed) return false;
        Debug.Log($"[super] try meter={meter:0} grounded={Grounded} phase={_phase} hitstun={_hitstunFrames}");
        if (meter < meterMax) return false;
        if (!Grounded) return false;

        meter = 0f;
        _attack = CombatFrames.Rush;
        _phase = Phase.Super;
        _frame = 0;
        _atkHit = false;
        _atkBlocked = false;
        _hitThisAttack.Clear();
        _rushHitWall = false;

        // 前 2 帧无敌
        _health.SetInvulnerable(CombatFrames.RushInvulnFrames / CombatFrames.Fps + 0.02f);

        Sfx.Play(Sfx.Swing, 1f, 0.7f);
        Fx.SwingArc(transform.position + transform.forward * 0.8f, transform.forward,
            new Color(1f, 0.75f, 0.45f));

        CombatFx.SuperCutin();
        HitStop.Freeze(6);

        Debug.Log("[combat] super rush start");
        return true;
    }

    void TickSuper()
    {
        _frame++;
        SwingProgress = Mathf.Clamp01(_frame / (float)Mathf.Max(1, _attack.TotalFrames));

        // 前 2 帧保持无敌
        if (_frame <= CombatFrames.RushInvulnFrames)
            _health.SetInvulnerable(1.5f / CombatFrames.Fps);

        // 恒速前冲（不受惯性影响）
        bool moving = _frame > 1 && _frame <= _attack.ActiveEnd;
        if (moving && _cc != null)
        {
            Vector3 before = transform.position;
            _cc.Move(transform.forward * (CombatFrames.RushSpeed / CombatFrames.Fps));

            // 位移被吃掉说明撞墙
            if ((transform.position - before).sqrMagnitude < 0.0004f)
            {
                _rushHitWall = true;
                EndSuper();
                return;
            }
        }

        if (_frame >= _attack.ActiveStart && _frame <= _attack.ActiveEnd)
            CheckHits();

        // 命中或撞墙都提前结束
        if (_atkHit && _frame > _attack.ActiveStart) { EndSuper(); return; }
        if (_frame >= _attack.TotalFrames) EndSuper();
    }

    void EndSuper()
    {
        _phase = Phase.Idle;
        _frame = 0;
        _chain = 0;
        SwingProgress = 0f;
        Debug.Log($"[combat] super end wall={_rushHitWall} hit={_atkHit}");
    }

    void TickAttack()
    {
        _frame++;
        SwingProgress = Mathf.Clamp01(_frame / (float)Mathf.Max(1, _attack.TotalFrames));

        // 判定窗口：t > startup 起，到 startup+active
        if (_frame >= _attack.ActiveStart && _frame <= _attack.ActiveEnd)
            CheckHits();

        // 取消窗口：打中 + 未格挡 + 在地面 + t > startup
        bool canCancel = _atkHit && !_atkBlocked && Grounded && _frame > _attack.startup;

        if (canCancel && _buffer != Queued.None)
        {
            if (_chain + 1 >= chainLimit)
            {
                _tiredUntilFrame = _globalFrame + tiredFrames;
                _buffer = Queued.None;
                _phase = Phase.Tired;
                _phaseFrames = tiredFrames;
                return;
            }

            var kind = _buffer == Queued.Heavy ? AttackKind.Heavy : AttackKind.Light;
            _buffer = Queued.None;
            _chain++;
            StartAttack(kind);
            return;
        }

        if (_frame >= _attack.TotalFrames)
        {
            _phase = Phase.Idle;
            _frame = 0;
            SwingProgress = 0f;

            // 收招结束仍未命中：连段断开
            if (!_atkHit) _chain = 0;

            // 缓冲里还有按键：直接接上
            if (_buffer != Queued.None && Grounded) TryConsumeBuffer();
        }

        // 落地复位空攻
        if (Grounded && _attack.kind == AttackKind.Air && _frame >= _attack.TotalFrames)
            _airUsed = false;
    }

    bool Grounded => _cc == null || _cc.isGrounded;

    // ---------------------------------------------------------------- 判定

    void CheckHits()
    {
        float near = 20f * CombatFrames.Px;             // 近端前移，贴脸打不到
        float far = _attack.reach;

        float centerZ = (near + far) * 0.5f;
        float halfZ = Mathf.Max(0.15f, (far - near) * 0.5f);
        float centerY = (_attack.y0 + _attack.y1) * 0.5f;
        float halfY = Mathf.Max(0.15f, (_attack.y1 - _attack.y0) * 0.5f);

        Vector3 center = transform.position + transform.forward * centerZ + Vector3.up * centerY;
        Vector3 half = new Vector3(0.85f, halfY, halfZ);

        var overlaps = Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Collide);

        foreach (var collider in overlaps)
        {
            var health = collider.GetComponentInParent<Health>();
            if (health == null) continue;
            if (health.transform.root == transform.root) continue;
            if (!_hitThisAttack.Add(health)) continue;

            if (health.IsDead) continue;

            float dmg = _attack.dmg * damageMultiplier;

            // 只有"对方真的在格挡"才算被格挡；
            // 目标已死 / 处于受击无敌也会返回 false，那不算格挡
            var defenderCombat = health.GetComponent<PlayerCombat>();
            bool defenderGuarding = defenderCombat != null && defenderCombat.IsGuarding;

            bool landed = health.TakeDamage(dmg, transform.position, 0f);

            if (!landed)
            {
                if (defenderGuarding)
                {
                    _atkBlocked = true;
                    Debug.Log("[combat] blocked by " + health.name);
                }
                continue;
            }

            _atkHit = true;
            meter = Mathf.Min(meterMax, meter + _attack.meter);

            if (health.GetComponent<RemotePlayer>() != null)
                NetDuel.Instance?.ReportHit(dmg);

            Debug.Log($"[combat] hit {health.name} dmg={dmg:0.#} frame={_frame} chain={_chain} meter={meter:0}");

            HitStop.Freeze(_attack.stop);

            Fx.HitStop(_attack.stop / CombatFrames.Fps * 0.5f);
            Fx.Burst(collider.ClosestPoint(center), new Color(1f, 0.9f, 0.55f), 12, 5f, 0.45f, 0.09f);
            HitLanded?.Invoke();

            var enemy = health.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.ApplyKnockback(transform.forward, _attack.kb);
                enemy.ApplyStun(_attack.stun / CombatFrames.Fps);
            }
        }
    }

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

    // ---------------------------------------------------------------- 防御

    /// <summary>
    /// 受击前调用：格挡吃 6%，招架完全免伤并惩罚攻击者。
    /// </summary>
    float FilterIncomingDamage(float amount, Vector3 fromPosition)
    {
        // 背后不能挡
        Vector3 to = fromPosition - transform.position;
        to.y = 0f;
        bool facingAttacker = to.sqrMagnitude <= 0.001f || Vector3.Dot(transform.forward, to.normalized) >= 0.15f;

        if (!IsGuarding || !facingAttacker)
        {
            // 没挡住：进受击硬直，正在出的招被打断
            _hitstunFrames = 14;
            if (_phase == Phase.Startup || _phase == Phase.Active || _phase == Phase.Recover || _phase == Phase.Super)
                _phase = Phase.Idle;

            return amount;
        }

        // 招架：按下格挡后 10 帧内挨打
        if (_parryFrames > 0)
        {
            _parryFrames = 0;
            meter = Mathf.Min(meterMax, meter + 8f);
            HitStop.Freeze(10);

            // 攻击者吃 30 帧硬直
            foreach (var enemy in Enemy.All)
            {
                if (enemy == null) continue;
                if ((enemy.transform.position - fromPosition).sqrMagnitude < 9f)
                {
                    enemy.ApplyStun(30f / CombatFrames.Fps);
                    break;
                }
            }

            // 招架成功后自动派生一记横斩（刀盾型的反击）
            _counterDelay = CombatFrames.CounterDelayFrames;

            CombatFx.Parry();
            Parried?.Invoke(fromPosition);
            Sfx.Play(Sfx.Hit, 1f, 0.7f);
            Debug.Log("[combat] parry!");
            return 0f;
        }

        // 普通格挡：6% 伤害，且永远打不死；格挡不走受击硬直
        float chip = Mathf.Min(_health.Current - 1f, amount * 0.06f);
        chip = Mathf.Max(0f, chip);

        HitStop.Freeze(5);
        Blocked?.Invoke(fromPosition);
        Sfx.Play(Sfx.Hit, 0.6f, 1.3f);
        Debug.Log($"[combat] block chip={chip:0.##}");

        return chip;
    }

    /// <summary>给 UI 用：当前段伤害</summary>
    public float CurrentDamage => comboDamage[Mathf.Clamp(_chain, 0, comboDamage.Length - 1)];
}

using UnityEngine;

/// <summary>
/// 敌人 AI：待机 → 追击 → 前摇 → 挥击。
/// 用 CharacterController 移动，所以能贴着坡和台阶走，不会陷地。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour
{
    public enum State { Idle, Chase, Windup, Recover, Dead }

    [Header("感知与移动")]
    public float moveSpeed = 2.6f;
    public float detectRange = 16f;
    public float attackRange = 2.1f;
    public float stopDistance = 1.6f;

    [Header("攻击")]
    public float damage = 6f;
    public float windupTime = 0.45f;
    public float recoverTime = 0.5f;
    public float attackCooldown = 1.8f;

    [Header("其它")]
    public float gravity = -22f;
    public float knockbackResistance = 0.5f;

    public State CurrentState { get; private set; } = State.Idle;

    /// <summary>场上所有存活的敌人（战斗日志与索敌用）。</summary>
    public static readonly System.Collections.Generic.List<Enemy> All =
        new System.Collections.Generic.List<Enemy>();

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    Transform _target;
    CharacterController _cc;
    Health _health;
    Renderer[] _renderers;
    Color[] _baseColors;
    float _verticalVelocity;
    float _stateTimer;
    float _nextAttackTime;
    Vector3 _knockback;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _health = GetComponent<Health>();

        _renderers = GetComponentsInChildren<Renderer>();
        _baseColors = new Color[_renderers.Length];

        // 关键：把共享材质实例化，否则闪白会同时影响所有同色敌人
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].sharedMaterial != null)
                _renderers[i].material = new Material(_renderers[i].sharedMaterial);

            _baseColors[i] = _renderers[i].material != null && _renderers[i].material.HasProperty("_Color")
                ? _renderers[i].material.color
                : Color.white;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _target = player.transform;

        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
    }

    /// <summary>
    /// 外观被替换（例如换成 VRM 模型）后重新收集渲染器，
    /// 否则受击闪白还是作用在旧的程序化图元上。
    /// </summary>
    public void RefreshVisuals()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _baseColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            // 每只怪物独立材质，避免闪白串到所有怪身上
            if (_renderers[i].sharedMaterial != null)
                _renderers[i].material = new Material(_renderers[i].sharedMaterial);

            _baseColors[i] = _renderers[i].material != null && _renderers[i].material.HasProperty("_Color")
                ? _renderers[i].material.color
                : Color.white;
        }

        RestoreTint();
    }

    void OnDestroy()
    {
        if (_health == null) return;
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
    }

    void Update()
    {
        if (CurrentState == State.Dead) return;
        if (GameManager.Instance != null && !GameManager.Instance.CanPlayerMove) return;
        if (_target == null) return;

        float distance = Vector3.Distance(transform.position, _target.position);

        switch (CurrentState)
        {
            case State.Idle:
                if (distance < detectRange) CurrentState = State.Chase;
                break;

            case State.Chase:
                if (distance > detectRange * 1.4f)
                {
                    CurrentState = State.Idle;
                    break;
                }
                if (distance <= attackRange && Time.time >= _nextAttackTime)
                {
                    EnterWindup();
                    break;
                }
                MoveToward(_target.position, distance > stopDistance);
                break;

            case State.Windup:
                _stateTimer -= Time.deltaTime;
                FaceTarget();
                if (_stateTimer <= 0f)
                {
                    Strike();
                    CurrentState = State.Recover;
                    _stateTimer = recoverTime;
                }
                break;

            case State.Recover:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f) CurrentState = State.Chase;
                break;
        }

        ApplyGravity();
    }

    void EnterWindup()
    {
        CurrentState = State.Windup;
        _stateTimer = windupTime;
        _nextAttackTime = Time.time + attackCooldown + windupTime + recoverTime;

        // 前摇提示：整体后仰一点（用缩放近似，避免额外骨骼）
        transform.localScale = new Vector3(1f, 0.92f, 1f);
    }

    void Strike()
    {
        transform.localScale = Vector3.one;

        if (_target == null) return;
        if (Vector3.Distance(transform.position, _target.position) > attackRange + 0.6f) return;

        var health = _target.GetComponent<Health>();
        if (health == null) return;

        Vector3 dir = (_target.position - transform.position).normalized;
        if (health.TakeDamage(damage, transform.position))
        {
            Fx.Burst(_target.position + Vector3.up * 1.1f, new Color(1f, 0.35f, 0.3f), 6, 3.5f, 0.35f, 0.07f);
            Sfx.Play(Sfx.Hurt);
            GameManager.Instance?.OnPlayerDamaged();
        }

        // 轻击退
        var motor = _target.GetComponent<PlayerMotor>();
        if (motor != null) motor.AddImpulse(0.6f);
    }

    void MoveToward(Vector3 targetPosition, bool move)
    {
        Vector3 to = targetPosition - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.001f) return;

        Vector3 dir = to.normalized;

        // 与其它敌人保持间距，避免叠在一起
        foreach (var other in Physics.OverlapSphere(transform.position, 1.6f))
        {
            if (other.gameObject == gameObject) continue;
            if (other.GetComponent<Enemy>() == null) continue;
            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) dir += away.normalized * 0.6f;
        }
        dir.Normalize();

        Vector3 motion = dir * (move ? moveSpeed : 0f) + _knockback + Vector3.up * _verticalVelocity;
        _cc.Move(motion * Time.deltaTime);

        _knockback = Vector3.Lerp(_knockback, Vector3.zero, 8f * Time.deltaTime);

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
        }
    }

    void FaceTarget()
    {
        Vector3 to = _target.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(to.normalized, Vector3.up), 12f * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        _verticalVelocity += gravity * Time.deltaTime;
    }

    /// <summary>受到攻击时的击退（由玩家战斗调用）。</summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        _knockback += direction.normalized * force * (1f - knockbackResistance);
    }

    void OnDamaged(Vector3 from)
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());

        // 击退
        Vector3 away = (transform.position - from);
        away.y = 0f;
        if (away.sqrMagnitude > 0.001f)
            _knockback += away.normalized * 6f * (1f - knockbackResistance);

        Fx.Burst(transform.position + Vector3.up * 0.9f, new Color(1f, 0.85f, 0.5f), 10, 4.5f, 0.4f, 0.08f);
        Sfx.Play(Sfx.Hit, 0.9f, Random.Range(0.9f, 1.1f));
    }

    System.Collections.IEnumerator FlashRoutine()
    {
        SetTint(Color.white);
        yield return new WaitForSeconds(0.08f);
        RestoreTint();
    }

    void SetTint(Color c)
    {
        foreach (var r in _renderers)
        {
            if (r == null || r.material == null) continue;
            if (r.material.HasProperty("_Color")) r.material.color = c;
        }
    }

    void RestoreTint()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null || _renderers[i].material == null) continue;
            if (_renderers[i].material.HasProperty("_Color"))
                _renderers[i].material.color = _baseColors[i];
        }
    }

    void OnDied()
    {
        CurrentState = State.Dead;
        if (_cc != null) _cc.enabled = false;
        transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);

        Fx.Burst(transform.position + Vector3.up * 0.8f, new Color(0.9f, 0.3f, 0.4f), 18, 6f, 0.7f, 0.12f);
        Sfx.Play(Sfx.Death, 0.9f);
        GameManager.Instance?.OnEnemyKilled();

        Destroy(gameObject, 1.2f);
    }
}

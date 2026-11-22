using UnityEngine;

/// <summary>
/// 角色移动核心。只负责"怎么动"：加速、减速、转向、重力、跳跃、坡道。
/// 不读输入、不管动画，方便单独调参，也方便以后替换成网络同步版本。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("水平速度")]
    [Tooltip("步行速度，m/s")]
    public float walkSpeed = 4.5f;

    [Tooltip("冲刺速度，m/s")]
    public float sprintSpeed = 8f;

    [Tooltip("加速度，m/s²。越大起步越冲")]
    public float acceleration = 30f;

    [Tooltip("减速度，m/s²。一般比加速度大，松手才干脆")]
    public float deceleration = 45f;

    [Header("转向")]
    [Tooltip("转身平滑时间（秒）。越小越跟手，越大越黏手")]
    public float turnSmoothTime = 0.08f;

    /// <summary>转向锁：挥剑索敌瞬间会强行转身，短暂锁住避免被移动方向立刻拽回去。</summary>
    float _turnLockUntil;

    [Header("跳跃与重力")]
    public float jumpHeight = 1.3f;

    [Tooltip("重力加速度，负数。Unity 默认 -9.81 会显得飘，游戏里常用 -20 ~ -30")]
    public float gravity = -25f;

    [Tooltip("离开地面后仍允许起跳的宽限时间（土狼时间）")]
    public float coyoteTime = 0.12f;

    [Tooltip("落地前提前按跳的缓冲时间")]
    public float jumpBufferTime = 0.15f;

    [Header("坡道")]
    [Tooltip("坡度超过 slopeLimit 时沿坡面下滑的速度")]
    public float slopeSlideSpeed = 6f;

    /// <summary>地面探测额外下探的距离</summary>
    const float GroundProbeExtra = 0.12f;

    CharacterController _cc;
    Vector3 _horizontalVelocity;
    float _verticalVelocity;
    float _turnSmoothVelocity;
    float _lastGroundedTime = -999f;
    float _lastJumpPressedTime = -999f;

    bool _hasGroundContact;
    bool _grounded;
    Vector3 _groundNormal = Vector3.up;

    public float HorizontalSpeed => new Vector2(_horizontalVelocity.x, _horizontalVelocity.z).magnitude;
    public float VerticalVelocity => _verticalVelocity;
    public bool IsGrounded => _grounded;
    public Vector3 GroundNormal => _groundNormal;
    public bool IsSprinting { get; private set; }

    /// <summary>地面坡度（度）</summary>
    public float GroundAngle => Vector3.Angle(_groundNormal, Vector3.up);

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 每帧由控制器调用。
    /// </summary>
    /// <param name="moveDirection">世界空间的水平输入方向（已归一化或为零）</param>
    /// <param name="sprint">是否按住冲刺</param>
    /// <param name="jumpPressed">本帧是否按下跳跃</param>
    public void Tick(Vector3 moveDirection, bool sprint, bool jumpPressed)
    {
        UpdateGroundState();

        bool hasInput = moveDirection.sqrMagnitude > 0.0001f;
        IsSprinting = sprint && hasInput;

        // ---- 1. 水平速度：加速 / 减速 ----
        float targetSpeed = hasInput ? (IsSprinting ? sprintSpeed : walkSpeed) : 0f;
        Vector3 targetVelocity = moveDirection * targetSpeed;

        // 目标速度比当前大 → 用加速度；否则用减速度
        float rate = targetVelocity.sqrMagnitude > _horizontalVelocity.sqrMagnitude
            ? acceleration
            : deceleration;

        _horizontalVelocity = Vector3.MoveTowards(
            _horizontalVelocity, targetVelocity, rate * Time.deltaTime);

        // ---- 2. 转向：朝速度方向平滑转身 ----
        if (Time.time >= _turnLockUntil && _horizontalVelocity.sqrMagnitude > 0.01f)
        {
            float targetYaw = Mathf.Atan2(_horizontalVelocity.x, _horizontalVelocity.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetYaw, ref _turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---- 3. 跳跃：土狼时间 + 输入缓冲 ----
        if (jumpPressed) _lastJumpPressedTime = Time.time;
        if (_grounded) _lastGroundedTime = Time.time;

        bool withinCoyote = Time.time - _lastGroundedTime <= coyoteTime;
        bool withinBuffer = Time.time - _lastJumpPressedTime <= jumpBufferTime;

        if (withinCoyote && withinBuffer)
        {
            // v = sqrt(2 * g * h)
            _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            _lastJumpPressedTime = -999f;
            _lastGroundedTime = -999f;
            _grounded = false;
        }

        // ---- 4. 重力 ----
        // 贴地时给一个小的向下速度，让 CC 稳定吸附在地面，而不是每帧悬空
        if (_grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        _verticalVelocity += gravity * Time.deltaTime;

        // ---- 5. 组合位移 ----
        Vector3 displacement = _horizontalVelocity + Vector3.up * _verticalVelocity;

        // ---- 6. 陡坡下滑 ----
        // CharacterController 只会挡住超过 slopeLimit 的上坡，不会自动滑下去，要自己补
        if (_hasGroundContact && GroundAngle > _cc.slopeLimit + 0.5f)
        {
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
            displacement += downhill * slopeSlideSpeed;
        }

        // ---- 7. 交给 CharacterController 处理碰撞、台阶、坡道 ----
        Vector3 before = transform.position;
        _cc.Move(displacement * Time.deltaTime);

        // ---- 8. 撞墙时吃掉速度，避免顶着墙继续加速 ----
        Vector3 actual = transform.position - before;
        Vector3 intended = new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z) * Time.deltaTime;

        if (intended.sqrMagnitude > 1e-6f)
        {
            float along = Vector3.Dot(new Vector3(actual.x, 0f, actual.z), intended.normalized);
            if (along < intended.magnitude * 0.5f) _horizontalVelocity *= 0.5f;
        }
    }

    /// <summary>
    /// 地面检测。CharacterController.isGrounded 在斜坡和台阶边缘会抖，
    /// 所以这里用一条向下的球形探测补充判断。
    /// </summary>
    void UpdateGroundState()
    {
        Vector3 capsuleCenter = transform.TransformPoint(_cc.center);
        float halfHeight = _cc.height * 0.5f;

        // 胶囊底部球心
        Vector3 bottomSphere = capsuleCenter + Vector3.down * (halfHeight - _cc.radius);
        float maxDistance = _cc.radius + GroundProbeExtra;

        _hasGroundContact = Physics.SphereCast(
            bottomSphere,
            _cc.radius * 0.95f,
            Vector3.down,
            out RaycastHit hit,
            maxDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (_hasGroundContact)
        {
            _groundNormal = hit.normal;

            // 坡度在 slopeLimit 以内、且不是刚起跳，才算站住
            _grounded = GroundAngle <= _cc.slopeLimit + 0.5f && _verticalVelocity <= 0.1f;
        }
        else
        {
            _groundNormal = Vector3.up;
            _grounded = false;
        }
    }

    /// <summary>
    /// 立即面朝某个方向，并在接下来的一小段时间内锁定转向。
    /// 用在挥剑自动索敌上：让角色瞬间对准目标，而不是被移动方向拽回去。
    /// </summary>
    public void SnapFacing(Vector3 direction, float lockSeconds = 0.28f)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _turnSmoothVelocity = 0f;
        _turnLockUntil = Time.time + lockSeconds;
    }

    /// <summary>
    /// 给一个向上的速度冲量（弹跳台、风场用）。
    /// 传跳跃高度，内部换算成初速度。
    /// </summary>
    public void AddImpulse(float heightInMeters)
    {
        float v = Mathf.Sqrt(-2f * gravity * Mathf.Max(0.1f, heightInMeters));
        if (v > _verticalVelocity) _verticalVelocity = v;
        _grounded = false;
    }

    /// <summary>外部重置速度（重生时用）。</summary>
    public void ResetVelocity()
    {
        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _turnSmoothVelocity = 0f;
    }
}

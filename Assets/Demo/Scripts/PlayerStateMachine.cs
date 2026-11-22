using UnityEngine;

public enum PlayerState
{
    Idle,
    Move,
    Sprint,
    Jump,
    Fall,
}

/// <summary>
/// 角色状态机。只根据 PlayerMotor 的观测值推导状态，不直接改移动逻辑。
/// 好处是：动画、音效、UI 都订阅 StateChanged，移动逻辑保持独立可测。
/// </summary>
public class PlayerStateMachine : MonoBehaviour
{
    [Header("可选：有 Animator 就自动同步参数")]
    public Animator animator;

    public PlayerState Current { get; private set; } = PlayerState.Idle;
    public PlayerState Previous { get; private set; } = PlayerState.Idle;

    /// <summary>状态切换事件：(from, to)</summary>
    public event System.Action<PlayerState, PlayerState> StateChanged;

    static readonly int StateParam = Animator.StringToHash("State");
    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int GroundedParam = Animator.StringToHash("Grounded");

    float _speed;
    public float Speed => _speed;

    public void Tick(PlayerMotor motor)
    {
        PlayerState next = Resolve(motor);

        if (next != Current)
        {
            Previous = Current;
            Current = next;
            StateChanged?.Invoke(Previous, Current);
        }

        _speed = motor.HorizontalSpeed;

        if (animator != null)
        {
            animator.SetInteger(StateParam, (int)Current);
            animator.SetFloat(SpeedParam, _speed);
            animator.SetBool(GroundedParam, motor.IsGrounded);
        }
    }

    /// <summary>
    /// 状态判定规则。顺序很重要：先判断空中，再判断速度。
    /// 阈值 0.15 m/s 是为了避免减速尾巴上在 Idle/Move 之间来回跳。
    /// </summary>
    static PlayerState Resolve(PlayerMotor m)
    {
        if (!m.IsGrounded)
            return m.VerticalVelocity > 0.1f ? PlayerState.Jump : PlayerState.Fall;

        if (m.HorizontalSpeed < 0.15f)
            return PlayerState.Idle;

        return m.IsSprinting ? PlayerState.Sprint : PlayerState.Move;
    }
}

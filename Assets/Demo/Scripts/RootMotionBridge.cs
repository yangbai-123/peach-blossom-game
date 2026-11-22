using UnityEngine;

/// <summary>
/// 可选组件：把 Animator 的根运动交给 CharacterController。
///
/// 什么时候需要它：
///   - 跳跃/翻滚/攻击的位移是"烘焙在动画里"的（动画自身带位移）
///   - 你需要位移和动画严格同步，而不是代码算出来的抛物线
///
/// 什么时候不需要：
///   - 本 Demo 的跳跃是程序化抛物线（PlayerMotor 里用重力算），
///     移动速度由参数控制，动画只做表现。绝大多数动作游戏是这种。
///
/// 用法：挂在角色根节点上，Animator 勾选 Apply Root Motion，
/// 把 PlayerMotor 的跳跃速度/水平速度调低，由动画驱动位移。
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class RootMotionBridge : MonoBehaviour
{
    [Tooltip("关闭后完全由 PlayerMotor 驱动，本组件不做任何事")]
    public bool applyRootMotion = true;

    Animator _animator;
    CharacterController _controller;

    Vector3 _pendingDelta;
    Quaternion _pendingRotation = Quaternion.identity;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _controller = GetComponent<CharacterController>();
        _animator.applyRootMotion = applyRootMotion;
    }

    // Animator 每帧算完根运动后回调这里，由我们决定怎么用
    void OnAnimatorMove()
    {
        if (!applyRootMotion) return;

        _pendingDelta += _animator.deltaPosition;
        _pendingRotation *= _animator.deltaRotation;
    }

    // 统一在 LateUpdate 里一次性提交，避免同一帧多次 Move 导致抖动
    void LateUpdate()
    {
        if (!applyRootMotion) return;
        if (_pendingDelta.sqrMagnitude < 1e-8f && _pendingRotation == Quaternion.identity) return;

        _controller.Move(_pendingDelta);
        transform.rotation *= _pendingRotation;

        _pendingDelta = Vector3.zero;
        _pendingRotation = Quaternion.identity;
    }
}

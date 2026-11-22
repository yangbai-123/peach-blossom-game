using UnityEngine;

/// <summary>
/// 给外部导入的人形模型（VRM / FBX）做程序化动画。
///
/// 不依赖 Animator Controller，直接用 Humanoid 骨骼的局部旋转驱动：
/// 走路摆腿摆臂、跳跃收腿、挥剑劈砍、奔跑前倾。
/// 挂到模型实例上，把 motor / combat 指向玩家组件即可。
/// </summary>
public class VrmCharacterRig : MonoBehaviour
{
    [Header("数据来源")]
    public PlayerMotor motor;
    public PlayerCombat combat;

    [Header("幅度")]
    public float legSwing = 32f;
    public float armSwing = 26f;
    public float lean = 6f;

    [Header("基础姿势")]
    [Tooltip("把 VRM 自带的 T/A 姿势手臂放下来")]
    public float armDown = 62f;

    Transform _hips, _spine, _head;
    Transform _armL, _armR, _foreArmR;
    Transform _legL, _legR, _shinL, _shinR;

    Quaternion _hipsRest, _spineRest, _headRest;
    Quaternion _armLRest, _armRRest, _foreArmRRest;
    Quaternion _legLRest, _legRRest, _shinLRest, _shinRRest;

    Vector3 _hipsBasePos;
    float _phase;
    float _idleTime;
    bool _ready;

    void Start()
    {
        var animator = GetComponentInChildren<Animator>();

        if (animator != null && animator.isHuman)
        {
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _armL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _armR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _foreArmR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _legL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _legR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _shinL = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _shinR = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        if (_hips == null)
        {
            // 没有 Humanoid 绑定时退化成只做整体起伏
            Debug.LogWarning("[vrm] 未找到 Humanoid 骨骼，动画降级");
            return;
        }

        _hipsRest = _hips.localRotation;
        _spineRest = _spine != null ? _spine.localRotation : Quaternion.identity;
        _headRest = _head != null ? _head.localRotation : Quaternion.identity;
        _armLRest = _armL != null ? _armL.localRotation : Quaternion.identity;
        _armRRest = _armR != null ? _armR.localRotation : Quaternion.identity;
        _foreArmRRest = _foreArmR != null ? _foreArmR.localRotation : Quaternion.identity;
        _legLRest = _legL != null ? _legL.localRotation : Quaternion.identity;
        _legRRest = _legR != null ? _legR.localRotation : Quaternion.identity;
        _shinLRest = _shinL != null ? _shinL.localRotation : Quaternion.identity;
        _shinRRest = _shinR != null ? _shinR.localRotation : Quaternion.identity;

        _hipsBasePos = _hips.localPosition;
        _ready = true;

        Debug.Log("[vrm] 骨骼就绪: hips=" + _hips.name +
                  " spine=" + (_spine != null ? _spine.name : "none") +
                  " armR=" + (_armR != null ? _armR.name : "none"));
    }

    void LateUpdate()
    {
        if (!_ready || motor == null) return;

        float speed = motor.HorizontalSpeed;
        float speedF = Mathf.Clamp01(speed / 5.5f);
        bool grounded = motor.IsGrounded;
        bool moving = speedF > 0.05f && grounded;

        if (moving) _phase += Time.deltaTime * (4f + speedF * 7f);
        else _phase = Mathf.Lerp(_phase, 0f, 6f * Time.deltaTime);

        _idleTime += Time.deltaTime;

        float swing = Mathf.Sin(_phase) * legSwing * speedF;

        // 腿
        Apply(_legL, _legLRest, swing, 0f, 0f);
        Apply(_legR, _legRRest, -swing, 0f, 0f);

        float knee = Mathf.Max(0f, -Mathf.Sin(_phase)) * 35f * speedF;
        Apply(_shinL, _shinLRest, knee, 0f, 0f);
        Apply(_shinR, _shinRRest, knee, 0f, 0f);

        // 手臂（与腿反相），并加上基础下垂角度把 A/T 姿势放下来
        float arm = Mathf.Sin(_phase) * armSwing * speedF;
        Apply(_armL, _armLRest, -arm, 0f, -armDown);
        Apply(_armR, _armRRest, arm, 0f, armDown);

        // 跳跃 / 下落
        if (!grounded)
        {
            float fall = Mathf.Clamp01(-motor.VerticalVelocity / 10f);
            Apply(_legL, _legLRest, -18f, 0f, 0f);
            Apply(_legR, _legRRest, -8f, 0f, 0f);
            Apply(_armL, _armLRest, -25f, 0f, -armDown + 12f);
            Apply(_armR, _armRRest, -20f, 0f, armDown - 12f);
            Apply(_spine, _spineRest, fall * 8f, 0f, 0f);
        }
        else
        {
            Apply(_spine, _spineRest, speedF * lean, 0f, 0f);
        }

        // 挥剑
        if (combat != null && combat.IsAttacking)
        {
            float p = combat.SwingProgress;
            float angle = p < 0.35f
                ? Mathf.Lerp(10f, -95f, p / 0.35f)
                : Mathf.Lerp(-95f, 65f, (p - 0.35f) / 0.65f);

            Apply(_armR, _armRRest, angle, -12f, armDown * 0.35f);
            Apply(_foreArmR, _foreArmRRest, Mathf.Min(angle * 0.4f, 30f), 0f, 0f);
            Apply(_spine, _spineRest, 0f, Mathf.Lerp(-12f, 18f, p), 0f);
        }

        // 起伏与呼吸
        if (_hips != null)
        {
            float bob = grounded ? Mathf.Abs(Mathf.Sin(_phase)) * 0.03f * speedF : 0f;
            float breathe = Mathf.Sin(_idleTime * 1.6f) * 0.008f * (1f - speedF);
            _hips.localPosition = _hipsBasePos + new Vector3(0f, bob + breathe, 0f);
        }

        if (_head != null)
            Apply(_head, _headRest, Mathf.Sin(_idleTime * 1.3f) * 2f * (1f - speedF), 0f, 0f);
    }

    static void Apply(Transform bone, Quaternion rest, float x, float y, float z)
    {
        if (bone == null) return;
        bone.localRotation = rest * Quaternion.Euler(x, y, z);
    }
}

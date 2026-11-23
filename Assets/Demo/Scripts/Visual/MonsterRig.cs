using UnityEngine;

/// <summary>
/// 怪物骨骼动画：追击时走路摆臂，前摇时举臂蓄力，死亡时前倾倒下。
/// 不依赖 Animator Controller，直接驱动 Humanoid 骨骼。
/// </summary>
public class MonsterRig : MonoBehaviour
{
    public Enemy enemy;

    [Header("幅度")]
    public float legSwing = 26f;
    public float armSwing = 20f;
    public float armDown = 58f;

    [Tooltip("手臂下垂方向：不同 VRM 的骨骼 roll 可能相反，这里可翻符号")]
    public float armDownSign = 1f;

    Transform _spine, _head, _armL, _armR, _legL, _legR, _shinL, _shinR, _hips;
    Quaternion _spineRest, _headRest, _armLRest, _armRRest, _legLRest, _legRRest, _shinLRest, _shinRRest, _hipsRest;
    Vector3 _hipsBase;
    float _phase;
    bool _ready;

    void Start()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return;

        _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        _head = animator.GetBoneTransform(HumanBodyBones.Head);
        _armL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _armR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _legL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        _legR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        _shinL = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        _shinR = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        if (_hips == null) return;

        _hipsRest = _hips.localRotation;
        _spineRest = Rest(_spine);
        _headRest = Rest(_head);
        _armLRest = Rest(_armL);
        _armRRest = Rest(_armR);
        _legLRest = Rest(_legL);
        _legRRest = Rest(_legR);
        _shinLRest = Rest(_shinL);
        _shinRRest = Rest(_shinR);
        _hipsBase = _hips.localPosition;

        _ready = true;
    }

    static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

    static void Apply(Transform bone, Quaternion rest, float x, float y, float z)
    {
        if (bone == null) return;
        bone.localRotation = rest * Quaternion.Euler(x, y, z);
    }

    void LateUpdate()
    {
        if (!_ready) return;

        var state = enemy != null ? enemy.CurrentState : Enemy.State.Idle;
        bool walking = state == Enemy.State.Chase;

        _phase += Time.deltaTime * (walking ? 7f : 2f);

        float speedF = walking ? 1f : 0f;
        float swing = Mathf.Sin(_phase) * legSwing * speedF;

        Apply(_legL, _legLRest, swing, 0f, 0f);
        Apply(_legR, _legRRest, -swing, 0f, 0f);

        float knee = Mathf.Max(0f, -Mathf.Sin(_phase)) * 26f * speedF;
        Apply(_shinL, _shinLRest, knee, 0f, 0f);
        Apply(_shinR, _shinRRest, knee, 0f, 0f);

        float arm = Mathf.Sin(_phase) * armSwing * speedF;
        Apply(_armL, _armLRest, -arm, 0f, armDown * armDownSign);
        Apply(_armR, _armRRest, arm, 0f, -armDown * armDownSign);

        // 前摇：举臂 + 后仰蓄力
        if (state == Enemy.State.Windup)
        {
            Apply(_armL, _armLRest, -105f, 0f, armDown * armDownSign * 0.5f);
            Apply(_armR, _armRRest, -105f, 0f, -armDown * armDownSign * 0.5f);
            Apply(_spine, _spineRest, -12f, 0f, 0f);
        }
        else if (state == Enemy.State.Dead)
        {
            Apply(_spine, _spineRest, 55f, 0f, 0f);
            Apply(_armL, _armLRest, -30f, 0f, armDown * armDownSign * 0.3f);
            Apply(_armR, _armRRest, -30f, 0f, -armDown * armDownSign * 0.3f);
        }
        else
        {
            // 缓慢呼吸
            float breathe = Mathf.Sin(Time.time * 1.4f) * 3f;
            Apply(_spine, _spineRest, breathe * 0.6f, 0f, 0f);
        }

        if (_head != null)
            Apply(_head, _headRest, Mathf.Sin(Time.time * 1.1f) * 3f, 0f, 0f);

        if (_hips != null)
        {
            float bob = walking ? Mathf.Abs(Mathf.Sin(_phase)) * 0.05f : 0f;
            _hips.localPosition = _hipsBase + new Vector3(0f, bob, 0f);
        }
    }
}

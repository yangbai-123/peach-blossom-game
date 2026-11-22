using UnityEngine;

/// <summary>
/// 程序化人形角色：用图元拼出躯干、四肢、头发和佩剑，并按移动速度做程序化动画。
/// 想换成真实模型时，把整个 Rig 子物体换成 FBX，接上 Animator 即可（见 README）。
/// </summary>
public class HumanoidRig : MonoBehaviour
{
    [Header("数据来源")]
    public PlayerMotor motor;
    public PlayerCombat combat;
    public PlayerStateMachine stateMachine;

    [Header("配色")]
    public Color robe = new Color(0.95f, 0.95f, 0.98f);
    public Color robeShade = new Color(0.86f, 0.88f, 0.94f);
    public Color hairColor = new Color(0.13f, 0.11f, 0.14f);
    public Color skin = new Color(0.96f, 0.86f, 0.80f);
    public Color blade = new Color(0.88f, 0.93f, 0.99f);

    Transform _pelvis, _torso, _head, _hair, _shoulderL, _shoulderR, _hipL, _hipR, _sword;
    Vector3 _pelvisBase;
    float _phase;
    float _idleTime;

    void Awake()
    {
        Build();
    }

    void LateUpdate()
    {
        Animate();
    }

    // ------------------------------------------------------------ 搭建

    void Build()
    {
        _pelvis = Pivot("Pelvis", transform, new Vector3(0f, 0.92f, 0f));
        _pelvisBase = _pelvis.localPosition;

        // 裙摆
        Part(PrimitiveType.Cylinder, "Skirt", _pelvis, new Vector3(0f, -0.22f, 0f),
            new Vector3(0.52f, 0.24f, 0.52f), robe);

        // 上身
        _torso = Pivot("Torso", _pelvis, new Vector3(0f, 0.18f, 0f));
        Part(PrimitiveType.Cube, "Chest", _torso, new Vector3(0f, 0.16f, 0f),
            new Vector3(0.42f, 0.46f, 0.26f), robe);
        Part(PrimitiveType.Cube, "Sash", _torso, new Vector3(0f, 0.02f, 0f),
            new Vector3(0.44f, 0.1f, 0.28f), new Color(0.82f, 0.86f, 0.95f));

        // 头与发
        _head = Pivot("Head", _torso, new Vector3(0f, 0.44f, 0f));
        Part(PrimitiveType.Sphere, "Face", _head, Vector3.zero,
            new Vector3(0.27f, 0.3f, 0.26f), skin);
        _hair = Pivot("Hair", _head, new Vector3(0f, -0.02f, 0f));
        Part(PrimitiveType.Sphere, "HairBack", _hair, new Vector3(0f, 0.04f, -0.03f),
            new Vector3(0.3f, 0.32f, 0.3f), hairColor);
        Part(PrimitiveType.Cube, "HairTail", _hair, new Vector3(0f, -0.2f, -0.12f),
            new Vector3(0.18f, 0.5f, 0.14f), hairColor);

        // 手臂（左右各一条，右臂挂剑）
        _shoulderL = Pivot("ShoulderL", _torso, new Vector3(-0.27f, 0.3f, 0f));
        BuildArm(_shoulderL, robe);

        _shoulderR = Pivot("ShoulderR", _torso, new Vector3(0.27f, 0.3f, 0f));
        BuildArm(_shoulderR, robe);
        _sword = BuildSword(_shoulderR);

        // 腿
        _hipL = Pivot("HipL", _pelvis, new Vector3(-0.13f, -0.3f, 0f));
        BuildLeg(_hipL);
        _hipR = Pivot("HipR", _pelvis, new Vector3(0.13f, -0.3f, 0f));
        BuildLeg(_hipR);
    }

    void BuildArm(Transform shoulder, Color color)
    {
        Part(PrimitiveType.Cube, "UpperArm", shoulder, new Vector3(0f, -0.18f, 0f),
            new Vector3(0.1f, 0.36f, 0.1f), color);
        Part(PrimitiveType.Cube, "Sleeve", shoulder, new Vector3(0f, -0.34f, 0f),
            new Vector3(0.16f, 0.16f, 0.16f), robeShade);
    }

    void BuildLeg(Transform hip)
    {
        Part(PrimitiveType.Cube, "Leg", hip, new Vector3(0f, -0.22f, 0f),
            new Vector3(0.13f, 0.44f, 0.13f), robeShade);
        Part(PrimitiveType.Cube, "Foot", hip, new Vector3(0f, -0.44f, 0.05f),
            new Vector3(0.15f, 0.08f, 0.22f), new Color(0.9f, 0.9f, 0.93f));
    }

    Transform BuildSword(Transform shoulder)
    {
        var hilt = Pivot("Hand", shoulder, new Vector3(0f, -0.38f, 0f));

        Part(PrimitiveType.Cube, "Grip", hilt, Vector3.zero,
            new Vector3(0.05f, 0.16f, 0.05f), new Color(0.35f, 0.28f, 0.22f));

        var sword = Pivot("Sword", hilt, new Vector3(0f, -0.1f, 0f));
        Part(PrimitiveType.Cube, "Blade", sword, new Vector3(0f, -0.62f, 0f),
            new Vector3(0.045f, 1.15f, 0.045f), blade);
        Part(PrimitiveType.Cube, "Guard", sword, new Vector3(0f, -0.04f, 0f),
            new Vector3(0.18f, 0.04f, 0.06f), new Color(0.85f, 0.75f, 0.45f));

        return sword;
    }

    Transform Pivot(string name, Transform parent, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    void Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
        Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }

        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = MaterialLibrary.For(color);
        if (mat != null) renderer.sharedMaterial = mat;
    }

    // ------------------------------------------------------------ 动画

    void Animate()
    {
        if (motor == null) return;

        bool grounded = motor.IsGrounded;
        float speed = motor.HorizontalSpeed;
        float speedF = Mathf.Clamp01(speed / 5.5f);
        bool moving = speedF > 0.05f && grounded;

        // 走路相位
        if (moving) _phase += Time.deltaTime * (3.5f + speedF * 7f);
        else _phase = Mathf.Lerp(_phase, 0f, 6f * Time.deltaTime);

        _idleTime += Time.deltaTime;

        float legSwing = Mathf.Sin(_phase) * 34f * speedF;
        float armSwing = Mathf.Sin(_phase) * 26f * speedF;

        if (!grounded)
        {
            // 空中：收腿、抬手
            float tuck = Mathf.Clamp01(Mathf.Abs(motor.VerticalVelocity) / 8f);
            _hipL.localRotation = Quaternion.Euler(-28f - tuck * 10f, 0f, 0f);
            _hipR.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            _shoulderL.localRotation = Quaternion.Euler(-40f, 0f, 18f);
            _shoulderR.localRotation = Quaternion.Euler(-35f, 0f, -14f);
        }
        else
        {
            _hipL.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
            _hipR.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);

            _shoulderL.localRotation = Quaternion.Euler(-armSwing, 0f, 10f);
            _shoulderR.localRotation = Quaternion.Euler(armSwing, 0f, -10f);
        }

        // 攻击时右臂主导：从举起到劈下的弧线
        if (combat != null && combat.IsAttacking)
        {
            float p = combat.SwingProgress;
            // 前段抬起，后段劈下
            float angle = p < 0.35f
                ? Mathf.Lerp(10f, -110f, p / 0.35f)
                : Mathf.Lerp(-110f, 75f, (p - 0.35f) / 0.65f);

            _shoulderR.localRotation = Quaternion.Euler(angle, -18f, -6f);
            _torso.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-18f, 22f, p), 0f);
        }
        else
        {
            _torso.localRotation = Quaternion.Slerp(_torso.localRotation, Quaternion.Euler(0f, 0f, 0f),
                10f * Time.deltaTime);
        }

        // 呼吸 / 走路起伏
        float bob = grounded ? Mathf.Sin(_phase * 2f) * 0.025f * speedF : 0f;
        float breathe = Mathf.Sin(_idleTime * 1.8f) * 0.012f * (1f - speedF);
        _pelvis.localPosition = _pelvisBase + new Vector3(0f, bob + breathe, 0f);

        // 前倾：跑得越快越前倾
        _torso.localRotation *= Quaternion.Euler(speedF * 6f, 0f, 0f);
    }
}

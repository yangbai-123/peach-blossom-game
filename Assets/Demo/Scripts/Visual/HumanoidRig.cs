using UnityEngine;

/// <summary>
/// 程序化人形角色：白衣、长发、佩剑的剑仙造型。
///
/// 只用基础图元，所以面部和布料细节有限，但轮廓、配色和体态尽量贴近设定：
/// 层叠的白裙、宽袖、及腰长发、发饰、带剑穗的长剑。
/// 想换成真实模型时禁用本组件，把 FBX 挂到 Player 下即可。
/// </summary>
public class HumanoidRig : MonoBehaviour
{
    [Header("数据来源")]
    public PlayerMotor motor;
    public PlayerCombat combat;
    public PlayerStateMachine stateMachine;

    [Header("配色")]
    public Color robe = new Color(0.96f, 0.96f, 0.98f);
    public Color robeShade = new Color(0.88f, 0.90f, 0.95f);
    public Color robeEdge = new Color(0.80f, 0.84f, 0.92f);
    public Color hairColor = new Color(0.10f, 0.09f, 0.12f);
    public Color skin = new Color(0.97f, 0.88f, 0.83f);
    public Color blade = new Color(0.90f, 0.94f, 1f);
    public Color silver = new Color(0.86f, 0.87f, 0.90f);

    Transform _pelvis, _torso, _head, _hairRoot, _hairTail;
    Transform _shoulderL, _shoulderR, _hipL, _hipR, _sword, _skirt;
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
        // 全身以 pelvis（y=0.95）为基准，下面的 y 都是世界高度减去 0.95
        _pelvis = Pivot("Pelvis", transform, new Vector3(0f, 0.95f, 0f));
        _pelvisBase = _pelvis.localPosition;

        // 层叠长裙：由腰到脚逐步外扩，保持修长
        _skirt = Pivot("Skirt", _pelvis, Vector3.zero);
        Part(PrimitiveType.Cylinder, "Waist", _skirt, new Vector3(0f, 0.07f, 0f),
            new Vector3(0.30f, 0.06f, 0.30f), robe);
        Part(PrimitiveType.Cylinder, "SkirtUpper", _skirt, new Vector3(0f, -0.15f, 0f),
            new Vector3(0.38f, 0.18f, 0.38f), robe);
        Part(PrimitiveType.Cylinder, "SkirtLower", _skirt, new Vector3(0f, -0.45f, 0f),
            new Vector3(0.46f, 0.15f, 0.46f), robeShade);
        Part(PrimitiveType.Cylinder, "SkirtHem", _skirt, new Vector3(0f, -0.73f, 0f),
            new Vector3(0.52f, 0.11f, 0.52f), robe);
        Part(PrimitiveType.Cylinder, "HemEdge", _skirt, new Vector3(0f, -0.85f, 0f),
            new Vector3(0.56f, 0.02f, 0.56f), robeEdge);

        // 上身
        _torso = Pivot("Torso", _pelvis, new Vector3(0f, 0.33f, 0f));
        Part(PrimitiveType.Cube, "Chest", _torso, Vector3.zero,
            new Vector3(0.29f, 0.42f, 0.19f), robe);
        Part(PrimitiveType.Cube, "Sash", _torso, new Vector3(0f, -0.20f, 0f),
            new Vector3(0.31f, 0.07f, 0.21f), silver);
        Part(PrimitiveType.Cube, "Collar", _torso, new Vector3(0f, 0.18f, 0.08f),
            new Vector3(0.22f, 0.07f, 0.08f), robeShade);

        // 背后飘带
        Part(PrimitiveType.Cube, "RibbonL", _torso, new Vector3(-0.09f, -0.42f, -0.09f),
            new Vector3(0.045f, 0.62f, 0.02f), robeShade);
        Part(PrimitiveType.Cube, "RibbonR", _torso, new Vector3(0.09f, -0.42f, -0.09f),
            new Vector3(0.045f, 0.62f, 0.02f), robeShade);

        // 头
        _head = Pivot("Head", _torso, new Vector3(0f, 0.35f, 0f));
        Part(PrimitiveType.Cylinder, "Neck", _head, new Vector3(0f, -0.14f, 0f),
            new Vector3(0.07f, 0.05f, 0.07f), skin);
        Part(PrimitiveType.Sphere, "Face", _head, new Vector3(0f, 0.01f, 0.01f),
            new Vector3(0.22f, 0.25f, 0.22f), skin);

        // 长发：后脑 + 头顶 + 刘海 + 侧发
        _hairRoot = Pivot("Hair", _head, new Vector3(0f, 0.02f, -0.02f));
        Part(PrimitiveType.Sphere, "HairBack", _hairRoot, new Vector3(0f, 0.03f, -0.04f),
            new Vector3(0.235f, 0.235f, 0.20f), hairColor);
        Part(PrimitiveType.Sphere, "HairTop", _hairRoot, new Vector3(0f, 0.09f, 0f),
            new Vector3(0.225f, 0.14f, 0.21f), hairColor);
        Part(PrimitiveType.Cube, "Bangs", _hairRoot, new Vector3(0f, 0.06f, 0.085f),
            new Vector3(0.21f, 0.09f, 0.06f), hairColor);
        Part(PrimitiveType.Sphere, "Bun", _hairRoot, new Vector3(0f, 0.15f, -0.09f),
            new Vector3(0.13f, 0.12f, 0.13f), hairColor);

        _hairTail = Pivot("HairTail", _hairRoot, new Vector3(0f, -0.04f, -0.10f));
        Part(PrimitiveType.Cube, "TailA", _hairTail, new Vector3(0f, -0.36f, 0f),
            new Vector3(0.19f, 0.72f, 0.09f), hairColor);
        Part(PrimitiveType.Cube, "TailB", _hairTail, new Vector3(-0.07f, -0.28f, 0.02f),
            new Vector3(0.06f, 0.55f, 0.06f), hairColor);
        Part(PrimitiveType.Cube, "TailC", _hairTail, new Vector3(0.07f, -0.28f, 0.02f),
            new Vector3(0.06f, 0.55f, 0.06f), hairColor);

        // 鬓发：细长，贴着两颊
        Part(PrimitiveType.Cube, "StrandL", _head, new Vector3(-0.115f, -0.10f, 0.03f),
            new Vector3(0.035f, 0.30f, 0.04f), hairColor);
        Part(PrimitiveType.Cube, "StrandR", _head, new Vector3(0.115f, -0.10f, 0.03f),
            new Vector3(0.035f, 0.30f, 0.04f), hairColor);

        // 发饰
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f;
            Part(PrimitiveType.Sphere, "Blossom", _head,
                new Vector3(Mathf.Cos(a) * 0.09f, 0.13f, -0.09f + Mathf.Sin(a) * 0.04f),
                Vector3.one * 0.05f, new Color(0.98f, 0.97f, 1f));
        }

        // 手臂
        _shoulderL = Pivot("ShoulderL", _torso, new Vector3(-0.21f, 0.16f, 0f));
        BuildArm(_shoulderL);

        _shoulderR = Pivot("ShoulderR", _torso, new Vector3(0.21f, 0.16f, 0f));
        BuildArm(_shoulderR);
        _sword = BuildSword(_shoulderR);

        // 腿
        _hipL = Pivot("HipL", _pelvis, new Vector3(-0.10f, -0.30f, 0f));
        _hipR = Pivot("HipR", _pelvis, new Vector3(0.10f, -0.30f, 0f));
        BuildLeg(_hipL);
        BuildLeg(_hipR);
    }

    void BuildArm(Transform shoulder)
    {
        Part(PrimitiveType.Cube, "UpperArm", shoulder, new Vector3(0f, -0.22f, 0f),
            new Vector3(0.08f, 0.44f, 0.08f), robe);

        // 宽袖
        Part(PrimitiveType.Cube, "Cuff", shoulder, new Vector3(0f, -0.36f, 0f),
            new Vector3(0.16f, 0.26f, 0.16f), robeShade);

        Part(PrimitiveType.Cube, "Hand", shoulder, new Vector3(0f, -0.53f, 0f),
            new Vector3(0.055f, 0.08f, 0.055f), skin);
    }

    void BuildLeg(Transform hip)
    {
        Part(PrimitiveType.Cube, "Leg", hip, new Vector3(0f, -0.22f, 0f),
            new Vector3(0.08f, 0.44f, 0.08f), robeShade);
        Part(PrimitiveType.Cube, "Foot", hip, new Vector3(0f, -0.45f, 0.05f),
            new Vector3(0.10f, 0.06f, 0.18f), new Color(0.95f, 0.95f, 0.97f));
    }

    Transform BuildSword(Transform shoulder)
    {
        var hand = Pivot("Hand", shoulder, new Vector3(0f, -0.55f, 0f));

        Part(PrimitiveType.Cube, "Grip", hand, new Vector3(0f, -0.04f, 0f),
            new Vector3(0.05f, 0.16f, 0.05f), new Color(0.28f, 0.22f, 0.18f));

        var sword = Pivot("Sword", hand, new Vector3(0f, -0.10f, 0f));
        Part(PrimitiveType.Cube, "Blade", sword, new Vector3(0f, -0.62f, 0f),
            new Vector3(0.035f, 1.16f, 0.035f), blade);
        Part(PrimitiveType.Cube, "Guard", sword, new Vector3(0f, -0.02f, 0f),
            new Vector3(0.16f, 0.032f, 0.055f), silver);

        // 剑穗
        Part(PrimitiveType.Cube, "Tassel", sword, new Vector3(0f, -0.07f, 0.05f),
            new Vector3(0.018f, 0.16f, 0.018f), new Color(0.95f, 0.95f, 0.97f));
        Part(PrimitiveType.Sphere, "TasselHead", sword, new Vector3(0f, -0.16f, 0.05f),
            Vector3.one * 0.032f, silver);

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

        if (moving) _phase += Time.deltaTime * (3.5f + speedF * 7f);
        else _phase = Mathf.Lerp(_phase, 0f, 6f * Time.deltaTime);

        _idleTime += Time.deltaTime;

        float legSwing = Mathf.Sin(_phase) * 30f * speedF;
        float armSwing = Mathf.Sin(_phase) * 24f * speedF;

        if (!grounded)
        {
            float tuck = Mathf.Clamp01(Mathf.Abs(motor.VerticalVelocity) / 8f);
            _hipL.localRotation = Quaternion.Euler(-26f - tuck * 8f, 0f, 0f);
            _hipR.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            _shoulderL.localRotation = Quaternion.Euler(-38f, 0f, 16f);
            _shoulderR.localRotation = Quaternion.Euler(-32f, 0f, -12f);
        }
        else
        {
            _hipL.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
            _hipR.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);

            _shoulderL.localRotation = Quaternion.Euler(-armSwing, 0f, 8f);
            _shoulderR.localRotation = Quaternion.Euler(armSwing, 0f, -8f);
        }

        // 攻击：右臂主导的劈砍弧线
        if (combat != null && combat.IsAttacking)
        {
            float p = combat.SwingProgress;
            float angle = p < 0.35f
                ? Mathf.Lerp(8f, -105f, p / 0.35f)
                : Mathf.Lerp(-105f, 70f, (p - 0.35f) / 0.65f);

            _shoulderR.localRotation = Quaternion.Euler(angle, -15f, -5f);
            _torso.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-15f, 20f, p), 0f);
        }
        else
        {
            _torso.localRotation = Quaternion.Slerp(_torso.localRotation,
                Quaternion.Euler(0f, 0f, 0f), 10f * Time.deltaTime);
        }

        // 长发与裙摆随速度后飘
        float sway = Mathf.Sin(_phase * 2f) * 4f * speedF;
        if (_hairTail != null)
            _hairTail.localRotation = Quaternion.Euler(-speedF * 12f - sway * 0.5f, 0f, sway * 0.3f);

        if (_skirt != null)
        {
            float flare = 1f + speedF * 0.06f + Mathf.Sin(_phase * 2f) * 0.01f;
            _skirt.localScale = new Vector3(flare, 1f, flare);
            _skirt.localRotation = Quaternion.Euler(0f, 0f, sway * 0.2f);
        }

        // 呼吸与步伐起伏
        float bob = grounded ? Mathf.Sin(_phase * 2f) * 0.022f * speedF : 0f;
        float breathe = Mathf.Sin(_idleTime * 1.7f) * 0.01f * (1f - speedF);
        _pelvis.localPosition = _pelvisBase + new Vector3(0f, bob + breathe, 0f);

        // 前倾
        _torso.localRotation *= Quaternion.Euler(speedF * 5f, 0f, 0f);
    }
}

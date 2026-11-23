using UnityEngine;

/// <summary>
/// 玩家相机：第三人称环绕 / 第一人称，两种视角可随时切换。
///
/// - PC：鼠标转视角，滚轮缩放，V 键切换视角
/// - 手机：右侧拖动转视角，"视角"按钮切换
/// 第一人称会隐藏角色模型（避免看到后脑勺），但保留手里的剑。
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target;

    [Tooltip("第三人称视点相对目标的偏移（肩部高度）")]
    public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);

    [Tooltip("第一人称眼睛高度")]
    public float eyeHeight = 1.58f;

    [Header("距离与角度")]
    public float distance = 4.5f;
    public float minDistance = 2.4f;

    [Tooltip("被遮挡时的最小距离")]
    public float minCollisionDistance = 2.2f;

    [Tooltip("被遮挡时镜头抬高的最大高度")]
    public float liftHeight = 2.4f;
    public float maxDistance = 9f;
    public float zoomStep = 0.6f;
    public float mouseSensitivity = 3f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Header("平滑")]
    [Tooltip("位置平滑强度，越大越跟手")]
    public float positionSmooth = 14f;

    [Header("视角模式")]
    public bool firstPerson;
    public KeyCode toggleKey = KeyCode.V;

    [Header("视野")]
    [Tooltip("横屏时的垂直 FOV")]
    public float landscapeFov = 55f;

    [Tooltip("竖屏时放宽视野，否则左右可视范围太窄")]
    public float portraitFov = 72f;

    float _yaw;
    float _pitch = 15f;

    SkinnedMeshRenderer[] _bodyRenderers;
    bool _bodyHidden;

    void Start()
    {
        CacheBodyRenderers();
    }

    void CacheBodyRenderers()
    {
        if (target == null) return;

        var model = target.Find("CharacterModel");
        if (model != null)
        {
            _bodyRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return;
        }

        _bodyRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 竖屏时放宽视野：竖屏的横向可视范围太窄，跑图会很难受
        var cam = GetComponent<Camera>();
        if (cam != null)
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            cam.fieldOfView = aspect < 1f ? portraitFov : landscapeFov;
        }

        if (Input.GetKeyDown(toggleKey) || (MobileInput.Enabled && MobileInput.ConsumeViewToggle()))
            ToggleFirstPerson();

        // 键鼠：锁定鼠标时用鼠标转视角（PC 默认）
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        }

        // 触屏：右侧拖动，叠加在上面（LookDelta 已经是角度）
        if (MobileInput.Enabled)
        {
            Vector2 look = MobileInput.LookDelta;
            if (look.sqrMagnitude > 0.0001f)
            {
                _yaw += look.x;
                _pitch += look.y;
            }
            MobileInput.LookDelta = Vector2.zero;
        }

        // 第一人称允许更大的俯仰范围
        float pitchMax = firstPerson ? 80f : maxPitch;
        float pitchMin = firstPerson ? -75f : minPitch;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // 滚轮缩放（仅第三人称）
        if (!firstPerson)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        if (firstPerson)
        {
            Vector3 chest = target.position + Vector3.up * (eyeHeight * 0.62f);
            Vector3 head = target.position + Vector3.up * eyeHeight;
            Vector3 eye = head;

            // 贴墙回避：从胸口往头部方向做个球形检测，被挡住了就把镜头拉回胸口，
            // 避免视角钻进石台/柱子内部
            Vector3 dir = head - chest;
            float dist = dir.magnitude;

            if (dist > 0.01f &&
                Physics.SphereCast(chest, 0.22f, dir.normalized, out RaycastHit fpHit,
                    dist + 0.02f, ~0, QueryTriggerInteraction.Ignore))
            {
                eye = chest + dir.normalized * Mathf.Max(0.25f, fpHit.distance - 0.02f);
            }

            // 指数平滑，避免跳跃时头部抖动
            float ft = 1f - Mathf.Exp(-24f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, eye, ft);
            transform.rotation = rotation;

            SetBodyVisible(false);
            return;
        }

        SetBodyVisible(true);

        Vector3 basePivot = target.position + pivotOffset;
        Vector3 pullDir = -(rotation * Vector3.forward);

        float allowed = distance;
        float lift = 0f;

        // 遮挡回避：被挡住时不是一味往脸上拉，而是抬高镜头从上方越过去。
        // 用 SphereCast 而不是 Linecast —— 射线起点在角色胶囊内部，
        // Linecast 会立刻命中自己。
        if (Physics.SphereCast(basePivot, 0.3f, pullDir, out RaycastHit pullHit, distance, ~0, QueryTriggerInteraction.Ignore)
            && !ShouldIgnoreHit(pullHit.collider))
        {
            allowed = Mathf.Clamp(pullHit.distance - 0.2f, minCollisionDistance, distance);

            // 挡得越狠，抬得越高
            float blocked = 1f - Mathf.Clamp01(allowed / Mathf.Max(0.01f, distance));
            lift = Mathf.Lerp(0f, liftHeight, blocked);
        }

        Vector3 pivot = basePivot + Vector3.up * lift;
        Vector3 desired = pivot + pullDir * allowed;

        float t = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
        transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
    }

    /// <summary>命中体是不是玩家自己（含子物体）。</summary>
    bool IsSelfCollider(Collider collider)
    {
        if (target == null) return false;
        return collider.transform.root == target.root;
    }

    // 植被、道具、云、花瓣不该把镜头推走，只让"实心"的墙和地形参与遮挡
    static readonly string[] IgnoredPrefixes =
    {
        "Cherry", "Bamboo", "Prop_", "Cloud", "FarPeak", "Grass", "Petal",
        "Loot", "Lantern", "Lamp", "Seam", "RoadEdge", "WetPatch", "LightPool",
    };

    bool ShouldIgnoreHit(Collider collider)
    {
        if (collider == null) return true;
        if (IsSelfCollider(collider)) return true;
        if (collider.isTrigger) return true;

        string name = collider.gameObject.name;
        foreach (var prefix in IgnoredPrefixes)
            if (name.StartsWith(prefix)) return true;

        return false;
    }

    public void ToggleFirstPerson()
    {
        firstPerson = !firstPerson;

        // 两种视角的俯仰习惯不同，切换时把极端角度收回来
        if (firstPerson)
            _pitch = Mathf.Clamp(_pitch, -10f, 20f);
        else
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        if (!firstPerson)
            SetBodyVisible(true);

        Debug.Log($"[camera] 视角切换为 {(firstPerson ? "第一人称" : "第三人称")}");
    }

    void SetBodyVisible(bool visible)
    {
        if (_bodyRenderers == null) CacheBodyRenderers();
        if (_bodyRenderers == null) return;

        if (_bodyHidden == !visible) return;

        foreach (var r in _bodyRenderers)
        {
            if (r != null) r.enabled = visible;
        }

        _bodyHidden = !visible;
    }

    /// <summary>切换跟随目标（重生后不用改）。</summary>
    public void SetTarget(Transform t)
    {
        target = t;
        CacheBodyRenderers();
    }
}

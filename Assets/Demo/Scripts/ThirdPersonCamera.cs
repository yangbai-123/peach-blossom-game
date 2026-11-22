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
    public float minDistance = 1.8f;
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
            Vector3 eye = target.position + Vector3.up * eyeHeight;

            // 指数平滑，避免跳跃时头部抖动
            float ft = 1f - Mathf.Exp(-24f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, eye, ft);
            transform.rotation = rotation;

            SetBodyVisible(false);
            return;
        }

        SetBodyVisible(true);

        Vector3 pivot = target.position + pivotOffset;
        Vector3 desired = pivot - rotation * Vector3.forward * distance;

        // 遮挡回避：镜头与视点之间有东西就把镜头拉近
        if (Physics.Linecast(pivot, desired, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = (desired - pivot).normalized;
            desired = hit.point - dir * 0.2f;
        }

        float t = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
        transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
    }

    public void ToggleFirstPerson()
    {
        firstPerson = !firstPerson;

        if (!firstPerson)
            SetBodyVisible(true);

        Debug.Log($"[camera] 视角切换为 {(firstPerson ? "第一人称" : "第三人称")}");
    }

    void SetBodyVisible(bool visible)
    {
        if (_bodyRenderers == null) CacheBodyRenderers();

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

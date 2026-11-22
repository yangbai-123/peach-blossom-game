using UnityEngine;

/// <summary>
/// 第三人称环绕相机。鼠标转视角，带简单的遮挡回避。
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target;

    [Tooltip("视点相对目标的偏移（肩部高度）")]
    public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);

    [Header("距离与角度")]
    public float distance = 4.5f;
    public float mouseSensitivity = 3f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Header("平滑")]
    [Tooltip("位置平滑强度，越大越跟手")]
    public float positionSmooth = 14f;

    float _yaw;
    float _pitch = 15f;

    void LateUpdate()
    {
        if (target == null) return;

        // 锁定鼠标时才有意义；未锁定就当作不转视角
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = target.position + pivotOffset;
        Vector3 desired = pivot - rotation * Vector3.forward * distance;

        // 遮挡回避：镜头与视点之间有东西就把镜头拉近
        if (Physics.Linecast(pivot, desired, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = (desired - pivot).normalized;
            desired = hit.point - dir * 0.2f;
        }

        // 指数平滑，和帧率无关
        float t = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
        transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
    }

    /// <summary>切换跟随目标（重生后不用改）。</summary>
    public void SetTarget(Transform t) => target = t;
}

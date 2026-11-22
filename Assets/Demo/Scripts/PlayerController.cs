using UnityEngine;

/// <summary>
/// 输入层。读键鼠 → 转成世界方向 → 调用 PlayerMotor.Tick → 驱动状态机。
/// 游戏状态不是 Playing 时（标题、结局），输入被屏蔽，角色自然停下。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("引用")]
    public PlayerMotor motor;
    public PlayerStateMachine stateMachine;
    public Transform cameraTransform;

    [Header("重生")]
    public Vector3 respawnPoint = new Vector3(0f, 0.1f, 0f);
    public float fallLimit = -20f;

    void Reset()
    {
        motor = GetComponent<PlayerMotor>();
        stateMachine = GetComponent<PlayerStateMachine>();
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        // Esc 释放/锁定鼠标（标题界面不锁）
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

        bool canMove = GameManager.Instance != null && GameManager.Instance.CanPlayerMove;

        if (!canMove)
        {
            // 仍然 Tick 一次零输入，让速度自然收敛、状态机回到 Idle
            motor.Tick(Vector3.zero, false, false);
            if (stateMachine != null) stateMachine.Tick(motor);
            return;
        }

        // 1. 原始输入
        Vector2 raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        // 2. 相机相对方向
        Vector3 dir = CameraRelative(raw);

        // 3. 动作
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jumpPressed = Input.GetButtonDown("Jump");

        // 4. 驱动
        motor.Tick(dir, sprint, jumpPressed);
        if (stateMachine != null) stateMachine.Tick(motor);

        // 5. 掉出世界
        if (transform.position.y < fallLimit) Respawn();
    }

    Vector3 CameraRelative(Vector2 input)
    {
        if (cameraTransform == null) return new Vector3(input.x, 0f, input.y);

        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        Vector3 dir = forward * input.y + right * input.x;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    public void Respawn()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = respawnPoint;
        transform.rotation = Quaternion.identity;

        if (cc != null) cc.enabled = true;
        motor.ResetVelocity();
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}

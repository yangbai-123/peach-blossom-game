using UnityEngine;

/// <summary>
/// 弹跳台：把玩家弹到指定的落点。
///
/// 这里刻意不用 OnTriggerEnter —— CharacterController 的触发器事件在某些移动方式下不可靠，
/// 改成每帧做距离判定；同时按抛物线反解横向速度，确保落点稳定，
/// 玩家站上去什么都不按也能准确落到岛上。
/// </summary>
public class BouncePad : MonoBehaviour
{
    [Header("落点")]
    [Tooltip("弹射目标（岛中心）。留空则只向上弹。")]
    public Transform target;

    [Tooltip("最高点相对起跳点的高度（米），要比目标高 2 米左右")]
    public float launchHeight = 9f;

    [Header("触发")]
    public float radius = 1.7f;
    public float cooldown = 0.8f;

    Transform _player;
    float _nextLaunchTime;
    float _spin;

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;
    }

    void Update()
    {
        // 装饰环自转
        _spin += Time.deltaTime * 45f;
        transform.localRotation = Quaternion.Euler(0f, _spin, 0f);

        if (_player == null) return;
        if (Time.time < _nextLaunchTime) return;
        if (GameManager.Instance != null && !GameManager.Instance.CanPlayerMove) return;

        Vector3 pad = transform.position;
        Vector3 player = _player.position;

        float flatDistance = Vector2.Distance(new Vector2(pad.x, pad.z), new Vector2(player.x, player.z));
        if (flatDistance > radius) return;
        if (Mathf.Abs(player.y - pad.y) > 2.5f) return;

        Launch();
    }

    void Launch()
    {
        var motor = _player.GetComponent<PlayerMotor>();
        if (motor == null) return;

        float g = Mathf.Abs(motor.gravity);
        float vy = Mathf.Sqrt(2f * g * launchHeight);

        Vector3 horizontal = Vector3.zero;

        if (target != null)
        {
            Vector3 delta = target.position - _player.position;
            delta.y = 0f;

            float distance = delta.magnitude;
            float heightGain = target.position.y - _player.position.y;

            // 解 y(t) = vy*t - g*t²/2 = heightGain，取下降段（更自然）
            float discriminant = vy * vy - 2f * g * heightGain;
            float airTime = discriminant > 0f
                ? (vy + Mathf.Sqrt(discriminant)) / g
                : 2f * vy / g;

            if (distance > 0.01f && airTime > 0.05f)
            {
                horizontal = delta.normalized * (distance / airTime);
                motor.SnapFacing(delta, 0.4f);
            }
        }

        motor.Launch(horizontal, vy);

        Debug.Log($"[pad] launch height={launchHeight} target={(target != null ? target.position.ToString() : "none")}");

        Sfx.Play(Sfx.Swing, 0.6f, 1.45f);
        Fx.Burst(transform.position + Vector3.up * 0.4f,
            new Color(1f, 0.86f, 0.5f), 10, 4f, 0.45f, 0.09f, -6f);

        _nextLaunchTime = Time.time + cooldown;
    }
}

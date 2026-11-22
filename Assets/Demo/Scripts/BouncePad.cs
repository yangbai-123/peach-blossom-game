using UnityEngine;

/// <summary>
/// 弹跳台：踩上去把人弹到指定高度。用来上浮空岛。
/// </summary>
public class BouncePad : MonoBehaviour
{
    [Tooltip("弹起高度（米）")]
    public float launchHeight = 7f;

    [Tooltip("两次弹跳之间的冷却，避免连续触发")]
    public float cooldown = 0.6f;

    float _nextAllowedTime;

    void Update()
    {
        // 旋转的装饰环
        transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) Launch(other.gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        // CharacterController 移动时有时只触发 Stay，不触发 Enter
        if (other.CompareTag("Player")) Launch(other.gameObject);
    }

    void Launch(GameObject player)
    {
        if (Time.time < _nextAllowedTime) return;

        var motor = player.GetComponent<PlayerMotor>();
        if (motor == null) return;

        motor.AddImpulse(launchHeight);
        _nextAllowedTime = Time.time + cooldown;
    }
}

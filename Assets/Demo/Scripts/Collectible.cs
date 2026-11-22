using UnityEngine;

/// <summary>
/// 可拾取的花瓣。
/// 触发器 + 距离双保险：就算 CharacterController 的触发器事件没触发，
/// 靠近到 pickupRadius 也会被拾取，避免出现"看得见吃不到"。
/// </summary>
public class Collectible : MonoBehaviour
{
    public float spinSpeed = 80f;
    public float bobAmplitude = 0.18f;
    public float bobSpeed = 1.8f;
    public float pickupRadius = 1.1f;

    Transform _player;
    Vector3 _basePosition;
    float _phase;

    void Start()
    {
        _basePosition = transform.position;
        _phase = Random.Range(0f, Mathf.PI * 2f);

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // 轻微摇摆，像被风吹起来的花瓣
        float sway = Mathf.Sin(Time.time * bobSpeed * 0.8f + _phase) * 8f;
        var euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(sway, euler.y, sway * 0.6f);

        float y = _basePosition.y + Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude;
        transform.position = new Vector3(_basePosition.x, y, _basePosition.z);

        if (_player != null)
        {
            float dist = Vector3.Distance(_player.position + Vector3.up * 0.9f, transform.position);
            if (dist < pickupRadius) Pick();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) Pick();
    }

    void Pick()
    {
        if (GameManager.Instance == null || !gameObject.activeSelf) return;

        GameManager.Instance.Collect();

        Sfx.Play(Sfx.Pickup, 0.8f, Random.Range(0.95f, 1.08f));
        Fx.Burst(transform.position, new Color(1f, 0.72f, 0.85f), 14, 2.8f, 0.55f, 0.07f, -3f);

        Destroy(gameObject);
    }
}

using UnityEngine;

/// <summary>
/// 剑碑：集齐花瓣后剑会升起、光环亮起，走进光柱即结局。
/// </summary>
public class ShrineGate : MonoBehaviour
{
    [Header("引用")]
    public Transform sword;
    public Light glow;
    public Transform player;

    [Header("动作")]
    public float lockedY = 3.2f;
    public float unlockedY = 7.4f;
    public float riseSpeed = 1.2f;
    public float finishRadius = 2.6f;

    float _currentY;
    bool _finished;

    void Start()
    {
        if (sword != null) _currentY = lockedY;
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        bool unlocked = GameManager.Instance.ShrineUnlocked;
        float targetY = unlocked ? unlockedY : lockedY;
        _currentY = Mathf.MoveTowards(_currentY, targetY, riseSpeed * Time.deltaTime);

        if (sword != null)
        {
            sword.localPosition = new Vector3(0f, _currentY, 0f);
            // 未解锁时缓慢沉浮，解锁后缓慢自转
            float spin = unlocked ? 60f : 20f;
            sword.Rotate(Vector3.up, spin * Time.deltaTime, Space.Self);
        }

        if (glow != null)
        {
            float targetIntensity = unlocked ? 4.5f : 0.6f;
            glow.intensity = Mathf.MoveTowards(glow.intensity, targetIntensity, 2.5f * Time.deltaTime);
        }

        // 走进光柱
        if (!unlocked || _finished) return;
        if (player == null) return;

        float dist = Vector3.Distance(
            new Vector3(player.position.x, transform.position.y, player.position.z),
            transform.position);

        if (dist < finishRadius)
        {
            _finished = true;
            GameManager.Instance.EndGame();
        }
    }
}

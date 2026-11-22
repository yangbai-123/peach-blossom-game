using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Title,
    Playing,
    Ending,
}

/// <summary>
/// 全局流程：标题 → 游玩 → 结局。收集计数、计时都放这里。
/// 场景里只需要挂一个。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("目标")]
    [Tooltip("全图花瓣总数，收集满即解锁剑碑")]
    public int totalPetals = 12;

    public GameState State { get; private set; } = GameState.Title;
    public bool CanPlayerMove => State == GameState.Playing;

    public int Collected { get; private set; }
    public int Kills { get; private set; }
    public float PlayTime { get; private set; }
    public bool ShrineUnlocked => Collected >= totalPetals;

    /// <summary>玩家受伤时屏幕泛红，0..1</summary>
    public float DamageFlash { get; private set; }

    float _flashUntil;

    public event System.Action PetalCollected;
    public event System.Action ShrineUnlockedEvent;
    public event System.Action GameEnded;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (State == GameState.Playing) PlayTime += Time.deltaTime;

        float remain = _flashUntil - Time.time;
        DamageFlash = remain > 0f ? Mathf.Clamp01(remain / 0.4f) : 0f;
    }

    public void OnPlayerDamaged()
    {
        _flashUntil = Time.time + 0.4f;
    }

    public void OnEnemyKilled()
    {
        Kills++;
    }

    /// <summary>玩家死亡：短暂等待后原地复活。</summary>
    public void HandlePlayerDeath(PlayerController controller, Health health)
    {
        if (State != GameState.Playing) return;
        StartCoroutine(RespawnRoutine(controller, health));
    }

    System.Collections.IEnumerator RespawnRoutine(PlayerController controller, Health health)
    {
        yield return new WaitForSeconds(1.2f);
        if (controller != null) controller.Respawn();
        if (health != null) health.ResetHealth();
    }

    public void StartGame()
    {
        if (State != GameState.Title) return;
        State = GameState.Playing;
        PlayTime = 0f;
        PlayerController.SetCursorLocked(true);
    }

    /// <summary>拾取一片花瓣。超过总数后忽略。</summary>
    public void Collect()
    {
        if (State != GameState.Playing || ShrineUnlocked) return;

        Collected++;
        PetalCollected?.Invoke();

        if (ShrineUnlocked) ShrineUnlockedEvent?.Invoke();
    }

    public void EndGame()
    {
        if (State != GameState.Playing || !ShrineUnlocked) return;
        State = GameState.Ending;
        PlayerController.SetCursorLocked(false);
        GameEnded?.Invoke();
    }

    public void Restart()
    {
        PlayerController.SetCursorLocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Title,
    Playing,
    Ending,
}

/// <summary>玩法模式。</summary>
public enum GameMode
{
    /// <summary>寻剑：收集 12 片花瓣，登顶唤醒剑碑。</summary>
    Shrine,

    /// <summary>烽火地带：限时搜集物资并撤离。</summary>
    Extraction,

    /// <summary>联机对枪：和朋友 1v1。</summary>
    Duel,
}

/// <summary>
/// 全局流程：标题 → 游玩 → 结局。收集计数、计时、模式都放这里。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("目标")]
    [Tooltip("全图花瓣总数，收集满即解锁剑碑")]
    public int totalPetals = 12;

    public GameState State { get; private set; } = GameState.Title;
    public GameMode Mode { get; private set; } = GameMode.Shrine;
    public GameMode PendingMode { get; set; } = GameMode.Shrine;

    public bool CanPlayerMove => State == GameState.Playing;

    public int Collected { get; private set; }
    public int Kills { get; private set; }
    public float PlayTime { get; private set; }
    public bool ShrineUnlocked => Collected >= totalPetals;

    /// <summary>本局结果（结局界面用）。</summary>
    public bool Success { get; private set; }
    public string EndReason { get; private set; } = "";

    /// <summary>玩家受伤时屏幕泛红，0..1</summary>
    public float DamageFlash { get; private set; }

    float _flashUntil;

    public event System.Action PetalCollected;
    public event System.Action ShrineUnlockedEvent;
    public event System.Action GameEnded;
    public event System.Action ModeStarted;

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

    /// <summary>玩家死亡：寻剑模式原地复活；烽火地带直接失败。</summary>
    public void HandlePlayerDeath(PlayerController controller, Health health)
    {
        if (State != GameState.Playing) return;

        if (Mode == GameMode.Extraction)
        {
            EndGame(false, "你在撤离前倒下了");
            return;
        }

        StartCoroutine(RespawnRoutine(controller, health));
    }

    System.Collections.IEnumerator RespawnRoutine(PlayerController controller, Health health)
    {
        yield return new WaitForSeconds(1.2f);
        if (controller != null) controller.Respawn();
        if (health != null) health.ResetHealth();
    }

    public void StartGame() => StartGame(PendingMode);

    public void StartGame(GameMode mode)
    {
        if (State != GameState.Title) return;

        Mode = mode;
        State = GameState.Playing;
        PlayTime = 0f;
        Collected = 0;
        Kills = 0;
        Success = false;
        EndReason = "";

        PlayerController.SetCursorLocked(true);
        ModeStarted?.Invoke();
    }

    /// <summary>拾取一片花瓣。超过总数后忽略。</summary>
    public void Collect()
    {
        if (State != GameState.Playing || Mode != GameMode.Shrine || ShrineUnlocked) return;

        Collected++;
        PetalCollected?.Invoke();

        if (ShrineUnlocked) ShrineUnlockedEvent?.Invoke();
    }

    /// <summary>寻剑模式：走进光柱。</summary>
    public void EndGame()
    {
        EndGame(true, "剑已归鞘");
    }

    public void EndGame(bool success, string reason)
    {
        if (State != GameState.Playing) return;

        State = GameState.Ending;
        Success = success;
        EndReason = reason;

        // 通关成绩只记录成功的一次
        if (success) UserProfile.SubmitTime(PlayTime);

        PlayerController.SetCursorLocked(false);
        GameEnded?.Invoke();
    }

    public void Restart()
    {
        PlayerController.SetCursorLocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

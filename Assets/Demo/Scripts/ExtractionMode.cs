using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 烽火地带：限时搜集物资，最后在撤离点待满时间完成撤离。
/// 死亡或超时算失败。
/// </summary>
public class ExtractionMode : MonoBehaviour
{
    public static ExtractionMode Instance { get; private set; }

    [Header("规则")]
    public float timeLimit = 900f;
    public float zoneActivateAt = 180f;
    public float extractHoldTime = 6f;

    /// <summary>撤离点开放时间（秒，开局后计时），HUD 用它显示文案。</summary>
    public float ZoneActivateAt => zoneActivateAt;
    public int lootCount = 14;

    [Header("撤离点")]
    public Vector3 zoneCenter = new Vector3(0f, 5.4f, 32f);
    public float zoneRadius = 3.4f;

    public float TimeLeft { get; private set; }
    public bool ZoneActive { get; private set; }
    public float ExtractProgress { get; private set; }
    public bool Playing { get; private set; }
    public int LootCollected { get; private set; }
    public int LootTotal { get; private set; }

    Transform _player;
    readonly List<GameObject> _loot = new List<GameObject>();
    GameObject _zoneVisual;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.ModeStarted += OnModeStarted;
    }

    void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.ModeStarted -= OnModeStarted;
    }

    void OnModeStarted()
    {
        if (GameManager.Instance == null || GameManager.Instance.Mode != GameMode.Extraction) return;
        Begin();
    }

    void Begin()
    {
        Playing = true;
        TimeLeft = timeLimit;

        // 两个撤离点随机选一个：峰顶剑台 / 山腰西台
        zoneCenter = Random.value > 0.5f
            ? new Vector3(0f, 5.4f, 32f)
            : new Vector3(-14f, 3.8f, 20f);
        ZoneActive = false;
        ExtractProgress = 0f;
        LootCollected = 0;

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        // 寻剑模式的收集物不参与本局
        foreach (var c in FindObjectsByType<Collectible>(FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);

        SpawnLoot();
        SpawnZoneVisual();

        Debug.Log($"[extraction] 开始：{timeLimit} 秒，{LootTotal} 个物资点");
    }

    void SpawnLoot()
    {
        _loot.Clear();

        for (int i = 0; i < lootCount; i++)
        {
            Vector3 pos = RandomGroundPosition();
            var crate = LootCrate.Create(pos, transform);
            if (crate != null) _loot.Add(crate.gameObject);
        }

        LootTotal = _loot.Count;
    }

    Vector3 RandomGroundPosition()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = Random.Range(-26f, 26f);
            float z = Random.Range(-26f, 26f);

            // 避开出生点正压和湖面
            if (new Vector2(x, z).magnitude < 6f) continue;
            if (Mathf.Abs(x - 22f) < 8f && Mathf.Abs(z - 4f) < 8f) continue;

            if (Physics.Raycast(new Vector3(x, 20f, z), Vector3.down, out RaycastHit hit, 40f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * 0.5f;
            }
        }

        return new Vector3(Random.Range(-20f, 20f), 1f, Random.Range(-20f, 20f));
    }

    void SpawnZoneVisual()
    {
        _zoneVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _zoneVisual.name = "ExtractionZone";
        _zoneVisual.transform.SetParent(transform);
        _zoneVisual.transform.position = zoneCenter;
        _zoneVisual.transform.localScale = new Vector3(zoneRadius * 2f, 0.05f, zoneRadius * 2f);

        var col = _zoneVisual.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }

        var renderer = _zoneVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = MaterialLibrary.For(new Color(0.45f, 0.95f, 0.75f));
            if (mat != null) renderer.sharedMaterial = mat;
        }

        _zoneVisual.SetActive(false);
    }

    void Update()
    {
        if (!Playing || GameManager.Instance == null) return;
        if (GameManager.Instance.State != GameState.Playing || GameManager.Instance.Mode != GameMode.Extraction) return;

        TimeLeft -= Time.deltaTime;

        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            Finish(false, $"撤离超时 · 搜集 {LootCollected}/{LootTotal}");
            return;
        }

        if (!ZoneActive && timeLimit - TimeLeft >= zoneActivateAt)
        {
            ZoneActive = true;
            if (_zoneVisual != null) _zoneVisual.SetActive(true);
            Debug.Log("[extraction] 撤离点已开放");
        }

        if (_zoneVisual != null && ZoneActive)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.05f;
            _zoneVisual.transform.localScale = new Vector3(zoneRadius * 2f * pulse, 0.05f, zoneRadius * 2f * pulse);
        }

        if (ZoneActive && _player != null)
        {
            Vector3 delta = _player.position - zoneCenter;
            delta.y = 0f;

            if (delta.magnitude <= zoneRadius)
            {
                ExtractProgress += Time.deltaTime;

                if (ExtractProgress >= extractHoldTime)
                {
                    Finish(true, $"成功撤离 · 搜集 {LootCollected}/{LootTotal}");
                    return;
                }
            }
            else if (ExtractProgress > 0f)
            {
                ExtractProgress = Mathf.Max(0f, ExtractProgress - Time.deltaTime * 1.5f);
            }
        }
    }

    public void OnLootCollected()
    {
        LootCollected++;
    }

    void Finish(bool success, string reason)
    {
        Playing = false;
        GameManager.Instance.EndGame(success, reason);
    }
}

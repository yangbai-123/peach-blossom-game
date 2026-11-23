using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 联机对枪：PeerJS + WebRTC 直连，房间号加入。
///
/// 协议（| 分隔的文本）：
///   S            开局
///   T|x|y|z|yaw  位置同步（10Hz）
///   H|dmg        我打中了你
///   W            我赢了
///   L            我倒了
///   B            对手离开
/// </summary>
public class NetDuel : MonoBehaviour
{
    public enum State { Offline, Hosting, Joining, Connected, Playing, Finished }

    public static NetDuel Instance { get; private set; }

    public State Current { get; private set; } = State.Offline;
    public string RoomCode { get; private set; } = "";
    public string Status { get; private set; } = "";
    public bool IsHost { get; private set; }

    public Health LocalHealth { get; private set; }
    public Health RemoteHealth { get; private set; }
    public Transform RemoteAvatar { get; private set; }

    Transform _player;
    PlayerCombat _combat;
    float _sendTimer;
    float _winShownAt;
    bool _remoteDeadSent;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void DSH_NetHost();
    [DllImport("__Internal")] static extern void DSH_NetJoin(string room);
    [DllImport("__Internal")] static extern void DSH_NetSend(string message);
    [DllImport("__Internal")] static extern void DSH_NetClose();
#endif

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
            LocalHealth = playerObj.GetComponent<Health>();
            _combat = playerObj.GetComponent<PlayerCombat>();
        }

        var gm = GameManager.Instance;
        if (gm != null) gm.GameEnded += OnGameEnded;
    }

    void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.GameEnded -= OnGameEnded;
    }

    // ------------------------------------------------------------ 房间

    public void Host()
    {
        IsHost = true;
        Status = "正在创建房间…";
        Current = State.Hosting;

#if UNITY_WEBGL && !UNITY_EDITOR
        DSH_NetHost();
#else
        OnNetError("联机仅在网页版可用");
#endif
    }

    public void Join(string code)
    {
        code = (code ?? "").Trim();
        if (code.Length < 4) { Status = "请输入房间号"; return; }

        IsHost = false;
        Status = "正在加入…";
        Current = State.Joining;

#if UNITY_WEBGL && !UNITY_EDITOR
        DSH_NetJoin(code);
#else
        OnNetError("联机仅在网页版可用");
#endif
    }

    public void Leave()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        DSH_NetClose();
#endif
        Current = State.Offline;
        Status = "";
        RoomCode = "";
        DestroyRemote();
    }

    // 由 SendMessage 调用 -------------------------------------------------

    public void OnReady(string id)
    {
        RoomCode = id;
        Status = "房间号 " + id + " · 等待对手";
        Debug.Log("[net] 房间号 " + id);
    }

    public void OnPeerJoined(string _)
    {
        Current = State.Connected;
        Status = "已连接";

        if (IsHost)
        {
            Send("S");
            BeginDuel(true);
        }
    }

    public void OnPeerLeft(string _)
    {
        Status = "对手已离开";
        Current = State.Finished;
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            GameManager.Instance.EndGame(false, "对手离开了");
    }

    public void OnNetError(string message)
    {
        Status = "联机失败：" + message;
        Current = State.Offline;
    }

    public void OnData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;

        var parts = data.Split('|');

        switch (parts[0])
        {
            case "S":
                BeginDuel(false);
                break;

            case "T" when parts.Length >= 6:
                ApplyRemoteTransform(parts);
                break;

            case "H" when parts.Length >= 2:
                if (LocalHealth != null && float.TryParse(parts[1], out float dmg))
                {
                    LocalHealth.TakeDamage(dmg, transform.position, 0f);
                    GameManager.Instance?.OnPlayerDamaged();

                    if (LocalHealth.IsDead)
                    {
                        Send("W");
                        GameManager.Instance?.EndGame(false, "你被对手击倒");
                    }
                }
                break;

            case "W":
                GameManager.Instance?.EndGame(false, "你被对手击倒");
                break;

            case "L":
                GameManager.Instance?.EndGame(true, "对手倒下了");
                break;

            case "B":
                Status = "对手离开了房间";
                break;
        }
    }

    // ------------------------------------------------------------ 对局

    void BeginDuel(bool host)
    {
        if (Current == State.Playing) return;
        Current = State.Playing;

        var gm = GameManager.Instance;
        if (gm == null) return;

        // 清掉 AI 与花瓣，只留 1v1
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) Destroy(e.gameObject);
        foreach (var c in FindObjectsByType<Collectible>(FindObjectsSortMode.None)) Destroy(c.gameObject);

        SpawnRemoteAvatar(host);
        MoveLocalPlayerToStart(host);

        if (gm.State == GameState.Title) gm.StartGame(GameMode.Duel);

        Status = "对局开始";
        Debug.Log("[duel] 开始，host=" + host);
    }

    /// <summary>对枪开局：双方分到场地两侧，避免出生点重合。</summary>
    void MoveLocalPlayerToStart(bool host)
    {
        var bootstrap = FindFirstObjectByType<GameBootstrap>();
        if (bootstrap == null) return;

        Vector3 spot = host ? new Vector3(0f, 0.4f, -8f) : new Vector3(0f, 0.4f, 8f);
        bootstrap.Teleport(spot);
    }

    void SpawnRemoteAvatar(bool host)
    {
        if (RemoteAvatar != null) return;
        if (_player == null) return;

        Vector3 offset = host ? new Vector3(0f, 0f, 8f) : new Vector3(0f, 0f, -8f);
        Vector3 pos = _player.position + offset;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "RemotePlayer";
        go.transform.position = pos;

        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = MaterialLibrary.For(new Color(0.95f, 0.55f, 0.6f));
            if (mat != null) renderer.sharedMaterial = mat;
        }

        // 命中盒
        var hit = new GameObject("Hitbox");
        hit.transform.SetParent(go.transform, false);
        hit.transform.localPosition = Vector3.zero;
        var hitCol = hit.AddComponent<CapsuleCollider>();
        hitCol.isTrigger = true;
        hitCol.radius = 0.5f;
        hitCol.height = 2f;

        var health = go.AddComponent<Health>();
        health.maxHealth = 100f;
        health.ResetHealth();

        go.AddComponent<RemotePlayer>();

        RemoteAvatar = go.transform;
        RemoteHealth = health;
    }

    void DestroyRemote()
    {
        if (RemoteAvatar != null) Destroy(RemoteAvatar.gameObject);
        RemoteAvatar = null;
        RemoteHealth = null;
    }

    void ApplyRemoteTransform(string[] p)
    {
        if (RemoteAvatar == null) return;

        if (float.TryParse(p[1], out float x) &&
            float.TryParse(p[2], out float y) &&
            float.TryParse(p[3], out float z) &&
            float.TryParse(p[4], out float yaw))
        {
            var target = new Vector3(x, y, z);
            RemoteAvatar.position = Vector3.Lerp(RemoteAvatar.position, target, 0.5f);
            RemoteAvatar.rotation = Quaternion.Lerp(RemoteAvatar.rotation,
                Quaternion.Euler(0f, yaw, 0f), 0.5f);
        }

        if (p.Length >= 6 && RemoteHealth != null && float.TryParse(p[5], out float hp))
            RemoteHealth.SetCurrent(hp);
    }

    /// <summary>本地玩家打中了远端角色。</summary>
    public void ReportHit(float damage)
    {
        Send("H|" + damage.ToString("0.#"));
    }

    void Send(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        DSH_NetSend(message);
#endif
    }

    void Update()
    {
        if (Current != State.Playing || _player == null) return;

        _sendTimer += Time.deltaTime;
        if (_sendTimer < 0.1f) return;
        _sendTimer = 0f;

        float hp = LocalHealth != null ? LocalHealth.Current : 0f;
        string msg = $"T|{_player.position.x:0.00}|{_player.position.y:0.00}|{_player.position.z:0.00}|{_player.eulerAngles.y:0.0}|{hp:0.#}";
        Send(msg);

        // 远端被打死：我们赢
        if (RemoteHealth != null && RemoteHealth.IsDead && !_remoteDeadSent)
        {
            _remoteDeadSent = true;
            Send("L");
            GameManager.Instance?.EndGame(true, "对手倒下了");
        }
    }

    void OnGameEnded()
    {
        if (Current == State.Playing) Current = State.Finished;
    }
}

/// <summary>远端玩家的标记组件，用于区分伤害来源。</summary>
public class RemotePlayer : MonoBehaviour { }

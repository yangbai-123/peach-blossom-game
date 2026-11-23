using UnityEngine;

/// <summary>烽火地带里的物资箱：靠近即可拾取，随机给一种增益。</summary>
public class LootCrate : MonoBehaviour
{
    public enum Kind { Medkit, Power, Swift }

    public Kind kind;
    public float pickupRadius = 1.4f;

    Transform _player;
    float _bob;
    Vector3 _base;

    public static LootCrate Create(Vector3 position, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "LootCrate";
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);

        var col = go.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }

        var crate = go.AddComponent<LootCrate>();
        crate.kind = (Kind)Random.Range(0, 3);

        Color c = crate.kind switch
        {
            Kind.Medkit => new Color(1f, 0.45f, 0.45f),
            Kind.Power => new Color(1f, 0.72f, 0.32f),
            _ => new Color(0.45f, 0.82f, 1f),
        };

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = MaterialLibrary.For(c);
            if (mat != null) renderer.sharedMaterial = mat;
        }

        // 底座光环
        var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        glow.name = "Glow";
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        glow.transform.localScale = new Vector3(2.6f, 0.02f, 2.6f);

        var glowCol = glow.GetComponent<Collider>();
        if (glowCol != null) { glowCol.enabled = false; Destroy(glowCol); }

        var glowRenderer = glow.GetComponent<Renderer>();
        if (glowRenderer != null)
        {
            var mat = MaterialLibrary.For(new Color(c.r, c.g, c.b, 0.6f));
            if (mat != null) glowRenderer.sharedMaterial = mat;
        }

        return crate;
    }

    void Start()
    {
        _base = transform.position;
        _bob = Random.Range(0f, Mathf.PI * 2f);

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        transform.position = _base + Vector3.up * Mathf.Sin(Time.time * 2f + _bob) * 0.15f;

        if (_player == null) return;

        Vector3 delta = _player.position + Vector3.up * 0.8f - transform.position;
        delta.y = 0f;
        if (delta.magnitude <= pickupRadius) Collect();
    }

    void Collect()
    {
        var buffs = _player != null ? _player.GetComponent<PlayerBuffs>() : null;
        string label = "物资";

        if (buffs != null)
        {
            switch (kind)
            {
                case Kind.Medkit:
                    buffs.Heal(35f);
                    label = "医疗包 +35";
                    break;
                case Kind.Power:
                    buffs.AddDamage(1.6f, 14f);
                    label = "攻击强化 ×1.6";
                    break;
                case Kind.Swift:
                    buffs.AddSpeed(1.45f, 12f);
                    label = "疾行 ×1.45";
                    break;
            }
        }

        ExtractionMode.Instance?.OnLootCollected();
        Sfx.Play(Sfx.Pickup, 0.85f, Random.Range(0.95f, 1.1f));
        Fx.Burst(transform.position, new Color(1f, 0.85f, 0.5f), 12, 2.5f, 0.5f, 0.08f, -4f);

        GameBootstrap.NotifyLoot(label);
        Debug.Log("[loot] " + label);

        Destroy(gameObject);
    }
}

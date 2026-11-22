using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 轻量粒子：用缩小的立方体模拟火花/花瓣，不依赖粒子系统与透明材质。
/// 命中、死亡、拾取都用它。
/// </summary>
public class FxParticle : MonoBehaviour
{
    Vector3 _velocity;
    float _life;
    float _maxLife;
    float _size;
    float _gravity = -10f;

    public void Init(Vector3 velocity, float life, float size, float gravity)
    {
        _velocity = velocity;
        _life = life;
        _maxLife = Mathf.Max(0.01f, life);
        _size = size;
        _gravity = gravity;
        transform.localScale = Vector3.one * size;
    }

    void Update()
    {
        _velocity.y += _gravity * Time.deltaTime;
        transform.position += _velocity * Time.deltaTime;
        transform.Rotate(Vector3.up, 240f * Time.deltaTime, Space.World);

        _life -= Time.deltaTime;
        float t = Mathf.Clamp01(_life / _maxLife);
        transform.localScale = Vector3.one * (_size * t);

        if (_life <= 0f) Destroy(gameObject);
    }
}

/// <summary>
/// 特效与打击感的静态入口。
/// </summary>
public static class Fx
{
    static Transform _root;
    static FxRunner _runner;

    static void EnsureRoot()
    {
        if (_root != null) return;
        var go = new GameObject("Fx");
        Object.DontDestroyOnLoad(go);
        _root = go.transform;

        _runner = go.AddComponent<FxRunner>();
    }

    static Material MaterialFor(Color color)
    {
        // 统一走 MaterialLibrary：它优先用 Resources 里的材质资产，
        // 保证着色器被打进包里（否则打包后是洋红）。
        return MaterialLibrary.For(color);
    }

    /// <summary>向四周炸开一团小方块。</summary>
    public static void Burst(Vector3 position, Color color, int count, float speed, float life, float size,
        float gravity = -10f)
    {
        EnsureRoot();
        var mat = MaterialFor(color);

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_root, false);
            go.transform.position = position;

            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Object.Destroy(col); }

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && mat != null) renderer.sharedMaterial = mat;

            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;

            var particle = go.AddComponent<FxParticle>();
            particle.Init(dir * speed * Random.Range(0.5f, 1f),
                life * Random.Range(0.7f, 1.2f),
                size * Random.Range(0.7f, 1.3f),
                gravity);
        }
    }

    /// <summary>挥剑时的弧形火花。</summary>
    public static void SwingArc(Vector3 position, Vector3 forward, Color color)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f;
            Vector3 offset = Quaternion.AngleAxis(Mathf.Lerp(-70f, 70f, t), Vector3.up) * forward * 1.1f;
            Burst(position + offset + Vector3.up * 1.0f, color, 1, 1.2f, 0.22f, 0.06f, -2f);
        }
    }

    /// <summary>命中瞬间的短暂顿帧。</summary>
    public static void HitStop(float seconds = 0.06f)
    {
        EnsureRoot();
        _runner.StartCoroutine(_runner.StopTime(seconds));
    }
}

/// <summary>承载顿帧协程的小对象，避免每处都建 MonoBehaviour。</summary>
public class FxRunner : MonoBehaviour
{
    public IEnumerator StopTime(float seconds)
    {
        Time.timeScale = 0.08f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = 1f;
    }
}

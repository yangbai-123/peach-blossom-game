using UnityEngine;

/// <summary>
/// 程序化音效：运行时合成波形，不依赖任何音频文件。
/// 音色很基础（噪声 + 正弦），但足以提供挥砍、命中、受伤、拾取的反馈。
/// </summary>
public static class Sfx
{
    public static AudioClip Swing { get; private set; }
    public static AudioClip Hit { get; private set; }
    public static AudioClip Hurt { get; private set; }
    public static AudioClip Pickup { get; private set; }
    public static AudioClip Death { get; private set; }

    static AudioSource _source;
    static bool _ready;

    public static void Init()
    {
        if (_ready) return;
        _ready = true;

        var go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.volume = 0.8f;

        Swing = Build("swing", 0.20f, t =>
        {
            float env = Mathf.Exp(-t * 16f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.2f));
            float noise = Noise(t) * 0.55f;
            float sweep = Mathf.Sin(2f * Mathf.PI * (500f + 1500f * t) * t) * 0.25f;
            return (noise + sweep) * env;
        });

        Hit = Build("hit", 0.16f, t =>
        {
            float env = Mathf.Exp(-t * 30f);
            float body = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.7f;
            float crack = Noise(t) * 0.6f;
            return (body + crack) * env;
        });

        Hurt = Build("hurt", 0.22f, t =>
        {
            float env = Mathf.Exp(-t * 12f);
            float f = Mathf.Lerp(260f, 120f, Mathf.Clamp01(t / 0.22f));
            float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.35f;
            return (tone + Noise(t) * 0.25f) * env;
        });

        Pickup = Build("pickup", 0.28f, t =>
        {
            float env = Mathf.Exp(-t * 9f);
            float a = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.35f;
            float b = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.25f;
            return (a + b) * env;
        });

        Death = Build("death", 0.45f, t =>
        {
            float env = Mathf.Exp(-t * 6f);
            float f = Mathf.Lerp(420f, 70f, Mathf.Clamp01(t / 0.45f));
            return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.45f + Noise(t) * 0.25f) * env;
        });
    }

    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (!_ready || _source == null || clip == null) return;
        _source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        _source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // ------------------------------------------------------------ 合成

    static AudioClip Build(string name, float seconds, System.Func<float, float> sample)
    {
        const int rate = 44100;
        int count = Mathf.CeilToInt(seconds * rate);
        var data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Clamp(sample(t), -1f, 1f);
        }

        var clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise(float t)
    {
        // 便宜的白噪声：用时间做整数哈希，保证同一次播放内可重复
        int x = Mathf.FloorToInt(t * 44100f);
        x = (x << 13) ^ x;
        return 1f - ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
    }
}

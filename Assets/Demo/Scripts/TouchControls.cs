using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 移动端触屏控制：左下虚拟摇杆 + 右下攻击/跳跃/冲刺按钮 + 右侧拖动转视角。
///
/// 用 IMGUI 绘制，不依赖 uGUI/EventSystem，手机浏览器和 WebGL 都能直接跑。
/// 非触屏设备不显示，按 T 可以强制切换。
/// </summary>
public class TouchControls : MonoBehaviour
{
    [Header("自动检测")]
    public bool autoDetect = true;

    [Header("摇杆")]
    public float baseRadius = 92f;
    public float knobRadius = 42f;

    [Header("视角")]
    public float lookSensitivity = 0.35f;

    int _moveFinger = -1;
    int _lookFinger = -1;
    int _buttonFinger = -1;
    Vector2 _knobOffset;
    Vector2 _baseCenter;
    bool _draggingKnob;

    Texture2D _circleTex;
    GUIStyle _labelStyle;

    // 浏览器触摸事件的坐标系尺寸（CSS × devicePixelRatio），不一定等于 Screen
    Vector2 _inputSize;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int DSH_CanvasInputWidth();
    [DllImport("__Internal")] static extern int DSH_CanvasInputHeight();
    [DllImport("__Internal")] static extern int DSH_IsTouchOnly();
#endif

    /// <summary>是不是触屏为主的设备（手机/平板）。</summary>
    static bool IsTouchDevice()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (DSH_IsTouchOnly() == 1) return true;
        }
        catch { }
#endif
        return Application.isMobilePlatform;
    }

    // 按钮在屏幕上的位置（左上原点）
    Rect AttackRect, JumpRect, SprintRect, ViewRect;

    void Awake()
    {
        _circleTex = MakeCircleTexture(128);
    }

    void Start()
    {
        if (autoDetect)
        {
            // 只有触屏为主的设备才启用触屏输入。
            // 之前用 Input.touchSupported 判定，带触摸屏的 Windows 笔记本也会被当成手机，
            // 导致键盘失效、鼠标点击被摇杆抢走。
            MobileInput.Enabled = IsTouchDevice();
        }

        Layout();
        RefreshInputSize();
    }

    void Layout()
    {
        float s = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.4f);

        AttackRect = new Rect(Screen.width - 150f * s, Screen.height - 170f * s, 120f * s, 120f * s);
        JumpRect = new Rect(Screen.width - 290f * s, Screen.height - 130f * s, 96f * s, 96f * s);
        SprintRect = new Rect(Screen.width - 160f * s, Screen.height - 320f * s, 88f * s, 88f * s);
        ViewRect = new Rect(Screen.width - 112f * s, 24f * s, 88f * s, 88f * s);

        baseRadius = 92f * s;
        knobRadius = 42f * s;
        _baseCenter = new Vector2(150f * s, Screen.height - 170f * s);
    }

    void RefreshInputSize()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int w = DSH_CanvasInputWidth();
        int h = DSH_CanvasInputHeight();
        if (w > 0 && h > 0)
        {
            _inputSize = new Vector2(w, h);
            return;
        }
#endif
        _inputSize = new Vector2(Screen.width, Screen.height);
    }

    /// <summary>把触摸坐标换算到 Screen 空间（左下原点）。</summary>
    Vector2 ToScreen(Vector2 touchPos)
    {
        if (_inputSize.x <= 1f || _inputSize.y <= 1f) RefreshInputSize();

        return new Vector2(
            touchPos.x * (Screen.width / _inputSize.x),
            touchPos.y * (Screen.height / _inputSize.y));
    }

    void Update()
    {
        // 手动切换（方便桌面调试）
        if (Input.GetKeyDown(KeyCode.T))
        {
            MobileInput.Enabled = !MobileInput.Enabled;
            MobileInput.Reset();
        }

        if (!MobileInput.Enabled)
        {
            MobileInput.Move = Vector2.zero;
            return;
        }

        Layout();

        // 只处理真实触摸，不再用鼠标模拟——PC 端鼠标要留给视角和攻击
        if (Input.touchCount == 0)
        {
            if (_draggingKnob) OnRelease(_moveFinger);
            MobileInput.Move = Vector2.zero;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            Vector2 pos;
            Vector2 delta;

            if (MobileOrientation.IsRotated)
            {
                // 画布被 CSS 转了 90°，触摸坐标要还原回横屏画布空间
                pos = MobileOrientation.FromRotatedInput(t.position);
                Vector2 prev = MobileOrientation.FromRotatedInput(t.position - t.deltaPosition);
                delta = pos - prev;
            }
            else
            {
                Vector2 screenPos = ToScreen(t.position);
                pos = new Vector2(screenPos.x, Screen.height - screenPos.y);

                Vector2 screenDelta = ToScreen(t.deltaPosition);
                delta = new Vector2(screenDelta.x, -screenDelta.y);
            }

            if (t.phase == TouchPhase.Began) OnPress(pos, t.fingerId);
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                OnDrag(pos, t.fingerId, delta);
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                OnRelease(t.fingerId);
        }
    }

    void OnPress(Vector2 pos, int id)
    {
        if (InRect(pos, AttackRect)) { MobileInput.PressAttack(); _buttonFinger = id; return; }
        if (InRect(pos, JumpRect)) { MobileInput.PressJump(); _buttonFinger = id; return; }
        if (InRect(pos, SprintRect)) { MobileInput.Sprint = !MobileInput.Sprint; _buttonFinger = id; return; }
        if (InRect(pos, ViewRect)) { MobileInput.PressViewToggle(); _buttonFinger = id; return; }

        if (_moveFinger < 0 && Vector2.Distance(pos, _baseCenter) <= baseRadius * 1.7f)
        {
            _moveFinger = id;
            _baseCenter = pos;
            _knobOffset = Vector2.zero;
            _draggingKnob = true;
            return;
        }

        // 只有右侧区域才算转视角，避免点 HUD 或空白处也让镜头乱转
        bool inLookArea = pos.x > Screen.width * 0.34f && pos.y > Screen.height * 0.10f;
        if (_lookFinger < 0 && inLookArea) _lookFinger = id;
    }

    void OnDrag(Vector2 pos, int id, Vector2 delta)
    {
        if (id == _moveFinger && _draggingKnob)
        {
            Vector2 offset = pos - _baseCenter;
            if (offset.magnitude > baseRadius) offset = offset.normalized * baseRadius;

            _knobOffset = offset;

            // GUI 坐标 y 向下，移动方向 y 向上，这里要翻转，
            // 否则推摇杆向上会往后退。
            MobileInput.Move = new Vector2(offset.x / baseRadius, -offset.y / baseRadius);
        }
        else if (id == _lookFinger && delta.sqrMagnitude > 0.25f)
        {
            // 换算成角度：横扫半个屏幕约转 90°，手感与鼠标接近
            float degPerPixel = 180f / Mathf.Max(320f, Screen.width);
            MobileInput.LookDelta = new Vector2(delta.x * degPerPixel, delta.y * degPerPixel);
        }
    }

    void OnRelease(int id)
    {
        if (id == _moveFinger)
        {
            _moveFinger = -1;
            _draggingKnob = false;
            _knobOffset = Vector2.zero;
            MobileInput.Move = Vector2.zero;
        }

        if (id == _lookFinger) _lookFinger = -1;
        if (id == _buttonFinger) _buttonFinger = -1;
    }

    static bool InRect(Vector2 p, Rect r) => r.Contains(p);

    void OnGUI()
    {
        if (!MobileInput.Enabled) return;

        EnsureStyles();

        if (_draggingKnob)
        {
            DrawCircle(_baseCenter, baseRadius, new Color(1f, 1f, 1f, 0.18f));
            DrawCircle(_baseCenter, baseRadius * 0.96f, new Color(1f, 1f, 1f, 0.05f));
            DrawCircle(_baseCenter + _knobOffset, knobRadius, new Color(1f, 0.95f, 0.85f, 0.85f));
        }
        else
        {
            DrawCircle(_baseCenter, baseRadius * 0.78f, new Color(1f, 1f, 1f, 0.10f));
            GUI.Label(new Rect(_baseCenter.x - 48f, _baseCenter.y - 12f, 96f, 28f), "移动", _labelStyle);
        }

        DrawButton(AttackRect, "攻击", new Color(1f, 0.45f, 0.45f, 0.55f));
        DrawButton(JumpRect, "跳跃", new Color(0.5f, 0.75f, 1f, 0.5f));
        DrawButton(SprintRect, "冲刺", MobileInput.Sprint
            ? new Color(1f, 0.85f, 0.4f, 0.85f)
            : new Color(1f, 1f, 1f, 0.28f));
        DrawButton(ViewRect, "视角", new Color(0.75f, 0.85f, 1f, 0.45f));
    }

    void DrawButton(Rect r, string text, Color color)
    {
        DrawCircle(new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f), r.width * 0.5f, color);
        GUI.Label(r, text, _labelStyle);
    }

    void DrawCircle(Vector2 center, float radius, Color color)
    {
        if (_circleTex == null) return;

        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), _circleTex);
        GUI.color = prev;
    }

    void EnsureStyles()
    {
        if (_labelStyle != null) return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(20f * Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.8f, 1.5f)),
            fontStyle = FontStyle.Bold,
        };
        _labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
    }

    static Texture2D MakeCircleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float r = size * 0.5f;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.Clamp01((r - d) / 1.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}

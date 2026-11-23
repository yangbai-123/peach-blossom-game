using UnityEngine;

/// <summary>
/// 全部 UI：标题、游戏内 HUD、结局。用 IMGUI 画，不依赖 Canvas 预制体。
/// 素材从 Resources/Art 读取（三张人物图）。
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("引用")]
    public PlayerMotor motor;
    public Health playerHealth;
    public PlayerCombat combat;
    public VrmCharacterLoader characterLoader;

    [Header("立绘")]
    public float portraitWidth = 132f;

    Texture2D _bgBack;
    Texture2D _bgSide;
    Texture2D _bgSword;

    Font _font;
    GUIStyle _title;
    GUIStyle _subtitle;
    GUIStyle _body;
    GUIStyle _small;
    GUIStyle _hud;

    float _scale = 1f;
    float _titleTime;

    void Awake()
    {
        _bgBack = Resources.Load<Texture2D>("Art/bg_back");
        _bgSide = Resources.Load<Texture2D>("Art/bg_side");
        _bgSword = Resources.Load<Texture2D>("Art/bg_sword");

        // 优先用 Resources 里的字体：WebGL 没有系统字体，CreateDynamicFontFromOSFont 会失败。
        // NotoSansSC 是 SIL OFL 协议，可自由分发；同目录的 OFL-LICENSE.txt 随包附带。
        _font = Resources.Load<Font>("Fonts/NotoSansSC-Regular");

        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Noto Sans SC", "Arial" }, 32);
    }

    void OnGUI()
    {
        if (GameManager.Instance == null) return;

        _scale = Mathf.Max(0.75f, Screen.height / 1080f);
        EnsureStyles();

        switch (GameManager.Instance.State)
        {
            case GameState.Title:
                DrawTitle();
                break;
            case GameState.Playing:
                DrawGameplay();
                break;
            case GameState.Ending:
                DrawEnding();
                break;
        }
    }

    // ------------------------------------------------------------- 标题

    void DrawTitle()
    {
        // OnGUI 每帧会走多次事件，只在 Repaint 里累加，脉冲节奏才正确
        if (Event.current.type == EventType.Repaint) _titleTime += Time.unscaledDeltaTime;

        GUI.color = Color.white;
        if (_bgBack != null)
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _bgBack, ScaleMode.ScaleAndCrop);
        else
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.72f, 0.8f, 0.9f));

        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.05f, 0.08f, 0.15f, 0.45f));

        float w = Screen.width;
        float h = Screen.height;

        // 竖排感：标题放在偏右，避开人物
        GUI.Label(new Rect(w * 0.52f, h * 0.26f, w * 0.42f, 120f * _scale),
            "桃夭 · 寻剑", _title);
        GUI.Label(new Rect(w * 0.54f, h * 0.36f, w * 0.40f, 60f * _scale),
            "一段山路，十二片落花，一柄沉睡的剑。", _subtitle);

        float alpha = 0.55f + Mathf.Sin(_titleTime * 2.2f) * 0.35f;
        var c = _body.normal.textColor;
        _body.normal.textColor = new Color(c.r, c.g, c.b, alpha);
        GUI.Label(new Rect(w * 0.54f, h * 0.72f, w * 0.40f, 60f * _scale),
            MobileInput.Enabled ? "点击屏幕开始" : "点击或按任意键开始", _body);
        _body.normal.textColor = c;

        DrawCharacterCards(w, h);

        var hintRect = new Rect(16f, h - 52f * _scale, 780f, 42f * _scale);
        DrawRect(hintRect, new Color(0f, 0f, 0f, 0.38f));
        GUI.Label(new Rect(hintRect.x + 8f, hintRect.y + 4f, hintRect.width, hintRect.height),
            MobileInput.Enabled
                ? "左下摇杆 移动　右侧拖动 视角　右上 视角切换"
                : "WASD 移动　Shift 冲刺　空格 跳跃　左键 挥剑　V 视角　鼠标 视角", _small);

        // 开始与角色选择在 Update 里处理（OnGUI 在 WebGL 收不到鼠标事件）
        _cardClicked = false;
    }

    bool _cardClicked;

    /// <summary>
    /// 卡片矩形（GUI 坐标，左上原点）。绘制和点击判定共用，保证一致。
    /// </summary>
    Rect CardRect(int index)
    {
        float w = Screen.width;
        float h = Screen.height;

        float cardW = w * 0.34f;
        float cardH = h * 0.13f;
        float x = w * 0.055f;
        float y0 = h * 0.44f;
        float gap = h * 0.018f;

        return new Rect(x, y0 + index * (cardH + gap), cardW, cardH);
    }

    void SelectCharacter(int index)
    {
        if (CharacterCatalog.SelectedIndex == index) return;

        CharacterCatalog.SelectedIndex = index;
        if (characterLoader != null)
            characterLoader.Select(CharacterCatalog.Entries[index].Resource);
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Title) return;

        // WebGL 的 OnGUI 收不到鼠标事件，这里直接用 Input 做命中判定
        if (Input.GetMouseButtonDown(0))
        {
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            for (int i = 0; i < CharacterCatalog.Entries.Length; i++)
            {
                if (!CardRect(i).Contains(mouse)) continue;

                SelectCharacter(i);
                _cardClicked = true;
                return;
            }
        }

        if (_cardClicked)
        {
            _cardClicked = false;
            return;
        }

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
            GameManager.Instance.StartGame();
    }

    /// <summary>标题界面的角色选择卡片（只负责绘制，点击在 Update 里判定）。</summary>
    void DrawCharacterCards(float w, float h)
    {
        var entries = CharacterCatalog.Entries;
        if (entries == null || entries.Length == 0) return;

        GUI.Label(new Rect(w * 0.055f, h * 0.44f - h * 0.07f, w * 0.34f, 40f * _scale), "选择角色", _body);

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var rect = CardRect(i);
            bool selected = CharacterCatalog.SelectedIndex == i;

            DrawRect(rect, selected
                ? new Color(0.12f, 0.18f, 0.30f, 0.88f)
                : new Color(0.05f, 0.08f, 0.13f, 0.62f));

            // 左侧色条
            DrawRect(new Rect(rect.x, rect.y, 6f * _scale, rect.height),
                selected ? entry.Accent : new Color(entry.Accent.r, entry.Accent.g, entry.Accent.b, 0.32f));

            GUI.Label(new Rect(rect.x + 18f * _scale, rect.y + 4f * _scale, rect.width, 38f * _scale),
                entry.Name, _body);
            GUI.Label(new Rect(rect.x + 18f * _scale, rect.y + 42f * _scale, rect.width, 30f * _scale),
                entry.Subtitle, _small);

            if (selected)
                GUI.Label(new Rect(rect.x + rect.width - 88f * _scale, rect.y + 6f * _scale, 76f * _scale, 30f * _scale),
                    "已选", _small);
        }
    }

    // ------------------------------------------------------------- 游玩

    void DrawGameplay()
    {
        var gm = GameManager.Instance;
        float w = Screen.width;
        float h = Screen.height;

        float panelW = 260f * _scale;
        float panelH = 170f * _scale;
        var panel = new Rect(20f, 20f, panelW, panelH);
        DrawRect(panel, new Color(0.06f, 0.09f, 0.16f, 0.55f));

        // 立绘：只取左上角一块，做头像
        if (_bgSide != null)
        {
            var portrait = new Rect(panel.x + 10f, panel.y + 10f, 52f * _scale, 52f * _scale);
            GUI.DrawTextureWithTexCoords(portrait, _bgSide, new Rect(0.28f, 0.62f, 0.34f, 0.34f));
        }

        string count = $"{gm.Collected} / {gm.totalPetals}";
        GUI.Label(new Rect(panel.x + 72f * _scale, panel.y + 14f * _scale, 170f * _scale, 34f * _scale),
            "桃 花  " + count, _hud);

        GUI.Label(new Rect(panel.x + 72f * _scale, panel.y + 50f * _scale, 170f * _scale, 28f * _scale),
            FormatTime(gm.PlayTime) + "　斩 " + gm.Kills, _small);

        // 血条
        float barX = panel.x + 12f * _scale;
        float barY = panel.y + 82f * _scale;
        float barW = panelW - 24f * _scale;
        float barH = 12f * _scale;

        DrawRect(new Rect(barX, barY, barW, barH), new Color(0f, 0f, 0f, 0.55f));

        if (playerHealth != null)
        {
            float pct = Mathf.Clamp01(playerHealth.Current / playerHealth.Max);
            var hpColor = Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.55f, 0.9f, 0.6f), pct);
            DrawRect(new Rect(barX, barY, barW * pct, barH), hpColor);

            GUI.Label(new Rect(barX + barW + 8f * _scale, barY - 4f * _scale, 90f * _scale, 26f * _scale),
                $"{Mathf.CeilToInt(playerHealth.Current)}", _small);
        }

        string hint = gm.ShrineUnlocked
            ? "剑碑已醒 · 登上山顶"
            : "花瓣散落各处 · 金色台子可弹上浮空岛";

        GUI.Label(new Rect(panel.x + 12f * _scale, panel.y + 102f * _scale, panelW - 24f * _scale, 28f * _scale),
            hint, _small);

        // 解锁后：屏幕中央的引导
        if (gm.ShrineUnlocked)
        {
            float pulse = 0.6f + Mathf.Sin(Time.time * 3f) * 0.3f;
            var style = new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 0.93f, 0.75f, pulse);
            GUI.Label(new Rect(0f, h * 0.14f, w, 60f * _scale), "剑已离石，前往山巅", style);
        }

        var playHintRect = new Rect(12f, h - 48f * _scale, 720f * _scale, 36f * _scale);
        DrawRect(playHintRect, new Color(0f, 0f, 0f, 0.34f));
        GUI.Label(new Rect(playHintRect.x + 8f, playHintRect.y + 3f, playHintRect.width, playHintRect.height),
            MobileInput.Enabled
                ? "左下摇杆 移动　右侧拖动 视角　V/按钮 切换视角"
                : "WASD 移动　Shift 冲刺　空格 跳跃　左键/J 挥剑　V 视角　鼠标 视角", _small);

        if (motor != null)
        {
            var speedRect = new Rect(w - 232f * _scale, h - 48f * _scale, 220f * _scale, 36f * _scale);
            DrawRect(speedRect, new Color(0f, 0f, 0f, 0.34f));
            GUI.Label(new Rect(speedRect.x + 8f, speedRect.y + 3f, speedRect.width, speedRect.height),
                $"速度 {motor.HorizontalSpeed:0.0} m/s", _small);
        }

        // 受伤泛红
        if (gm.DamageFlash > 0.001f)
            DrawRect(new Rect(0f, 0f, w, h), new Color(0.75f, 0.08f, 0.1f, gm.DamageFlash * 0.3f));
    }

    // ------------------------------------------------------------- 结局

    void DrawEnding()
    {
        var gm = GameManager.Instance;

        if (_bgSword != null)
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _bgSword, ScaleMode.ScaleAndCrop);
        else
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.85f, 0.88f, 0.94f));

        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.04f, 0.07f, 0.12f, 0.45f));

        float w = Screen.width;
        float h = Screen.height;

        var center = new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, h * 0.24f, w, 120f * _scale), "剑 归 桃 夭", center);

        var sub = new GUIStyle(_subtitle) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, h * 0.35f, w, 60f * _scale),
            $"十二片花瓣 · 用时 {FormatTime(gm.PlayTime)} · 斩敌 {gm.Kills}", sub);

        var again = new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, h * 0.72f, w, 60f * _scale), "按 R 再来一次", again);

        if (Input.GetKeyDown(KeyCode.R)) gm.Restart();
    }

    // ------------------------------------------------------------- 工具

    void EnsureStyles()
    {
        if (_title != null) return;

        _title = new GUIStyle
        {
            font = _font,
            fontSize = Mathf.RoundToInt(64 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.98f, 0.95f, 0.92f) },
            richText = true,
        };

        _subtitle = new GUIStyle
        {
            font = _font,
            fontSize = Mathf.RoundToInt(22 * _scale),
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.9f, 0.92f, 0.96f, 0.92f) },
        };

        _body = new GUIStyle
        {
            font = _font,
            fontSize = Mathf.RoundToInt(26 * _scale),
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(1f, 0.97f, 0.9f) },
        };

        _small = new GUIStyle
        {
            font = _font,
            fontSize = Mathf.RoundToInt(18 * _scale),
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.92f, 0.94f, 0.98f, 0.85f) },
        };

        _hud = new GUIStyle
        {
            font = _font,
            fontSize = Mathf.RoundToInt(28 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(1f, 0.96f, 0.9f) },
        };
    }

    static void DrawRect(Rect rect, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}

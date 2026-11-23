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

        DrawOrientationHint();
    }

    /// <summary>竖屏提示已停用：竖屏可以直接游玩，不再引导用户旋转。</summary>
    void DrawOrientationHint()
    {
        // 留空：之前用来提示横屏，现在竖屏布局本身可用
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
        DrawAccountPanel(w, h);
        DrawLeaderboard(w, h);
        DrawModeButtons(w, h);

        if (GameManager.Instance != null && GameManager.Instance.PendingMode == GameMode.Duel)
        {
            _cardClicked = false;
            return;
        }

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
        float cardH = h * 0.115f;
        float x = w * 0.055f;
        float y0 = h * 0.505f;
        float gap = h * 0.016f;

        return new Rect(x, y0 + index * (cardH + gap), cardW, cardH);
    }

    // ------------------------------------------------------------- 玩法模式

    static readonly (GameMode mode, string label, string hint)[] Modes =
    {
        (GameMode.Shrine, "寻剑", "收集十二片花瓣，登顶归剑"),
        (GameMode.Extraction, "烽火地带", "限时搜集物资并撤离"),
        (GameMode.Duel, "联机对枪", "和朋友 1v1"),
    };

    Rect ModeRect(int index)
    {
        float w = Screen.width, h = Screen.height;
        float bw = w * 0.108f;
        float gap = w * 0.008f;
        return new Rect(w * 0.055f + index * (bw + gap), h * 0.405f, bw, h * 0.05f);
    }

    void DrawModeButtons(float w, float h)
    {
        GUI.Label(new Rect(w * 0.055f, h * 0.375f, w * 0.34f, 26f * _scale), "玩法", _small);

        for (int i = 0; i < Modes.Length; i++)
        {
            var rect = ModeRect(i);
            bool picked = GameManager.Instance != null && GameManager.Instance.PendingMode == Modes[i].mode;

            DrawButtonRow(rect, Modes[i].label, picked);

            if (picked)
                GUI.Label(new Rect(rect.x, rect.yMax + 2f, w * 0.34f, 24f * _scale),
                    Modes[i].hint, _small);
        }

        if (GameManager.Instance != null && GameManager.Instance.PendingMode == GameMode.Duel)
            DrawNetPanel(w, h);
    }

    // ------------------------------------------------------------- 联机面板

    string _roomInput = "";

    Rect NetHostBtn() => new Rect(Screen.width * 0.545f, Screen.height * 0.735f, Screen.width * 0.12f, Screen.height * 0.05f);

    Rect NetCodeDisplay() => new Rect(Screen.width * 0.68f, Screen.height * 0.735f, Screen.width * 0.12f, Screen.height * 0.05f);

    /// <summary>数字键盘：0-9 占前 10 格，10=退格，11=加入。</summary>
    Rect KeyRect(int index)
    {
        float w = Screen.width, h = Screen.height;
        int col = index % 3;
        int row = index / 3;

        return new Rect(w * 0.545f + col * (w * 0.064f), h * 0.795f + row * (h * 0.052f), w * 0.056f, h * 0.046f);
    }

    void DrawNetPanel(float w, float h)
    {
        var net = NetDuel.Instance;
        if (net == null) return;

        GUI.Label(new Rect(w * 0.545f, h * 0.695f, w * 0.40f, 26f * _scale), "联机对枪 · 4 位房号", _body);

        // 房间号：自己创建时显示，加入时显示已输入
        string code = net.IsHost && !string.IsNullOrEmpty(net.RoomCode) ? net.RoomCode : _roomInput;
        string shown = string.IsNullOrEmpty(code) ? "----" : code.PadRight(4, '-');

        var display = NetCodeDisplay();
        DrawRect(display, new Color(0.05f, 0.09f, 0.16f, 0.9f));

        var codeStyle = new GUIStyle(_hud) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(display, shown, codeStyle);

        DrawButtonRow(NetHostBtn(), net.IsHost ? "重新创建" : "创建房间", net.Current == NetDuel.State.Hosting);

        // 数字键盘
        for (int i = 0; i < 12; i++)
        {
            string label = i switch
            {
                9 => "删",
                10 => "0",
                11 => "加入",
                _ => (i + 1).ToString(),
            };

            // 排列：1 2 3 / 4 5 6 / 7 8 9 / ⌫ 0 加入
            int cell = i switch { 9 => 9, 10 => 10, 11 => 11, _ => i };
            DrawButtonRow(KeyRect(cell), label, i == 11);
        }

        if (!string.IsNullOrEmpty(net.Status))
            GUI.Label(new Rect(w * 0.545f, h * 0.722f, w * 0.44f, 24f * _scale), net.Status, _small);
    }

    bool HandleNetClick(Vector2 mouse)
    {
        var net = NetDuel.Instance;
        if (net == null) return false;
        if (GameManager.Instance == null || GameManager.Instance.PendingMode != GameMode.Duel) return false;

        if (NetHostBtn().Contains(mouse)) { net.Host(); return true; }

        for (int i = 0; i < 12; i++)
        {
            if (!KeyRect(i).Contains(mouse)) continue;

            if (i < 9) AppendDigit((i + 1).ToString());
            else if (i == 9) { if (_roomInput.Length > 0) _roomInput = _roomInput.Substring(0, _roomInput.Length - 1); }
            else if (i == 10) AppendDigit("0");
            else net.Join(_roomInput);

            return true;
        }

        return false;
    }

    void AppendDigit(string digit)
    {
        if (_roomInput.Length < 4) _roomInput += digit;
    }

    /// <summary>桌面端可以直接敲数字，手机端用上面的数字键盘。</summary>
    void HandleRoomInput()
    {
        if (GameManager.Instance == null || GameManager.Instance.PendingMode != GameMode.Duel) return;

        foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (_roomInput.Length > 0) _roomInput = _roomInput.Substring(0, _roomInput.Length - 1);
            }
            else if (c == '\n' || c == '\r')
            {
                NetDuel.Instance?.Join(_roomInput);
            }
            else if (c >= '0' && c <= '9')
            {
                AppendDigit(c.ToString());
            }
        }
    }

    /// <summary>标题界面点玩法按钮，返回 true 表示已消耗。</summary>
    bool HandleModeClick(Vector2 mouse)
    {
        for (int i = 0; i < Modes.Length; i++)
        {
            if (!ModeRect(i).Contains(mouse)) continue;

            GameManager.Instance.PendingMode = Modes[i].mode;
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------- 账户与排行榜

    string _inputName = "";
    string _authMessage = "";
    bool _nameFocused;

    Rect NameFieldRect()
    {
        float w = Screen.width, h = Screen.height;
        return new Rect(w * 0.055f, h * 0.315f, w * 0.16f, h * 0.056f);
    }

    Rect EnterBtn()
    {
        var f = NameFieldRect();
        return new Rect(f.xMax + 8f * _scale, f.y, Screen.width * 0.062f, f.height);
    }

    Rect RandomBtn()
    {
        var e = EnterBtn();
        return new Rect(e.xMax + 6f * _scale, e.y, Screen.width * 0.058f, e.height);
    }

    Rect SwitchBtn()
    {
        float w = Screen.width, h = Screen.height;
        return new Rect(w * 0.055f + w * 0.13f, h * 0.30f, w * 0.06f, 34f * _scale);
    }

    Rect ChipRect(int index)
    {
        float w = Screen.width, h = Screen.height;
        return new Rect(w * 0.055f + index * (w * 0.062f + 6f * _scale), h * 0.383f, w * 0.062f, 30f * _scale);
    }

    void DrawButtonRow(Rect r, string text, bool accent)
    {
        DrawRect(r, accent
            ? new Color(0.18f, 0.30f, 0.48f, 0.92f)
            : new Color(0.08f, 0.12f, 0.19f, 0.85f));

        GUI.Label(r, text, _small);
    }

    void DrawAccountPanel(float w, float h)
    {
        if (UserProfile.HasUser)
        {
            GUI.Label(new Rect(w * 0.055f, h * 0.302f, w * 0.24f, 34f * _scale),
                "玩家：" + UserProfile.Current, _body);
            DrawButtonRow(SwitchBtn(), "切换", false);
            return;
        }

        GUI.Label(new Rect(w * 0.055f, h * 0.285f, w * 0.34f, 28f * _scale), "起个名字（会记住）", _small);

        var field = NameFieldRect();
        DrawRect(field, new Color(0.05f, 0.08f, 0.14f, 0.9f));

        if (_nameFocused && Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f)
            DrawRect(new Rect(field.xMax - 3f, field.y + 6f, 2f, field.height - 12f), new Color(1f, 1f, 1f, 0.8f));

        GUI.Label(new Rect(field.x + 10f * _scale, field.y + 4f, field.width, field.height),
            string.IsNullOrEmpty(_inputName) ? "点击输入名字" : _inputName, _body);

        DrawButtonRow(EnterBtn(), "进入", true);
        DrawButtonRow(RandomBtn(), "随机", false);

        if (!string.IsNullOrEmpty(_authMessage))
            GUI.Label(new Rect(w * 0.055f, h * 0.383f, w * 0.34f, 26f * _scale), _authMessage, _small);

        var names = UserProfile.SavedNames();
        for (int i = 0; i < names.Count && i < 5; i++)
        {
            var chip = ChipRect(i);
            DrawButtonRow(chip, names[i], false);
        }
    }

    void DrawLeaderboard(float w, float h)
    {
        float x = w * 0.545f;
        float y = h * 0.455f;
        float cw = w * 0.36f;

        GUI.Label(new Rect(x, y, cw, 34f * _scale), "最快通关", _body);

        var rows = UserProfile.Leaderboard(8);

        if (rows.Count == 0)
        {
            GUI.Label(new Rect(x, y + 38f * _scale, cw, 30f * _scale), "还没有通关记录", _small);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rect = new Rect(x, y + (38f + i * 32f) * _scale, cw, 30f * _scale);

            if (row.name == UserProfile.Current)
                DrawRect(rect, new Color(0.18f, 0.30f, 0.48f, 0.55f));

            GUI.Label(rect, $"{i + 1}. {row.name}", _small);
            GUI.Label(new Rect(rect.x + rect.width * 0.62f, rect.y, rect.width * 0.38f, rect.height),
                UserProfile.FormatTime(row.time), _small);
        }
    }

    /// <summary>处理账户区的点击，返回 true 表示这次点击被消耗。</summary>
    bool HandleAccountClick(Vector2 mouse)
    {
        if (UserProfile.HasUser)
        {
            if (SwitchBtn().Contains(mouse)) { UserProfile.Logout(); return true; }
            return false;
        }

        if (NameFieldRect().Contains(mouse)) { _nameFocused = true; return true; }
        if (EnterBtn().Contains(mouse)) { SubmitName(_inputName); return true; }

        if (RandomBtn().Contains(mouse))
        {
            string[] pool = { "青衫客", "听雨", "拾花人", "负剑", "山月", "拾光" };
            _inputName = pool[Random.Range(0, pool.Length)] + Random.Range(10, 99);
            SubmitName(_inputName);
            return true;
        }

        var names = UserProfile.SavedNames();
        for (int i = 0; i < names.Count && i < 5; i++)
        {
            if (ChipRect(i).Contains(mouse)) { SubmitName(names[i]); return true; }
        }

        return false;
    }

    void SubmitName(string name)
    {
        if (UserProfile.Enter(name, out string error))
        {
            _authMessage = "";
            _inputName = UserProfile.Current;
            _nameFocused = false;
        }
        else
        {
            _authMessage = error;
        }
    }

    /// <summary>自己处理文字输入：WebGL 的 OnGUI 收不到键盘焦点事件。</summary>
    void HandleNameInput()
    {
        if (!_nameFocused) return;

        foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (_inputName.Length > 0) _inputName = _inputName.Substring(0, _inputName.Length - 1);
            }
            else if (c == '\n' || c == '\r')
            {
                SubmitName(_inputName);
            }
            else if (!char.IsControl(c) && _inputName.Length < 12)
            {
                _inputName += c;
            }
        }
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
        if (GameManager.Instance == null) return;

        // 移动端第一次触摸就请求全屏 + 横屏锁定（浏览器要求用户手势）
        if (MobileInput.Enabled && (Input.GetMouseButtonDown(0) || Input.touchCount > 0))
            MobileOrientation.RequestLandscape();

        if (GameManager.Instance.State != GameState.Title) return;

        // 名字输入（自己处理，OnGUI 在 WebGL 收不到键盘事件）
        HandleNameInput();
        HandleRoomInput();

        // WebGL 的 OnGUI 收不到鼠标事件，这里直接用 Input 做命中判定
        if (Input.GetMouseButtonDown(0))
        {
            var mouse = MobileOrientation.IsRotated
                ? MobileOrientation.FromRotatedInput(Input.mousePosition)
                : new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            // 1. 账户区
            if (HandleAccountClick(mouse)) return;

            // 2. 联机面板
            if (HandleNetClick(mouse)) return;

            // 3. 取消输入焦点（点空白先失焦，不误触开始）
            if (_nameFocused) { _nameFocused = false; return; }

            // 4. 玩法按钮
            if (HandleModeClick(mouse)) return;

            // 5. 角色卡
            for (int i = 0; i < CharacterCatalog.Entries.Length; i++)
            {
                if (!CardRect(i).Contains(mouse)) continue;

                SelectCharacter(i);
                _cardClicked = true;
                return;
            }
        }

        // 正在输入名字时，键盘不能触发开始
        if (_nameFocused) return;

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

        GUI.Label(new Rect(w * 0.055f, h * 0.505f - h * 0.052f, w * 0.34f, 34f * _scale), "选择角色", _body);

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

        DrawModeInfo(w, h);

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
        GUI.Label(new Rect(0f, h * 0.24f, w, 120f * _scale),
            gm.Success ? "剑 归 桃 夭" : "功 亏 一 篑", center);

        var sub = new GUIStyle(_subtitle) { alignment = TextAnchor.MiddleCenter };

        string detail = string.IsNullOrEmpty(gm.EndReason)
            ? $"用时 {FormatTime(gm.PlayTime)} · 斩敌 {gm.Kills}"
            : $"{gm.EndReason} · 用时 {FormatTime(gm.PlayTime)} · 斩敌 {gm.Kills}";

        GUI.Label(new Rect(0f, h * 0.35f, w, 60f * _scale), detail, sub);

        var again = new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, h * 0.72f, w, 60f * _scale), "按 R 再来一次", again);

        if (Input.GetKeyDown(KeyCode.R)) gm.Restart();
    }

    /// <summary>玩法相关的界面：物资提示、撤离倒计时、进度。</summary>
    void DrawModeInfo(float w, float h)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 拾取提示
        string toast = GameBootstrap.ToastText;
        if (!string.IsNullOrEmpty(toast))
        {
            var tr = new Rect(w * 0.30f, h * 0.115f, w * 0.40f, 42f * _scale);
            DrawRect(tr, new Color(0.06f, 0.11f, 0.18f, 0.8f));
            GUI.Label(tr, toast, _body);
        }

        if (gm.Mode == GameMode.Duel)
        {
            DrawDuelInfo(w, h);
            return;
        }

        if (gm.Mode != GameMode.Extraction) return;

        var ex = ExtractionMode.Instance;
        if (ex == null || !ex.Playing) return;

        // 顶部中央：倒计时 + 物资
        float pw = w * 0.24f;
        var panel = new Rect(w * 0.5f - pw * 0.5f, h * 0.02f, pw, 72f * _scale);
        DrawRect(panel, new Color(0.05f, 0.09f, 0.15f, 0.72f));

        string timer = "撤离倒计时 " + UserProfile.FormatTime(ex.TimeLeft);
        GUI.Label(new Rect(panel.x, panel.y + 4f * _scale, panel.width, 34f * _scale), timer, _hud);

        string loot = $"物资 {ex.LootCollected}/{ex.LootTotal}";
        GUI.Label(new Rect(panel.x, panel.y + 38f * _scale, panel.width, 28f * _scale), loot, _small);

        // 撤离点状态
        string zone = ex.ZoneActive
            ? "撤离点已开放 · 前往山顶石台"
            : $"撤离点将在开局 {Mathf.RoundToInt(ex.ZoneActivateAt / 60f)} 分钟后开放";

        GUI.Label(new Rect(w * 0.5f - w * 0.2f, h * 0.105f, w * 0.4f, 28f * _scale), zone, _small);

        // 撤离进度
        if (ex.ExtractProgress > 0.01f)
        {
            float p = Mathf.Clamp01(ex.ExtractProgress / 6f);
            var bar = new Rect(w * 0.5f - w * 0.12f, h * 0.83f, w * 0.24f, 22f * _scale);
            DrawRect(bar, new Color(0f, 0f, 0f, 0.5f));
            DrawRect(new Rect(bar.x, bar.y, bar.width * p, bar.height), new Color(0.35f, 0.95f, 0.7f, 0.9f));
            GUI.Label(new Rect(bar.x, bar.y - 30f * _scale, bar.width, 28f * _scale), "撤离中…", _small);
        }

        // 增益
        var buffs = motor != null ? motor.GetComponent<PlayerBuffs>() : null;
        if (buffs != null)
        {
            float x = w - 220f * _scale;
            float y = h - 96f * _scale;

            if (buffs.DamageLeft > 0f)
                GUI.Label(new Rect(x, y, 200f * _scale, 26f * _scale), $"攻击强化 {buffs.DamageLeft:0}s", _small);

            if (buffs.SpeedLeft > 0f)
                GUI.Label(new Rect(x, y + 26f * _scale, 200f * _scale, 26f * _scale), $"疾行 {buffs.SpeedLeft:0}s", _small);
        }
    }

    /// <summary>联机对枪：双方血量。</summary>
    void DrawDuelInfo(float w, float h)
    {
        var net = NetDuel.Instance;
        if (net == null) return;

        float barW = w * 0.26f;
        float barH = 16f * _scale;

        // 我方
        var mine = new Rect(w * 0.5f - barW - 14f * _scale, h * 0.03f, barW, barH);
        DrawRect(mine, new Color(0f, 0f, 0f, 0.45f));
        if (net.LocalHealth != null)
        {
            float p = Mathf.Clamp01(net.LocalHealth.Current / net.LocalHealth.Max);
            DrawRect(new Rect(mine.x, mine.y, mine.width * p, mine.height), new Color(0.5f, 0.85f, 0.6f, 0.95f));
        }
        GUI.Label(new Rect(mine.x, mine.y - 26f * _scale, mine.width, 24f * _scale), "我方", _small);

        // 对手
        var theirs = new Rect(w * 0.5f + 14f * _scale, h * 0.03f, barW, barH);
        DrawRect(theirs, new Color(0f, 0f, 0f, 0.45f));
        if (net.RemoteHealth != null)
        {
            float p = Mathf.Clamp01(net.RemoteHealth.Current / net.RemoteHealth.Max);
            DrawRect(new Rect(theirs.x, theirs.y, theirs.width * p, theirs.height), new Color(0.95f, 0.5f, 0.55f, 0.95f));
        }
        GUI.Label(new Rect(theirs.x, theirs.y - 26f * _scale, theirs.width, 24f * _scale), "对手", _small);

        if (!string.IsNullOrEmpty(net.Status))
            GUI.Label(new Rect(w * 0.5f - w * 0.2f, h * 0.075f, w * 0.4f, 26f * _scale), net.Status, _small);
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

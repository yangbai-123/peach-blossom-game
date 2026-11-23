using UnityEngine;

/// <summary>
/// 一次性搭出整张地图和玩法的入口脚本：挂在空场景的空物体上按 Play 即可。
/// 场景元素全部用内置图元运行时生成，不依赖任何美术资源。
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("玩家")]
    public Vector3 spawnPoint = new Vector3(0f, 0.15f, -12f);

    [Header("配色")]
    public Color stone = new Color(0.62f, 0.66f, 0.72f);
    public Color stoneDark = new Color(0.48f, 0.52f, 0.58f);
    public Color grass = new Color(0.55f, 0.66f, 0.52f);
    public Color blossom = new Color(0.96f, 0.78f, 0.86f);
    public Color wood = new Color(0.45f, 0.33f, 0.28f);
    public Color water = new Color(0.45f, 0.68f, 0.85f);
    public Color gold = new Color(0.85f, 0.72f, 0.42f);

    Transform _root;
    Transform _player;
    ShrineGate _shrine;

    void Awake()
    {
        _root = new GameObject("World").transform;

        Sfx.Init();
        CreateManager();
        BuildTerrain();
        BuildPagoda();
        BuildLake();
        BuildTrees();
        BuildFloatingIslands();
        BuildShrine();
        BuildPetals();
        BuildClouds();
        EnsureLight();

        _player = BuildPlayer();
        BuildEnemies();
        BuildCamera(_player);
        BuildHud(_player);
    }

    /// <summary>给导入的人形模型挂一把程序化长剑（VRM 本身没有武器）。</summary>
    // ------------------------------------------------------------ 基础地形

    void BuildTerrain()
    {
        Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 70f), grass);

        // 上山的路：两段坡 + 两级平台
        Ramp("RampA", new Vector3(0f, 0.03f, 4f), new Vector3(0f, 2.4f, 12f), 6f, 0.4f, stone);
        Box("HillPlatform", new Vector3(0f, 2.0f, 16f), new Vector3(14f, 0.8f, 10f), grass);

        Ramp("RampB", new Vector3(0f, 2.43f, 20f), new Vector3(0f, 5.2f, 28f), 6f, 0.4f, stone);
        Box("ShrinePlatform", new Vector3(0f, 4.8f, 32f), new Vector3(12f, 0.8f, 10f), stone);

        // 山门：两根柱子 + 横梁，纯装饰
        Box("GateLeft", new Vector3(-3.2f, 2.2f, 29f), new Vector3(0.6f, 4.4f, 0.6f), wood);
        Box("GateRight", new Vector3(3.2f, 2.2f, 29f), new Vector3(0.6f, 4.4f, 0.6f), wood);
        Box("GateBeam", new Vector3(0f, 4.55f, 29f), new Vector3(7.4f, 0.5f, 0.8f), wood);

        // 栏杆：南、东、西三边，北边留给上山的路
        Railing(-33f, 33f, -33f, true);   // 南（z = -33）
        Railing(-33f, 33f, 33f, false);   // 东（x = 33）
        Railing(-33f, 33f, -33f, false);  // 西（x = -33）
    }

    void Railing(float from, float to, float fixedCoord, bool alongX)
    {
        for (float t = from; t <= to; t += 3f)
        {
            Vector3 pos = alongX
                ? new Vector3(t, 0.6f, fixedCoord)
                : new Vector3(fixedCoord, 0.6f, t);

            // 只做装饰，不加碰撞，免得挡住跑动
            Box("Rail", pos, new Vector3(0.22f, 1.2f, 0.22f), stoneDark, false);
        }
    }

    // ------------------------------------------------------------ 建筑

    void BuildPagoda()
    {
        Vector3 basePos = new Vector3(-24f, 0f, 18f);

        Box("PagodaBase", basePos + new Vector3(0f, 0.4f, 0f), new Vector3(8f, 0.8f, 8f), stone);

        float y = 0.8f;
        for (int i = 0; i < 3; i++)
        {
            float w = 6f - i * 1.2f;
            float h = 3.2f - i * 0.4f;

            Box($"PagodaBody{i}", new Vector3(basePos.x, y + h * 0.5f, basePos.z),
                new Vector3(w, h, w), wood);

            // 屋檐：比塔身宽一圈的薄板
            Box($"PagodaRoof{i}", new Vector3(basePos.x, y + h + 0.2f, basePos.z),
                new Vector3(w + 1.6f, 0.4f, w + 1.6f), stoneDark);

            y += h + 0.4f;
        }

        // 塔尖
        Box("PagodaTip", new Vector3(basePos.x, y + 0.8f, basePos.z), new Vector3(0.4f, 1.6f, 0.4f), gold);
    }

    void BuildLake()
    {
        Box("Lake", new Vector3(22f, 0.06f, 4f), new Vector3(14f, 0.12f, 12f), water, true);

        // 假山 + 一道细水
        Box("Rock", new Vector3(27f, 1.6f, 10f), new Vector3(4f, 3.2f, 4f), stoneDark);
        Box("Fall", new Vector3(27f, 1.6f, 8.1f), new Vector3(1.2f, 3.0f, 0.25f), water, false);

        // 池边石
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            Box("PondStone",
                new Vector3(22f + Mathf.Cos(angle) * 7f, 0.25f, 4f + Mathf.Sin(angle) * 6f),
                new Vector3(1.2f, 0.5f, 1.2f), stone, false);
        }
    }

    void BuildTrees()
    {
        Vector3[] spots =
        {
            new Vector3(-12f, 0f, -8f),
            new Vector3(12f, 0f, -14f),
            new Vector3(-20f, 0f, 6f),
            new Vector3(16f, 0f, 16f),
            new Vector3(-8f, 0f, 20f),
            new Vector3(24f, 0f, -8f),
            new Vector3(-28f, 0f, -16f),
            new Vector3(8f, 0f, 30f),
        };

        foreach (var spot in spots) Tree(spot, Random.Range(0.9f, 1.25f));
    }

    void Tree(Vector3 pos, float scale)
    {
        var trunk = Cylinder("Trunk", pos + Vector3.up * 1.6f * scale,
            new Vector3(0.32f, 1.6f, 0.32f) * scale, wood, true);
        trunk.name = "Trunk";

        // 三团花冠，错开一点显得不呆板
        Prim(PrimitiveType.Sphere, "Canopy", pos + new Vector3(0f, 3.4f, 0f) * scale,
            Vector3.one * 1.9f * scale, blossom, false);
        Prim(PrimitiveType.Sphere, "Canopy", pos + new Vector3(0.8f, 3.0f, 0.5f) * scale,
            Vector3.one * 1.4f * scale, blossom, false);
        Prim(PrimitiveType.Sphere, "Canopy", pos + new Vector3(-0.7f, 3.1f, -0.6f) * scale,
            Vector3.one * 1.4f * scale, blossom, false);
    }

    // ------------------------------------------------------------ 浮空岛

    void BuildFloatingIslands()
    {
        Island(new Vector3(18f, 7f, -16f), 9f);
        Island(new Vector3(-18f, 9.5f, -14f), 9f);
        Island(new Vector3(0f, 12.5f, -26f), 8f);

        // 三个地面弹跳台。距离要够远，让抛物线在进入岛的投影之前就升到岛面以上，
        // 否则会撞到浮空岛底面被挡下来（这是之前"上不去"的真正原因）。
        Pad(new Vector3(18f, 0.1f, -8f), 9.5f, new Vector3(18f, 7.1f, -16f));
        Pad(new Vector3(-18f, 0.1f, 0f), 12.5f, new Vector3(-18f, 9.6f, -14f));
        Pad(new Vector3(0f, 0.1f, -16f), 15f, new Vector3(0f, 12.6f, -26f));
    }

    void Island(Vector3 center, float size)
    {
        Box("IslandTop", center + Vector3.down * 0.3f, new Vector3(size, 0.6f, size), grass);

        // 岛底：倒锥形的几层石头，压出"浮空"感
        Box("IslandUnder", center + Vector3.down * 1.0f, new Vector3(size * 0.7f, 0.8f, size * 0.7f), stoneDark, false);
        Box("IslandUnder", center + Vector3.down * 1.8f, new Vector3(size * 0.4f, 0.8f, size * 0.4f), stoneDark, false);

        // 岛上小树
        Prim(PrimitiveType.Cylinder, "MiniTrunk", center + Vector3.up * 0.9f,
            new Vector3(0.18f, 0.9f, 0.18f), wood, false);
        Prim(PrimitiveType.Sphere, "MiniCanopy", center + Vector3.up * 2.2f,
            Vector3.one * 1.1f, blossom, false);
    }

    void Pad(Vector3 pos, float height, Vector3 targetPoint)
    {
        // 不加碰撞体：BouncePad 用距离判定，避免依赖 CharacterController 的触发器事件
        var padGo = Prim(PrimitiveType.Cylinder, "BouncePad", pos,
            new Vector3(2.2f, 0.1f, 2.2f), gold, false);

        var bounce = padGo.AddComponent<BouncePad>();
        bounce.launchHeight = height;

        var targetGo = new GameObject("PadTarget");
        targetGo.transform.SetParent(_root);
        targetGo.transform.position = targetPoint;
        bounce.target = targetGo.transform;
    }

    // ------------------------------------------------------------ 剑碑与花瓣

    void BuildShrine()
    {
        Vector3 basePos = new Vector3(0f, 5.2f, 32f);

        Box("ShrineBase", basePos + Vector3.up * 0.3f, new Vector3(4f, 0.6f, 4f), stoneDark);
        Box("Stele", basePos + Vector3.up * 1.6f, new Vector3(0.9f, 2.2f, 0.35f), stone);

        var shrineGo = new GameObject("ShrineGate");
        shrineGo.transform.position = basePos + Vector3.up * 0.6f;
        shrineGo.transform.SetParent(_root);

        // 悬浮的剑
        var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sword.name = "Sword";
        sword.transform.SetParent(shrineGo.transform, false);
        sword.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        sword.transform.localScale = new Vector3(0.12f, 2.2f, 0.12f);
        Destroy(sword.GetComponent<Collider>());
        Paint(sword, new Color(0.85f, 0.9f, 0.95f));

        // 剑柄
        var hilt = Prim(PrimitiveType.Cube, "Hilt", Vector3.zero,
            new Vector3(0.3f, 0.12f, 0.18f), gold, false, sword.transform);
        hilt.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        // 点光源当作灵光，集齐后变亮
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(shrineGo.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        var light = glowGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 14f;
        light.intensity = 0.6f;
        light.color = new Color(1f, 0.9f, 0.65f);

        _shrine = shrineGo.AddComponent<ShrineGate>();
        _shrine.sword = sword.transform;
        _shrine.glow = light;
    }

    void BuildPetals()
    {
        Vector3[] spots =
        {
            new Vector3(0f, 1.1f, 0f),
            new Vector3(6f, 1.1f, 6f),
            new Vector3(-6f, 1.1f, 6f),
            new Vector3(0f, 3.5f, 16f),
            new Vector3(10f, 1.1f, -4f),
            new Vector3(-10f, 1.1f, 10f),
            new Vector3(-24f, 1.1f, 24f),
            new Vector3(22f, 1.1f, -2f),
            new Vector3(18f, 8.1f, -16f),
            new Vector3(-18f, 10.6f, -14f),
            new Vector3(0f, 13.6f, -26f),
            new Vector3(14f, 1.1f, -20f),
        };

        foreach (var spot in spots)
        {
            var petal = BuildPetalVisual(spot);

            var trigger = petal.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = Vector3.one * 2.2f;

            petal.AddComponent<Collectible>();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.totalPetals = spots.Length;
    }

    /// <summary>
    /// 一片会飘的花瓣：主瓣 + 两片错开的副瓣 + 花蕊，比一个方块更像花。
    /// </summary>
    GameObject BuildPetalVisual(Vector3 position)
    {
        var root = new GameObject("Petal");
        root.transform.SetParent(_root);
        root.transform.position = position;
        root.transform.localScale = new Vector3(0.34f, 0.40f, 0.34f);

        var mesh = PetalMesh.Create();

        var pink = MaterialLibrary.For(new Color(1f, 0.72f, 0.85f));
        var pinkLight = MaterialLibrary.For(new Color(1f, 0.86f, 0.93f));

        AddPetalPart(root.transform, mesh, pink, Vector3.zero, new Vector3(0f, 0f, 0f), 1f);
        AddPetalPart(root.transform, mesh, pinkLight, new Vector3(0.10f, -0.02f, 0.04f), new Vector3(12f, 38f, 22f), 0.82f);
        AddPetalPart(root.transform, mesh, pinkLight, new Vector3(-0.10f, -0.02f, 0.04f), new Vector3(-10f, -40f, -20f), 0.78f);

        // 花蕊：根部一颗小球 + 三根细丝
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localPosition = new Vector3(0f, 0.04f, 0.02f);
        core.transform.localScale = Vector3.one * 0.16f;

        var coreCol = core.GetComponent<Collider>();
        if (coreCol != null) { coreCol.enabled = false; Destroy(coreCol); }

        var coreRenderer = core.GetComponent<Renderer>();
        if (coreRenderer != null)
        {
            var mat = MaterialLibrary.For(new Color(1f, 0.92f, 0.55f));
            if (mat != null) coreRenderer.sharedMaterial = mat;
        }

        for (int i = 0; i < 3; i++)
        {
            var stamen = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stamen.name = "Stamen";
            stamen.transform.SetParent(root.transform, false);
            stamen.transform.localPosition = new Vector3(Mathf.Cos(i * 2.1f) * 0.06f, 0.14f, Mathf.Sin(i * 2.1f) * 0.06f);
            stamen.transform.localRotation = Quaternion.Euler(18f * Mathf.Cos(i * 2.1f), 0f, 18f * Mathf.Sin(i * 2.1f));
            stamen.transform.localScale = new Vector3(0.012f, 0.07f, 0.012f);

            var stamenCol = stamen.GetComponent<Collider>();
            if (stamenCol != null) { stamenCol.enabled = false; Destroy(stamenCol); }

            var stamenRenderer = stamen.GetComponent<Renderer>();
            if (stamenRenderer != null)
            {
                var mat = MaterialLibrary.For(new Color(1f, 0.95f, 0.75f));
                if (mat != null) stamenRenderer.sharedMaterial = mat;
            }
        }

        return root;
    }

    void AddPetalPart(Transform parent, Mesh mesh, Material material,
        Vector3 localPosition, Vector3 localEuler, float scale)
    {
        var go = new GameObject("PetalPart");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = Vector3.one * scale;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        if (material != null) renderer.sharedMaterial = material;
    }

    void BuildClouds()
    {
        Vector3[] clouds =
        {
            new Vector3(-40f, 22f, -40f), new Vector3(38f, 26f, -30f),
            new Vector3(-30f, 30f, 40f), new Vector3(45f, 24f, 25f),
            new Vector3(0f, 34f, -55f), new Vector3(-55f, 20f, 5f),
        };

        foreach (var c in clouds)
        {
            Prim(PrimitiveType.Sphere, "Cloud", c,
                new Vector3(22f, 5f, 16f), new Color(0.95f, 0.96f, 1f), false);
        }
    }

    // ------------------------------------------------------------ 角色与相机

    void CreateManager()
    {
        var go = new GameObject("GameManager");
        go.transform.SetParent(_root);
        go.AddComponent<GameManager>();
    }

    Transform BuildPlayer()
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = spawnPoint;

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.35f;
        cc.skinWidth = 0.02f;
        cc.minMoveDistance = 0f;

        // 身体：程序化人形（白裙 + 长发 + 佩剑）
        var motor = player.AddComponent<PlayerMotor>();
        var fsm = player.AddComponent<PlayerStateMachine>();
        var controller = player.AddComponent<PlayerController>();

        controller.motor = motor;
        controller.stateMachine = fsm;
        controller.respawnPoint = spawnPoint;

        // 身体：运行时加载 VRM 模型，失败自动退回程序化人形
        var combat = player.AddComponent<PlayerCombat>();

        var loader = player.AddComponent<VrmCharacterLoader>();
        loader.motor = motor;
        loader.combat = combat;

        var health = player.AddComponent<Health>();
        health.maxHealth = 100f;
        health.Died += () =>
        {
            if (GameManager.Instance != null)
                GameManager.Instance.HandlePlayerDeath(controller, health);
        };

        if (_shrine != null) _shrine.player = player.transform;

        return player.transform;
    }

    void BuildEnemies()
    {
        Vector3[] spots =
        {
            new Vector3(12f, 0.2f, 8f),
            new Vector3(-14f, 0.2f, -2f),
            new Vector3(9f, 0.2f, -18f),
            new Vector3(-9f, 0.2f, 12f),
            new Vector3(24f, 0.2f, -12f),
            new Vector3(-24f, 0.2f, 24f),
            new Vector3(0f, 0.2f, -24f),
        };

        foreach (var spot in spots)
        {
            var enemy = new GameObject("Enemy");
            enemy.transform.position = spot;

            var cc = enemy.AddComponent<CharacterController>();
            cc.height = 1.4f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.7f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.3f;

            // 外观图元都挂在 BodyPrims 下，VRM 模型加载后整体隐藏
            var prims = new GameObject("BodyPrims");
            prims.transform.SetParent(enemy.transform, false);

            var body = Prim(PrimitiveType.Capsule, "Body", Vector3.zero,
                new Vector3(0.62f, 0.7f, 0.62f), new Color(0.32f, 0.18f, 0.26f), false, prims.transform);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);

            var head = Prim(PrimitiveType.Sphere, "Head", Vector3.zero,
                new Vector3(0.44f, 0.42f, 0.44f), new Color(0.22f, 0.14f, 0.2f), false, prims.transform);
            head.transform.localPosition = new Vector3(0f, 1.32f, 0f);

            // 两点妖火似的眼睛
            for (int i = -1; i <= 1; i += 2)
            {
                var eye = Prim(PrimitiveType.Sphere, "Eye", Vector3.zero,
                    new Vector3(0.08f, 0.08f, 0.08f), new Color(1f, 0.72f, 0.35f), false, prims.transform);
                eye.transform.localPosition = new Vector3(0.1f * i, 1.34f, 0.2f);
            }

            // 尖角
            for (int i = -1; i <= 1; i += 2)
            {
                var horn = Prim(PrimitiveType.Cube, "Horn", Vector3.zero,
                    new Vector3(0.09f, 0.44f, 0.09f), new Color(0.86f, 0.8f, 0.68f), false, prims.transform);
                horn.transform.localPosition = new Vector3(0.17f * i, 1.62f, 0f);
                horn.transform.localRotation = Quaternion.Euler(0f, 0f, -20f * i);
            }

            // 细长的胳膊
            for (int i = -1; i <= 1; i += 2)
            {
                var arm = Prim(PrimitiveType.Cube, "Arm", Vector3.zero,
                    new Vector3(0.12f, 0.52f, 0.12f), new Color(0.3f, 0.17f, 0.24f), false, prims.transform);
                arm.transform.localPosition = new Vector3(0.37f * i, 0.86f, 0.02f);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, 14f * i);
            }

            var hp = enemy.AddComponent<Health>();
            hp.maxHealth = 60f;
            hp.ResetHealth();   // Awake 已按默认值初始化，这里改完最大值要重算当前值

            // 命中盒：专用 trigger，给挥剑判定用。
            // 身体图元的碰撞体被去掉了（避免和 CharacterController 打架），
            // 所以这里单独提供一个不参与物理碰撞、只参与查询的胶囊。
            var hitbox = new GameObject("Hitbox");
            hitbox.transform.SetParent(enemy.transform, false);
            hitbox.transform.localPosition = new Vector3(0f, 0.7f, 0f);

            var hitboxCol = hitbox.AddComponent<CapsuleCollider>();
            hitboxCol.isTrigger = true;
            hitboxCol.radius = 0.5f;
            hitboxCol.height = 1.6f;
            hitboxCol.direction = 1;

            enemy.AddComponent<Enemy>();

            // 模型就绪后自动替换上面的程序化图元
            var visual = enemy.AddComponent<VrmMonsterVisual>();
            visual.prims = prims.transform;
            visual.targetHeight = 1.55f;
        }
    }

    void BuildCamera(Transform target)
    {
        var cam = Camera.main;

        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = target.position + new Vector3(0f, 2.6f, -5f);
        }

        cam.fieldOfView = 55f;
        cam.farClipPlane = 400f;
        cam.backgroundColor = new Color(0.86f, 0.9f, 0.96f);

        var follow = cam.GetComponent<ThirdPersonCamera>();
        if (follow == null) follow = cam.gameObject.AddComponent<ThirdPersonCamera>();
        follow.target = target;
        follow.distance = 5f;
        follow.pivotOffset = new Vector3(0f, 1.5f, 0f);

        var controller = target.GetComponent<PlayerController>();
        if (controller != null) controller.cameraTransform = cam.transform;
    }

    void BuildHud(Transform target)
    {
        var hud = new GameObject("HUD");
        var gameHud = hud.AddComponent<GameHUD>();
        gameHud.motor = target.GetComponent<PlayerMotor>();
        gameHud.playerHealth = target.GetComponent<Health>();
        gameHud.combat = target.GetComponent<PlayerCombat>();
        gameHud.characterLoader = target.GetComponent<VrmCharacterLoader>();

        // 触屏控制（非触屏设备不显示）
        var touch = new GameObject("TouchControls");
        touch.AddComponent<TouchControls>();
    }

    // ------------------------------------------------------------ 调试入口

    /// <summary>传送玩家（调试/测试用，也可以通过 SendMessage 从外部调用）。</summary>
    public void Teleport(Vector3 position)
    {
        if (_player == null) return;

        var cc = _player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        _player.position = position;

        if (cc != null) cc.enabled = true;

        var motor = _player.GetComponent<PlayerMotor>();
        if (motor != null) motor.ResetVelocity();
    }

    public void TeleportToPad1() => Teleport(new Vector3(18f, 0.4f, -8f));
    public void TeleportToPad2() => Teleport(new Vector3(-18f, 0.4f, 0f));
    public void TeleportToPad3() => Teleport(new Vector3(0f, 0.4f, -16f));

    void Update()
    {
        // 调试快捷键：1/2/3 传送到三个弹跳台，方便验证动线
        if (GameManager.Instance == null || !GameManager.Instance.CanPlayerMove) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) TeleportToPad1();
        else if (Input.GetKeyDown(KeyCode.Alpha2)) TeleportToPad2();
        else if (Input.GetKeyDown(KeyCode.Alpha3)) TeleportToPad3();
    }

    void EnsureLight()
    {
        var lightGo = new GameObject("Sun");
        lightGo.transform.SetParent(_root);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.97f, 0.92f);
        lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
    }

    // ------------------------------------------------------------ 图元工具

    GameObject Box(string name, Vector3 position, Vector3 size, Color color, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_root);
        go.transform.position = position;
        go.transform.localScale = size;
        if (!collider) StripCollider(go);
        Paint(go, color);
        return go;
    }

    GameObject Cylinder(string name, Vector3 position, Vector3 scale, Color color, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(_root);
        go.transform.position = position;
        go.transform.localScale = scale;
        if (!collider) StripCollider(go);
        Paint(go, color);
        return go;
    }

    GameObject Prim(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color,
        bool collider, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        else go.transform.SetParent(_root);

        go.transform.position = position;
        go.transform.localScale = scale;
        if (!collider) StripCollider(go);
        Paint(go, color);
        return go;
    }

    GameObject Ramp(string name, Vector3 start, Vector3 end, float width, float thickness, Color color)
    {
        Vector3 delta = end - start;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_root);
        go.transform.position = (start + end) * 0.5f;
        go.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        go.transform.localScale = new Vector3(width, thickness, delta.magnitude);
        Paint(go, color);
        return go;
    }

    static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        col.enabled = false;
        Destroy(col);
    }

    static void Paint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = MaterialLibrary.For(color);
        if (mat != null) renderer.sharedMaterial = mat;
    }
}

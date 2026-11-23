using UnityEngine;

/// <summary>
/// 一次性搭出整张地图和玩法的入口脚本：挂在空场景的空物体上按 Play 即可。
/// 场景元素全部用内置图元运行时生成，不依赖任何美术资源。
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("玩家")]
    public Vector3 spawnPoint = new Vector3(0f, 0.35f, -38f);

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

        // 开源道具（CC0，Kenney）异步加载，完成后替换云 / 浮空岛并撒草
        PropLoader.LoadAll(OnPropsReady);
    }

    /// <summary>给导入的人形模型挂一把程序化长剑（VRM 本身没有武器）。</summary>
    // ------------------------------------------------------------ 基础地形

    void BuildTerrain()
    {
        Box("Ground", new Vector3(0f, -0.5f, -14f), new Vector3(120f, 1f, 120f), grass);

        // 上山的路：山脚 → 山腰平台 → 石阶 → 峰顶剑台
        Ramp("RampA", new Vector3(0f, 0.03f, 4f), new Vector3(0f, 2.4f, 12f), 6f, 0.4f, stone);
        Box("HillPlatform", new Vector3(0f, 2.0f, 16f), new Vector3(14f, 0.8f, 10f), grass);

        // 山腰西侧的独立台地，靠断桥连接（要跳过去）
        Box("SpurPlatform", new Vector3(-14f, 2.0f, 20f), new Vector3(8f, 0.8f, 8f), grass);
        BrokenBridge();

        // 石阶：比斜坡更有"登山"的感觉
        Stairs(new Vector3(0f, 2.4f, 21f), 8, 0.36f, 0.9f, 4.6f, stone);

        Box("ShrinePlatform", new Vector3(0f, 4.8f, 32f), new Vector3(12f, 0.8f, 10f), stone);

        // 山门：两根柱子 + 横梁，纯装饰
        Box("GateLeft", new Vector3(-3.2f, 2.2f, 29f), new Vector3(0.6f, 4.4f, 0.6f), wood);
        Box("GateRight", new Vector3(3.2f, 2.2f, 29f), new Vector3(0.6f, 4.4f, 0.6f), wood);
        Box("GateBeam", new Vector3(0f, 4.55f, 29f), new Vector3(7.4f, 0.5f, 0.8f), wood);

        // 栏杆：南、东、西三边，北边留给上山的路
        Railing(-58f, 58f, -62f, true);   // 南（z = -62）
        Railing(-58f, 58f, 58f, false);   // 东（x = 58）
        Railing(-58f, 58f, -58f, false);  // 西（x = -58）

        BuildLandmarks();
        BuildVillage();
    }

    /// <summary>断桥：三段木板，中间留缺口，玩家得跳过去。</summary>
    void BrokenBridge()
    {
        float y = 2.55f;
        float z = 18f;

        Box("BridgeA", new Vector3(-8.6f, y, z), new Vector3(2.4f, 0.25f, 2.2f), wood);
        Box("BridgeB", new Vector3(-11.4f, y + 0.15f, z + 0.3f), new Vector3(1.6f, 0.25f, 2.0f), wood);

        // 断口两侧的残桩
        Box("BridgeStumpA", new Vector3(-9.8f, y + 0.1f, z - 1.2f), new Vector3(0.35f, 1.1f, 0.35f), wood);
        Box("BridgeStumpB", new Vector3(-10.6f, y + 0.3f, z + 1.4f), new Vector3(0.35f, 1.3f, 0.35f), wood);
    }

    /// <summary>石阶：一级一级的薄台阶。</summary>
    void Stairs(Vector3 start, int count, float rise, float run, float width, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            var pos = start + new Vector3(0f, i * rise, i * run + run * 0.5f);
            Box("Step", pos, new Vector3(width, rise * 1.6f, run * 1.05f), color);
        }
    }

    /// <summary>山腰的亭子、竹丛等景观。</summary>
    void BuildLandmarks()
    {
        // 亭子：四柱 + 两层顶
        Vector3 pavilion = new Vector3(0f, 2.4f, 14f);

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -2.2f : 2.2f;
            float sz = (i < 2) ? -2.2f : 2.2f;
            Box("PavilionPillar", pavilion + new Vector3(sx, 1.6f, sz),
                new Vector3(0.3f, 3.2f, 0.3f), wood);
        }

        Box("PavilionRoof", pavilion + new Vector3(0f, 3.4f, 0f), new Vector3(6.2f, 0.3f, 6.2f), wood);
        Box("PavilionRoofTop", pavilion + new Vector3(0f, 3.85f, 0f), new Vector3(3.6f, 0.35f, 3.6f), blossom);

        // 竹林：成簇的细竹竿
        for (int cluster = 0; cluster < 6; cluster++)
        {
            float cx = Random.Range(-50f, 50f);
            float cz = Random.Range(-56f, 30f);

            if (new Vector2(cx, cz).magnitude < 8f) continue;   // 别堵出生点

            for (int b = 0; b < 5; b++)
            {
                Vector3 pos = new Vector3(
                    cx + Random.Range(-1.6f, 1.6f), 2.4f,
                    cz + Random.Range(-1.6f, 1.6f));

                float height = Random.Range(3.5f, 6f);
                Prim(PrimitiveType.Cylinder, "Bamboo", pos + Vector3.up * (height * 0.5f - 2.4f),
                    new Vector3(0.09f, height * 0.5f, 0.09f), grass, false);

                Prim(PrimitiveType.Sphere, "BambooLeaves",
                    pos + Vector3.up * (height - 2.2f), Vector3.one * 0.9f, grass, false);
            }
        }
    }

    /// <summary>带旋转的方块（路面、屋顶、招牌都靠它）。</summary>
    GameObject BoxRot(string name, Vector3 position, Vector3 size, Color color, Vector3 euler, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_root);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(euler);
        go.transform.localScale = size;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = MaterialLibrary.For(color);
            if (mat != null) renderer.sharedMaterial = mat;
        }

        if (!collider) Destroy(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>四元数版本的方块，屋顶斜面要用复合旋转。</summary>
    GameObject BoxRotQ(string name, Vector3 position, Vector3 size, Color color, Quaternion rotation, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_root);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = size;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = MaterialLibrary.For(color);
            if (mat != null) renderer.sharedMaterial = mat;
        }

        if (!collider) Destroy(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>
    /// 山村聚落：环形主路 + 木构民居 + 樱花隧道 + 街边道具，
    /// 南面是生活区，北面的山与剑碑收住主路尽头。
    /// </summary>
    void BuildVillage()
    {
        BuildSkyAndFog();
        BuildRingRoad();
        BuildCherryTunnel();
        BuildHouses();
        BuildStreetProps();
    }

    void BuildSkyAndFog()
    {
        // 空气透视：远景被雾压成灰蓝，近景保持清晰
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.011f;
        RenderSettings.fogColor = new Color(0.86f, 0.80f, 0.82f);

        BuildSkyDome();

        // 三层远山：由近到远逐层变淡变高，配合雾形成纵深
        var layers = new[]
        {
            (radius: 95f, spread: 25f, height: 30f, tall: 26f, color: new Color(0.44f, 0.52f, 0.64f), count: 16),
            (radius: 165f, spread: 35f, height: 45f, tall: 30f, color: new Color(0.55f, 0.63f, 0.75f), count: 20),
            (radius: 245f, spread: 45f, height: 60f, tall: 34f, color: new Color(0.64f, 0.72f, 0.82f), count: 24),
        };

        foreach (var layer in layers)
        {
            for (int i = 0; i < layer.count; i++)
            {
                float angle = i * Mathf.PI * 2f / layer.count + Random.Range(-0.06f, 0.06f);
                float radius = layer.radius + Random.Range(-layer.spread, layer.spread);
                float height = layer.height + Random.Range(0f, layer.tall);

                var pos = new Vector3(Mathf.Cos(angle) * radius, height * 0.33f, Mathf.Sin(angle) * radius);

                var peak = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                peak.name = "FarPeak";
                peak.transform.SetParent(_root);
                peak.transform.position = pos;
                peak.transform.localScale = new Vector3(
                    Random.Range(28f, 48f), height * 0.5f, Random.Range(28f, 48f));
                Destroy(peak.GetComponent<Collider>());

                var renderer = peak.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = MaterialLibrary.For(layer.color);
                    if (mat != null) renderer.sharedMaterial = mat;
                }
            }
        }
    }

    /// <summary>
    /// 渐变天穹：运行时生成一张竖直渐变贴图塞进天空盒，
    /// 地平线暖白 → 中景淡紫 → 天顶深蓝，解决远处天空直接切开的问题。
    /// </summary>
    void BuildSkyDome()
    {
        var shader = Shader.Find("Skybox/Panoramic");

        if (shader == null)
        {
            Debug.LogWarning("[sky] 找不到 Skybox/Panoramic，退回纯色背景");
            return;
        }

        const int height = 128;
        var tex = new Texture2D(4, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        Color horizon = new Color(0.96f, 0.84f, 0.76f);
        Color middle = new Color(0.68f, 0.68f, 0.82f);
        Color zenith = new Color(0.30f, 0.38f, 0.60f);

        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            float v = 1f - t;                    // 贴图 v=0 是天顶

            Color c = t < 0.55f
                ? Color.Lerp(horizon, middle, t / 0.55f)
                : Color.Lerp(middle, zenith, (t - 0.55f) / 0.45f);

            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }

        tex.Apply();

        var mat = new Material(shader);
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Mapping", 1f);      // 1 = Latitude/Longitude（等距柱状）
        mat.SetFloat("_Exposure", 1f);

        RenderSettings.skybox = mat;
        RenderSettings.ambientSkyColor = Color.Lerp(horizon, zenith, 0.5f);
    }

    void BuildRingRoad()
    {
        Vector3 center = new Vector3(0f, 0f, -20f);
        float rx = 24f;
        float rz = 18f;
        int segments = 32;

        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;

            var p0 = center + new Vector3(Mathf.Cos(a0) * rx, 0f, Mathf.Sin(a0) * rz);
            var p1 = center + new Vector3(Mathf.Cos(a1) * rx, 0f, Mathf.Sin(a1) * rz);
            var mid = (p0 + p1) * 0.5f;

            float length = Vector3.Distance(p0, p1) * 1.42f;
            float yaw = Mathf.Atan2(p1.x - p0.x, p1.z - p0.z) * Mathf.Rad2Deg;
            var rot = new Vector3(0f, yaw, 0f);

            // 石板明暗交替，读起来像一块块铺出来的
            Color tone = (i % 2 == 0)
                ? stoneDark
                : new Color(0.60f, 0.63f, 0.67f);

            BoxRot("Road", mid + Vector3.up * 0.08f, new Vector3(3.4f, 0.22f, length), tone, rot);

            // 两侧碎石边，压住路缘
            foreach (var side in new[] { -1f, 1f })
            {
                Vector3 edge = mid + Quaternion.Euler(rot) * new Vector3(side * 1.95f, 0f, 0f) + Vector3.up * 0.055f;
                BoxRot("RoadEdge", edge, new Vector3(0.6f, 0.2f, length),
                    new Color(0.52f, 0.55f, 0.58f), rot, false);
            }

            // 接缝：横跨路面的深色细条
            BoxRot("Seam", p0 + Vector3.up * 0.105f,
                new Vector3(3.4f, 0.24f, 0.14f), new Color(0.34f, 0.36f, 0.40f), rot, false);

            // 潮湿反光：偶尔一块偏亮的水迹
            if (i % 3 == 0)
            {
                var wet = mid + Vector3.up * 0.108f;
                BoxRot("WetPatch", wet + new Vector3(Random.Range(-1.2f, 1.2f), 0f, 0f),
                    new Vector3(Random.Range(1.4f, 2.4f), 0.235f, Random.Range(0.8f, 1.6f)),
                    new Color(0.60f, 0.63f, 0.68f), rot, false);
            }
        }

        // 从主环通往北面山路的岔路
        BoxRot("RoadBranch", new Vector3(0f, 0.09f, 0.5f), new Vector3(3.4f, 0.22f, 12f), stoneDark, Vector3.zero);
    }

    void BuildCherryTunnel()
    {
        Vector3 center = new Vector3(0f, 0f, -14f);

        // 沿主环内侧种树，树冠在路面上方交汇，形成隧道感
        for (int i = 0; i < 18; i++)
        {
            float angle = i * Mathf.PI * 2f / 18f;
            float radius = 18.5f;

            var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            float trunkH = Random.Range(3.4f, 4.6f);
            Prim(PrimitiveType.Cylinder, "CherryTrunk", pos + Vector3.up * (trunkH * 0.5f),
                new Vector3(0.32f, trunkH * 0.5f, 0.32f), wood, true);

            // 树冠偏向路心，让枝条在头顶交叠
            Vector3 inward = (center - pos).normalized * Random.Range(1.6f, 2.6f);
            var canopy = pos + Vector3.up * (trunkH + 1.5f) + inward;

            Prim(PrimitiveType.Sphere, "CherryCanopy", canopy,
                new Vector3(Random.Range(2.4f, 3.0f), Random.Range(1.8f, 2.3f), Random.Range(2.4f, 3.0f)),
                blossom, false);

            Prim(PrimitiveType.Sphere, "CherryCanopy", canopy + new Vector3(1.8f, 0.5f, 0.8f),
                new Vector3(2.6f, 2.0f, 2.6f), blossom, false);
        }
    }

    void BuildHouses()
    {
        Vector3 center = new Vector3(0f, 0f, -14f);
        var wall = new Color(0.90f, 0.86f, 0.78f);
        var beam = new Color(0.28f, 0.20f, 0.16f);
        var roof = new Color(0.32f, 0.24f, 0.22f);
        var warm = new Color(1f, 0.86f, 0.52f);

        for (int i = 0; i < 9; i++)
        {
            float angle = i * Mathf.PI * 2f / 9f + 0.22f;
            float radius = 32f;

            var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            // 房子朝向路心
            float yaw = Mathf.Atan2(center.x - pos.x, center.z - pos.z) * Mathf.Rad2Deg;
            var rot = new Vector3(0f, yaw, 0f);

            float w = Random.Range(6.5f, 8.5f);
            float d = Random.Range(5.5f, 7f);
            float h = Random.Range(4.2f, 5.6f);

            // 木构架尺寸
            float halfW = w * 0.5f;
            float halfD = d * 0.5f;

            var yawRot = Quaternion.Euler(0f, yaw, 0f);

            // 墙体：做成空心的四壁，门洞留空，玩家可以走进去
            const float thickness = 0.3f;
            float doorWidth = 1.7f;

            Vector3 Local(float x, float y, float z) => pos + yawRot * new Vector3(x, y, z);

            // 室内地面（比外面低一点点，踏进去有落差）
            BoxRot("HouseFloor", Local(0f, 0.06f, 0f), new Vector3(w - thickness, 0.16f, d - thickness),
                new Color(0.38f, 0.30f, 0.26f), rot, false);

            // 后墙
            BoxRot("HouseWall", Local(0f, h * 0.5f, halfD - thickness * 0.5f),
                new Vector3(w, h, thickness), wall, rot);

            // 左右墙
            foreach (var side in new[] { -1f, 1f })
            {
                BoxRot("HouseWall", Local(side * (halfW - thickness * 0.5f), h * 0.5f, 0f),
                    new Vector3(thickness, h, d), wall, rot);
            }

            // 前墙：中间留门洞，两侧各一段
            float sideWidth = halfW - doorWidth * 0.5f - thickness;
            foreach (var side in new[] { -1f, 1f })
            {
                float cx = side * (doorWidth * 0.5f + sideWidth * 0.5f);
                BoxRot("HouseWall", Local(cx, h * 0.5f, -halfD + thickness * 0.5f),
                    new Vector3(sideWidth, h, thickness), wall, rot);
            }

            // 门楣（门洞上方补一段墙）
            float lintelHeight = h - 2.5f;
            if (lintelHeight > 0.1f)
            {
                BoxRot("HouseWall", Local(0f, 2.5f + lintelHeight * 0.5f, -halfD + thickness * 0.5f),
                    new Vector3(doorWidth, lintelHeight, thickness), wall, rot);
            }

            // 室内一盏小灯，走进去不至于全黑
            var innerGlow = new GameObject("InnerLight");
            innerGlow.transform.SetParent(_root);
            innerGlow.transform.position = Local(0f, 2.2f, 0f);

            var innerLight = innerGlow.AddComponent<Light>();
            innerLight.type = LightType.Point;
            innerLight.color = new Color(1f, 0.78f, 0.5f);
            innerLight.range = Mathf.Max(w, d) * 0.9f;
            innerLight.intensity = 1.3f;
            innerLight.shadows = LightShadows.None;

            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                {
                    Vector3 corner = pos + Quaternion.Euler(rot) * new Vector3(sx * halfW, h * 0.5f, sz * halfD);
                    BoxRot("Beam", corner, new Vector3(0.32f, h, 0.32f), beam, rot, false);
                }
            }

            BoxRot("BeamBelt", pos + Vector3.up * (h * 0.62f), new Vector3(w * 1.02f, 0.28f, d * 1.02f), beam, rot, false);

            // 坡屋顶：两块斜板沿屋脊对称，屋脊平行于进深方向
            float roofHeight = w * 0.34f;
            float halfWidth = w * 0.5f;
            float slope = Mathf.Atan2(roofHeight, halfWidth) * Mathf.Rad2Deg;
            float slabLen = Mathf.Sqrt(halfWidth * halfWidth + roofHeight * roofHeight);

            foreach (var side in new[] { -1f, 1f })
            {
                var localOffset = new Vector3(side * halfWidth * 0.5f, h + roofHeight * 0.5f, 0f);
                var slabRot = yawRot * Quaternion.Euler(0f, 0f, -side * slope);

                BoxRotQ("Roof", pos + yawRot * localOffset,
                    new Vector3(slabLen * 1.04f, 0.26f, d * 1.18f), roof, slabRot, false);
            }

            // 屋脊压条
            BoxRot("Ridge", pos + Vector3.up * (h + roofHeight),
                new Vector3(0.34f, 0.26f, d * 1.2f), beam, rot, false);

            // 檐口：沿前后檐口各一条深色横木
            foreach (var eave in new[] { -1f, 1f })
            {
                Vector3 ep = pos + yawRot * new Vector3(0f, h + 0.12f, eave * (d * 0.5f + 0.1f));
                BoxRot("Eave", ep, new Vector3(w * 1.08f, 0.22f, 0.24f), beam, rot, false);
            }

            // 窗户：木框 + 内嵌暖光 + 窗台
            for (int win = -1; win <= 1; win += 2)
            {
                Vector3 wp = pos + yawRot * new Vector3(win * w * 0.24f, h * 0.46f, -halfD - 0.045f);

                BoxRot("WindowFrame", wp, new Vector3(1.55f, 1.35f, 0.1f), beam, rot, false);
                BoxRot("WindowGlass", wp + yawRot * new Vector3(0f, 0f, -0.07f),
                    new Vector3(1.22f, 1.02f, 0.06f), warm, rot, false);
                BoxRot("WindowSill", wp + yawRot * new Vector3(0f, -0.78f, -0.12f),
                    new Vector3(1.75f, 0.12f, 0.36f), beam, rot, false);
            }

            // 门：门框 + 门板 + 门楣
            // 门框 + 半开的门板（门洞是空的，玩家能走进去）
            var doorRot = new Vector3(0f, yaw + 32f, 0f);
            Vector3 door = pos + yawRot * new Vector3(-doorWidth * 0.5f, 1.2f, -halfD);
            BoxRot("DoorPanel", door, new Vector3(1.15f, 2.3f, 0.08f),
                new Color(0.36f, 0.24f, 0.18f), doorRot, false);
            BoxRot("DoorLintel", pos + yawRot * new Vector3(0f, 2.55f, -halfD),
                new Vector3(doorWidth + 0.5f, 0.2f, 0.36f), beam, rot, false);

            // 檐下阴影：靠顶的墙面压暗，模拟挑檐遮挡
            BoxRot("EaveShadow", pos + yawRot * new Vector3(0f, h - 0.38f, -halfD - 0.03f),
                new Vector3(w * 0.98f, 0.75f, 0.07f), new Color(0.74f, 0.70f, 0.62f), rot, false);

            // 檐下灯笼
            Vector3 lanternPos = pos + Quaternion.Euler(rot) * new Vector3(0f, h - 0.5f, -halfD - 0.35f);
            Prim(PrimitiveType.Sphere, "Lantern", lanternPos, Vector3.one * 0.5f, new Color(1f, 0.55f, 0.45f), false);

            var glow = new GameObject("LanternLight");
            glow.transform.SetParent(_root);
            glow.transform.position = lanternPos;

            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.45f);
            light.range = 7f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;

            // 灯光洒在地面的暖色光池（点光源在低端设备上衰减得厉害，用贴片补）
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "LightPool";
            pool.transform.SetParent(_root);
            pool.transform.position = new Vector3(lanternPos.x, 0.115f, lanternPos.z);
            pool.transform.localScale = new Vector3(3.6f, 0.008f, 3.6f);
            Destroy(pool.GetComponent<Collider>());

            var poolRenderer = pool.GetComponent<Renderer>();
            if (poolRenderer != null)
            {
                var mat = MaterialLibrary.For(new Color(0.96f, 0.84f, 0.64f));
                if (mat != null) poolRenderer.sharedMaterial = mat;
            }
        }
    }

    void BuildStreetProps()
    {
        Vector3 center = new Vector3(0f, 0f, -14f);
        var benchWood = new Color(0.42f, 0.30f, 0.24f);

        for (int i = 0; i < 14; i++)
        {
            float angle = i * Mathf.PI * 2f / 14f + 0.5f;
            var pos = center + new Vector3(Mathf.Cos(angle) * 26.5f, 0f, Mathf.Sin(angle) * 26.5f);
            float yaw = Mathf.Atan2(center.x - pos.x, center.z - pos.z) * Mathf.Rad2Deg;
            var rot = new Vector3(0f, yaw, 0f);

            switch (i % 3)
            {
                case 0: // 长椅
                    BoxRot("BenchSeat", pos + Vector3.up * 0.5f, new Vector3(2.2f, 0.18f, 0.7f), benchWood, rot);
                    BoxRot("BenchBack", pos + Vector3.up * 0.85f + Quaternion.Euler(rot) * new Vector3(0f, 0f, -0.3f),
                        new Vector3(2.2f, 0.5f, 0.14f), benchWood, rot, false);
                    break;

                case 1: // 路灯
                    Prim(PrimitiveType.Cylinder, "LampPost", pos + Vector3.up * 1.7f,
                        new Vector3(0.16f, 1.7f, 0.16f), new Color(0.30f, 0.28f, 0.26f), true);
                    Prim(PrimitiveType.Sphere, "LampGlobe", pos + Vector3.up * 3.6f,
                        Vector3.one * 0.55f, new Color(1f, 0.92f, 0.66f), false);
                    break;

                default: // 木箱堆
                    BoxRot("Crate", pos + Vector3.up * 0.45f, Vector3.one * 0.9f, benchWood, rot);
                    BoxRot("Crate", pos + Vector3.up * 1.25f, Vector3.one * 0.75f, benchWood,
                        new Vector3(0f, yaw + 18f, 0f));
                    break;
            }
        }
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
        _islandSpots = new[]
        {
            new Vector3(18f, 7f, -16f),
            new Vector3(-18f, 9.5f, -14f),
            new Vector3(0f, 12.5f, -26f),
        };

        Island(_islandSpots[0], 9f);
        Island(_islandSpots[1], 9f);
        Island(_islandSpots[2], 8f);

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
        // 三层分布：山脚 4 片、山腰 4 片（含断桥对岸）、峰顶 2 片、浮空岛 2 片
        Vector3[] spots =
        {
            // 山脚
            new Vector3(0f, 1.1f, 0f),
            new Vector3(6f, 1.1f, 6f),
            new Vector3(-6f, 1.1f, 6f),
            new Vector3(10f, 1.1f, -4f),

            // 山腰：亭子、断桥对岸的独立台地
            new Vector3(0f, 3.6f, 14f),
            new Vector3(5f, 3.6f, 18f),
            new Vector3(-4f, 3.6f, 12f),
            new Vector3(-14f, 3.9f, 20f),

            // 峰顶剑台
            new Vector3(0f, 6.8f, 32f),
            new Vector3(3f, 6.8f, 34f),

            // 浮空岛
            new Vector3(18f, 8.1f, -16f),
            new Vector3(-18f, 10.6f, -14f),
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

        // 先把位置记下来，等开源模型加载完再生成；模型没加载成功就用程序化云
        _cloudSpots = clouds;

        foreach (var c in clouds)
        {
            Prim(PrimitiveType.Sphere, "Cloud", c,
                new Vector3(22f, 5f, 16f), new Color(0.95f, 0.96f, 1f), false);
        }
    }

    Vector3[] _cloudSpots;

    /// <summary>开源道具加载完成后，用模型替换程序化云与浮空岛、撒草。</summary>
    void OnPropsReady()
    {
        // 目前模型能加载进内存但画面里不显示（材质/变换还在查），
        // 先关掉替换，避免删掉原来的云和浮空岛造成回退。
        const bool ReplaceProcedural = true;

        if (!ReplaceProcedural) return;

        if (_cloudSpots != null && PropLoader.Has("cloud"))
        {
            foreach (var t in _root.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Cloud" || t.name == "CloudBackup") Destroy(t.gameObject);
            }

            foreach (var spot in _cloudSpots)
            {
                PropLoader.Spawn("cloud", spot + Vector3.up * 18f, 12f, Random.Range(0f, 360f));
                PropLoader.Spawn("cloud", spot + new Vector3(10f, 14f, 8f), 8f, Random.Range(0f, 360f));
            }

            Debug.Log("[prop] 云已替换为开源模型");
        }

        if (PropLoader.Has("island"))
        {
            foreach (var t in _root.GetComponentsInChildren<Transform>())
            {
                if (t.name == "IslandTop" || t.name == "IslandUnder") Destroy(t.gameObject);
            }

            foreach (var island in _islandSpots)
            {
                var go = PropLoader.Spawn("island", island + Vector3.down * 0.4f, 1.8f, Random.Range(0f, 360f));
                if (go != null) go.name = "IslandGrass";
            }
        }

        if (PropLoader.Has("grass"))
        {
            // 同一帧内创建的碰撞体还没注册进物理世界，先同步一次再射线
            Physics.SyncTransforms();
            for (int i = 0; i < 60; i++)
            {
                float x = Random.Range(-32f, 32f);
                float z = Random.Range(-32f, 32f);

                if (Physics.Raycast(new Vector3(x, 20f, z), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.point.y > 2f) continue;   // 只撒在平地
                    PropLoader.Spawn(Random.value > 0.5f ? "grass" : "grass_small",
                        hit.point, Random.Range(0.8f, 1.4f), Random.Range(0f, 360f));
                }
            }

            Debug.Log("[prop] 草地细节已生成");
        }
    }

    Vector3[] _islandSpots;


    // ------------------------------------------------------------ 角色与相机

    void CreateManager()
    {
        var go = new GameObject("GameManager");
        go.transform.SetParent(_root);
        go.AddComponent<GameManager>();

        // 烽火地带模式的运行时逻辑（监听模式开始事件）
        var extraction = new GameObject("ExtractionMode");
        extraction.transform.SetParent(_root);
        extraction.AddComponent<ExtractionMode>();

        // 联机对枪
        var net = new GameObject("Net");
        net.transform.SetParent(_root);
        net.AddComponent<NetDuel>();
    }

    /// <summary>顶部滚动提示（拾取物资等）。</summary>
    public static void NotifyLoot(string message)
    {
        _toast = message;
        _toastUntil = Time.time + 2.6f;
    }

    public static string ToastText => Time.time < _toastUntil ? _toast : "";

    static string _toast;
    static float _toastUntil;

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

        // 物资增益（医疗/攻击/疾行）
        player.AddComponent<PlayerBuffs>();

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
        cam.backgroundColor = new Color(0.86f, 0.88f, 0.95f);
        cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;

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
        light.intensity = 1.42f;
        light.color = new Color(1f, 0.88f, 0.72f);      // 低角度暖光
        lightGo.transform.rotation = Quaternion.Euler(28f, -52f, 0f);   // 黄昏斜射光
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.62f;

        // 环境光：偏冷的暮色，和暖色阳光形成冷暖对比
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.56f, 0.56f, 0.62f);
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

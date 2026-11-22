# 桃夭 · 寻剑

小型 3D 开放关卡 + 动作战斗：**探索山门 → 挥剑清路 → 收集十二片花瓣 → 唤醒剑碑 → 归剑**。

移动用 `CharacterController` + 状态机；战斗是自研的挥剑判定 + 敌人 AI；场景、角色、特效、音效全部**运行时生成**，不依赖任何美术或音频素材。你提供的三张人物图用在标题、结局和 HUD 立绘。

工程已在 **Unity 6000.6.10f1** 下用命令行编译验证：**0 error、0 warning**。

## 跑起来（2 步）

1. Unity Hub → **Add project from disk** → 选 `unity-peach` 文件夹 → 打开（首次导入 1–2 分钟）。
2. 打开 `Assets/Demo/Scenes/Main.unity` → 按 **Play**。

## 操作

| 按键 | 动作 |
| --- | --- |
| WASD | 移动 |
| Shift | 冲刺 |
| 空格 | 跳跃 |
| **鼠标左键 / J** | **挥剑（三段连击）** |
| 鼠标 | 视角 |
| Esc | 释放/锁定鼠标 |
| 任意键 | 标题界面开始 |
| R | 结局界面重来 |

## 战斗

- **三段连击**：伤害 18 / 20 / 32，0.85 秒内不继续攻击会重置连段。
- **自动索敌**：起手时自动转向最近的敌人（前方 4.5m，或贴身 2.6m 内允许背身），并在 0.3 秒内锁住转向，不会被移动方向立刻拽回去。
- **判定方式**：挥砍的"生效帧"里，在角色前方 1.5m、半径 1.2m 做球形检测（包含敌人的 trigger 命中盒），一次挥砍每个目标只结算一次伤害。
- **打击感**：命中瞬间 50ms 顿帧 + 火花 + 击退 + 音效，屏幕左右轻震。
- **敌人 AI**：待机 → 发现玩家追击 → 进入攻击距离后前摇 0.45 秒 → 挥击。前摇期间会压扁身体作为预警。
- **玩家生命**：100 点，受击有 0.35 秒无敌帧，屏幕泛红提示。血量归零后 1.2 秒原地复活。
- **敌人生命**：60 点，两到三刀击杀；死亡时炸成碎片并计数（HUD 显示"斩 N"）。

## 音效与特效

**全部是程序化生成的**，没有任何音频/贴图文件：

- 音效：`Sfx.cs` 在运行时合成波形——挥剑是噪声扫频，命中是低频撞击 + 噪声，受伤是方波下滑，拾取是双音阶提示音，死亡是下坠音。44.1kHz 单声道，`AudioClip.Create` + `SetData`。
- 粒子：`Fx.cs` 用大量小方块模拟火花与花瓣碎片，带重力、旋转、缩小消失。没有用 ParticleSystem，避免材质与渲染管线依赖。
- 挥剑还会沿刀弧生成一串小火花，爆炸感来自 `Fx.SwingArc`。

## 地图

| 位置 | 内容 |
| --- | --- |
| 出生点 (0, 0, -12) | 平地，正前方是上山的路 |
| 山道 | 两段坡道 + 两级平台，通往山顶剑碑 |
| 山顶剑碑 (0, 5, 32) | 石台 + 石碑 + 悬浮的剑 + 灵光 |
| 塔 (-24, 0, 18) | 三层塔 |
| 湖 (22, 0, 4) | 水面 + 假山 + 细流 + 池边石 |
| 桃树 ×8 | 树干 + 三团粉色花冠 |
| 浮空岛 ×3 | y = 7 / 9.5 / 12.5，各藏一片花瓣，靠弹跳台上岛 |
| 敌人 ×7 | 紫色妖物，分布在平地与山道 |

**上岛路线**：地图上有三个金色弹跳台，站上去就会被弹到对应浮空岛（会自动对准落点）：

| 弹跳台 | 位置 | 落点 |
| --- | --- | --- |
| 1 号 | (18, 0, -8) | 一号岛（y=7） |
| 2 号 | (-18, 0, 0) | 二号岛（y=9.5） |
| 3 号 | (0, 0, -16) | 三号岛（y=12.5） |

弹跳台用**距离判定**而不是触发器，并按抛物线反解横向速度——站上去什么都不按也能准确落到岛上。

## 代码结构

```
Assets/Demo/Scripts/
  GameBootstrap.cs       入口：搭地图、造角色、生成敌人、接相机/HUD
  GameManager.cs         流程：标题/游玩/结局，花瓣、击杀、计时、死亡复活
  GameHUD.cs             所有 UI：标题页、HUD（血条/计数）、结局页、受伤泛红
  PlayerMotor.cs         移动核心：加减速、转向、重力、跳跃、坡道、冲量
  PlayerStateMachine.cs  状态机：Idle / Move / Sprint / Jump / Fall
  PlayerController.cs    输入层，非游玩状态屏蔽输入
  ThirdPersonCamera.cs   第三人称相机
  Combat/Health.cs       通用生命值 + 无敌帧
  Combat/PlayerCombat.cs 三段连击、球形判定、打击感
  Combat/Enemy.cs        敌人 AI（追击/前摇/挥击）、受击闪白、击退、死亡
  Visual/HumanoidRig.cs  程序化人形：拼装 + 走路/跳跃/挥剑动画
  Fx/Fx.cs               粒子爆炸、刀弧火花、顿帧
  Fx/Sfx.cs              程序化音效合成
  Collectible.cs         花瓣：自转浮动、触发器 + 距离双保险拾取
  BouncePad.cs           弹跳台
  ShrineGate.cs          剑碑解锁动画 + 结局触发
  RootMotionBridge.cs    可选：动画根运动
Assets/Demo/Editor/
  SetupScene.cs          生成主场景（菜单 Tools → 桃夭 → 生成主场景）
  WebGLBuilder.cs        WebGL 打包（菜单 Tools → 桃夭 → 导出 WebGL）
Assets/Demo/Resources/Art/
  bg_back.jpg / bg_side.jpg / bg_sword.jpg
```

## 导出 WebGL

工程里**已经带了一份打包好的网页版**，就在 `Build/WebGL/`，直接起个静态服务器就能玩：

```powershell
cd Build/WebGL
python -m http.server 8000
# 浏览器打开 http://127.0.0.1:8000
```

打包参数已经在 `WebGLBuilder.cs` 里配好（`decompressionFallback` + `stripEngineCode=false`），重新打包只需：

**编辑器里**：菜单 `Tools → 桃夭 → 导出 WebGL`。

**命令行**：

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath D:\dsh\unity-peach `
  -buildTarget WebGL `
  -executeMethod WebGLBuilder.BuildWebGL
```

> 打包完成后**不能直接双击 index.html**（浏览器会拦 `file://` 的加载），必须走本地服务器。

### 发布到公网（给别人玩）

`Build/WebGL` 是纯静态站点，任何静态托管都能放：

1. 打开 <https://app.netlify.com/drop>
2. 把 `Build/WebGL` 整个文件夹拖进去
3. 拿到 `xxx.netlify.app` 链接，发给谁都能玩（手机横屏也行）

同理可用于 Cloudflare Pages、GitHub Pages、itch.io。**不需要后端**，游戏逻辑全在客户端。

**两个必须注意的打包参数**（脚本里已设置，换构建方式时别丢）：

| 设置 | 值 | 不设会怎样 |
| --- | --- | --- |
| `decompressionFallback` | `true` | `.gz` 需要服务器带 `Content-Encoding`，否则报 *Malformed data* |
| `stripEngineCode` | `false` | 运行时创建的碰撞体被剥离，报 `class 'SphereCollider' doesn't exist`，**挥剑完全打不中** |

另外 `Assets/link.xml` 里保留了物理、音频、粒子等模块，双保险。

## 换成真实 3D 角色模型

现在的角色是 `HumanoidRig.cs` 用图元拼的（躯干、四肢、头发、佩剑），配程序化动画。要换成真实模型：

1. 把 `.fbx` / `.glb` 拖进 `Assets/Demo/Models/`，材质会自动导入。
2. 在场景里选中 `Player`，把 `HumanoidRig` 组件**禁用**（或删掉），模型挂到 `Player` 下面当子物体。
3. 模型如果是人形，给它加 `Animator` + 人形 Avatar，在 `PlayerStateMachine` 的 `animator` 字段里拖进去——它会自动写 `State` / `Speed` / `Grounded` 三个参数。
4. 如果想用动画自带的位移，给模型挂 `RootMotionBridge.cs`（见文件头注释）。
5. 攻击动作：在 `PlayerCombat.Swung` 事件里触发 `animator.SetTrigger("Attack")` 即可。

模型来源建议用 CC0 或商用许可的资源（Mixamo 的动作库对个人免费），不要把有版权的模型打进公开版本。

## 调试开关

| 按键 | 作用 |
| --- | --- |
| `1` / `2` / `3` | 传送到对应弹跳台，用来快速验证上岛动线 |

## 免费 3D 模型资源（可商用）

当前角色和怪物都是程序化图元拼的。想换成真实模型，下面这些来源可以直接用：

| 来源 | 内容 | 授权 | 链接 |
| --- | --- | --- | --- |
| **Quaternius** | 低多边形角色、怪物、动画（Ultimate Modular Characters / Monsters） | CC0（QAL） | <https://quaternius.com/license.html> |
| **Kenney** | 角色、怪物、道具，成套打包 | CC0 | <https://kenney.nl/assets> |
| **KayKit** | 低多边形角色与怪物，风格统一 | CC0 | <https://kaylousberg.itch.io> |
| **Poly Pizza** | CC0/CC-BY 模型聚合搜索 | 按条目 | <https://poly.pizza> |
| **Mixamo** | 人形角色 + 大量动作库 | 免费商用，但**不能把模型文件本身再分发** | <https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html> |

**推荐组合**：Quaternius 的 `Ultimate Modular Characters`（角色）+ `Ultimate Monsters`（怪物），都是 CC0，FBX 直接拖进 Unity，比例和这张低多边形地图也搭。

注意 Mixamo：可以用于游戏，但不能把下载的模型作为素材再分发，别直接放进公开仓库的 `Assets` 里。

### 替换步骤

1. 把 FBX 拖进 `Assets/Demo/Models/`
2. 选中 `Player`，禁用或删除 `HumanoidRig` 组件
3. 模型挂到 `Player` 下当子物体，缩放对齐（角色高约 1.8）
4. 人形模型加 `Animator` + Avatar，拖到 `PlayerStateMachine.animator` 字段，会自动写 `State` / `Speed` / `Grounded`
5. 敌人同理：替换 `BuildEnemies()` 里生成的图元子物体，保留 `Enemy` / `Health` / `CharacterController` 与 `Hitbox` 子物体

## 调参速查

| 想改什么 | 改哪儿 |
| --- | --- |
| 连段伤害 / 判定范围 | `PlayerCombat.comboDamage` / `reach` / `radius` |
| 敌人强度 | 每个敌人的 `Health.maxHealth`，以及 `Enemy.damage` / `moveSpeed` |
| 掉帧感 | `Fx.HitStop(0.05f)` 的参数 |
| 音效音量 | `Sfx.Play(clip, volume)` |
| 花瓣数量 | `GameBootstrap.BuildPetals()` 的坐标数组（自动同步总数） |
| 地图布局 | `GameBootstrap` 各 `BuildXxx()` |
| 移动手感 | `PlayerMotor` 上的公开字段 |

## 已知限制

- **美术是程序化的**：角色和场景都是基础几何体，风格统一但不精细。真实模型需要外部资源（见上一节）。
- **字体是开源字体**：`Resources/Fonts/NotoSansSC-Regular.ttf` 为思源黑体（SIL OFL 1.1），可自由分发，同目录附 `OFL-LICENSE.txt`。早期版本用的 Windows 自带黑体已移除。
- **没有存档和关卡切换**：单场景闭环。
- **敌人寻路是直线追击**：没有用 NavMesh，绕不开复杂障碍，地图特意留出了开阔空间。
- **WebGL 需要本地服务器**：`file://` 打不开是浏览器安全限制，不是工程问题。

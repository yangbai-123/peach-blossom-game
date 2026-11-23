using System.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using VRM;

/// <summary>
/// 怪物 VRM 模型：加载一次，所有敌人共享同一份网格与材质。
///
/// 和主角一样走运行时加载（UniVRM 的编辑器导入器在本工程没注册成功），
/// 加载失败时保留原来的程序化外观。
/// </summary>
public static class MonsterModel
{
    public static GameObject Template { get; private set; }
    public static bool Failed { get; private set; }

    public static bool Ready => Template != null;

    static bool _loading;
    static System.Action _onReady;

    public static void Load(System.Action onReady = null)
    {
        if (onReady != null) _onReady += onReady;

        if (Ready)
        {
            _onReady?.Invoke();
            _onReady = null;
            return;
        }

        if (_loading || Failed) return;
        _loading = true;
        LoadInternal();
    }

    static async void LoadInternal()
    {
        var bytes = Resources.Load<TextAsset>("Models/monster");

        if (bytes == null)
        {
            Debug.LogWarning("[monster] 找不到 Models/monster.bytes，保留程序化外观");
            Failed = true;
            _loading = false;
            return;
        }

        Debug.Log($"[monster] 开始加载 {bytes.bytes.Length} 字节");

        try
        {
            // ImmediateCaller：WebGL 下用 RuntimeOnlyAwaitCaller 会卡住不恢复
            var instance = await VrmUtility.LoadBytesAsync(
                "monster.vrm", bytes.bytes, new ImmediateCaller());

            instance.ShowMeshes();

            Template = instance.Root;
            Template.name = "MonsterTemplate";
            Template.SetActive(false);
            Object.DontDestroyOnLoad(Template);

            Debug.Log("[monster] 模型加载完成");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[monster] 加载失败: " + e.Message);
            Failed = true;
        }
        finally
        {
            _loading = false;
        }

        _onReady?.Invoke();
        _onReady = null;
    }

    /// <summary>按包围盒把模型缩放到指定身高，并让脚底贴地。</summary>
    public static void NormalizeScale(GameObject root, float targetHeight)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        float height = bounds.size.y;
        if (height < 0.05f) return;

        float scale = targetHeight / height;
        root.transform.localScale = Vector3.one * scale;
        root.transform.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);
    }
}

/// <summary>挂在每个敌人身上：模型就绪后替换掉程序化身体。</summary>
public class VrmMonsterVisual : MonoBehaviour
{
    public Transform prims;
    public float targetHeight = 1.55f;

    Enemy _enemy;
    bool _applied;

    void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    void Start()
    {
        MonsterModel.Load(TryApply);
    }

    void TryApply()
    {
        if (_applied || MonsterModel.Template == null) return;
        _applied = true;

        var model = Instantiate(MonsterModel.Template, transform);
        model.name = "MonsterModel";
        model.SetActive(true);
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        model.transform.localPosition = Vector3.zero;

        MonsterModel.NormalizeScale(model, targetHeight);

        if (prims != null) prims.gameObject.SetActive(false);

        var rig = model.AddComponent<MonsterRig>();
        rig.enemy = _enemy;

        if (_enemy != null) _enemy.RefreshVisuals();
    }
}

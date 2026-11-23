using System.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using VRM;

/// <summary>
/// 运行时加载 VRM 模型并挂到玩家身上。
///
/// UniVRM 的 .vrm 编辑器导入器在本工程里没注册成功，所以改成把模型当二进制资源
/// （TextAsset），用 VrmUtility.LoadBytesAsync 在运行时解析——WebGL 也能用。
/// 加载失败会自动退回程序化人形。
/// </summary>
public class VrmCharacterLoader : MonoBehaviour
{
    public PlayerMotor motor;
    public PlayerCombat combat;

    public RuntimeAnimatorController animatorController;

    GameObject _model;
    bool _fallbackUsed;

    void Start()
    {
        ApplyCharacter(CharacterCatalog.Selected.Resource);
    }

    /// <summary>切换角色：销毁旧模型并重新加载。</summary>
    public void Select(string resourceName)
    {
        if (_model != null)
        {
            Destroy(_model);
            _model = null;
        }

        ApplyCharacter(resourceName);
    }

    void ApplyCharacter(string resourceName)
    {
        Debug.Log("[vrm] 加载角色: " + resourceName);
        LoadAsync(resourceName);
    }

    async void LoadAsync(string resourceName)
    {
        var bytes = Resources.Load<TextAsset>("Models/" + resourceName);
        Debug.Log($"[vrm] 资源读取 {resourceName}: {(bytes != null ? bytes.bytes.Length + " 字节" : "NULL")}");

        if (bytes == null)
        {
            Debug.LogWarning($"[vrm] 找不到 Models/{resourceName}.bytes，使用程序化角色");
            Fallback();
            return;
        }

        try
        {
            // ImmediateCaller 是同步执行：WebGL 下用 RuntimeOnlyAwaitCaller 会卡住不恢复
            var instance = await VrmUtility.LoadBytesAsync(
                resourceName + ".vrm", bytes.bytes, new ImmediateCaller());

            instance.ShowMeshes();

            var root = instance.Root;
            root.name = "CharacterModel";
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            // 模型自身朝向已与玩家前进方向一致，不要再额外旋转
            root.transform.localRotation = Quaternion.identity;

            // 按实际包围盒把模型缩放到 1.75 米左右，并让脚底贴地
            NormalizeScale(root, 1.75f);

            var rig = root.AddComponent<VrmCharacterRig>();
            rig.motor = motor;
            rig.combat = combat;

            AttachSword(root);

            _model = root;
            Debug.Log("[vrm] 模型加载完成: " + root.name);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[vrm] 加载失败: " + e.Message);
            Fallback();
        }
    }

    void Fallback()
    {
        if (_fallbackUsed) return;
        _fallbackUsed = true;

        var rig = gameObject.AddComponent<HumanoidRig>();
        rig.motor = motor;
        rig.combat = combat;
    }

    /// <summary>把模型缩放到指定身高，并让脚底落在玩家原点。</summary>
    static void NormalizeScale(GameObject root, float targetHeight)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        float height = bounds.size.y;
        if (height < 0.05f) return;

        float scale = targetHeight / height;
        root.transform.localScale = Vector3.one * scale;

        // 缩放后重新计算脚底位置，把模型整体上移/下移贴地
        float footDelta = -bounds.min.y * scale;
        root.transform.localPosition = new Vector3(0f, footDelta, 0f);

        Debug.Log($"[vrm] 原始高度={height:0.00}m 缩放={scale:0.00} 脚底偏移={footDelta:0.00}");
    }

    /// <summary>给模型挂一把程序化长剑（VRM 本身没有武器）。</summary>
    void AttachSword(GameObject model)
    {
        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return;

        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) return;

        var sword = new GameObject("Sword(Procedural)");
        sword.transform.SetParent(hand, false);
        sword.transform.localPosition = Vector3.zero;
        sword.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // 抵消模型的整体缩放，保证剑在世界里是正常长度
        float lossy = hand.lossyScale.x;
        float inv = lossy > 0.001f ? 1f / lossy : 1f;
        sword.transform.localScale = Vector3.one * inv;

        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "Blade";
        blade.transform.SetParent(sword.transform, false);
        blade.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        blade.transform.localScale = new Vector3(0.028f, 1.1f, 0.028f);

        var col = blade.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }

        var renderer = blade.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = MaterialLibrary.For(new Color(0.9f, 0.94f, 1f));
            if (mat != null) renderer.sharedMaterial = mat;
        }
    }
}

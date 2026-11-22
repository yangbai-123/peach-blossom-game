#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class VrmCheck
{
    const string Path = "Assets/Demo/Resources/Models/character.vrm";

    public static void Run()
    {
        // 如果之前被 DefaultImporter 处理过，删掉 meta 让 UniVRM 的 ScriptedImporter 重新接管
        var metaPath = Path + ".meta";
        if (System.IO.File.Exists(metaPath))
        {
            System.IO.File.Delete(metaPath);
            Debug.Log("[check] 已删除旧 meta，准备重新导入");
        }

        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(Path);
        Debug.Log($"[check] importer = {(importer != null ? importer.GetType().FullName : "NULL")}");

        var main = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        Debug.Log($"[check] main object = {(main != null ? main.name + " (" + main.GetType().Name + ")" : "NULL")}");

        var all = AssetDatabase.LoadAllAssetsAtPath(Path);
        Debug.Log($"[check] 资产数量 = {all.Length}");
        foreach (var a in all)
        {
            if (a == null) continue;
            string extra = "";
            if (a is GameObject go) extra = $" children={go.transform.childCount}";
            Debug.Log($"[check] - {a.GetType().FullName} : {a.name}{extra}");
        }

        var res = Resources.Load<GameObject>("Models/character");
        Debug.Log($"[check] Resources.Load = {(res != null ? res.name : "NULL")}");
    }
}
#endif

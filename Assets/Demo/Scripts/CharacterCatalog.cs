using UnityEngine;

/// <summary>
/// 可选角色清单。模型都放在 Resources/Models/ 下，以 .bytes 形式随包发布。
/// 选择结果用 PlayerPrefs 记住。
/// </summary>
public static class CharacterCatalog
{
    public class Entry
    {
        public string Resource;
        public string Name;
        public string Subtitle;
        public Color Accent;
    }

    public static readonly Entry[] Entries =
    {
        new Entry
        {
            Resource = "character",
            Name = "剑仙",
            Subtitle = "白衣长发 · 携一柄长剑",
            Accent = new Color(0.82f, 0.90f, 1f),
        },
        new Entry
        {
            Resource = "monster",
            Name = "妖",
            Subtitle = "异色校服 · 赤瞳",
            Accent = new Color(1f, 0.72f, 0.82f),
        },
    };

    const string Key = "selected_character";

    public static int SelectedIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Entries.Length - 1);
        set
        {
            PlayerPrefs.SetInt(Key, Mathf.Clamp(value, 0, Entries.Length - 1));
            PlayerPrefs.Save();
        }
    }

    public static Entry Selected => Entries[SelectedIndex];
}

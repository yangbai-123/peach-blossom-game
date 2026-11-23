using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本机用户档案 + 排行榜。
///
/// 纯静态站点没有后端，所以"用户"存在本机 PlayerPrefs（浏览器 localStorage）里：
///   - 注册/登录只按用户名区分，不做密码（本地单机没有必要，也不好输入）
///   - 每个用户名记录最快通关时间，排行榜按时间升序
///   - 换设备不会同步；真正的全服排行榜需要后端（见 README）
/// </summary>
public static class UserProfile
{
    const string CurrentKey = "profile_current";
    const string NamesKey = "profile_names";      // 用 ; 分隔
    const string BestPrefix = "profile_best_";

    /// <summary>当前登录的用户名，空字符串表示游客。</summary>
    public static string Current => PlayerPrefs.GetString(CurrentKey, "");

    public static bool HasUser => !string.IsNullOrEmpty(Current);

    /// <summary>本机注册过的用户名（最近使用的排前面）。</summary>
    public static List<string> SavedNames()
    {
        var list = new List<string>();
        foreach (var n in PlayerPrefs.GetString(NamesKey, "").Split(';'))
            if (!string.IsNullOrEmpty(n)) list.Add(n);
        return list;
    }

    static void SaveNames(List<string> names) =>
        PlayerPrefs.SetString(NamesKey, string.Join(";", names));

    /// <summary>创建或切换用户。</summary>
    public static bool Enter(string name, out string error)
    {
        error = null;
        name = Sanitize(name);

        if (name.Length < 1) { error = "请输入名字"; return false; }
        if (name.Length > 12) name = name.Substring(0, 12);

        var names = SavedNames();
        names.Remove(name);
        names.Insert(0, name);
        if (names.Count > 8) names.RemoveRange(8, names.Count - 8);
        SaveNames(names);

        PlayerPrefs.SetString(CurrentKey, name);
        PlayerPrefs.Save();
        return true;
    }

    public static void Logout()
    {
        PlayerPrefs.DeleteKey(CurrentKey);
        PlayerPrefs.Save();
    }

    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        var sb = new System.Text.StringBuilder();
        foreach (var c in s.Trim())
        {
            if (char.IsControl(c) || c == ';' || c == '|') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>提交一次通关成绩，只保留最快的一次。</summary>
    public static void SubmitTime(float seconds)
    {
        if (!HasUser || seconds <= 0f) return;

        string key = BestPrefix + Current;
        float old = PlayerPrefs.GetFloat(key, -1f);

        if (old < 0f || seconds < old)
        {
            PlayerPrefs.SetFloat(key, seconds);
            PlayerPrefs.Save();
        }
    }

    public static float BestTime(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1f;
        return PlayerPrefs.GetFloat(BestPrefix + name, -1f);
    }

    /// <summary>按最快时间升序返回排行榜。</summary>
    public static List<(string name, float time)> Leaderboard(int max = 8)
    {
        var list = new List<(string, float)>();

        foreach (var name in SavedNames())
        {
            float t = BestTime(name);
            if (t > 0f) list.Add((name, t));
        }

        list.Sort((a, b) => a.Item2.CompareTo(b.Item2));

        if (list.Count > max) list.RemoveRange(max, list.Count - max);
        return list;
    }

    public static string FormatTime(float seconds)
    {
        if (seconds <= 0f) return "--:--";
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }
}

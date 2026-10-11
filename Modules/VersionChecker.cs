using System.Text.RegularExpressions;
using UnityEngine;

namespace TONE.Modules;

// https://github.com/tukasa0001/TownOfHost/blob/main/Modules/VersionChecker.cs
public static class VersionChecker
{
    public static bool IsSupported { get; private set; } = true;
    private static bool Ischecked = false;

    /// <summary>
    /// 判断当前 Among Us 版本是否受支持。
    ///
    /// ⚠️ 原来的实现只比较 Constants.GetVersion(Constants.Year, Month, Day, Revision)，
    ///    但实测发现 Constants 的这几个分量与游戏实际的语义版本（Application.version）
    ///    **并不一致**：
    ///
    ///        同一台机器   Application.version = "2026.9.29"
    ///                     Constants           = (2026, 7, 20, 0)   ← 对不上
    ///
    ///    而不同 build 的机器 Constants 分量又各不相同（例如 build 7492 的
    ///    AU 版本值是 50663650，而 build 7489 是 50663600）。
    ///
    ///    结果是：只要玩家的游戏 build 与声明的那一个不完全相同，就会被判定为
    ///    「不支持的版本」—— 表现为寻找游戏按钮消失、无法联机。
    ///    （见 ERR-002-000-1：该模组不支持《Among us》的版本）
    ///
    ///    因此改为「语义版本优先」：用 Application.version 与 <see cref="Main.SupportedVersionAU"/>
    ///    逐项比对；同时保留 Constants 比对作为兜底（两种方式任一命中即通过）。
    /// </summary>
    public static void Check()
    {
        if (Ischecked) return;

        var appVersion = Application.version;                                  // 例如 "2026.9.29"
        var amongUsVersion = Constants.GetVersion(Constants.Year, Constants.Month, Constants.Day, Constants.Revision);

        Logger.Info($"AU: {amongUsVersion}", "Among Us Version Check");
        Logger.Info(
            $"AU raw: Application.version=\"{appVersion}\"; "
            + $"Constants=({Constants.Year},{Constants.Month},{Constants.Day},{Constants.Revision})",
            "Among Us Version Check");

        IsSupported = false;

        // ① 语义版本比对（主）
        foreach (var v in Main.SupportedVersionAU)
        {
            if (VersionsEqual(appVersion, v.year, v.month, v.day))
            {
                IsSupported = true;
                Logger.Info($"AU 版本匹配（Application.version）：{appVersion} ≈ {v.year}.{v.month}.{v.day}", "Among Us Version Check");
                break;
            }
        }

        // ② Constants 比对（兜底，兼容原来能跑通的情况）
        //    注意：SupportedVersionAU 现在只有年月日，revision 传 0 比较。
        if (!IsSupported)
        {
            foreach (var v in Main.SupportedVersionAU)
            {
                if (Constants.GetVersion(v.year, v.month, v.day, 0) == amongUsVersion)
                {
                    IsSupported = true;
                    Logger.Info($"AU 版本匹配（Constants）：{amongUsVersion}", "Among Us Version Check");
                    break;
                }
            }
        }

        Logger.Info($"AU version supported: {IsSupported}", "Among Us Version Check");

        if (!IsSupported)
        {
            ErrorText.Instance.AddError(ErrorCode.UnsupportedVersion);
        }

        Ischecked = true;
    }

    /// <summary>
    /// 比较 Application.version（如 "2026.9.29" / "v2026.9.29" / "2026.9.29.1"）
    /// 与目标版本的年月日是否一致。只比较前三个数字，忽略前缀与多余后缀。
    /// </summary>
    private static bool VersionsEqual(string appVersion, int year, int month, int day)
    {
        if (string.IsNullOrWhiteSpace(appVersion)) return false;

        var nums = Regex.Matches(appVersion, @"\d+");
        if (nums.Count < 3) return false;

        return int.TryParse(nums[0].Value, out var y) && y == year
            && int.TryParse(nums[1].Value, out var m) && m == month
            && int.TryParse(nums[2].Value, out var d) && d == day;
    }
}

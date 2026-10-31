using System.Text.RegularExpressions;
using UnityEngine;

namespace TONE.Modules;

public static class VersionChecker
{
    public static bool IsSupported { get; private set; } = true;
    private static bool Ischecked = false;

    public static void Check()
    {
        if (Ischecked) return;

        var appVersion = Application.version;
        var amongUsVersion = Constants.GetVersion(Constants.Year, Constants.Month, Constants.Day, Constants.Revision);

        Logger.Info($"AU: {amongUsVersion}", "Among Us Version Check");
        Logger.Info(
            $"AU raw: Application.version=\"{appVersion}\"; "
            + $"Constants=({Constants.Year},{Constants.Month},{Constants.Day},{Constants.Revision})",
            "Among Us Version Check");

        IsSupported = false;

        foreach (var v in Main.SupportedVersionAU)
        {
            if (VersionsEqual(appVersion, v.year, v.month, v.day))
            {
                IsSupported = true;
                Logger.Info($"AU 版本匹配（Application.version）：{appVersion} ≈ {v.year}.{v.month}.{v.day}", "Among Us Version Check");
                break;
            }
        }

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

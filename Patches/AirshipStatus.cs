namespace TONE;

[HarmonyPatch(typeof(AirshipStatus), nameof(AirshipStatus.PrespawnStep))]
public static class AirshipStatusPrespawnStepPatch
{
    public static bool Prefix()
    {
        Logger.Info("AirshipStatus.PrespawnStep called", "AirshipStatus");

        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
        {
            RandomSpawn.AirshipSpawn(PlayerControl.LocalPlayer);

            return false;
        }
        return true;
    }
}

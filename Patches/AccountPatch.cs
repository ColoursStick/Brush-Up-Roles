using AmongUs.Data.Player;

namespace TONE;

[HarmonyPatch(typeof(PlayerBanData), nameof(PlayerBanData.BanPoints), MethodType.Setter)]
public static class RemoveDisconnectPenalty_PlayerBanData_BanPoints_Prefix
{

    public static bool Prefix(PlayerBanData __instance, ref float value)
    {
        if (!(bool)(Object)AmongUsClient.Instance || AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            return true;

        value = 0f;

        return false;
    }
}
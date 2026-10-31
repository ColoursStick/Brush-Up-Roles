using AmongUs.GameOptions;
using UnityEngine;

namespace TONE;

[HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
class CanUsePatch
{
    public static bool Prefix(Console __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
    {
        canUse = couldUse = false;

        return __instance.AllowImpostor || Utils.HasTasks(PlayerControl.LocalPlayer.Data, false);
    }
}
[HarmonyPatch(typeof(EmergencyMinigame), nameof(EmergencyMinigame.Update))]
class EmergencyMinigamePatch
{
    public static void Postfix(EmergencyMinigame __instance)
    {
        if (Options.DisableMeeting.GetBool() || !GameModeBase.GetGameMode().GetGameModeClass().CanReport)
            __instance.Close();
        return;
    }
}
[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
class CanUseVentPatch
{
    public static bool Prefix(Vent __instance, [HarmonyArgument(0)] NetworkedPlayerInfo pc,
        [HarmonyArgument(1)] ref bool canUse,
        [HarmonyArgument(2)] ref bool couldUse,
        ref float __result)
    {
        if (GameStates.IsHideNSeek) return true;

        PlayerControl playerControl = pc.Object;

        couldUse = playerControl.CanUseImpostorVentButton() || (pc.Role.Role == RoleTypes.Engineer && pc.Role.CanUse(__instance.CastFast<IUsable>()));

        canUse = couldUse;

        if (!canUse)
        {
            return false;
        }

        IUsable usableVent = __instance.CastFast<IUsable>();

        float actualDistance = float.MaxValue;

        couldUse =

            GameManager.Instance.LogicUsables.CanUse(usableVent, playerControl) &&

            (!playerControl.MustCleanVent(__instance.Id) || (playerControl.inVent && Vent.currentVent == __instance)) &&
            playerControl.IsAlive() &&
            (playerControl.CanMove || playerControl.inVent);

        if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out var systemType))
        {
            VentilationSystem ventilationSystem = systemType.CastFast<VentilationSystem>();

            if (ventilationSystem != null && ventilationSystem.IsVentCurrentlyBeingCleaned(__instance.Id))
            {
                couldUse = false;
            }
        }

        canUse = couldUse;
        if (canUse)
        {
            Vector3 center = playerControl.Collider.bounds.center;
            Vector3 ventPosition = __instance.transform.position;
            actualDistance = Utils.GetDistance(center, ventPosition);
            canUse &= actualDistance <= __instance.UsableDistance && !PhysicsHelpers.AnythingBetween(playerControl.Collider, center, ventPosition, Constants.ShipOnlyMask, false);
        }
        __result = actualDistance;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPurchasesData), nameof(PlayerPurchasesData.GetPurchase))]
public static class PlayerPurchasesDataPatch
{
    public static bool Prefix(ref bool __result)
    {
#if PC

        if (RunLoginPatch.ClickCount < 20) return true;
        __result = true;
        return false;
#else

        return true;
#endif
    }
}

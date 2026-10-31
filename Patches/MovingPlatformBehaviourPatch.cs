namespace TONE;

[HarmonyPatch(typeof(MovingPlatformBehaviour))]
public static class MovingPlatformBehaviourPatch
{
    private static bool isDisabled = false;

    [HarmonyPatch(nameof(MovingPlatformBehaviour.Start)), HarmonyPrefix]
    public static void Start_Prefix(MovingPlatformBehaviour __instance)
    {
        isDisabled = Options.DisableAirshipMovingPlatform.GetBool();

        if (isDisabled)
        {
            __instance.transform.localPosition = __instance.DisabledPosition;
            ShipStatus.Instance.CastFast<AirshipStatus>().outOfOrderPlat.SetActive(true);
        }
    }
    [HarmonyPatch(nameof(MovingPlatformBehaviour.IsDirty), MethodType.Getter), HarmonyPrefix]
    public static bool GetIsDirty_Prefix(ref bool __result)
    {
        if (isDisabled)
        {
            __result = false;
            return false;
        }
        return true;
    }
    [HarmonyPatch(nameof(MovingPlatformBehaviour.Use), typeof(PlayerControl)), HarmonyPrefix]
    public static bool UsePrefix([HarmonyArgument(0)] PlayerControl player)
    {

        if (Main.PlayerStates.TryGetValue(player.PlayerId, out var state) && !state.CanUseMovingPlatform)
        {
            return false;
        }
        return !isDisabled;
    }
    [HarmonyPatch(nameof(MovingPlatformBehaviour.SetSide)), HarmonyPrefix]
    public static bool SetSide_Prefix() => !isDisabled;
}

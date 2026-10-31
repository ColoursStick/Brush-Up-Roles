namespace TONE;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
class OnDisconnectedPatch
{
    public static void Postfix()
    {
        Main.VisibleTasksCount = false;
    }
}
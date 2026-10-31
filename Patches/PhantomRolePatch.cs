using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using TONE.Modules;
using TONE.Roles.Core;
using TONE.Roles.Crewmate;
using TONE.Roles.Impostor;
using TONE.Roles.Neutral;

namespace TONE.Patches;

[HarmonyPatch(typeof(PlayerControl))]
public static class PhantomRolePatch
{

    public static readonly Dictionary<byte, string> PetsList = [];

    [HarmonyPatch(nameof(PlayerControl.CmdCheckVanish)), HarmonyPrefix]
    private static bool CmdCheckVanish_Prefix(PlayerControl __instance, float maxDuration)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            __instance.CheckVanish();
            return false;
        }
        __instance.SetRoleInvisibility(true, true, false);
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.CheckVanish, SendOption.Reliable, AmongUsClient.Instance.HostId);
        messageWriter.Write(maxDuration);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        return false;
    }

    [HarmonyPatch(nameof(PlayerControl.CmdCheckAppear)), HarmonyPrefix]
    private static bool CmdCheckAppear_Prefix(PlayerControl __instance, bool shouldAnimate)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            __instance.CheckAppear(shouldAnimate);
            return false;
        }
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.CheckAppear, SendOption.Reliable, AmongUsClient.Instance.HostId);
        messageWriter.Write(shouldAnimate);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        return false;
    }

    [HarmonyPatch(nameof(PlayerControl.CheckVanish)), HarmonyPrefix]
    private static bool CheckVanish_Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        Logger.Info($" {__instance.GetNameWithRole()}", "CheckVanish");
        return __instance.AmOwner && CheckTrigger(__instance);
    }

    public static bool CheckTrigger(PlayerControl phantom)
    {
        var role = phantom.GetRoleClass();
        if (TimeMaster.Rewinding || role?.OnCheckVanish(phantom) == false || TimeAssassin.TimeStop || Pelican.IsEaten(phantom.PlayerId))
        {
            var killCooldown = phantom.GetKillTimer();

            if (phantom.AmOwner)
            {
                try
                {
                    HudManager.Instance.AbilityButton.SetFromSettings(phantom.Data.Role.Ability);
                }
                catch { }
                phantom.Data.Role.SetCooldown();
                return false;
            }

            var sender = CustomRpcSender.Create($"Cancel vanish for {phantom.GetRealName()}", SendOption.Reliable);
            sender.StartMessage(phantom.OwnerId);

            sender.StartRpc(phantom.NetId, RpcCalls.SetRole)
                .Write((ushort)RoleTypes.Phantom)
                .Write(true)
                .EndRpc();

            sender.StartRpc(phantom.NetId, RpcCalls.ProtectPlayer)
                .WriteNetObject(phantom)
                .Write(0)
                .EndRpc();

            sender.EndMessage();
            sender.SendMessage();

            _ = new LateTask(() =>
            {
                phantom.SetKillCooldown(Math.Max(killCooldown, 0.001f));
                phantom.ResetKillCooldown();
                phantom.SyncSettings();
            }, 0.2f, $"Phantom Check");

            return false;
        }

        return true;
    }

}

[HarmonyPatch(typeof(PhantomRole), nameof(PhantomRole.UseAbility))]
public static class PhantomRoleUseAbilityPatch
{
    public static bool Prefix(PhantomRole __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (__instance.Player.AmOwner && !__instance.Player.Data.IsDead && __instance.Player.moveable && !Minigame.Instance && !__instance.IsCoolingDown && !__instance.fading)
        {
            System.Func<RoleEffectAnimation, bool> roleEffectAnimation = x => x.effectType == RoleEffectAnimation.EffectType.Vanish_Charge;
            if (!__instance.Player.currentRoleAnimations.Find(roleEffectAnimation) && !__instance.Player.walkingToVent && !__instance.Player.inMovingPlat)
            {
                if (__instance.isInvisible)
                {
                    __instance.MakePlayerVisible(true, true);
                    return false;
                }
                DestroyableSingleton<HudManager>.Instance.AbilityButton.SetSecondImage(__instance.Ability);
                DestroyableSingleton<HudManager>.Instance.AbilityButton.OverrideText(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.PhantomAbilityUndo, new Il2CppReferenceArray<Il2CppSystem.Object>(0)));
                __instance.Player.CmdCheckVanish(GameManager.Instance.LogicOptions.GetRoleFloat(FloatOptionNames.PhantomDuration));
                return false;
            }
        }
        return false;
    }
}

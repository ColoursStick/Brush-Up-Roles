using AmongUs.GameOptions;
using System;
using System.Text;
using TONE.Modules;
using TONE.Roles.AddOns;
using TONE.Roles.AddOns.Common;
using TONE.Roles.AddOns.Crewmate;
using TONE.Roles.AddOns.Impostor;
using TONE.Roles.Coven;
using TONE.Roles.Crewmate;
using TONE.Roles.Impostor;
using TONE.Roles.Neutral;
using TONE.Roles.Vanilla;

namespace TONE.Roles.Core;

public static class CustomRoleManager
{
    public static readonly Dictionary<CustomRoles, RoleBase> RoleClass = [];
    public static readonly Dictionary<CustomRoles, IAddon> AddonClasses = [];
    public static RoleBase GetStaticRoleClass(this CustomRoles role)
    {
        if (role is CustomRoles.RWoodCollector or CustomRoles.BWoodCollector) return new FireThief();

        var roleClass = RoleClass.FirstOrDefault(x => x.Key == role).Value;

        if (!role.IsVanilla() && !role.IsAdditionRole()
            && role is not CustomRoles.Apocalypse and not CustomRoles.NotAssigned and not CustomRoles.SpeedBooster and not CustomRoles.Killer and not
            CustomRoles.GM and not CustomRoles.RDeputy)
        {
            if (RoleClass.Count(x => x.Value.Role == role) > 1)
                Logger.Error($"RoleClass for {role} is not unique.", "GetStaticRoleClass");
            if (roleClass == null)
                Logger.Error($"RoleClass for {role} is null.", "GetStaticRoleClass");
        }
        return roleClass ?? new DefaultSetup();
    }
    public static List<RoleBase> AllEnabledRoles => Main.PlayerStates.Values.Select(x => x.RoleClass).ToList();
    public static bool HasEnabled(this CustomRoles role) => role.GetStaticRoleClass().IsEnable;

    public static bool OtherCollectionsSet = false;
    public static List<RoleBase> GetNormalOptions(Custom_RoleType type)
    {
        List<RoleBase> roles = [];
        foreach (var role in RoleClass.Values)
        {

            if (role.IsExperimental) continue;

            if (role.ThisRoleType == type)
            {
                roles.Add(role);
            }
        }
        return roles;
    }
    public static List<RoleBase> GetExperimentalOptions(Custom_Team team)
    {
        List<RoleBase> roles = [];
        switch (team)
        {
            case Custom_Team.Crewmate:
                roles = RoleClass.Where(r => r.Value.IsExperimental && r.Key.IsCrewmate()).Select(r => r.Value).ToList();
                break;

            case Custom_Team.Impostor:
                roles = RoleClass.Where(r => r.Value.IsExperimental && r.Key.IsImpostorTeam()).Select(r => r.Value).ToList();
                break;

            case Custom_Team.Neutral:
                roles = RoleClass.Where(r => r.Value.IsExperimental && r.Key.IsNeutralTeamV2()).Select(r => r.Value).ToList();
                break;

            case Custom_Team.Coven:
                roles = RoleClass.Where(r => r.Value.IsExperimental && r.Key.IsCoven()).Select(r => r.Value).ToList();
                break;

            default:
                Logger.Info("Unsupported team was sent.", "GetExperimentalOptions");
                break;
        }
        return roles;
    }

    public static void HideBlackListedOptions(IEnumerable<RoleBase> roles)
    {
        foreach (var r in roles)
        {
            var role = r.Role;

            var hide = IsOptBlackListed(r.GetType()) || role.IsCoven();
            if (!hide) continue;

            if (Options.CustomRoleSpawnChances != null
                && Options.CustomRoleSpawnChances.TryGetValue(role, out var spawn))
                spawn.SetHidden(true);

            if (Options.CustomRoleCounts != null
                && Options.CustomRoleCounts.TryGetValue(role, out var cnt))
                cnt.SetHidden(true);
        }
    }

    public static bool IsOptBlackListed(this Type role)
    {

        var owners = new List<CustomRoles>();
        foreach (var kv in RoleClass)
            if (kv.Value != null && kv.Value.GetType() == role) owners.Add(kv.Key);

        if (owners.Count == 0) return CustomRolesHelper.DuplicatedRoles.ContainsValue(role);

        foreach (var r in owners)
            if (CustomRolesHelper.KeepRoles.Contains(r)) return false;

        return true;
    }
    public static RoleBase GetRoleClass(this PlayerControl player) => GetRoleClassById(player.PlayerId);
    public static RoleBase GetRoleClassById(this byte playerId) => Main.PlayerStates.TryGetValue(playerId, out var statePlayer) && statePlayer != null ? statePlayer.RoleClass : new DefaultSetup();

    public static RoleBase CreateRoleClass(this CustomRoles role)
    {
        return (RoleBase)Activator.CreateInstance(role.GetStaticRoleClass().GetType());
    }

    public static bool HasDesyncRole(this PlayerControl player)
        => player != null &&
        (player.GetRoleClass().IsDesyncRole
        || Main.DesyncPlayerList.Contains(player.Data.PlayerId)
        || player.Is(CustomRoles.Killer)
        || (player.GetCustomRole().IsImpostor() && NarcManager.IsNarcAssigned()));

    public static bool OnCheckMurderAsTargetOnOthers(PlayerControl killer, PlayerControl target)
    {
        foreach (var roleClass in AllEnabledRoles.ToArray())
        {
            if (roleClass.CheckMurderOnOthersTarget(killer, target) == true)
            {
                Logger.Info($"Role class cancels kill: {roleClass}", "OnCheckMurderAsTargetOnOthers");
                return false;
            }
        }
        return true;
    }

    public static void BuildCustomGameOptions(this PlayerControl player, ref IGameOptions opt)
    {
        if (player.IsAnySubRole(x => x is CustomRoles.EvilSpirit))
        {
            AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
        }

        player.GetRoleClass()?.ApplyGameOptions(opt, player.PlayerId);

        if (NoisemakerTONE.HasEnabled) NoisemakerTONE.ApplyGameOptionsForOthers(player);

        if (DollMaster.HasEnabled && DollMaster.IsDoll(player.PlayerId))
        {
            DollMaster.ApplySettingsToDoll(opt);
            return;
        }

        if (Grenadier.HasEnabled) Grenadier.ApplyGameOptionsForOthers(opt, player);
        if (CustomRoles.Dazzler.RoleExist()) Dazzler.SetDazzled(player, opt);
        if (CustomRoles.Deathpact.RoleExist()) Deathpact.SetDeathpactVision(player, opt);
        if (CustomRoles.Spiritcaller.RoleExist()) Spiritcaller.ReduceVision(opt, player);
        if (CustomRoles.Pitfall.RoleExist()) Pitfall.SetPitfallTrapVision(opt, player);
        if (CustomRoles.Medusa.RoleExist()) Medusa.SetStoned(player, opt);
        if (CustomRoles.Sacrifist.RoleExist()) Sacrifist.SetVision(player, opt);

        var playerSubRoles = player.GetCustomSubRoles();

        if (playerSubRoles.Any())
            foreach (var subRole in playerSubRoles.ToArray())
            {
                switch (subRole)
                {
                    case CustomRoles.Watcher:
                        Watcher.RevealVotes(opt);
                        break;
                    case CustomRoles.Torch:
                        Torch.ApplyGameOptions(opt);
                        break;
                    case CustomRoles.Tired:
                        Tired.ApplyGameOptions(opt, player);
                        break;
                    case CustomRoles.Bewilder:
                        Bewilder.ApplyVisionOptions(opt);
                        break;
                    case CustomRoles.Reach:
                        Reach.ApplyGameOptions(opt);
                        break;
                    case CustomRoles.Madmate:
                        Madmate.ApplyGameOptions(opt);
                        break;
                    case CustomRoles.Mare:
                        Mare.ApplyGameOptions(player.PlayerId);
                        break;
                    case CustomRoles.Narc when !player.Is(CustomRoles.Torch) && !player.Is(CustomRoles.Bewilder):
                        NarcManager.ApplyGameOptions(opt, player);
                        break;
                }
            }

        if (Glow.IsEnable) Glow.ApplyGameOptions(opt, player);
        if (Bewilder.IsEnable) Bewilder.ApplyGameOptions(opt, player);
        if (Ghoul.IsEnable) Ghoul.ApplyGameOptions(player);

        var playerstates = Main.PlayerStates[player.PlayerId];
        if (playerstates.IsBlackOut)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0);
            opt.SetFloat(FloatOptionNames.CrewLightMod, 0);
        }

        if (AbilityTimeManager.ResetStartAbilityCooldown)
        {
            AURoleOptions.ScientistCooldown = 10f;
            AURoleOptions.EngineerCooldown = 10f;
            AURoleOptions.ShapeshifterCooldown = 10f;
            AURoleOptions.TrackerCooldown = 10f;
            AURoleOptions.PhantomCooldown = 10f;
        }

        if (player.UsesMeetingShapeshift() && Options.UseMeetingAbilityMethod.GetValue() == 2)
        {
            AURoleOptions.JudgeTaskRequirementPercentage = 0;
        }
    }

    public static bool OnCheckMurder(ref PlayerControl killer, ref PlayerControl target, ref bool __state)
    {
        if (killer == target) return true;

        if (target != null && target.Is(CustomRoles.Fragile))
        {
            if (Fragile.KillFragile(killer, target))
            {
                Logger.Info("Fragile killed in OnCheckMurder, returning false", "Fragile");
                return false;
            }
        }
        var canceled = false;
        var cancelbutkill = false;

        var killerRoleClass = killer.GetRoleClass();
        var killerSubRoles = killer.GetCustomSubRoles();

        target = DollMaster.SwapPlayerInfo(target);

        Logger.Info("Start", "PlagueBearer.CheckAndInfect");

        if (CustomRoles.PlagueBearer.RoleExist(true) && !killer.Is(CustomRoles.PlagueBearer))
        {
            PlagueBearer.CheckAndInfect(killer, target);
        }

        Logger.Info("Start", "NarcManager.CheckMurder");

        if (!NarcManager.CheckMurder(killer, target))
        {
            __state = true;
            Logger.Info("Killer trying to kill teammate,canceled", "NarcManager.CheckMurder");
            return false;
        }

        Logger.Info("Start", "ForcedCheckMurderAsKiller");

        if (killerRoleClass.ForcedCheckMurderAsKiller(killer, target) == false)
        {
            __state = true;
            Logger.Info("Cancels because for killer no need kill target", "ForcedCheckMurderAsKiller");
            return false;
        }

        Logger.Info("Start", "OnCheckMurder.RpcCheckAndMurder");

        if (killer.RpcCheckAndMurder(target, true) == false)
        {
            __state = true;
            Logger.Info("Cancels because target cancel kill", "OnCheckMurder.RpcCheckAndMurder");
            return false;
        }

        Logger.Info("Start foreach", "KillerSubRoles");

        if (killerSubRoles.Any())
            foreach (var killerSubRole in killerSubRoles.ToArray())
            {
                switch (killerSubRole)
                {
                    case CustomRoles.Madmate when target.Is(Custom_Team.Impostor) && !Madmate.MadmateCanKillImp.GetBool():
                    case CustomRoles.Infected when target.Is(CustomRoles.Infected) && !Infectious.TargetKnowOtherTargets:
                    case CustomRoles.Infected when target.Is(CustomRoles.Infectious):
                        canceled = true;
                        break;

                    case CustomRoles.Unlucky:
                        if (Unlucky.SuicideRand(killer, Unlucky.StateSuicide.TryKill))
                            canceled = true;
                        break;

                    case CustomRoles.Tired:
                        Tired.AfterActionTasks(killer);
                        break;

                    case CustomRoles.Mare:
                        if (Mare.IsLightsOut)
                            canceled = true;
                        break;

                    case CustomRoles.Clumsy:
                        if (!Clumsy.OnCheckMurder(killer))
                            canceled = true;
                        break;

                    case CustomRoles.Swift:
                        if (!Swift.OnCheckMurder(killer, target))
                            cancelbutkill = true;
                        break;
                }
            }

        Logger.Info("Start", "OnCheckMurderAsKiller");

        if (killerRoleClass.OnCheckMurderAsKiller(killer, target) == false)
        {
            __state = true;
            if (cancelbutkill && target.IsAlive()
                && !DoubleTrigger.FirstTriggerTimer.TryGetValue(killer.PlayerId, out _))
            {
                target.RpcMurderPlayer(target);
                target.SetRealKiller(killer);
                Oiiai.OnMurderPlayer(killer, target);
            }

            Logger.Info("Cancels because for killer no need kill target", "OnCheckMurderAsKiller");
            return false;
        }

        if (DollMaster.HasEnabled && killer.Is(CustomRoles.Sheriff) && target == DollMaster.DollMasterTarget)
        {
            target = DollMaster.SwapPlayerInfo(target);
        }

        if (DollMaster.HasEnabled && DollMaster.IsControllingPlayer)
            if (!(DollMaster.DollMasterTarget == null || DollMaster.controllingTarget == null))
                if (target == DollMaster.DollMasterTarget || target == DollMaster.controllingTarget)
                {
                    DollMaster.CheckMurderAsPossessed(killer, target);
                    return false;
                }

        if (canceled)
            return false;

        if (cancelbutkill)
        {
            target.RpcMurderPlayer(target);
            target.SetRealKiller(killer);
            Oiiai.OnMurderPlayer(killer, target);
            return false;
        }

        return true;
    }

    public static void OnMurderPlayer(PlayerControl killer, PlayerControl target, bool inMeeting, bool fromRole)
    {

        PlayerControl trueDMKiller = killer;
        killer = DollMaster.SwapPlayerInfo(killer);

        var killerRoleClass = killer.GetRoleClass();
        var targetRoleClass = target.GetRoleClass();

        var killerSubRoles = killer.GetCustomSubRoles();
        var targetSubRoles = target.GetCustomSubRoles();

        var isSuicide = killer.PlayerId == target.PlayerId;

        if (Options.CurrentGameMode == CustomGameMode.RoundUp) RoundUp.OnMurderPlayerAsTarget(killer, target, inMeeting, isSuicide);

        targetRoleClass.OnMurderPlayerAsTarget(killer, target, inMeeting, isSuicide);

        if (targetSubRoles.Any())
            foreach (var subRole in targetSubRoles.ToArray())
            {
                switch (subRole)
                {
                    case CustomRoles.Cyber:
                        Cyber.AfterCyberDeadTask(target, inMeeting);
                        break;

                    case CustomRoles.Bait when !inMeeting && !fromRole:
                        Bait.BaitAfterDeathTasks(trueDMKiller, target);
                        break;

                    case CustomRoles.Randomizer when !inMeeting && !fromRole && !isSuicide && !killer.Is(CustomRoles.KillingMachine):
                        Randomizer.RandomizerKilled(killer, target);
                        break;

                    case CustomRoles.Trapper when !inMeeting && !fromRole && !isSuicide && !killer.Is(CustomRoles.KillingMachine):
                        killer.TrapperKilled(target);
                        break;

                    case CustomRoles.Avanger when !inMeeting && !fromRole && !isSuicide:
                        Avanger.OnMurderPlayer(target);
                        break;

                    case CustomRoles.Burst when killer.IsAlive() && !inMeeting && !fromRole && !isSuicide && !killer.Is(CustomRoles.KillingMachine):
                        Burst.AfterBurstDeadTasks(killer, target);
                        break;

                    case CustomRoles.Oiiai when !fromRole && !isSuicide:
                        Oiiai.OnMurderPlayer(killer, target);
                        break;

                    case CustomRoles.EvilSpirit when !inMeeting && !isSuicide:
                        target.RpcSetRole(RoleTypes.GuardianAngel, true);
                        break;

                    case CustomRoles.Spurt:
                        Spurt.DeathTask(target);
                        break;

                }
            }

        killerRoleClass.OnMurderPlayerAsKiller(killer, target, inMeeting, isSuicide);

        if (killerSubRoles.Any())
            foreach (var subRole in killerSubRoles.ToArray())
            {
                switch (subRole)
                {
                    case CustomRoles.Stealer when !inMeeting && !isSuicide:
                        Stealer.OnMurderPlayer(killer, target);
                        break;

                    case CustomRoles.Plunderer when !inMeeting && !isSuicide:
                        Plunderer.OnMurderPlayer(killer, target);
                        break;

                    case CustomRoles.Tricky:
                        Tricky.AfterPlayerDeathTasks(target);
                        break;
                }
            }

        CheckDeadBody(killer, target, inMeeting);

        if (!(killer.PlayerId == target.PlayerId && target.IsDisconnected()))
        {
            Lovers.LoversSuicide(target.PlayerId, inMeeting);
        }
    }

    public static void OthersCompleteThisTask(PlayerControl player, PlayerTask task, bool playerIsOverridden, PlayerControl realPlayer)
        => AllEnabledRoles.Do(RoleClass => RoleClass.OnOthersTaskComplete(player, task, playerIsOverridden, realPlayer));

    public static HashSet<Action<PlayerControl, PlayerControl, bool>> CheckDeadBodyOthers = [];

    public static void CheckDeadBody(PlayerControl killer, PlayerControl deadBody, bool inMeeting)
    {
        if (!CheckDeadBodyOthers.Any()) return;

        foreach (var checkDeadBodyOthers in CheckDeadBodyOthers.ToArray())
        {
            checkDeadBodyOthers(killer, deadBody, inMeeting);
        }
    }

    public static HashSet<Action<PlayerControl, bool, long>> OnFixedUpdateOthers = [];

    public static void OnFixedUpdate(PlayerControl player, bool lowLoad, long nowTime, int timerLowLoad)
    {
        player.GetRoleClass()?.OnFixedUpdate(player, lowLoad, nowTime, timerLowLoad);

        if (!OnFixedUpdateOthers.Any()) return;

        foreach (var onFixedUpdate in OnFixedUpdateOthers.ToArray())
        {
            onFixedUpdate(player, lowLoad, nowTime);
        }
    }

    public static bool OthersCoEnterVent(PlayerPhysics physics, int ventId)
    {
        return AllEnabledRoles.Any(RoleClass => RoleClass.OnCoEnterVentOthers(physics, ventId));
    }

    private static HashSet<Func<PlayerControl, PlayerControl, bool, string>> MarkOthers = [];

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        var sb = new StringBuilder(100);
        foreach (var marker in MarkOthers)
        {
            sb.Append(marker(seer, seen, isForMeeting));
        }
        return sb.ToString();
    }

    private static HashSet<Func<PlayerControl, PlayerControl, bool, bool, string>> LowerOthers = [];

    public static string GetLowerTextOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false, bool isForHud = false)
    {
        var sb = new StringBuilder(100);
        foreach (var lower in LowerOthers)
        {
            sb.Append(lower(seer, seen, isForMeeting, isForHud));
        }

        return sb.ToString();
    }

    private static HashSet<Func<PlayerControl, PlayerControl, bool, string>> SuffixOthers = [];

    public static string GetSuffixOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {

        var sb = new StringBuilder(100);
        foreach (var suffix in SuffixOthers)
        {
            sb.Append(suffix(seer, seen, isForMeeting));
        }
        return sb.ToString();
    }

    public static readonly Dictionary<byte, HashSet<int>> DoNotUnlockVentsList = [];
    public static readonly Dictionary<byte, HashSet<int>> BlockedVentsList = [];

    public static void Initialize()
    {
        OtherCollectionsSet = false;
        OnFixedUpdateOthers.Clear();
        CheckDeadBodyOthers.Clear();
        BlockedVentsList.Clear();
        DoNotUnlockVentsList.Clear();
    }

    public static void Add()
    {
        MarkOthers = AllEnabledRoles.Select(mark => (Func<PlayerControl, PlayerControl, bool, string>)mark.GetMarkOthers).FilterDuplicates();
        LowerOthers = AllEnabledRoles.Select(lower => (Func<PlayerControl, PlayerControl, bool, bool, string>)lower.GetLowerTextOthers).FilterDuplicates();
        SuffixOthers = AllEnabledRoles.Select(suffix => (Func<PlayerControl, PlayerControl, bool, string>)suffix.GetSuffixOthers).FilterDuplicates();
        OtherCollectionsSet = true;
    }

    public static void OnFixedAddonUpdate(this PlayerControl pc, bool lowload) => pc.GetCustomSubRoles().Do(x =>
    {
        if (AddonClasses.TryGetValue(x, out var IAddon) && IAddon != null)
            IAddon.OnFixedUpdate(pc);
        else return;

        if (!lowload)
            IAddon.OnFixedUpdateLowLoad(pc);
    });
}

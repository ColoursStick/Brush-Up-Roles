using AmongUs.GameOptions;
using TONE.Roles.Crewmate;
using TONE.Roles.Impostor;
using TONE.Roles.Neutral;

namespace TONE.Roles.Core.AssignManager;

public class RoleAssign
{
    public static Dictionary<byte, CustomRoles> SetRoles = [];
    public static Dictionary<byte, CustomRoles> RoleResult = [];
    public static CustomRoles[] AllRoles => [.. RoleResult.Values];

    [Obfuscation(Exclude = true)]
    public enum RoleAssignType
    {
        Impostor,
        NeutralKilling,
        NonKillingNeutral,
        NeutralApocalypse,
        Coven,
        Crewmate,

        None
    }

    public class RoleAssignInfo(CustomRoles role, int spawnChance, int maxCount, int assignedCount = 0)
    {
        public CustomRoles Role { get => role; set => role = value; }
        public int SpawnChance { get => spawnChance; set => spawnChance = value; }
        public int MaxCount { get => maxCount; set => maxCount = value; }
        public int AssignedCount { get => assignedCount; set => assignedCount = value; }

        public RoleAssignInfo Copy()
        {
            return new(Role, SpawnChance, MaxCount, AssignedCount);
        }
    }

    public static void GetNeutralCounts(int NKmaxOpt, int NKminOpt, int NNKmaxOpt, int NNKminOpt, int NAmaxOpt, int NAminOpt, ref int ResultNKnum, ref int ResultNNKnum, ref int ResultNAnum)
    {
        var rd = IRandom.Instance;

        if (NNKmaxOpt > 0 && NNKmaxOpt >= NNKminOpt)
        {
            ResultNNKnum = rd.Next(NNKminOpt, NNKmaxOpt + 1);
        }

        if (NKmaxOpt > 0 && NKmaxOpt >= NKminOpt)
        {
            ResultNKnum = rd.Next(NKminOpt, NKmaxOpt + 1);
        }

        if (NAmaxOpt > 0 && NAmaxOpt >= NAminOpt)
        {
            ResultNAnum = rd.Next(NAminOpt, NAmaxOpt + 1);
        }
    }
    public static void GetCovenCounts(int CVmaxOpt, int CVminOpt, ref int ResultCVnum)
    {
        var rd = IRandom.Instance;

        if (CVmaxOpt > 0 && CVmaxOpt >= CVminOpt)
        {
            ResultCVnum = rd.Next(CVminOpt, CVmaxOpt + 1);
        }
    }

    public static void GetImpCounts(int ImpmaxOpt, int ImpminOpt, ref int ResultImpnum)
    {
        if (!Options.UseVariableImp.GetBool())
        {
            ResultImpnum = Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors);
            return;
        }
        var rd = IRandom.Instance;

        if (ImpmaxOpt > 0 && ImpmaxOpt >= ImpminOpt)
        {
            ResultImpnum = rd.Next(ImpminOpt, ImpmaxOpt + 1);
        }
    }
    public static void StartSelect()
    {
        if (!GameModeBase.GetGameMode().GetGameModeClass().NormalSelectRoles)
        {
            GameModeBase.GetGameMode().GetGameModeClass().SelectRoles();
            return;
        }

        var rd = IRandom.Instance;
        int playerCount = Main.AllAlivePlayerControls.Count;
        int optImpNum = 0;
        int optNonNeutralKillingNum = 0;
        int optNeutralKillingNum = 0;
        int optNeutralApocalypseNum = 0;
        int optCovenNum = 0;

        GetNeutralCounts(Options.NeutralKillingRolesMaxPlayer.GetInt(), Options.NeutralKillingRolesMinPlayer.GetInt(), Options.NonNeutralKillingRolesMaxPlayer.GetInt(), Options.NonNeutralKillingRolesMinPlayer.GetInt(), Options.NeutralApocalypseRolesMaxPlayer.GetInt(), Options.NeutralApocalypseRolesMinPlayer.GetInt(), ref optNeutralKillingNum, ref optNonNeutralKillingNum, ref optNeutralApocalypseNum);
        GetCovenCounts(Options.CovenRolesMaxPlayer.GetInt(), Options.CovenRolesMinPlayer.GetInt(), ref optCovenNum);
        GetImpCounts(Options.ImpRolesMaxPlayer.GetInt(), Options.ImpRolesMinPlayer.GetInt(), ref optImpNum);

        int readyRoleNum = 0;
        int readyImpNum = 0;
        int readyNonNeutralKillingNum = 0;
        int readyNeutralKillingNum = 0;
        int readyNeutralApocalypseNum = 0;
        int readyCovenNum = 0;

        List<CustomRoles> FinalRolesList = [];

        Dictionary<RoleAssignType, List<RoleAssignInfo>> Roles = [];

        Roles[RoleAssignType.Impostor] = [];
        Roles[RoleAssignType.NeutralKilling] = [];
        Roles[RoleAssignType.NonKillingNeutral] = [];
        Roles[RoleAssignType.NeutralApocalypse] = [];
        Roles[RoleAssignType.Coven] = [];
        Roles[RoleAssignType.Crewmate] = [];

        foreach (var id in SetRoles.Keys.Where(id => Utils.GetPlayerById(id) == null).ToArray()) SetRoles.Remove(id);

        foreach (var role in EnumHelper.GetAllValues<CustomRoles>())
        {
            int chance = role.GetMode();
            if (role.IsVanilla() || chance == 0 || role.IsAdditionRole() || role.IsGhostRole() || (role.OnlySpawnsWithPetsRole() && !Options.UsePets.GetBool())) continue;
            if ((role.NotAssignInVanillaServer() && Main.CurrentServerIsVanilla) || (role.NotSpawnInRoundUp() && Options.CurrentGameMode == CustomGameMode.RoundUp)) continue;
            if (role.OtherGameModesRole()) continue;
            switch (role)
            {
                case CustomRoles.Stalker when GameStates.FungleIsActive:
                case CustomRoles.Lighter when GameStates.FungleIsActive:
                case CustomRoles.Camouflager when GameStates.FungleIsActive:
                case CustomRoles.Doctor when Options.EveryoneCanSeeDeathReason.GetBool():
                case CustomRoles.VengefulRomantic:
                case CustomRoles.RuthlessRomantic:
                case CustomRoles.GM:
                case CustomRoles.NotAssigned:
                case CustomRoles.Runner:
                case CustomRoles.PhantomTONE when NarcManager.IsNarcAssigned():
                    continue;
            }

            int count = role.GetCount();
            RoleAssignInfo info = new(role, chance, count);

            if (role.IsImpostor())
            {
                if (role == NarcManager.RoleForNarcToSpawnAs)
                {
                    RoleAssignInfo newinfo = new(role, 100, 1);
                    Roles[RoleAssignType.Crewmate].Add(newinfo);
                }
                else Roles[RoleAssignType.Impostor].Add(info);
            }
            else if (role.IsNK()) Roles[RoleAssignType.NeutralKilling].Add(info);
            else if (role.IsNA()) Roles[RoleAssignType.NeutralApocalypse].Add(info);
            else if (role.IsNonNK()) Roles[RoleAssignType.NonKillingNeutral].Add(info);
            else if (role.IsCoven()) Roles[RoleAssignType.Coven].Add(info);
            else Roles[RoleAssignType.Crewmate].Add(info);
        }

        Logger.Info($"Number of NKs: {optNeutralKillingNum}, Number of NonNKs: {optNonNeutralKillingNum}", "NeutralNum");
        Logger.Msg("=====================================================", "AllActiveRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "ImpRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralApocalypse].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NARoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "NonNKRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Coven].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CovenRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => $"{x.Role}: {x.SpawnChance}% - {x.MaxCount}")), "CrewRoles");
        Logger.Msg("=====================================================", "AllActiveRoles");

        IEnumerable<RoleAssignInfo> TempAlwaysImpRoles = Roles[RoleAssignType.Impostor].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysNKRoles = Roles[RoleAssignType.NeutralKilling].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysNARoles = Roles[RoleAssignType.NeutralApocalypse].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysNNKRoles = Roles[RoleAssignType.NonKillingNeutral].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysCovenRoles = Roles[RoleAssignType.Coven].Where(x => x.SpawnChance == 100);
        IEnumerable<RoleAssignInfo> TempAlwaysCrewRoles = Roles[RoleAssignType.Crewmate].Where(x => x.SpawnChance == 100);

        Roles[RoleAssignType.Impostor] = Roles[RoleAssignType.Impostor].Shuffle(rd).Shuffle(rd).Take(optImpNum).ToList();
        Roles[RoleAssignType.NeutralKilling] = Roles[RoleAssignType.NeutralKilling].Shuffle(rd).Shuffle(rd).Take(optNeutralKillingNum).ToList();
        Roles[RoleAssignType.NeutralApocalypse] = Roles[RoleAssignType.NeutralApocalypse].Shuffle(rd).Shuffle(rd).Take(optNeutralApocalypseNum).ToList();
        Roles[RoleAssignType.NonKillingNeutral] = Roles[RoleAssignType.NonKillingNeutral].Shuffle(rd).Shuffle(rd).Take(optNonNeutralKillingNum).ToList();
        Roles[RoleAssignType.Coven] = Roles[RoleAssignType.Coven].Shuffle(rd).Shuffle(rd).Take(optCovenNum).ToList();
        Roles[RoleAssignType.Crewmate] = Roles[RoleAssignType.Crewmate].Shuffle(rd).Shuffle(rd).Take(playerCount).ToList();

        Roles[RoleAssignType.Impostor].AddRange(TempAlwaysImpRoles);
        Roles[RoleAssignType.NeutralKilling].AddRange(TempAlwaysNKRoles);
        Roles[RoleAssignType.NeutralApocalypse].AddRange(TempAlwaysNARoles);
        Roles[RoleAssignType.NonKillingNeutral].AddRange(TempAlwaysNNKRoles);
        Roles[RoleAssignType.Coven].AddRange(TempAlwaysCovenRoles);
        Roles[RoleAssignType.Crewmate].AddRange(TempAlwaysCrewRoles);

        Roles[RoleAssignType.Impostor] = Roles[RoleAssignType.Impostor].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.NeutralKilling] = Roles[RoleAssignType.NeutralKilling].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.NeutralApocalypse] = Roles[RoleAssignType.NeutralApocalypse].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.NonKillingNeutral] = Roles[RoleAssignType.NonKillingNeutral].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.Coven] = Roles[RoleAssignType.Coven].DistinctBy(x => x.Role).ToList();
        Roles[RoleAssignType.Crewmate] = Roles[RoleAssignType.Crewmate].DistinctBy(x => x.Role).ToList();

        Logger.Msg("======================================================", "SelectedRoles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Impostor].Select(x => x.Role.ToString())), "Selected-Impostor-Roles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralKilling].Select(x => x.Role.ToString())), "Selected-NK-Roles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NeutralApocalypse].Select(x => x.Role.ToString())), "Selected-NA-Roles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.NonKillingNeutral].Select(x => x.Role.ToString())), "Selected-NonNK-Roles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Coven].Select(x => x.Role.ToString())), "Selected-Coven-Roles");
        Logger.Info(string.Join(", ", Roles[RoleAssignType.Crewmate].Select(x => x.Role.ToString())), "Selected-Crew-Roles");
        Logger.Msg("======================================================", "SelectedRoles");

        var AllPlayers = Main.EnumeratePlayerControls().ToList();

        if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()))
        {
            Logger.Warn("Host presets in BanManager.CheckEACList", "EAC");
            Main.EnableGM.Value = true;
            RoleResult[PlayerControl.LocalPlayer.PlayerId] = CustomRoles.GM;
            AllPlayers.Remove(PlayerControl.LocalPlayer);
        }

        foreach (var player in Main.EnumeratePlayerControls())
        {
            if (player == null) continue;

            if (TagManager.AssignGameMaster(player.FriendCode))
            {
                Logger.Info($"Assign Game Master due to tag for [{player.PlayerId}]{player.GetRealName()}", "TagManager");
                RoleResult[player.PlayerId] = CustomRoles.GM;
                SetRoles.Remove(player.PlayerId);
                AllPlayers.Remove(player);
            }
            else if (Main.EnableGM.Value && player.IsHost())
            {
                RoleResult[PlayerControl.LocalPlayer.PlayerId] = CustomRoles.GM;
                SetRoles.Remove(PlayerControl.LocalPlayer.PlayerId);
                AllPlayers.Remove(PlayerControl.LocalPlayer);
            }
            else if (SetRoles.TryGetValue(player.PlayerId, out var role) && role == CustomRoles.GM)
            {
                RoleResult[player.PlayerId] = CustomRoles.GM;
                SetRoles.Remove(player.PlayerId);
                AllPlayers.Remove(player);
                Logger.Info($"Assign Game Master due to tag for [{player.PlayerId}]{player.GetRealName()}", "SetRoles");
            }
        }

        foreach (var item in SetRoles)
        {
            PlayerControl pc = Utils.GetPlayerById(item.Key);
            if (pc == null) continue;

            RoleResult[item.Key] = item.Value;
            AllPlayers.Remove(pc);

            if (item.Value.IsImpostor())
            {
                Roles[RoleAssignType.Impostor].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyImpNum++;
            }
            else if (item.Value.IsNK())
            {
                Roles[RoleAssignType.NeutralKilling].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyNeutralKillingNum++;
            }
            else if (item.Value.IsNA())
            {
                Roles[RoleAssignType.NeutralApocalypse].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyNeutralApocalypseNum++;
            }
            else if (item.Value.IsCoven())
            {
                Roles[RoleAssignType.Coven].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyCovenNum++;
            }
            else if (item.Value.IsNonNK())
            {
                Roles[RoleAssignType.NonKillingNeutral].Where(x => x.Role == item.Value).Do(x => x.AssignedCount++);
                readyNonNeutralKillingNum++;
            }

            Logger.Warn($"Pre-Set Role Assigned: {pc.GetRealName()} => {item.Value}", "RoleAssign");
        }

        RoleAssignInfo[] Imps = [];
        RoleAssignInfo[] NNKs = [];
        RoleAssignInfo[] NKs = [];
        RoleAssignInfo[] NAs = [];
        RoleAssignInfo[] Covs = [];
        RoleAssignInfo[] Crews = [];

        List<RoleAssignType> KillingFractions = [];

        bool spawnNK = false;
        bool spawnNA = false;
        bool spawnCoven = false;

        if (Roles[RoleAssignType.NeutralKilling].Count > 0)
        {
            KillingFractions.Add(RoleAssignType.NeutralKilling);
        }
        if (Roles[RoleAssignType.NeutralApocalypse].Count > 0)
        {
            KillingFractions.Add(RoleAssignType.NeutralApocalypse);
        }
        if (Roles[RoleAssignType.Coven].Count > 0)
        {
            KillingFractions.Add(RoleAssignType.Coven);
        }

        var randomType = Options.SpawnOneRandomKillingFraction.GetBool()
            ? KillingFractions.RandomElement() : RoleAssignType.None;

        switch (randomType)
        {
            case RoleAssignType.NeutralKilling:
                spawnNK = true;
                break;
            case RoleAssignType.NeutralApocalypse:
                spawnNA = true;
                break;
            case RoleAssignType.Coven:
                spawnCoven = true;
                break;
            default:
                spawnNK = true;
                spawnNA = true;
                spawnCoven = true;
                break;
        }

        if (Options.DraftMode.GetBool() && Options.SpawnOneRandomKillingFraction.GetBool() && !DraftAssign.DraftRoles.Values.All(x => x is CustomRoles.NotAssigned))
        {
            spawnNK = DraftAssign.KillingFractions.Contains("NK");
            spawnNA = DraftAssign.KillingFractions.Contains("NA");
            spawnCoven = DraftAssign.KillingFractions.Contains("Coven");
        }

        Logger.Info($"Spawn NK: {spawnNK}, Spawn NA: {spawnNA}, Spawn Coven: {spawnCoven}", "SpawnKillingFractions");

        playerCount = AllPlayers.Count;

        {
            List<CustomRoles> AlwaysImpRoles = [];
            List<CustomRoles> ChanceImpRoles = [];
            for (int i = 0; i < Roles[RoleAssignType.Impostor].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Impostor][i];

                if (item.SpawnChance == 100)
                {
                    for (int j = 0; j < item.MaxCount; j++)
                    {

                        if (SetRoles.ContainsValue(item.Role))
                        {
                            var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                            SetRoles.Remove(playerId);
                            continue;
                        }

                        AlwaysImpRoles.Add(item.Role);
                    }
                }
                else
                {

                    for (int k = 0; k < item.MaxCount; k++)
                    {

                        if (SetRoles.ContainsValue(item.Role))
                        {
                            var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                            SetRoles.Remove(playerId);
                            continue;
                        }

                        for (int j = 0; j < item.SpawnChance / 5; j++)
                        {

                            ChanceImpRoles.Add(item.Role);
                        }
                    }
                }
            }

            RoleAssignInfo[] ImpRoleCounts = AlwaysImpRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceImpRoles.Distinct().Select(GetAssignInfo).ToArray());
            Imps = ImpRoleCounts;

            if (readyImpNum < optImpNum)
            {
                while (AlwaysImpRoles.Any())
                {
                    var selected = AlwaysImpRoles.RandomElement();
                    var info = ImpRoleCounts.FirstOrDefault(x => x.Role == selected);
                    AlwaysImpRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyImpNum++;

                    Imps = ImpRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyImpNum >= optImpNum) break;
                }
            }

            if (readyRoleNum < playerCount && readyImpNum < optImpNum)
            {
                while (ChanceImpRoles.Any())
                {
                    var selected = ChanceImpRoles.RandomElement();
                    var info = ImpRoleCounts.FirstOrDefault(x => x.Role == selected);

                    for (int j = 0; j < info.SpawnChance / 5; j++)
                        ChanceImpRoles.Remove(selected);

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;
                    readyImpNum++;

                    Imps = ImpRoleCounts;

                    if (info.AssignedCount >= info.MaxCount)
                        while (ChanceImpRoles.Contains(selected))
                            ChanceImpRoles.Remove(selected);

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                    if (readyImpNum >= optImpNum) break;
                }
            }
        }

        {

            {
                List<CustomRoles> AlwaysNonNKRoles = [];
                List<CustomRoles> ChanceNonNKRoles = [];
                for (int i = 0; i < Roles[RoleAssignType.NonKillingNeutral].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.NonKillingNeutral][i];

                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount; j++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            AlwaysNonNKRoles.Add(item.Role);
                        }
                    }
                    else
                    {

                        for (int k = 0; k < item.MaxCount; k++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            for (int j = 0; j < item.SpawnChance / 5; j++)
                            {

                                ChanceNonNKRoles.Add(item.Role);
                            }
                        }
                    }
                }

                RoleAssignInfo[] NonNKRoleCounts = AlwaysNonNKRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceNonNKRoles.Distinct().Select(GetAssignInfo).ToArray());
                NNKs = NonNKRoleCounts;

                if (readyNonNeutralKillingNum < optNonNeutralKillingNum)
                {
                    while (AlwaysNonNKRoles.Any() && optNonNeutralKillingNum > 0)
                    {
                        var selected = AlwaysNonNKRoles.RandomElement();
                        var info = NonNKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        AlwaysNonNKRoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNonNeutralKillingNum++;

                        NNKs = NonNKRoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNonNeutralKillingNum >= optNonNeutralKillingNum) break;
                    }
                }

                if (readyRoleNum < playerCount && readyNonNeutralKillingNum < optNonNeutralKillingNum)
                {
                    while (ChanceNonNKRoles.Any() && optNonNeutralKillingNum > 0)
                    {
                        var selected = ChanceNonNKRoles.RandomElement();
                        var info = NonNKRoleCounts.FirstOrDefault(x => x.Role == selected);

                        for (int j = 0; j < info.SpawnChance / 5; j++)
                            ChanceNonNKRoles.Remove(selected);

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNonNeutralKillingNum++;

                        NNKs = NonNKRoleCounts;

                        if (info.AssignedCount >= info.MaxCount)
                            while (ChanceNonNKRoles.Contains(selected))
                                ChanceNonNKRoles.Remove(selected);

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNonNeutralKillingNum >= optNonNeutralKillingNum) break;
                    }
                }
            }

            if (spawnNK)
            {
                List<CustomRoles> AlwaysNKRoles = [];
                List<CustomRoles> ChanceNKRoles = [];
                for (int i = 0; i < Roles[RoleAssignType.NeutralKilling].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.NeutralKilling][i];

                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount; j++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            AlwaysNKRoles.Add(item.Role);
                        }
                    }
                    else
                    {

                        for (int k = 0; k < item.MaxCount; k++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            for (int j = 0; j < item.SpawnChance / 5; j++)
                            {

                                ChanceNKRoles.Add(item.Role);
                            }
                        }
                    }
                }

                RoleAssignInfo[] NKRoleCounts = AlwaysNKRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceNKRoles.Distinct().Select(GetAssignInfo).ToArray());
                NKs = NKRoleCounts;

                if (readyNeutralKillingNum < optNeutralKillingNum)
                {
                    while (AlwaysNKRoles.Any() && optNeutralKillingNum > 0)
                    {
                        var selected = AlwaysNKRoles.RandomElement();
                        var info = NKRoleCounts.FirstOrDefault(x => x.Role == selected);
                        AlwaysNKRoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralKillingNum++;

                        NKs = NKRoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralKillingNum >= optNeutralKillingNum) break;
                    }
                }

                if (readyRoleNum < playerCount && readyNeutralKillingNum < optNeutralKillingNum)
                {
                    while (ChanceNKRoles.Any() && optNeutralKillingNum > 0)
                    {
                        var selected = ChanceNKRoles.RandomElement();
                        var info = NKRoleCounts.FirstOrDefault(x => x.Role == selected);

                        for (int j = 0; j < info.SpawnChance / 5; j++)
                            ChanceNKRoles.Remove(selected);

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralKillingNum++;

                        NKs = NKRoleCounts;

                        if (info.AssignedCount >= info.MaxCount) while (ChanceNKRoles.Contains(selected)) ChanceNKRoles.Remove(selected);

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralKillingNum >= optNeutralKillingNum) break;
                    }
                }
            }

            if (spawnNA)
            {
                List<CustomRoles> AlwaysNARoles = [];
                List<CustomRoles> ChanceNARoles = [];
                for (int i = 0; i < Roles[RoleAssignType.NeutralApocalypse].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.NeutralApocalypse][i];

                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount; j++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            AlwaysNARoles.Add(item.Role);
                        }
                    }
                    else
                    {

                        for (int k = 0; k < item.MaxCount; k++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            for (int j = 0; j < item.SpawnChance / 5; j++)
                            {

                                ChanceNARoles.Add(item.Role);
                            }
                        }
                    }
                }

                RoleAssignInfo[] NARoleCounts = AlwaysNARoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceNARoles.Distinct().Select(GetAssignInfo).ToArray());
                NAs = NARoleCounts;

                if (readyNeutralApocalypseNum < optNeutralApocalypseNum)
                {
                    while (AlwaysNARoles.Any() && optNeutralApocalypseNum > 0)
                    {
                        var selected = AlwaysNARoles[rd.Next(0, AlwaysNARoles.Count)];
                        var info = NARoleCounts.FirstOrDefault(x => x.Role == selected);
                        AlwaysNARoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralApocalypseNum++;

                        NAs = NARoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralApocalypseNum >= optNeutralApocalypseNum) break;
                    }
                }

                if (readyRoleNum < playerCount && readyNeutralApocalypseNum < optNeutralApocalypseNum)
                {
                    while (ChanceNARoles.Any() && optNeutralApocalypseNum > 0)
                    {
                        var selectesItem = rd.Next(0, ChanceNARoles.Count);
                        var selected = ChanceNARoles[selectesItem];
                        var info = NARoleCounts.FirstOrDefault(x => x.Role == selected);

                        for (int j = 0; j < info.SpawnChance / 5; j++)
                            ChanceNARoles.Remove(selected);

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyNeutralApocalypseNum++;

                        NAs = NARoleCounts;

                        if (info.AssignedCount >= info.MaxCount) while (ChanceNARoles.Contains(selected)) ChanceNARoles.Remove(selected);

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyNeutralApocalypseNum >= optNeutralApocalypseNum) break;
                    }
                }
            }
        }

        {
            if (spawnCoven)
            {
                List<CustomRoles> AlwaysCVRoles = [];
                List<CustomRoles> ChanceCVRoles = [];
                for (int i = 0; i < Roles[RoleAssignType.Coven].Count; i++)
                {
                    RoleAssignInfo item = Roles[RoleAssignType.Coven][i];

                    if (item.SpawnChance == 100)
                    {
                        for (int j = 0; j < item.MaxCount; j++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            AlwaysCVRoles.Add(item.Role);
                        }
                    }
                    else
                    {

                        for (int k = 0; k < item.MaxCount; k++)
                        {

                            if (SetRoles.ContainsValue(item.Role))
                            {
                                var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                                SetRoles.Remove(playerId);
                                continue;
                            }

                            for (int j = 0; j < item.SpawnChance / 5; j++)
                            {

                                ChanceCVRoles.Add(item.Role);
                            }
                        }
                    }
                }

                RoleAssignInfo[] CVRoleCounts = AlwaysCVRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceCVRoles.Distinct().Select(GetAssignInfo).ToArray());
                Covs = CVRoleCounts;

                if (readyCovenNum < optCovenNum)
                {
                    while (AlwaysCVRoles.Any() && optCovenNum > 0)
                    {
                        var selected = AlwaysCVRoles[rd.Next(0, AlwaysCVRoles.Count)];
                        var info = CVRoleCounts.FirstOrDefault(x => x.Role == selected);
                        AlwaysCVRoles.Remove(selected);
                        if (info.AssignedCount >= info.MaxCount) continue;

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyCovenNum++;

                        Covs = CVRoleCounts;

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyCovenNum >= optCovenNum) break;
                    }
                }

                if (readyRoleNum < playerCount && readyCovenNum < optCovenNum)
                {
                    while (ChanceCVRoles.Any() && optCovenNum > 0)
                    {
                        var selectesItem = rd.Next(0, ChanceCVRoles.Count);
                        var selected = ChanceCVRoles[selectesItem];
                        var info = CVRoleCounts.FirstOrDefault(x => x.Role == selected);

                        for (int j = 0; j < info.SpawnChance / 5; j++)
                            ChanceCVRoles.Remove(selected);

                        FinalRolesList.Add(selected);
                        info.AssignedCount++;
                        readyRoleNum++;
                        readyCovenNum++;

                        Covs = CVRoleCounts;

                        if (info.AssignedCount >= info.MaxCount) while (ChanceCVRoles.Contains(selected)) ChanceCVRoles.Remove(selected);

                        if (readyRoleNum >= playerCount) goto EndOfAssign;
                        if (readyCovenNum >= optCovenNum) break;
                    }
                }
            }
        }

        {
            List<CustomRoles> AlwaysCrewRoles = [];
            List<CustomRoles> ChanceCrewRoles = [];
            for (int i = 0; i < Roles[RoleAssignType.Crewmate].Count; i++)
            {
                RoleAssignInfo item = Roles[RoleAssignType.Crewmate][i];

                if (item.SpawnChance == 100)
                {
                    for (int j = 0; j < item.MaxCount; j++)
                    {

                        if (SetRoles.ContainsValue(item.Role))
                        {
                            var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                            SetRoles.Remove(playerId);
                            continue;
                        }

                        AlwaysCrewRoles.Add(item.Role);
                    }
                }
                else
                {

                    for (int k = 0; k < item.MaxCount; k++)
                    {

                        if (SetRoles.ContainsValue(item.Role))
                        {
                            var playerId = SetRoles.FirstOrDefault(x => x.Value == item.Role).Key;
                            SetRoles.Remove(playerId);
                            continue;
                        }

                        for (int j = 0; j < item.SpawnChance / 5; j++)
                        {

                            ChanceCrewRoles.Add(item.Role);
                        }
                    }
                }
            }

            RoleAssignInfo[] CrewRoleCounts = AlwaysCrewRoles.Distinct().Select(GetAssignInfo).ToArray().AddRangeToArray(ChanceCrewRoles.Distinct().Select(GetAssignInfo).ToArray());
            Crews = CrewRoleCounts;

            if (readyRoleNum < playerCount)
            {
                while (AlwaysCrewRoles.Any())
                {
                    var selected = AlwaysCrewRoles.RandomElement();
                    var info = CrewRoleCounts.FirstOrDefault(x => x.Role == selected);
                    AlwaysCrewRoles.Remove(selected);
                    if (info.AssignedCount >= info.MaxCount) continue;

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;

                    Crews = CrewRoleCounts;

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                }
            }

            if (readyRoleNum < playerCount)
            {
                while (ChanceCrewRoles.Any())
                {
                    var selected = ChanceCrewRoles.RandomElement();
                    var info = CrewRoleCounts.FirstOrDefault(x => x.Role == selected);

                    for (int j = 0; j < info.SpawnChance / 5; j++)
                        ChanceCrewRoles.Remove(selected);

                    FinalRolesList.Add(selected);
                    info.AssignedCount++;
                    readyRoleNum++;

                    Crews = CrewRoleCounts;

                    if (info.AssignedCount >= info.MaxCount) while (ChanceCrewRoles.Contains(selected)) ChanceCrewRoles.Remove(selected);

                    if (readyRoleNum >= playerCount) goto EndOfAssign;
                }
            }
        }

    EndOfAssign:

        if (Imps.Any()) Logger.Info(string.Join(", ", Imps.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "ImpRoleResult");
        if (NNKs.Any()) Logger.Info(string.Join(", ", NNKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NNKRoleResult");
        if (NKs.Any()) Logger.Info(string.Join(", ", NKs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NKRoleResult");
        if (NAs.Any()) Logger.Info(string.Join(", ", NAs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "NARoleResult");
        if (Covs.Any()) Logger.Info(string.Join(", ", Covs.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CovRoleResult");
        if (Crews.Any()) Logger.Info(string.Join(", ", Crews.Select(x => $"{x.Role} - {x.AssignedCount}/{x.MaxCount} ({x.SpawnChance}%)")), "CrewRoleResult");

        if (Sunnyboy.CheckSpawn() && FinalRolesList.Remove(CustomRoles.Jester) && CheckMax(CustomRoles.Sunnyboy, CustomRoles.Jester)) FinalRolesList.Add(CustomRoles.Sunnyboy);
        if (Bard.CheckSpawn() && FinalRolesList.Remove(CustomRoles.Arrogance) && CheckMax(CustomRoles.Bard, CustomRoles.Arrogance)) FinalRolesList.Add(CustomRoles.Bard);
        if (Requiter.CheckSpawn() && FinalRolesList.Remove(CustomRoles.Knight) && CheckMax(CustomRoles.Requiter, CustomRoles.Knight)) FinalRolesList.Add(CustomRoles.Requiter);

        if (AllPlayers.Count > FinalRolesList.Count)
        {
            while (FinalRolesList.Count < AllPlayers.Count)
            {
                FinalRolesList.Add(CustomRoles.CrewmateTONE);
            }
        }

        Logger.Info(string.Join(", ", FinalRolesList.Select(x => x.ToString())), "RoleResults");

        while (AllPlayers.Any() && FinalRolesList.Any())
        {

            if (AllPlayers.Count > 2)
                AllPlayers = AllPlayers.Shuffle(rd).Shuffle(rd).ToList();

            if (FinalRolesList.Count > 2)
                FinalRolesList = FinalRolesList.Shuffle(rd).Shuffle(rd).ToList();

            var randomPlayer = AllPlayers.RandomElement();
            var assignedRole = FinalRolesList.RandomElement();

            RoleResult[randomPlayer.PlayerId] = assignedRole;
            Logger.Info($"Player：{randomPlayer.GetRealName()} => {assignedRole}", "RoleAssign");

            AllPlayers.Remove(randomPlayer);
            FinalRolesList.Remove(assignedRole);
        }

        if (AllPlayers.Any())
            Logger.Warn("Role assignment warirng: There are players who have not been assigned a role", "RoleAssign");
        if (FinalRolesList.Any())
            Logger.Warn("Team assignment warirng: There is an unassigned team", "RoleAssign");
        return;

        RoleAssignInfo GetAssignInfo(CustomRoles role) => Roles.Values.FirstOrDefault(x => x.Any(y => y.Role == role))?.FirstOrDefault(x => x.Role == role);
        bool CheckMax(CustomRoles role, CustomRoles role2) => SetRoles.Values.Count(r => r == role) < role2.GetCount();
    }

    public static int AddScientistNum;
    public static int AddEngineerNum;
    public static int AddShapeshifterNum;
    public static int AddNoisemakerNum;
    public static int AddPhantomNum;
    public static int AddTrackerNum;
    public static int AddDetectiveNum;
    public static int AddViperNum;
    public static int AddJudgeNum;
    public static void CalculateVanillaRoleCount()
    {

        AddEngineerNum = 0;
        AddScientistNum = 0;
        AddShapeshifterNum = 0;
        AddNoisemakerNum = 0;
        AddPhantomNum = 0;
        AddTrackerNum = 0;
        AddDetectiveNum = 0;
        AddViperNum = 0;
        AddJudgeNum = 0;

        foreach (var role in AllRoles)
        {
            switch (role.GetVNRole())
            {
                case CustomRoles.Scientist:
                    AddScientistNum++;
                    break;
                case CustomRoles.Engineer:
                    AddEngineerNum++;
                    break;
                case CustomRoles.Shapeshifter:
                    AddShapeshifterNum++;
                    break;
                case CustomRoles.Noisemaker:
                    AddNoisemakerNum++;
                    break;
                case CustomRoles.Phantom:
                    AddPhantomNum++;
                    break;
                case CustomRoles.Tracker:
                    AddTrackerNum++;
                    break;
                case CustomRoles.Viper:
                    AddViperNum++;
                    break;
                case CustomRoles.Detective:
                    AddDetectiveNum++;
                    break;
                case CustomRoles.Judge:
                    AddJudgeNum++;
                    break;
            }
        }
    }
}

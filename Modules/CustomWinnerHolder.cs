using Hazel;

namespace TONE;

public static class CustomWinnerHolder
{

    public static CustomWinner WinnerTeam;

    public static HashSet<AdditionalWinners> AdditionalWinnerTeams = [];

    public static HashSet<CustomRoles> WinnerRoles = [];

    public static HashSet<byte> WinnerIds = [];

    public static void Reset()
    {
        WinnerTeam = CustomWinner.Default;
        AdditionalWinnerTeams.Clear();
        WinnerRoles.Clear();
        WinnerIds.Clear();
    }
    public static void ClearWinners()
    {
        WinnerRoles.Clear();
        WinnerIds.Clear();
    }

    public static void SetWinnerOrAdditonalWinner(CustomWinner winner)
    {
        if (WinnerTeam == CustomWinner.Default) WinnerTeam = winner;
        else AdditionalWinnerTeams.Add((AdditionalWinners)winner);
    }

    public static void ShiftWinnerAndSetWinner(CustomWinner winner)
    {
        if (WinnerTeam != CustomWinner.Default)
            AdditionalWinnerTeams.Add((AdditionalWinners)WinnerTeam);
        WinnerTeam = winner;
    }

    public static void ResetAndSetWinner(CustomWinner winner)
    {
        Reset();
        WinnerTeam = winner;
    }
    public static bool CheckForConvertedWinner(byte playerId)
    {
        foreach (var role in playerId.GetPlayer()?.GetCustomSubRoles().ToArray())
        {
            if (!(role is CustomRoles.Narc or CustomRoles.Admired || role.IsConverted())) continue;
            switch (role)
            {
                case CustomRoles.Admired:
                case CustomRoles.Narc:
                    ResetAndSetWinner(CustomWinner.Crewmate);
                    return true;
                case CustomRoles.Madmate:
                    ResetAndSetWinner(CustomWinner.Impostor);
                    return true;
                case CustomRoles.Recruit:
                    ResetAndSetWinner(CustomWinner.Jackal);
                    return true;
                case CustomRoles.Charmed:
                    ResetAndSetWinner(CustomWinner.Cultist);
                    return true;
                case CustomRoles.Infectious:
                    ResetAndSetWinner(CustomWinner.Infectious);
                    return true;
                case CustomRoles.Contagious:
                    ResetAndSetWinner(CustomWinner.Virus);
                    return true;
                case CustomRoles.Enchanted:
                    ResetAndSetWinner(CustomWinner.Coven);
                    return true;
            }
        }
        return false;
    }

    public static void ReadFrom(MessageReader reader)
    {
        WinnerTeam = (CustomWinner)reader.ReadPackedInt32();

        AdditionalWinnerTeams.Clear();
        int AdditionalWinnerTeamsCount = reader.ReadPackedInt32();
        for (int i = 0; i < AdditionalWinnerTeamsCount; i++)
            AdditionalWinnerTeams.Add((AdditionalWinners)reader.ReadPackedInt32());

        WinnerRoles.Clear();
        int WinnerRolesCount = reader.ReadPackedInt32();
        for (int i = 0; i < WinnerRolesCount; i++)
            WinnerRoles.Add((CustomRoles)reader.ReadPackedInt32());

        WinnerIds.Clear();
        int WinnerIdsCount = reader.ReadPackedInt32();
        for (int i = 0; i < WinnerIdsCount; i++)
            WinnerIds.Add(reader.ReadByte());
    }
}

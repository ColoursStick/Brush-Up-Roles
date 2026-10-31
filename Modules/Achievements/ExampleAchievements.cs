namespace TONE;

public sealed class BlackmailPerGame : AchievementBase
{
    public override int Id => 50;
    public override string Name => "BlackmailPerGame";
    public override CustomRoles Role => CustomRoles.Blackmailer;
    public override int TargetProgress => 1;

    public override void OnRoleAbility(AchievementEventType type, PlayerControl player)
    {
        if (type == AchievementEventType.BlackmailerBlackmailed && IsLocalPlayer(player))
            AddProgress();
    }
}

public sealed class BountyHunterStreak : AchievementBase
{
    public override int Id => 31;
    public override string Name => "BountyHunterStreak";
    public override CustomRoles Role => CustomRoles.BountyHunter;
    public override int TargetProgress => 3;
    public override bool SaveProgress => false;

    public override void OnRoleAbility(AchievementEventType type, PlayerControl player)
    {
        if (!IsLocalPlayer(player)) return;

        if (type == AchievementEventType.BountyHunterKilledTarget)
            AddProgress();
        else if (type == AchievementEventType.BountyHunterKilledNonTarget)
            SetProgress(0, false);
    }
}

public sealed class CleanerCleanBodies : AchievementBase
{
    public override int Id => 21;
    public override string Name => "CleanerCleanBodies";
    public override CustomRoles Role => CustomRoles.Cleaner;
    public override int TargetProgress => 3;

    public override void OnRoleAbility(AchievementEventType type, PlayerControl player)
    {
        if (type == AchievementEventType.CleanerCleanedBody && IsLocalPlayer(player))
            AddProgress();
    }
}

public sealed class SheriffMisfire : AchievementBase
{
    public override int Id => 0;
    public override string Name => "SheriffMisfire";
    public override CustomRoles Role => CustomRoles.Sheriff;
    public override int TargetProgress => 1;

    public override void OnRoleAbility(AchievementEventType type, PlayerControl player)
    {
        if (type == AchievementEventType.SheriffMisfire && IsLocalPlayer(player))
            AddProgress();
    }
}

public sealed class SheriffJustice : AchievementBase
{
    public override int Id => 1;
    public override string Name => "SheriffJustice";
    public override CustomRoles Role => CustomRoles.Sheriff;
    public override int TargetProgress => 3;

    public override void OnPlayerKilled(PlayerControl killer, PlayerControl target)
    {
        if (IsMyRole(killer) && !target.IsPlayerCrewmateTeam() && IsLocalPlayer(killer))
            AddProgress();
    }
}

public sealed class ArsonistWin : AchievementBase
{
    public override int Id => 10;
    public override string Name => "ArsonistWin";
    public override CustomRoles Role => CustomRoles.Arsonist;
    public override int TargetProgress => 1;

    public override void OnGameEnd(bool isWinner)
    {
        if (isWinner && Main.PlayerStates.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var state)
            && state.MainRole == Role)
            AddProgress();
    }
}

public sealed class JesterWin : AchievementBase
{
    public override int Id => 40;
    public override string Name => "JesterWin";
    public override CustomRoles Role => CustomRoles.Jester;
    public override int TargetProgress => 1;

    public override void OnGameEnd(bool isWinner)
    {
        if (isWinner && Main.PlayerStates.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var state)
            && state.MainRole == Role)
            AddProgress();
    }
}

public sealed class PelicanSnake : AchievementBase
{
    public override int Id => 60;
    public override string Name => "PelicanSnake";
    public override CustomRoles Role => CustomRoles.Pelican;
    public override int TargetProgress => 3;
    public override bool SaveProgress => false;

    public override void OnRoleAbility(AchievementEventType type, PlayerControl player)
    {
        if (type == AchievementEventType.PelicanEat && IsLocalPlayer(player))
            AddProgress();
    }
}

public sealed class PelicanBigEater : AchievementBase
{
    public override int Id => 61;
    public override string Name => "PelicanBigEater";
    public override CustomRoles Role => CustomRoles.Pelican;
    public override int TargetProgress => 6;

    public override void OnRoleAbility(AchievementEventType type, PlayerControl player)
    {
        if (type == AchievementEventType.PelicanEat && IsLocalPlayer(player))
            AddProgress();
    }
}

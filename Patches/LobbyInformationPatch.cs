using InnerNet;

namespace TONE;

[HarmonyPatch(typeof(GameContainer), nameof(GameContainer.SetupGameInfo))]
public static class MoreLobbyInfo_GameContainer_SetupGameInfo_Postfix
{

    public static void Postfix(GameContainer __instance)
    {
        var trueHostName = __instance.gameListing.TrueHostName;

        const string separator = "<#0000>000000000000000</color>";

        var age = __instance.gameListing.Age;
        var lobbyTime = $"Age: {age / 60}:{(age % 60 < 10 ? "0" : "")}{age % 60}";

        var platformId = __instance.gameListing.Platform switch
        {
            Platforms.StandaloneEpicPC => "Epic",
            Platforms.StandaloneSteamPC => "Steam",
            Platforms.StandaloneMac => "Mac",
            Platforms.StandaloneWin10 => "Microsoft Store",
            Platforms.StandaloneItch => "Itch.io",
            Platforms.IPhone => "iPhone / iPad",
            Platforms.Android => "Android",
            Platforms.Switch => "Nintendo Switch",
            Platforms.Xbox => "Xbox",
            Platforms.Playstation => "PlayStation",
            _ => "Unknown"
        };

        __instance.capacity.text = $"<size=40%>{separator}\n{trueHostName}\n{__instance.capacity.text}\n" +
                                   $"<#fb0>{GameCode.IntToGameName(__instance.gameListing.GameId)}</color>\n" +
                                   $"<#b0f>{platformId}</color>\n{lobbyTime}\n{separator}</size>";
    }
}
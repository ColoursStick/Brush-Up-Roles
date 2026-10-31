using UnityEngine;

namespace TONE.Patches;

[HarmonyPatch(typeof(LobbyBehaviour))]
public class LobbyBehaviourPatch
{
    [HarmonyPatch(nameof(LobbyBehaviour.Update)), HarmonyPostfix]
    public static void Update_Postfix(LobbyBehaviour __instance)
    {
        System.Func<ISoundPlayer, bool> lobbybgm = x => x.Name.Equals("MapTheme");
        ISoundPlayer MapThemeSound = SoundManager.Instance.soundPlayers.Find(lobbybgm);
        if (Main.DisableLobbyMusic.Value)
        {
            if (MapThemeSound == null) return;
            SoundManager.Instance.StopNamedSound("MapTheme");
        }
        else
        {
            if (MapThemeSound != null) return;
            SoundManager.Instance.CrossFadeSound("MapTheme", __instance.MapTheme, 0.5f);
        }
    }
}

[HarmonyPatch(typeof(PlayerMaterial), nameof(PlayerMaterial.SetColors), [typeof(int), typeof(Material)])]
public class PlayerMaterialPatch
{
    public static void Prefix([HarmonyArgument(0)] ref int colorId)
    {
        if (colorId < 0 || colorId >= Palette.PlayerColors.Length)
        {
            colorId = 0;
        }
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Init))]
public class NetworkedPlayerInfoInitPatch
{
    public static void Postfix(NetworkedPlayerInfo __instance)
    {
        foreach (var outfit in __instance.Outfits)
        {
            if (outfit.Value != null)
            {
                if (outfit.Value.ColorId < 0 || outfit.Value.ColorId >= Palette.PlayerColors.Length)
                {
                    outfit.Value.ColorId = 0;
                }
            }
        }
    }
}

namespace TONE;

[HarmonyPatch(typeof(RoleOptionSetting), nameof(RoleOptionSetting.UpdateValuesAndText))]
class ChanceChangePatch
{
    public static void Postfix(RoleOptionSetting __instance)
    {

        string disableText = $" ({Translator.GetString("Disabled")})";
        foreach (var button in __instance.GetComponentsInChildren<PassiveButton>())
        {
            button.gameObject.SetActive(false);
        }

        if (!__instance.titleText.text.Contains(disableText))
            __instance.titleText.text += disableText;

        if (__instance.roleChance != 0 || __instance.roleMaxCount != 0)
        {
            __instance.roleChance = 0;
            __instance.roleMaxCount = 0;
            __instance.OnValueChanged.Invoke(__instance);
        }
    }

}
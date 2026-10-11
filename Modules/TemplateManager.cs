using AmongUs.Data;
using Hazel;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static TONE.Translator;

namespace TONE;

public static class TemplateManager
{
    private static readonly string TEMPLATE_FILE_PATH = @$"{Main.Path}/BrushUpRoles-DATA/template.txt";

    private static readonly Dictionary<string, Func<string>> _replaceDictionaryNormalOptions = new()
    {
        ["RoomCode"] = () => InnerNet.GameCode.IntToGameName(AmongUsClient.Instance.GameId),
        ["HostName"] = () => DataManager.Player.Customization.Name,
        ["AmongUsVersion"] = () => UnityEngine.Application.version,
        ["InternalVersion"] = () => Main.PluginVersion,
        ["ModVersion"] = () => Main.PluginDisplayVersion,
        ["Map"] = () => Constants.MapNames[Main.NormalOptions.MapId],
        ["NumEmergencyMeetings"] = () => Main.NormalOptions.NumEmergencyMeetings.ToString(),
        ["EmergencyCooldown"] = () => Main.NormalOptions.EmergencyCooldown.ToString(),
        ["DiscussionTime"] = () => Main.NormalOptions.DiscussionTime.ToString(),
        ["VotingTime"] = () => Main.NormalOptions.VotingTime.ToString(),
        ["PlayerSpeedMod"] = () => Main.NormalOptions.PlayerSpeedMod.ToString(),
        ["CrewLightMod"] = () => Main.NormalOptions.CrewLightMod.ToString(),
        ["ImpostorLightMod"] = () => Main.NormalOptions.ImpostorLightMod.ToString(),
        ["KillCooldown"] = () => Main.NormalOptions.KillCooldown.ToString(),
        ["NumCommonTasks"] = () => Main.NormalOptions.NumCommonTasks.ToString(),
        ["NumLongTasks"] = () => Main.NormalOptions.NumLongTasks.ToString(),
        ["NumShortTasks"] = () => Main.NormalOptions.NumShortTasks.ToString(),
        ["Date"] = () => DateTime.Now.ToShortDateString(),
        ["Time"] = () => DateTime.Now.ToShortTimeString(),
        ["PlayerName"] = () => "",
        ["LobbyTimer"] = () =>
        {
            if (GameStates.IsLobby)
            {
                int timer = (int)GameStartManagerPatch.timer;
                int minutes = timer / 60;
                int seconds = timer % 60;
                return $"{minutes:D2}:{seconds:D2}";
            }
            else
            {
                return string.Empty;
            }
        }
    };

    private static readonly Dictionary<string, Func<string>> _replaceDictionaryHideNSeekOptions = new()
    {
        ["RoomCode"] = () => InnerNet.GameCode.IntToGameNameV2(AmongUsClient.Instance.GameId),
        ["HostName"] = () => DataManager.Player.Customization.Name,
        ["AmongUsVersion"] = () => UnityEngine.Application.version,
        ["InternalVersion"] = () => Main.PluginVersion,
        ["ModVersion"] = () => Main.PluginDisplayVersion,
        ["Map"] = () => Constants.MapNames[Main.HideNSeekOptions.MapId],
        ["PlayerSpeedMod"] = () => Main.HideNSeekOptions.PlayerSpeedMod.ToString(),
        ["Date"] = () => DateTime.Now.ToShortDateString(),
        ["Time"] = () => DateTime.Now.ToShortTimeString(),
        ["PlayerName"] = () => "",
        ["LobbyTimer"] = () =>
        {
            if (GameStates.IsLobby)
            {
                int timer = (int)GameStartManagerPatch.timer;
                int minutes = timer / 60;
                int seconds = timer % 60;
                return $"{minutes:D2}:{seconds:D2}";
            }
            else
            {
                return string.Empty;
            }
        }
    };

    public static void Init()
    {
        CreateIfNotExists();
    }

    public static void CreateIfNotExists()
    {

        try
        {
            string fileName = GetTemplateLanguageName();

            string dataDirectory = @$"{Main.Path}/BrushUpRoles-DATA";
            string defaultTemplatePath = @$"{Main.Path}/BrushUpRoles-DATA/Default_Teamplate.txt";

            if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);
            var defaultTemplateMsg = GetResourcesTxt($"TONE.Resources.Config.template.{fileName}.txt");

            if (!File.Exists(defaultTemplatePath))
            {
                Logger.Warn("Creating Default_Template.txt", "TemplateManager");
                using FileStream fs = File.Create(defaultTemplatePath);
            }
            File.WriteAllText(defaultTemplatePath, defaultTemplateMsg);

            if (!File.Exists(TEMPLATE_FILE_PATH))
            {
                if (File.Exists(@"./template.txt")) File.Move(@"./template.txt", TEMPLATE_FILE_PATH);
                else
                {
                    Logger.Warn($"Creating a new Template file from: {fileName}", "TemplateManager");
                    File.WriteAllText(TEMPLATE_FILE_PATH, defaultTemplateMsg);
                }
            }
            else
            {
                var text = File.ReadAllText(TEMPLATE_FILE_PATH, Encoding.GetEncoding("UTF-8"));

                // ⚠️ Brush Up Roles：修复「手机上进房看到英文欢迎语」。
                //
                //    旧版本按**系统区域设置**挑模板。手机常见「系统英文 + 游戏内简体中文」，
                //    于是生成了 English 模板并一直沿用，游戏内却显示英文欢迎语。
                //
                //    这里做一次自愈：如果现存模板与本次应当使用的语言不一致，
                //    但内容仍**等同于某个内嵌默认模板**（说明玩家没有自己改过），
                //    就换成正确语言的模板。玩家改过的模板一律不动。
                if (IsEqualToOtherLanguageTemplate(text, fileName))
                {
                    Logger.Info($"模板语言已切换为 {fileName}", "TemplateManager");
                    File.WriteAllText(TEMPLATE_FILE_PATH, defaultTemplateMsg);
                }
                else
                {
                    File.WriteAllText(TEMPLATE_FILE_PATH, text.Replace("5PNwUaN5", "hkk2p9ggv4"));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "TemplateManager");
        }
    }

    private static string GetResourcesTxt(string path)
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>所有内嵌欢迎语模板（对应 Resources/Config/template/*.txt）</summary>
    private static readonly string[] TemplateLanguages =
    [
        "English", "SChinese", "TChinese", "Japanese", "Russian",
        "Spanish", "Latam", "Brazilian", "French", "Italian", "Dutch"
    ];

    /// <summary>
    /// 现存模板是否**等同于其它语言**的内嵌默认模板（即玩家没改过，只是语言不对）。
    /// </summary>
    private static bool IsEqualToOtherLanguageTemplate(string currentText, string keepLanguage)
    {
        var normalized = currentText.Replace("hkk2p9ggv4", "5PNwUaN5");

        foreach (var lang in TemplateLanguages)
        {
            if (lang == keepLanguage) continue;

            string embedded;
            try { embedded = GetResourcesTxt($"TONE.Resources.Config.template.{lang}.txt"); }
            catch { continue; }
            if (string.IsNullOrEmpty(embedded)) continue;

            if (normalized.Replace("\r\n", "\n") == embedded.Replace("\r\n", "\n"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 决定用哪一份内嵌欢迎语模板。
    ///
    /// ⚠️ Brush Up Roles：原实现只看 <see cref="CultureInfo.CurrentCulture"/>（**系统区域设置**），
    ///    不看游戏内语言。手机上常见「系统是英文、游戏内选简体中文」的组合，
    ///    此时会取到 English.txt，玩家看到英文欢迎语。
    ///
    ///    现在改为**以游戏内语言为准**，系统区域仅在游戏语言取不到时兜底。
    /// </summary>
    private static string GetTemplateLanguageName()
    {
        // 1) 优先用游戏内语言
        try
        {
            if (TranslationController.InstanceExists)
            {
                var name = TranslationController.Instance.currentLanguage.languageID switch
                {
                    SupportedLangs.Russian => "Russian",
                    SupportedLangs.SChinese => "SChinese",
                    SupportedLangs.TChinese => "TChinese",
                    SupportedLangs.Brazilian => "Brazilian",
                    SupportedLangs.Latam => "Latam",
                    SupportedLangs.Spanish => "Spanish",
                    SupportedLangs.French => "French",
                    SupportedLangs.Japanese => "Japanese",
                    SupportedLangs.Dutch => "Dutch",
                    SupportedLangs.Italian => "Italian",
                    SupportedLangs.English => "English",
                    _ => null
                };
                if (name != null) return name;
            }
        }
        catch { /* 取不到就退回系统区域 */ }

        // 2) 兜底：系统区域设置
        try
        {
            var culture = CultureInfo.CurrentCulture.Name;
            if (!string.IsNullOrEmpty(culture))
            {
                var name = culture switch
                {
                    var lang when lang.StartsWith("ru") => "Russian",
                    "zh-Hans" or "zh" or "zh-CN" or "zn-SG" => "SChinese",
                    "zh-Hant" or "zh-HK" or "zh-MO" or "zh-TW" => "TChinese",
                    "pt-BR" => "Brazilian",
                    "es-419" => "Latam",
                    var lang when lang.StartsWith("es") => "Spanish",
                    var lang when lang.StartsWith("fr") => "French",
                    "ja" or "ja-JP" => "Japanese",
                    var lang when lang.StartsWith("nl") => "Dutch",
                    var lang when lang.StartsWith("it") => "Italian",
                    _ => null
                };
                if (name != null) return name;
            }
        }
        catch { }

        return "English";
    }

    public static void SendTemplate(string str = "", byte playerId = 0xff, bool noErr = false, SendOption sendOption = SendOption.Reliable)
    {
        CreateIfNotExists();
        using StreamReader sr = new(TEMPLATE_FILE_PATH, Encoding.GetEncoding("UTF-8"));
        string text;
        string[] tmp = [];
        List<string> sendList = [];
        HashSet<string> tags = [];
        Func<string> playerName = () => "";
        if (playerId != 0xff)
        {
            playerName = () => Main.AllPlayerNames[playerId];
        }

        _replaceDictionaryNormalOptions["PlayerName"] = playerName;
        _replaceDictionaryHideNSeekOptions["PlayerName"] = playerName;

        while ((text = sr.ReadLine()) != null)
        {
            tmp = text.Split(":");
            if (tmp.Length > 1 && tmp[1] != "")
            {
                tags.Add(tmp[0]);
                if (tmp[0].ToLower() == str.ToLower()) sendList.Add(tmp.Skip(1).Join(delimiter: ":").Replace("\\n", "\n"));
            }
        }
        if (sendList.Count == 0 && !noErr)
        {
            if (playerId == 0xff)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.TemplateNotFoundHost"), str, tags.Join(delimiter: ", ")));
            else Utils.SendMessage(string.Format(GetString("Message.TemplateNotFoundClient"), str), playerId, noReplay: true, sendOption: SendOption.None);
        }
        else for (int i = 0; i < sendList.Count; i++)
        {
            if (str == "welcome" && playerId != 0xff)
            {
                var player = Utils.GetPlayerById(playerId);
                if (player == null) continue;
                Utils.SendMessage(ApplyReplaceDictionary(sendList[i]), playerId, string.Format($"<color=#aaaaff>{GetString("OnPlayerJoinMsgTitle")}</color>", Utils.ColorString(Palette.PlayerColors.Length > player.cosmetics.ColorId ? Palette.PlayerColors[player.cosmetics.ColorId] : UnityEngine.Color.white, player.IsHost() ? Main.HostRealName : player.GetRealName(clientData: true))), sendOption: sendOption);
            }
            else Utils.SendMessage(ApplyReplaceDictionary(sendList[i]), playerId, sendOption: sendOption);
        }
    }

    private static string ApplyReplaceDictionary(string text)
    {
        try
        {
            if (GameStates.IsNormalGame)
            {
                foreach (var kvp in _replaceDictionaryNormalOptions.ToArray())
                {
                    text = Regex.Replace(text, "{{" + kvp.Key + "}}", kvp.Value.Invoke() ?? "", RegexOptions.IgnoreCase);
                }
            }
            else if (GameStates.IsHideNSeek)
            {
                foreach (var kvp in _replaceDictionaryHideNSeekOptions.ToArray())
                {
                    text = Regex.Replace(text, "{{" + kvp.Key + "}}", kvp.Value.Invoke() ?? "", RegexOptions.IgnoreCase);
                }
            }
            return text;
        }
        catch //(Exception ex)
        {
            //Logger.Exception(ex, "TemplateManager.ApplyReplaceDictionary");
            return text;
        }
    }
    private static string TryGetTitle(string Text, out bool Contains)
    {
        int start = Text.IndexOf("<title>");
        int end = Text.IndexOf("</title>");
        var contains = start != -1 && end != -1 && start < end;
        Contains = contains;
        string title = "";

        if (contains)
        {
            title = Text.Substring(start, end);
            title = title.Replace("<title>", "");
            title = title.Replace("</title>", "");

        }


        return title;
    }
}
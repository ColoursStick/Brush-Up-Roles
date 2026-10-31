using System.IO;
using System.Text.Json;

namespace TONE.Modules;

public static class OptionSaver
{
    private static readonly DirectoryInfo SaveDataDirectoryInfo = new(@$"{Main.Path}/BrushUpRoles-DATA/SaveData/");
    private static readonly FileInfo OptionSaverFileInfo = new(@$"{Main.Path}/BrushUpRoles-DATA/SaveData/Options.json");
    public static void Initialize()
    {
        if (!SaveDataDirectoryInfo.Exists)
        {
            SaveDataDirectoryInfo.Create();
            SaveDataDirectoryInfo.Attributes |= FileAttributes.Hidden;
        }
        if (!OptionSaverFileInfo.Exists)
        {
            OptionSaverFileInfo.Create().Dispose();
        }
    }

    private static SerializableOptionsData GenerateOptionsData()
    {
        Dictionary<int, int> singleOptions = [];
        Dictionary<int, int[]> presetOptions = [];
        foreach (var option in OptionItem.AllOptions)
        {
            if (option.IsSingleValue)
            {
                if (!singleOptions.TryAdd(option.Id, option.SingleValue))
                {
                    Logger.Warn($"Duplicate SingleOption ID: {option.Id}", "Option Saver");
                }
            }
            else if (!presetOptions.TryAdd(option.Id, option.AllValues))
            {
                Logger.Warn($"Duplicate preset option ID: {option.Id}", "Option Saver");
            }
        }
        return new SerializableOptionsData
        {
            Version = Version,
            SingleOptions = singleOptions,
            PresetOptions = presetOptions,
        };
    }

    private static void LoadOptionsData(SerializableOptionsData serializableOptionsData)
    {
        if (serializableOptionsData.Version != Version)
        {

            Logger.Info($"Loaded option version {serializableOptionsData.Version} does not match current version {Version}, overwriting with default value", "Option Saver");
            Save();
            return;
        }
        Dictionary<int, int> singleOptions = serializableOptionsData.SingleOptions;
        Dictionary<int, int[]> presetOptions = serializableOptionsData.PresetOptions;
        foreach (var singleOption in singleOptions)
        {
            var id = singleOption.Key;
            var value = singleOption.Value;
            if (OptionItem.FastOptions.TryGetValue(id, out var optionItem))
            {
                optionItem.SetValue(value, doSave: false);
            }
        }
        foreach (var presetOption in presetOptions)
        {
            var id = presetOption.Key;
            var values = presetOption.Value;
            if (OptionItem.FastOptions.TryGetValue(id, out var optionItem))
            {
                optionItem.SetAllValues(values);
            }
        }
    }

    public static void Save()
    {
        if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) return;

        try
        {
            var jsonString = JsonSerializer.Serialize(GenerateOptionsData(), new JsonSerializerOptions { WriteIndented = true, });
            File.WriteAllText(OptionSaverFileInfo.FullName, jsonString);
        }
        catch (System.Exception error)
        {
            Logger.Error($"Error: {error}", "OptionSaver.Save");
        }
    }

    public static void Load()
    {
        var jsonString = File.ReadAllText(OptionSaverFileInfo.FullName);

        if (jsonString.Length <= 0)
        {
            Logger.Info("Save default value as option data is empty", "Option Saver");
            Save();
            return;
        }
        LoadOptionsData(JsonSerializer.Deserialize<SerializableOptionsData>(jsonString));
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]

    public class SerializableOptionsData
    {
        public int Version { get; init; }

        public Dictionary<int, int> SingleOptions { get; init; }

        public Dictionary<int, int[]> PresetOptions { get; init; }
    }

    public static readonly int Version = 2;
}

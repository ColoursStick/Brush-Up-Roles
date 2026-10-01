using AmongUs.GameOptions;

namespace TONE.Modules.Extensions;

public static class IGameManagerEx
{
    public static void Set(this BoolOptionNames name, bool value, IGameOptions opt) => opt.SetBool(name, value);
    public static void Set(this BoolOptionNames name, bool value, NormalOptionsType opt)
    {
        if (name is not BoolOptionNames.GhostsDoTasks and not BoolOptionNames.Roles) opt.SetBool(name, value);
    }
    public static void Set(this BoolOptionNames name, bool value, HideNSeekOptionsType opt) => opt.SetBool(name, value);

    public static void Set(this Int32OptionNames name, int value, IGameOptions opt) => opt.SetInt(name, value);
    public static void Set(this Int32OptionNames name, int value, NormalOptionsType opt) => opt.SetInt(name, value);
    public static void Set(this Int32OptionNames name, int value, HideNSeekOptionsType opt) => opt.SetInt(name, value);

    public static void Set(this FloatOptionNames name, float value, IGameOptions opt) => opt.SetFloat(name, value);
    public static void Set(this FloatOptionNames name, float value, NormalOptionsType opt) => opt.SetFloat(name, value);
    public static void Set(this FloatOptionNames name, float value, HideNSeekOptionsType opt) => opt.SetFloat(name, value);

    public static void Set(this ByteOptionNames name, byte value, IGameOptions opt) => opt.SetByte(name, value);
    public static void Set(this ByteOptionNames name, byte value, NormalOptionsType opt) => opt.SetByte(name, value);
    public static void Set(this ByteOptionNames name, byte value, HideNSeekOptionsType opt) => opt.SetByte(name, value);

    public static void Set(this UInt32OptionNames name, uint value, IGameOptions opt) => opt.SetUInt(name, value);
    public static void Set(this UInt32OptionNames name, uint value, NormalOptionsType opt) => opt.SetUInt(name, value);
    public static void Set(this UInt32OptionNames name, uint value, HideNSeekOptionsType opt) => opt.SetUInt(name, value);
}

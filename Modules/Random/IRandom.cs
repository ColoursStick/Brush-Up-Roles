using System;

namespace TONE;

public interface IRandom
{

    public int Next(int maxValue);

    public int Next(int minValue, int maxValue);

    public static Dictionary<int, Type> randomTypes = new()
    {
        { 0, typeof(NetRandomWrapper) },
        { 1, typeof(HashRandomWrapper) },
        { 2, typeof(Xorshift) },
        { 3, typeof(MersenneTwister) },
    };

    public static IRandom Instance { get; private set; }

    public static void SetInstance(IRandom instance)
    {
        if (instance != null)
            Instance = instance;
    }

    public static void SetInstanceById(int id)
    {
        if (randomTypes.TryGetValue(id, out var type))
        {
            Instance = Activator.CreateInstance(type) as IRandom;
            Logger.Info($"Set IRandom instance to {type.Name}", "IRandom");
        }
        else
        {
            Logger.Warn($"Invalid ID: {id}", "IRandom.SetInstanceById");
            Instance = new NetRandomWrapper();
        }
    }
}

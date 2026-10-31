using System;

namespace TONE;

public static class EnumHelper
{

    public static T[] GetAllValues<T>() where T : Enum => Enum.GetValues(typeof(T)) as T[];

    public static string[] GetAllNames<T>() where T : Enum => Enum.GetNames(typeof(T));

    public static List<TEnum[]> Achunk<TEnum>(int chunkSize, bool shuffle = false, Func<TEnum, bool> exclude = null) where TEnum : Enum
    {
        List<TEnum[]> chunkedList = [];
        TEnum[] allValues = GetAllValues<TEnum>();
        if (shuffle) allValues = allValues.Shuffle().ToArray();
        if (exclude != null) allValues = allValues.Where(exclude).ToArray();

        for (int i = 0; i < allValues.Length; i += chunkSize)
        {
            TEnum[] chunk = new TEnum[Math.Min(chunkSize, allValues.Length - i)];
            Array.Copy(allValues, i, chunk, 0, chunk.Length);
            chunkedList.Add(chunk);
        }

        return chunkedList;

    }
}

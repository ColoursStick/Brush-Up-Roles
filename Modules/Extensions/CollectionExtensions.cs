using System;

namespace TONE;

public static class CollectionExtensions
{

    public static TKey GetKeyByValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TValue value)
    {
        foreach (KeyValuePair<TKey, TValue> pair in dictionary)
        {
            if (pair.Value.Equals(value))
            {
                return pair.Key;
            }
        }

        return default;
    }

    public static void SetAllValues<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TValue value)
    {
        foreach (TKey key in dictionary.Keys.ToArray())
            dictionary[key] = value;
    }

    public static T RandomElement<T>(this IList<T> collection)
    {
        if (collection.Count == 0) return default;
        return collection[IRandom.Instance.Next(collection.Count)];
    }
    public static T RandomElement<T>(this IEnumerable<T> collection)
    {
        if (collection is IList<T> list) return list.RandomElement();

        return collection.ToList().RandomElement();
    }

    public static IEnumerable<T> CombineWith<T>(this IEnumerable<T> firstCollection, params IEnumerable<T>[] collections)
    {
        return firstCollection.Concat(collections.SelectMany(x => x));
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection, IRandom random)
    {
        var list = collection.ToList();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
        return list;
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection)
    {
        var list = collection.ToList();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = IRandom.Instance.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
        return list;
    }

    public static HashSet<TDelegate> FilterDuplicates<TDelegate>(this IEnumerable<TDelegate> collection) where TDelegate : Delegate
    {

        var filteredCollection = collection.Where(d => d.Target != null);

        var distinctDelegates = filteredCollection
            .GroupBy(d => d.Target.GetType())
            .Select(g => g.First())
            .Concat(collection.Where(x => x.Target == null));

        return distinctDelegates.ToHashSet();
    }

    public static bool Find<T>(this IEnumerable<T> collection, Func<T, bool> predicate, out T element)
    {
        if (collection is List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                if (predicate(item))
                {
                    element = item;
                    return true;
                }
            }

            element = default;
            return false;
        }

        foreach (T item in collection)
        {
            if (predicate(item))
            {
                element = item;
                return true;
            }
        }

        element = default;
        return false;
    }

    public static (int TrueCount, int FalseCount) SplitCount<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
    {
        var int1 = 0;
        var int2 = 0;

        foreach (T element in collection)
        {
            if (predicate(element))
                int1++;
            else
                int2++;
        }

        return (int1, int2);
    }

    public static bool FindFirst<T>(this IEnumerable<T> collection, Func<T, bool> predicate, out T element)
    {
        if (collection is List<T> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                T item = list[i];

                if (predicate(item))
                {
                    element = item;
                    return true;
                }
            }

            element = default(T);
            return false;
        }

        foreach (T item in collection)
        {
            if (predicate(item))
            {
                element = item;
                return true;
            }
        }

        element = default(T);
        return false;
    }

    public static byte First(this HashSet<byte> source)
        => source.ToArray().First();

    #region Partition

    public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> collection, int parts)
    {
        List<T> list = collection.ToList();
        int length = list.Count;
        if (parts <= 0 || length == 0) yield break;

        if (parts > length) parts = length;

        int size = length / parts;
        int remainder = length % parts;
        var index = 0;

        for (var i = 0; i < parts; i++)
        {
            int partSize = size + (i < remainder ? 1 : 0);
            yield return list.Skip(index).Take(partSize);
            index += partSize;
        }
    }

    public static IEnumerable<IEnumerable<T>> Partition<T>(this IList<T> collection, int parts)
    {
        int length = collection.Count;
        if (parts <= 0 || length == 0) yield break;

        if (parts > length) parts = length;

        int size = length / parts;
        int remainder = length % parts;
        var index = 0;

        for (var i = 0; i < parts; i++)
        {
            int partSize = size + (i < remainder ? 1 : 0);
            yield return collection.Skip(index).Take(partSize);
            index += partSize;
        }
    }

    #endregion

    #region Without

    public static IEnumerable<T> Without<T>(this IEnumerable<T> collection, T element)
    {
        return collection.Where(x => !x.Equals(element));
    }

    public static IEnumerable<PlayerControl> Without(this IEnumerable<PlayerControl> collection, PlayerControl element)
    {
        return collection.Where(x => x.PlayerId != element.PlayerId);
    }

    public static IEnumerable<PlainShipRoom> Without(this IEnumerable<PlainShipRoom> collection, PlainShipRoom element)
    {
        return collection.Where(x => x != element);
    }

    #endregion
}

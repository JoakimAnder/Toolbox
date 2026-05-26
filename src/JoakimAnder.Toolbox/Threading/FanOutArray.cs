namespace JoakimAnder.Toolbox.Threading;

internal static class FanOutArray
{
    public static T[] Append<T>(T[]? source, T item)
    {
        if (source is null || source.Length == 0)
        {
            return [item];
        }

        var result = new T[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[source.Length] = item;
        return result;
    }

    public static T[] OrEmpty<T>(T[]? source) => source ?? [];
}

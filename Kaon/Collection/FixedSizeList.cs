using System.Collections;
using System.Text;

namespace Kaon.Collection;

public class FixedSizeList<T>(int size) : IEnumerable<T>, IEnumerator
{
    public virtual int Add(T value)
    {
        if (index == array.Length)
            return -1;
        array[index] = value;
        index++;
        return index - 1;
    }

    public void Set(int idx, T value)
    {
        if (idx < array.Length)
        {
            array[idx] = value;
        }
    }

    public void Remove(int idx)
    {
        if (idx >= array.Length) return;
        array[idx] = default!;
    }

    public T? this[int idx] => Get(idx);
    public T? Get(int idx)
    {
        if (idx >= array.Length) return default;
        return array[idx] ?? default;
    }

    public int IndexOf(T value)
    {
        var idx = 0;
        foreach (var item in array)
        {
            if (item!.Equals(value))
                return idx;
            idx++;
        }
        return -1;
    }

    public IEnumerator<T> GetEnumerator()
        => null!;//TODO
    IEnumerator IEnumerable.GetEnumerator()
        => this;
    public int Length() => array.Length;

    public override string ToString()
    {
        var builder = new StringBuilder("FixedList[");
        foreach (var item in array)
            builder.Append('"').Append(item).Append('"');
        return builder.Append(']').ToString();
    }

    public bool MoveNext()
    {
        enumIndex += 1;
        var success = enumIndex != array.Length;
        if (!success)
            enumIndex = 0;
        return success;
    }
    public void Reset()
        => enumIndex = 0;
    public object? Current => Get(enumIndex);

    internal int enumIndex;

    internal int index;
    internal readonly T[] array = new T[size];
    public int Count() => index;
}
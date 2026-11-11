namespace Kaon.Collection;

public sealed class RecycleList<T>(int size) : FixedSizeList<T>(size)
{
    public override int Add(T value)
    {
        if (index == array.Length)
            index = 0;
        array[index] = value;
        index++;
        return index - 1;
    }
}
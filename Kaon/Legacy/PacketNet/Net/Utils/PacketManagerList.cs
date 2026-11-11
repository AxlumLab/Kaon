using System.Collections;
using NetStickShared.Net.API;

namespace NetStickShared.Net.Utils;

//TODO rewrite it
public sealed class PacketManagerList : IEnumerator<ISocketListener>, IEnumerable<ISocketListener>
{
    public void Add(ISocketListener manager)
    {
        if (_coreList.Contains(manager))
            return;
        _coreList.Add(manager);
        manager.OnCollectAdd(this, _coreList.Count - 1);
    }

    public void Kill(ISocketListener manager)
    {
        if (!_coreList.Contains(manager))
            return;
        _coreList.Remove(manager);
    }

    public void KillAllManagers()
    {
        foreach (var man in _coreList)
        {
            man.OnCollectDelete(this);
        }
        _coreList.Clear();
    }

    public bool MoveNext()
    {
        _currentIndex++;
        if (_currentIndex < _coreList.Count) return true;
        _currentIndex = 0;
        return false;
    }

    public void Reset()
        => _currentIndex = 0;

    public IEnumerator<ISocketListener> GetEnumerator()
        => this;

    IEnumerator IEnumerable.GetEnumerator()
        => this;

    public ISocketListener Current => _coreList[_currentIndex];
    object IEnumerator.Current => _coreList[_currentIndex];
    public List<ISocketListener> GetClient() => _coreList;
        
    public int ConnectedCount() => _coreList.Count;
    ~PacketManagerList()
    {
        ReleaseUnmanagedResources();
    }

    private int _currentIndex;
    private readonly List<ISocketListener> _coreList = [];

    private void ReleaseUnmanagedResources()
    {
        
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }
}
namespace FlintSoft.Components;

public sealed class FsModalCoordinator
{
    private FsModal? _active;

    internal void Acquire(FsModal modal)
    {
        if (_active is not null)
            throw new InvalidOperationException("A modal is already opening or open. Close it before opening another.");
        _active = modal;
    }

    internal void Release(FsModal modal)
    {
        if (ReferenceEquals(_active, modal)) _active = null;
    }
}

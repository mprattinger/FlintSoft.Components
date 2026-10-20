using Microsoft.AspNetCore.Components;

namespace FlintSoft.Components;

public sealed record FsToast(Guid Id, FsToastStyle Style, string? Title, string? Text,
    RenderFragment? Content, TimeSpan? Duration);

public sealed class FsToastService : IDisposable
{
    private readonly List<FsToast> _items = [];
    private bool _disposed;
    public IReadOnlyList<FsToast> Items => _items.AsReadOnly();
    public event Action? Changed;

    public Guid Show(string text, FsToastStyle style = FsToastStyle.Info, string? title = null,
        TimeSpan? duration = null) => Add(text, null, style, title, duration);

    public Guid Show(RenderFragment content, FsToastStyle style = FsToastStyle.Info, string? title = null,
        TimeSpan? duration = null) => Add(null, content, style, title, duration);

    private Guid Add(string? text, RenderFragment? content, FsToastStyle style, string? title, TimeSpan? duration)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        duration ??= TimeSpan.FromSeconds(5);
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var toast = new FsToast(Guid.NewGuid(), style, title, text, content, duration == TimeSpan.Zero ? null : duration);
        _items.Add(toast);
        Changed?.Invoke();
        return toast.Id;
    }

    public void Dismiss(Guid id)
    {
        if (_items.RemoveAll(item => item.Id == id) > 0) Changed?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        _items.Clear();
        Changed = null;
    }
}

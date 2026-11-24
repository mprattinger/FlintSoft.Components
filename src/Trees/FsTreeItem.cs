namespace FlintSoft.Components;

public sealed class FsTreeItem<TItem>
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required bool ShowActions { get; set; }
    public TItem? Value { get; init; }
    public IReadOnlyList<FsTreeItem<TItem>> Children { get; set; } = [];
    public bool? IsExpandable { get; set; }
    public bool? IsClickable { get; set; }
    public bool IsDraggable { get; set; }
    public bool CanAcceptDrop { get; set; }
}

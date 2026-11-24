namespace FlintSoft.Components;

public sealed class FsTreeExpansionRequestEventArgs<TItem>(FsTreeItem<TItem> item,
    string? parentId, bool isExpanded, bool requestedExpanded, CancellationToken cancellationToken) : EventArgs
{
    public FsTreeItem<TItem> Item { get; } = item;
    public string Id => Item.Id;
    public string? ParentId { get; } = parentId;
    public bool IsExpanded { get; } = isExpanded;
    public bool RequestedExpanded { get; } = requestedExpanded;
    public FsTreeDecision Decision { get; set; }
    public CancellationToken CancellationToken { get; } = cancellationToken;
}

public sealed class FsTreeItemClickRequestEventArgs<TItem>(FsTreeItem<TItem> item,
    string? parentId, CancellationToken cancellationToken) : EventArgs
{
    public FsTreeItem<TItem> Item { get; } = item;
    public string Id => Item.Id;
    public string? ParentId { get; } = parentId;
    public FsTreeDecision Decision { get; set; }
    public CancellationToken CancellationToken { get; } = cancellationToken;
}

public sealed class FsTreeExpandedEventArgs<TItem>(FsTreeItem<TItem> item, bool isExpanded) : EventArgs
{
    public FsTreeItem<TItem> Item { get; } = item;
    public string Id => Item.Id;
    public bool IsExpanded { get; } = isExpanded;
}

public sealed class FsTreeItemClickedEventArgs<TItem>(FsTreeItem<TItem> item, string? selectedId) : EventArgs
{
    public FsTreeItem<TItem> Item { get; } = item;
    public string Id => Item.Id;
    public string? SelectedId { get; } = selectedId;
}

public sealed class FsTreeDropEventArgs<TItem>(IReadOnlyList<FsTreeItem<TItem>> items,
    FsTreeItem<TItem> item, FsTreeItem<TItem>? sourceParent, FsTreeItem<TItem> targetParent,
    int sourceIndex, int targetIndex, CancellationToken cancellationToken) : EventArgs
{
    public IReadOnlyList<FsTreeItem<TItem>> Items { get; } = items;
    public FsTreeItem<TItem> Item { get; } = item;
    public string Id => Item.Id;
    public FsTreeItem<TItem>? SourceParent { get; } = sourceParent;
    public string? SourceParentId => SourceParent?.Id;
    public FsTreeItem<TItem> TargetParent { get; } = targetParent;
    public string TargetParentId => TargetParent.Id;
    public int SourceIndex { get; } = sourceIndex;
    public int TargetIndex { get; } = targetIndex;
    public CancellationToken CancellationToken { get; } = cancellationToken;
}

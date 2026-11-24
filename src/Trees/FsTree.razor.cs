using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FlintSoft.Components;

public partial class FsTree<TItem>
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public IReadOnlyList<FsTreeItem<TItem>> Items { get; set; } = [];
    [Parameter] public bool? DefaultIsExpandable { get; set; }
    [Parameter] public bool DefaultIsClickable { get; set; } = true;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public IReadOnlyCollection<string>? ExpandedIds { get; set; }
    [Parameter] public EventCallback<IReadOnlyCollection<string>> ExpandedIdsChanged { get; set; }
    [Parameter] public string? SelectedId { get; set; }
    [Parameter] public EventCallback<string?> SelectedIdChanged { get; set; }
    [Parameter] public EventCallback<FsTreeExpansionRequestEventArgs<TItem>> ExpansionRequested { get; set; }
    [Parameter] public EventCallback<FsTreeItemClickRequestEventArgs<TItem>> ItemClickRequested { get; set; }
    [Parameter] public EventCallback<FsTreeExpandedEventArgs<TItem>> ExpandedChanged { get; set; }
    [Parameter] public EventCallback<FsTreeItemClickedEventArgs<TItem>> ItemClicked { get; set; }
    [Parameter] public EventCallback<FsTreeDropEventArgs<TItem>> ItemDropped { get; set; }
    [Parameter] public RenderFragment<FsTreeItem<TItem>>? ItemTemplate { get; set; }
    [Parameter] public RenderFragment<FsTreeItem<TItem>>? Actions { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public RenderFragment? EmptyChildrenContent { get; set; }
    [Parameter] public string AccessibleLabel { get; set; } = "Tree";
    [Parameter] public string? Class { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private sealed record Node(FsTreeItem<TItem> Item, string? ParentId, int Index, string DomId);
    private sealed class Request(Node node, bool expanded, string? selected, bool expansion) : IDisposable
    {
        public Node Node { get; } = node;
        public bool Expanded { get; } = expanded;
        public string? Selected { get; } = selected;
        public bool Expansion { get; } = expansion;
        public bool Committing { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();
        public void Dispose() => Cancellation.Dispose();
    }
    private sealed record ExpansionCompletion(FsTreeItem<TItem> Item, bool Expanded);
    private sealed record Drag(Node Source, string Session);
    private sealed record Drop(Node Source, Node Target, CancellationTokenSource Cancellation);

    private Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Request> _requests = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpansionCompletion> _completions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly string _instanceId = $"fs-tree-{Guid.NewGuid():N}";
    private int _nextDomId;
    private string? _selected, _focusId, _focusAfterRender, _moveSource, _moveTarget;
    private bool _selectionControlled, _disposed, _forceFocus;
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<FsTree<TItem>>? _reference;
    private Drag? _drag;
    private Drop? _drop;
    private string _status = "";
    private string _search = "";
    private long _searchTime;

    internal string? FocusId => _focusId;
    internal string? CurrentSelectedId => _selectionControlled ? ValidSelection(SelectedId) : _selected;
    private string? ValidSelection(string? id) => id is not null && _nodes.ContainsKey(id) ? id : null;
    internal static string Bool(bool value) => value ? "true" : "false";
    internal string DomId(string id) => _nodes[id].DomId;
    internal bool IsExpanded(string id) => _nodes.ContainsKey(id) && (ExpandedIds?.Contains(id, StringComparer.Ordinal) ?? _expanded.Contains(id));
    internal bool IsPending(string id) => _requests.ContainsKey(id);
    private bool ConfiguredExpansion(FsTreeItem<TItem> item) => item.IsExpandable ?? DefaultIsExpandable ?? item.Children.Count > 0;
    internal bool HasDisclosure(FsTreeItem<TItem> item) => item.Children.Count > 0 || ConfiguredExpansion(item) || IsExpanded(item.Id) || ExpansionRequested.HasDelegate;
    internal bool CanDrag(string id) => !Disabled && !_disposed && _drop is null && ItemDropped.HasDelegate &&
        _nodes.TryGetValue(id, out var node) && node.Item.IsDraggable && !IsPending(id);

    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (parameters.TryGetValue<string?>(nameof(SelectedId), out _)) _selectionControlled = true;
        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(AccessibleLabel);
        Refresh();
    }

    private void Refresh()
    {
        ArgumentNullException.ThrowIfNull(Items);
        var next = new Dictionary<string, Node>(StringComparer.Ordinal);
        void Visit(IReadOnlyList<FsTreeItem<TItem>> items, string? parent)
        {
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (item is null || string.IsNullOrWhiteSpace(item.Id) || item.Text is null || item.Children is null)
                    throw new ArgumentException("Tree items require a non-empty ID, text, and a children collection.", nameof(Items));
                var domId = _nodes.TryGetValue(item.Id, out var old) ? old.DomId : $"{_instanceId}-{++_nextDomId}";
                if (!next.TryAdd(item.Id, new(item, parent, index, domId)))
                    throw new ArgumentException($"Tree item ID '{item.Id}' appears more than once. IDs must be unique; cycles and multiple parents are not allowed.", nameof(Items));
                Visit(item.Children, item.Id);
            }
        }
        Visit(Items, null);
        var ancestors = new List<string>();
        for (var id = _focusId; id is not null && _nodes.TryGetValue(id, out var old); id = old.ParentId)
            ancestors.Add(id);
        _nodes = next;
        _expanded.RemoveWhere(id => !_nodes.ContainsKey(id));
        _selected = ValidSelection(_selected);
        foreach (var request in _requests.Values)
        {
            var id = request.Node.Item.Id;
            if (Disabled || !SameNode(request.Node) || (!request.Committing &&
                (request.Expansion ? IsExpanded(id) != request.Expanded : CurrentSelectedId != request.Selected)))
                request.Cancellation.Cancel();
        }
        foreach (var pair in _completions.ToArray())
            if (Disabled || !_nodes.TryGetValue(pair.Key, out var node) || !ReferenceEquals(node.Item, pair.Value.Item))
                _completions.Remove(pair.Key);
        if (_drag is not null && (!CanDrag(_drag.Source.Item.Id) || !SamePosition(_drag.Source))) _drag = null;
        if (_drop is not null)
        {
            var applied = _nodes.TryGetValue(_drop.Source.Item.Id, out var moved) && moved.ParentId == _drop.Target.Item.Id;
            if (Disabled || (!applied && (!SamePosition(_drop.Source) || !SameNode(_drop.Target) ||
                !_drop.Source.Item.IsDraggable || !_drop.Target.Item.CanAcceptDrop || !ItemDropped.HasDelegate)))
                _drop.Cancellation.Cancel();
        }
        var visible = VisibleNodes();
        if (_focusId is null || !visible.Any(n => n.Item.Id == _focusId))
        {
            _focusId = ancestors.FirstOrDefault(id => visible.Any(n => n.Item.Id == id)) ?? visible.FirstOrDefault()?.Item.Id;
            _focusAfterRender = _focusId;
        }
        if (_moveSource is not null && !_nodes.ContainsKey(_moveSource)) _moveSource = null;
        if (!CanMove(_moveSource, _moveTarget)) _moveTarget = null;
    }

    private bool SameNode(Node node) => _nodes.TryGetValue(node.Item.Id, out var current) &&
        ReferenceEquals(current.Item, node.Item) && current.ParentId == node.ParentId;
    private bool SamePosition(Node node) => SameNode(node) && _nodes[node.Item.Id].Index == node.Index;

    private List<Node> VisibleNodes()
    {
        var result = new List<Node>();
        void Visit(IReadOnlyList<FsTreeItem<TItem>> items)
        {
            foreach (var item in items)
            {
                result.Add(_nodes[item.Id]);
                if (IsExpanded(item.Id)) Visit(item.Children);
            }
        }
        Visit(Items);
        return result;
    }

    private static bool Resolve(FsTreeDecision decision, bool configured) => decision switch
    {
        FsTreeDecision.Default => configured,
        FsTreeDecision.Allow => true,
        FsTreeDecision.Deny => false,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown tree decision.")
    };

    private Request? StartRequest(string id, bool expansion)
    {
        Refresh();
        if (Disabled || _disposed || IsPending(id) || (_drop is not null &&
            (_drop.Source.Item.Id == id || _drop.Target.Item.Id == id)) || !_nodes.TryGetValue(id, out var node)) return null;
        var request = new Request(node, IsExpanded(id), CurrentSelectedId, expansion);
        _requests.Add(id, request);
        StateHasChanged();
        return request;
    }

    public async Task RequestExpansionAsync(string id, bool expanded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (_disposed) return;
        Refresh();
        if (!_nodes.ContainsKey(id) || IsExpanded(id) == expanded) return;
        using var request = StartRequest(id, true);
        if (request is null) return;
        var token = request.Cancellation.Token;
        try
        {
            var args = new FsTreeExpansionRequestEventArgs<TItem>(request.Node.Item, request.Node.ParentId, request.Expanded, expanded, token);
            await ExpansionRequested.InvokeAsync(args);
            if (_disposed) return;
            Refresh();
            if (token.IsCancellationRequested || !Resolve(args.Decision, !expanded || ConfiguredExpansion(request.Node.Item))) return;
            request.Committing = true;
            var ids = new HashSet<string>(ExpandedIds ?? _expanded, StringComparer.Ordinal);
            if (expanded) ids.Add(id); else ids.Remove(id);
            if (ExpandedIds is null)
            {
                if (expanded) _expanded.Add(id); else _expanded.Remove(id);
                await ExpandedIdsChanged.InvokeAsync(ids.ToArray());
                if (!_disposed)
                {
                    Refresh();
                    if (!token.IsCancellationRequested && IsExpanded(id) == expanded)
                        await ExpandedChanged.InvokeAsync(new(request.Node.Item, expanded));
                }
            }
            else
            {
                await ExpandedIdsChanged.InvokeAsync(ids.ToArray());
                if (!_disposed && !token.IsCancellationRequested)
                    _completions[id] = new(request.Node.Item, expanded);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            _requests.Remove(id);
            if (!_disposed) { Refresh(); StateHasChanged(); }
        }
    }

    public async Task ActivateAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (_disposed) return;
        using var request = StartRequest(id, false);
        if (request is null) return;
        var token = request.Cancellation.Token;
        try
        {
            var args = new FsTreeItemClickRequestEventArgs<TItem>(request.Node.Item, request.Node.ParentId, token);
            await ItemClickRequested.InvokeAsync(args);
            if (_disposed) return;
            Refresh();
            if (token.IsCancellationRequested || !Resolve(args.Decision, request.Node.Item.IsClickable ?? DefaultIsClickable)) return;
            request.Committing = true;
            if (CurrentSelectedId != id)
            {
                if (!_selectionControlled) _selected = id;
                await SelectedIdChanged.InvokeAsync(id);
            }
            if (!_disposed)
            {
                Refresh();
                if (!token.IsCancellationRequested)
                    await ItemClicked.InvokeAsync(new(request.Node.Item, CurrentSelectedId));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            _requests.Remove(id);
            if (!_disposed) StateHasChanged();
        }
    }

    internal bool CanMove(string? sourceId, string? targetId)
    {
        if (sourceId is null || targetId is null || !CanDrag(sourceId) || IsPending(targetId) ||
            !_nodes.TryGetValue(targetId, out var target) || !target.Item.CanAcceptDrop) return false;
        var source = _nodes[sourceId];
        if (source.ParentId == targetId) return false;
        for (var id = targetId; id is not null; id = _nodes[id].ParentId)
            if (id == sourceId) return false;
        return true;
    }

    public async Task MoveAsync(string sourceId, string targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        if (_disposed) return;
        Refresh();
        if (!CanMove(sourceId, targetId)) return;
        var source = _nodes[sourceId];
        var target = _nodes[targetId];
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        _drop = new(source, target, cancellation);
        _drag = null;
        _status = $"Moving {source.Item.Text}...";
        StateHasChanged();
        var completed = false;
        try
        {
            var args = new FsTreeDropEventArgs<TItem>(Items, source.Item,
                source.ParentId is null ? null : _nodes[source.ParentId].Item, target.Item,
                source.Index, target.Item.Children.Count, token);
            await ItemDropped.InvokeAsync(args);
            completed = true;
            if (_disposed) return;
            Refresh();
            if (token.IsCancellationRequested) { _status = "Move cancelled."; return; }
            var applied = _nodes.TryGetValue(sourceId, out var current) && current.ParentId == targetId;
            _status = applied ? $"Moved {source.Item.Text} to {target.Item.Text}." : "No move applied.";
            if (applied)
            {
                var visible = VisibleNodes();
                SetFocus(visible.Any(n => n.Item.Id == sourceId) ? sourceId :
                    visible.LastOrDefault(n => n.Item.Id == targetId || IsAncestor(n.Item.Id, targetId))?.Item.Id, false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            completed = true;
            _status = "Move cancelled.";
        }
        finally
        {
            if (!completed) _status = "Move failed.";
            _drop = null;
            if (!_disposed) { Refresh(); StateHasChanged(); }
        }
    }

    private bool IsAncestor(string ancestor, string id)
    {
        for (string? current = id; current is not null; current = _nodes[current].ParentId)
            if (current == ancestor) return true;
        return false;
    }

    private void MoveSourceChanged(ChangeEventArgs args) { _moveSource = args.Value?.ToString(); _moveTarget = null; }
    private Task MoveChosenAsync() => CanMove(_moveSource, _moveTarget) ? MoveAsync(_moveSource!, _moveTarget!) : Task.CompletedTask;

    [JSInvokable]
    public bool BeginTreeDrag(string id, string session)
    {
        if (_disposed) return false;
        Refresh();
        if (!CanDrag(id)) return false;
        _drag = new(_nodes[id], session);
        return true;
    }

    [JSInvokable]
    public Task EndTreeDragAsync(string session)
    {
        if (_drag?.Session == session) _drag = null;
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task DropTreeAsync(string sourceId, string targetId, string session)
    {
        if (_disposed) return Task.CompletedTask;
        Refresh();
        var valid = _drag is not null && _drag.Session == session && _drag.Source.Item.Id == sourceId && SamePosition(_drag.Source);
        _drag = null;
        return valid ? BrowserActionAsync(() => MoveAsync(sourceId, targetId)) : Task.CompletedTask;
    }

    private async Task BrowserActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception error)
        {
            // JS-invoked callbacks do not automatically reach an enclosing ErrorBoundary.
            await DispatchExceptionAsync(error);
        }
    }

    internal void OnNodeFocus(string id)
    {
        if (!_disposed && !Disabled && _nodes.ContainsKey(id) && _focusId != id)
        {
            _focusId = id;
            StateHasChanged();
        }
    }

    private void SetFocus(string? id, bool force)
    {
        if (id is null) return;
        _focusId = _focusAfterRender = id;
        _forceFocus |= force;
    }

    [JSInvokable]
    public Task TreeKeyAsync(string id, string key) => BrowserActionAsync(() => HandleKeyAsync(id, key));

    private async Task HandleKeyAsync(string id, string key)
    {
        if (_disposed || Disabled) return;
        Refresh();
        var visible = VisibleNodes();
        var index = visible.FindIndex(n => n.Item.Id == id);
        if (index < 0) return;
        var node = visible[index];
        switch (key)
        {
            case "ArrowDown": SetFocus(visible[Math.Min(index + 1, visible.Count - 1)].Item.Id, true); break;
            case "ArrowUp": SetFocus(visible[Math.Max(index - 1, 0)].Item.Id, true); break;
            case "Home": SetFocus(visible[0].Item.Id, true); break;
            case "End": SetFocus(visible[^1].Item.Id, true); break;
            case "ArrowRight":
                if (!IsExpanded(id)) await RequestExpansionAsync(id, true);
                else if (node.Item.Children.Count > 0) SetFocus(node.Item.Children[0].Id, true);
                break;
            case "ArrowLeft":
                if (IsExpanded(id)) await RequestExpansionAsync(id, false);
                else SetFocus(node.ParentId, true);
                break;
            case "Enter": case " ": await ActivateAsync(id); break;
            default:
                if (key.Length == 1 && !char.IsControl(key[0]))
                {
                    var now = Environment.TickCount64;
                    _search = now - _searchTime > 700 ? key : _search + key;
                    _searchTime = now;
                    var search = _search.All(c => c == _search[0]) ? key : _search;
                    var match = Enumerable.Range(1, visible.Count).Select(offset => visible[(index + offset) % visible.Count])
                        .FirstOrDefault(n => CultureInfo.CurrentCulture.CompareInfo.IsPrefix(n.Item.Text, search, CompareOptions.IgnoreCase));
                    SetFocus(match?.Item.Id, true);
                }
                break;
        }
        if (!_disposed) StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/FlintSoft.Components/tree.js");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            _reference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("attachTree", _root, _reference);
        }
        if (_disposed) return;
        foreach (var pair in _completions.ToArray())
        {
            _completions.Remove(pair.Key);
            if (_nodes.TryGetValue(pair.Key, out var node) && ReferenceEquals(node.Item, pair.Value.Item) && IsExpanded(pair.Key) == pair.Value.Expanded)
                await ExpandedChanged.InvokeAsync(new(pair.Value.Item, pair.Value.Expanded));
        }
        if (_focusAfterRender is not null && _module is not null && !Disabled)
        {
            var id = _focusAfterRender;
            var force = _forceFocus;
            _focusAfterRender = null;
            _forceFocus = false;
            if (_nodes.ContainsKey(id)) await _module.InvokeVoidAsync("focusTreeItem", _root, DomId(id), force);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var request in _requests.Values) request.Cancellation.Cancel();
        _drop?.Cancellation.Cancel();
        _drag = null;
        _completions.Clear();
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("detachTree", _root);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The module also cleans up when its root leaves the DOM.
            }
        }
        _reference?.Dispose();
    }
}

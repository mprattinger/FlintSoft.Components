using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FlintSoft.Components;

public partial class FsAutocomplete<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<FsAutocomplete<TItem>> Logger { get; set; } = default!;

    [Parameter, EditorRequired] public Func<string, CancellationToken, Task<IReadOnlyList<TItem>>> SearchAsync { get; set; } = default!;
    [Parameter, EditorRequired] public Func<TItem, string> GetText { get; set; } = default!;
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public EventCallback<TItem> ItemSelected { get; set; }
    [Parameter] public Func<string, TItem>? CreateItemFromText { get; set; }
    [Parameter] public bool AllowNewValues { get; set; }
    [Parameter] public int MinimumLength { get; set; } = 1;
    [Parameter] public int DebounceMilliseconds { get; set; } = 250;
    [Parameter] public string LoadingText { get; set; } = "Loading suggestions...";
    [Parameter] public string NoResultsText { get; set; } = "No suggestions found.";
    [Parameter] public string SearchErrorText { get; set; } = "Suggestions could not be loaded. Please try again.";

    private ElementReference _input;
    private IJSObjectReference? _module;
    private DotNetObjectReference<FsAutocomplete<TItem>>? _reference;
    private CancellationTokenSource? _search;
    private IReadOnlyList<TItem> _items = [];
    private string _text = "";
    private string _committedText = "";
    private TItem? _committedValue;
    private string? _searchError;
    private int _activeIndex = -1;
    private long _request;
    private bool _initialized;
    private bool _focused;
    private bool _open;
    private bool _loading;
    private bool _disposed;
    private bool _asyncDisposed;

    private string ListId => $"{ControlId}-suggestions";
    private string OptionId(int index) => $"{ListId}-{index}";
    private string? ActiveId => _open && _activeIndex >= 0 ? OptionId(_activeIndex) : null;
    private string? InvalidAttribute => EditContext?.GetValidationMessages(FieldIdentifier).Any() == true ? "true" : null;
    private string? DescribedBy
    {
        get
        {
            var ids = new List<string>();
            if (AdditionalAttributes?.TryGetValue("aria-describedby", out var description) == true)
                ids.Add(description?.ToString() ?? "");
            if (HelpText is not null) ids.Add($"{ControlId}-help");
            if (InvalidAttribute is not null) ids.Add($"{ControlId}-error");
            return ids.Count > 0 ? string.Join(" ", ids) : null;
        }
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ArgumentNullException.ThrowIfNull(SearchAsync);
        ArgumentNullException.ThrowIfNull(GetText);
        ArgumentOutOfRangeException.ThrowIfNegative(MinimumLength);
        ArgumentOutOfRangeException.ThrowIfNegative(DebounceMilliseconds);
        if (AllowNewValues && CreateItemFromText is null && typeof(TItem) != typeof(string))
            throw new InvalidOperationException($"{nameof(CreateItemFromText)} is required when {nameof(AllowNewValues)} is enabled for {typeof(TItem).Name}.");
        var valueText = Value is null ? "" : GetText(Value);
        if (!_initialized || !EqualityComparer<TItem?>.Default.Equals(Value, _committedValue) || valueText != _committedText)
        {
            _initialized = true;
            _committedValue = Value;
            _committedText = valueText;
            _text = valueText;
            CloseSuggestions();
        }
        if (Disabled || ReadOnly)
        {
            _text = _committedText;
            CloseSuggestions();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        SetControlElement(_input);
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/FlintSoft.Components/flintsoft.js");
            if (_disposed)
            {
                await _module.DisposeAsync();
                return;
            }
            _reference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("attachAutocomplete", _input, _reference);
        }
        if (_open && _activeIndex >= 0 && _module is not null)
            await _module.InvokeVoidAsync("scrollAutocompleteOption", _input);
    }

    private async Task InputAsync(ChangeEventArgs args)
    {
        if (Disabled || ReadOnly) return;
        _text = args.Value?.ToString() ?? "";
        await FindSuggestionsAsync();
    }

    private async Task HandleFocusAsync()
    {
        _focused = true;
        if (!Disabled && !ReadOnly) await FindSuggestionsAsync();
    }

    private async Task FindSuggestionsAsync()
    {
        CloseSuggestions();
        if (_disposed || !_focused || Disabled || ReadOnly || _text.Length < MinimumLength) return;
        var cancellation = new CancellationTokenSource();
        _search = cancellation;
        var request = _request;
        var query = _text;
        _open = true;
        _loading = true;
        try
        {
            await Task.Delay(DebounceMilliseconds, cancellation.Token);
            var items = await SearchAsync(query, cancellation.Token);
            if (_disposed || request != _request || cancellation.IsCancellationRequested) return;
            _items = items ?? throw new InvalidOperationException("SearchAsync must return a non-null suggestion list.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (_disposed || request != _request) return;
            Logger.LogError(exception, "Autocomplete suggestion search failed.");
            _searchError = SearchErrorText;
        }
        finally
        {
            if (!_disposed && request == _request)
            {
                _loading = false;
                _search = null;
            }
            cancellation.Dispose();
        }
    }

    private async Task KeyDownAsync(KeyboardEventArgs args)
    {
        if (Disabled || ReadOnly) return;
        switch (args.Key)
        {
            case "ArrowDown":
            case "ArrowUp":
                if (!_open)
                {
                    var search = FindSuggestionsAsync();
                    var request = _request;
                    await search;
                    if (!_open || _request != request) return;
                }
                if (_items.Count > 0)
                {
                    _activeIndex = args.Key == "ArrowDown"
                        ? (_activeIndex + 1) % _items.Count
                        : (_activeIndex <= 0 ? _items.Count - 1 : _activeIndex - 1);
                }
                break;
            case "Enter":
                if (_open && _activeIndex >= 0) await SelectAsync(_activeIndex);
                else if (AllowNewValues) CommitText();
                break;
            case "Escape":
                CloseSuggestions();
                break;
        }
    }

    private async Task SelectAsync(int index)
    {
        if (Disabled || ReadOnly || !_open || index < 0 || index >= _items.Count) return;
        var item = _items[index];
        _text = GetText(item);
        CloseSuggestions();
        CurrentValue = item;
        _committedValue = item;
        _committedText = _text;
        await ItemSelected.InvokeAsync(item);
    }

    private void CommitText()
    {
        CloseSuggestions();
        var item = CreateItemFromText is not null
            ? CreateItemFromText(_text)
            : (TItem)(object)_text;
        CurrentValue = item;
        _committedValue = item;
        _committedText = GetText(item);
        _text = _committedText;
    }

    private Task BlurAsync()
    {
        _focused = false;
        if (!Disabled && !ReadOnly && AllowNewValues) CommitText();
        else
        {
            _text = _committedText;
            CloseSuggestions();
        }
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task DismissSuggestionsAsync() => InvokeAsync(() =>
    {
        if (_disposed) return;
        CloseSuggestions();
        StateHasChanged();
    });

    private void CloseSuggestions()
    {
        _request++;
        _search?.Cancel();
        _search = null;
        _open = false;
        _loading = false;
        _items = [];
        _activeIndex = -1;
        _searchError = null;
    }

    protected override bool TryParse(string? value, out TItem? result,
        [NotNullWhen(false)] out string? validationErrorMessage)
    {
        result = default;
        validationErrorMessage = "Select a suggestion to set the value.";
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        CloseSuggestions();
        base.Dispose(disposing);
    }

    public async ValueTask DisposeAsync()
    {
        if (_asyncDisposed) return;
        _asyncDisposed = true;
        ((IDisposable)this).Dispose();
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("detachAutocomplete", _input);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The DOM removal observer cleans up browser listeners after a circuit disconnect.
            }
        }
        _reference?.Dispose();
    }
}

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FlintSoft.Components;

public sealed class FsMarkdownEditor : FsInputBase<string?>, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<FsMarkdownEditor> Logger { get; set; } = default!;

    [Parameter] public RenderFragment? Toolbar { get; set; }
    [Parameter] public Func<FsMarkdownPaste, Task<string>>? ProcessPasteAsync { get; set; }
    [Parameter] public int Rows { get; set; } = 8;
    [Parameter] public long MaxPasteSize { get; set; } = 10 * 1024 * 1024;
    [Parameter] public string PasteErrorText { get; set; } = "The pasted image could not be inserted.";

    private static readonly (string Action, string Text, string Label)[] Actions =
    [
        ("bold", "B", "Bold"), ("italic", "I", "Italic"),
        ("heading-1", "H1", "Heading 1"), ("heading-2", "H2", "Heading 2"),
        ("heading-3", "H3", "Heading 3"), ("list", "*", "Bulleted list"),
        ("numbered-list", "1.", "Numbered list"), ("code", "</>", "Code block"),
        ("link", "Link", "Link"), ("quote", "Quote", "Quote")
    ];

    private ElementReference _textarea;
    private IJSObjectReference? _module;
    private DotNetObjectReference<FsMarkdownEditor>? _reference;
    private string? _pasteError;
    private bool _disposed;
    private bool _asyncDisposed;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxPasteSize);
    }

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fs-markdown-toolbar");
        builder.AddAttribute(2, "role", "group");
        builder.AddAttribute(3, "aria-label", "Markdown formatting");
        if (Toolbar is not null) builder.AddContent(4, Toolbar);
        else
        {
            foreach (var (action, text, label) in Actions)
            {
                builder.OpenElement(5, "button");
                builder.SetKey(action);
                builder.AddAttribute(6, "type", "button");
                builder.AddAttribute(7, "data-fs-markdown-action", action);
                builder.AddAttribute(8, "aria-label", label);
                builder.AddAttribute(9, "title", label);
                builder.AddAttribute(10, "disabled", Disabled || ReadOnly);
                builder.AddContent(11, text);
                builder.CloseElement();
            }
        }
        builder.CloseElement();
        builder.OpenElement(12, "textarea");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "rows", Rows);
        builder.AddAttribute(39, "placeholder", " ");
        builder.AddAttribute(40, "data-fs-paste-handler", ProcessPasteAsync is not null ? "true" : "false");
        builder.AddAttribute(50, "value", CurrentValueAsString);
        builder.AddAttribute(51, "oninput", EventCallback.Factory.CreateBinder<string?>(this, SetInputText, CurrentValueAsString));
        builder.SetUpdatesAttributeName("value");
        builder.AddElementReferenceCapture(60, element => { _textarea = element; SetControlElement(element); });
        builder.CloseElement();
        if (_pasteError is not null)
        {
            builder.OpenElement(61, "div");
            builder.AddAttribute(62, "class", "fs-validation");
            builder.AddAttribute(63, "role", "alert");
            builder.AddContent(64, _pasteError);
            builder.CloseElement();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/FlintSoft.Components/markdown-editor.js");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            _reference = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("attach", _textarea, _reference);
        }
        else if (!_disposed && _module is not null)
            await _module.InvokeVoidAsync("sync", _textarea);
    }

    public Task BoldAsync() => ApplyAsync("bold");
    public Task ItalicAsync() => ApplyAsync("italic");
    public Task HeadingAsync(int level)
    {
        if (level is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(level));
        return ApplyAsync($"heading-{level}");
    }
    public Task BulletedListAsync() => ApplyAsync("list");
    public Task NumberedListAsync() => ApplyAsync("numbered-list");
    public Task CodeAsync() => ApplyAsync("code");
    public Task LinkAsync() => ApplyAsync("link");
    public Task QuoteAsync() => ApplyAsync("quote");

    private async Task ApplyAsync(string action)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FsMarkdownEditor));
        if (Disabled || ReadOnly) return;
        if (_module is null) throw new InvalidOperationException("The editor must be rendered interactively before formatting.");
        await _module.InvokeVoidAsync("apply", _textarea, action);
    }

    [JSInvokable]
    public async Task<string> ProcessImagePasteAsync(IJSStreamReference image, string contentType, string? fileName)
    {
        await using (image)
        {
            var handler = ProcessPasteAsync;
            if (_disposed || Disabled || ReadOnly || handler is null)
                throw new InvalidOperationException("Image paste is no longer available.");
            await using var stream = await image.OpenReadStreamAsync(MaxPasteSize);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            if (_disposed || Disabled || ReadOnly || ProcessPasteAsync != handler)
                throw new InvalidOperationException("Image paste is no longer available.");
            var path = await handler(new FsMarkdownPaste(buffer.ToArray(), contentType, fileName));
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("The paste handler must return a non-empty image path.");
            return path;
        }
    }

    [JSInvokable]
    public Task ReportPasteErrorAsync(string message) => InvokeAsync(() =>
    {
        if (_disposed) return;
        Logger.LogError("Markdown image paste failed: {Message}", message);
        _pasteError = PasteErrorText;
        StateHasChanged();
    });

    [JSInvokable]
    public Task ClearPasteErrorAsync() => InvokeAsync(() =>
    {
        if (_disposed || _pasteError is null) return;
        _pasteError = null;
        StateHasChanged();
    });

    protected override bool TryParse(string? value, out string? result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
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
                await _module.InvokeVoidAsync("detach", _textarea);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
        _reference?.Dispose();
    }
}

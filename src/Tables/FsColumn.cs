using Microsoft.AspNetCore.Components;

namespace FlintSoft.Components;

public sealed record FsColumn<TItem>(string Header, RenderFragment<TItem> Cell,
    string? Class = null, RenderFragment? HeaderTemplate = null);

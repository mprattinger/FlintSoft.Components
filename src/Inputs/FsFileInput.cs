using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsFileInput : ComponentBase
{
    private readonly string _generatedId = $"fs-{Guid.NewGuid():N}";

    [Parameter] public string? Id { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string? HelpText { get; set; }
    [Parameter] public string? Prefix { get; set; }
    [Parameter] public string? Suffix { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Multiple { get; set; }
    [Parameter] public string? Accept { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnChange { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string ControlId => Id ??
        (AdditionalAttributes?.TryGetValue("id", out var id) == true ? id?.ToString() : null) ??
        _generatedId;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fs-field fs-float");
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "fs-input-row");
        if (Prefix is not null)
        {
            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "class", "fs-affix");
            builder.AddContent(6, Prefix);
            builder.CloseElement();
        }
        builder.OpenElement(7, "div");
        builder.AddAttribute(8, "class", "fs-input-control");
        // Render the Blazor InputFile component so JS interop and IBrowserFile wiring work correctly
        builder.OpenComponent<InputFile>(9);
        builder.AddMultipleAttributes(10, AdditionalAttributes);
        builder.AddAttribute(11, "id", ControlId);
        builder.AddAttribute(12, "class", $"fs-input");
        builder.AddAttribute(13, "disabled", Disabled);
        if (Multiple)
            builder.AddAttribute(14, "multiple", true);
        if (Accept is not null)
            builder.AddAttribute(15, "accept", Accept);
        if (HelpText is not null)
            builder.AddAttribute(16, "aria-describedby", $"{ControlId}-help");
        builder.AddAttribute(17, "OnChange", OnChange);
        builder.CloseComponent();
        // Floating label
        builder.OpenElement(18, "label");
        builder.AddAttribute(19, "for", ControlId);
        builder.AddContent(20, Label);
        builder.CloseElement();
        builder.CloseElement(); // fs-input-control
        if (Suffix is not null)
        {
            builder.OpenElement(21, "span");
            builder.AddAttribute(22, "class", "fs-affix");
            builder.AddContent(23, Suffix);
            builder.CloseElement();
        }
        builder.CloseElement(); // fs-input-row
        if (HelpText is not null)
        {
            builder.OpenElement(24, "div");
            builder.AddAttribute(25, "id", $"{ControlId}-help");
            builder.AddAttribute(26, "class", "fs-help");
            builder.AddContent(27, HelpText);
            builder.CloseElement();
        }
        builder.CloseElement(); // fs-field
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsCheckbox : FsInputBase<bool>
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    protected override bool IsChoice => true;

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "class", "fs-checkbox");
        builder.AddAttribute(2, "for", ControlId);
        builder.OpenElement(3, "input");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "type", "checkbox");
        builder.AddAttribute(39, "disabled", Disabled || ReadOnly);
        builder.AddAttribute(40, "checked", CurrentValue);
        builder.AddAttribute(41, "onchange", EventCallback.Factory.CreateBinder<bool>(this, value => CurrentValue = value, CurrentValue));
        builder.SetUpdatesAttributeName("checked");
        builder.CloseElement();
        builder.OpenElement(42, "span");
        builder.AddContent(43, ChildContent ?? (content => content.AddContent(0, Label)));
        builder.CloseElement();
        builder.CloseElement();
    }

    protected override bool TryParse(string? value, out bool result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage) =>
        throw new NotSupportedException("Checkbox values are bound as booleans.");
}

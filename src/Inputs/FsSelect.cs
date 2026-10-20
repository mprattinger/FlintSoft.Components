using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsSelect<TValue> : FsInputBase<TValue>
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    protected override bool AlwaysFloat => true;

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "select");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "disabled", Disabled || ReadOnly);
        AddTextBinding(builder);
        builder.AddContent(60, ChildContent);
        builder.CloseElement();
    }

    protected override bool TryParse(string? value, out TValue result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (BindConverter.TryConvertTo(value, CultureInfo.CurrentCulture, out result!))
        {
            validationErrorMessage = null;
            return true;
        }
        validationErrorMessage = $"The {DisplayName ?? Label} field contains an invalid selection.";
        return false;
    }
}

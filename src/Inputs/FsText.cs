using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsText : FsInputBase<string?>
{
    [Parameter] public string Type { get; set; } = "text";

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        if (Type is not ("text" or "password" or "email" or "tel" or "url" or "search"))
            throw new InvalidOperationException($"Unsupported text input type '{Type}'. Use a typed input component.");
        builder.OpenElement(0, "input");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "type", Type);
        builder.AddAttribute(39, "placeholder", " ");
        AddTextBinding(builder);
        builder.CloseElement();
    }

    protected override bool TryParse(string? value, out string? result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null;
        return true;
    }
}

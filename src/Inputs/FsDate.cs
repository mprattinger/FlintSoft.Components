using System.Globalization;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsDate : FsInputBase<DateOnly?>
{
    protected override bool AlwaysFloat => true;

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "type", "date");
        AddTextBinding(builder);
        builder.CloseElement();
    }

    protected override string? FormatValueAsString(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected override bool TryParse(string? value, out DateOnly? result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage)
    {
        var valid = DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed);
        result = valid ? parsed : null;
        validationErrorMessage = valid ? null : $"The {DisplayName ?? Label} field must be a valid date.";
        return valid;
    }
}

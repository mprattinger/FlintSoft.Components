using System.Globalization;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsTime : FsInputBase<TimeOnly?>
{
    protected override bool AlwaysFloat => true;

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "type", "time");
        builder.AddAttribute(39, "step", AdditionalAttributes?.TryGetValue("step", out var step) == true ? step : "any");
        AddTextBinding(builder);
        builder.CloseElement();
    }

    protected override string? FormatValueAsString(TimeOnly? value) => value?.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    protected override bool TryParse(string? value, out TimeOnly? result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage)
    {
        var valid = TimeOnly.TryParseExact(value, ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed);
        result = valid ? parsed : null;
        validationErrorMessage = valid ? null : $"The {DisplayName ?? Label} field must be a valid time.";
        return valid;
    }
}

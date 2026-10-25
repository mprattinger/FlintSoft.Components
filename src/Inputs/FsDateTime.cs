using System.Globalization;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public class FsDateTime : FsInputBase<DateTime?>
{
    protected override bool AlwaysFloat => true;

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "type", "datetime-local");
        builder.AddAttribute(39, "step", AdditionalAttributes?.TryGetValue("step", out var step) == true ? step : "any");
        AddTextBinding(builder);
        builder.CloseElement();
    }

    protected override string? FormatValueAsString(DateTime? value)
    {
        if (value is null)
            return null;

        var dateTime = value.Value;
        return dateTime.Millisecond == 0 && dateTime.Second == 0
            ? dateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
            : dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    protected override bool TryParse(string? value, out DateTime? result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage)
    {
        var valid = DateTime.TryParseExact(value,
            [
                "yyyy-MM-ddTHH:mm",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss.FFFFFFF"
            ],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed);

        result = valid ? parsed : null;
        validationErrorMessage = valid ? null : $"The {DisplayName ?? Label} field must be a valid date and time.";
        return valid;
    }
}

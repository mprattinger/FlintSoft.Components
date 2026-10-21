using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FlintSoft.Components;

public class FsNumber<TValue> : FsInputBase<TValue>
{
    private bool _editing;
    private string? _rawText;
    private TValue? _lastParsedValue;
    protected override string? InputText => _editing ? _rawText : base.InputText;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (_editing && !EqualityComparer<TValue>.Default.Equals(Value, _lastParsedValue))
            _editing = false;
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (type != typeof(int) && type != typeof(long) && type != typeof(short) &&
            type != typeof(decimal) && type != typeof(double) && type != typeof(float))
            throw new InvalidOperationException($"{nameof(FsNumber<TValue>)} does not support {type.Name}.");
    }

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        AddControlAttributes(builder);
        builder.AddAttribute(38, "type", "text");
        builder.AddAttribute(39, "inputmode", "decimal");
        builder.AddAttribute(40, "placeholder", " ");
        AddTextBinding(builder);
        builder.AddAttribute(60, "onblur", EventCallback.Factory.Create<FocusEventArgs>(this, () => _editing = false));
        builder.CloseElement();
    }

    protected override void SetInputText(string? value)
    {
        _rawText = value;
        _editing = UpdateOnInput;
        base.SetInputText(value);
        _lastParsedValue = Value;
    }

    protected override string? FormatValueAsString(TValue? value) =>
        value is IFormattable number ? number.ToString(null, CultureInfo.CurrentCulture) : null;

    protected override bool TryParse(string? value, out TValue result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (BindConverter.TryConvertTo(value, CultureInfo.CurrentCulture, out result!))
        {
            validationErrorMessage = null;
            return true;
        }
        validationErrorMessage = $"The {DisplayName ?? Label} field must be a valid number.";
        return false;
    }
}

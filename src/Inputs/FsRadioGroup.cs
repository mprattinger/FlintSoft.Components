using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public record FsOption<TValue>(TValue Value, string Label, bool Disabled = false);

public class FsRadioGroup<TValue> : FsInputBase<TValue>
{
    [Parameter, EditorRequired] public IReadOnlyList<FsOption<TValue>> Options { get; set; } = [];
    protected override bool IsChoice => true;

    protected override void RenderControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "fieldset");
        builder.AddAttribute(1, "class", "fs-radio-group");
        builder.AddAttribute(2, "disabled", Disabled || ReadOnly);
        builder.OpenElement(3, "legend");
        builder.AddContent(4, Label);
        builder.CloseElement();
        for (var index = 0; index < Options.Count; index++)
        {
            var option = Options[index];
            builder.OpenElement(5, "label");
            builder.AddAttribute(6, "class", "fs-radio");
            builder.OpenElement(7, "input");
            AddControlAttributes(builder);
            builder.AddAttribute(38, "id", $"{ControlId}-{index}");
            builder.AddAttribute(39, "name", ControlId);
            builder.AddAttribute(40, "type", "radio");
            builder.AddAttribute(41, "value", index);
            builder.AddAttribute(42, "disabled", Disabled || ReadOnly || option.Disabled);
            builder.AddAttribute(43, "checked", EqualityComparer<TValue>.Default.Equals(CurrentValue, option.Value));
            builder.AddAttribute(44, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, _ => CurrentValue = option.Value));
            builder.CloseElement();
            builder.AddContent(45, option.Label);
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    protected override bool TryParse(string? value, out TValue result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage) =>
        throw new NotSupportedException("Radio values are bound through their typed options.");
}

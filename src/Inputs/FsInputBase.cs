using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FlintSoft.Components;

public abstract class FsInputBase<TValue> : InputBase<TValue>
{
    private readonly string _generatedId = $"fs-{Guid.NewGuid():N}";
    private string? _standaloneError;

    [Parameter] public string? Id { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string? HelpText { get; set; }
    [Parameter] public string? Prefix { get; set; }
    [Parameter] public string? Suffix { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public bool UpdateOnInput { get; set; }

    protected string ControlId => Id ??
        (AdditionalAttributes?.TryGetValue("id", out var id) == true ? id?.ToString() : null) ??
        _generatedId;
    protected virtual bool IsChoice => false;
    protected virtual bool AlwaysFloat => false;
    protected virtual string? InputText => CurrentValueAsString;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var messages = EditContext?.GetValidationMessages(FieldIdentifier).ToArray() ??
            (_standaloneError is null ? [] : new[] { _standaloneError });
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", $"fs-field {(IsChoice ? "fs-choice-field" : "")} {(AlwaysFloat ? "fs-float" : "")} {(messages.Length > 0 ? "fs-has-error" : "")}");
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
        RenderControl(builder);
        if (!IsChoice)
        {
            builder.OpenElement(9, "label");
            builder.AddAttribute(10, "for", ControlId);
            builder.AddContent(11, Label);
            builder.CloseElement();
        }
        builder.CloseElement();
        if (Suffix is not null)
        {
            builder.OpenElement(12, "span");
            builder.AddAttribute(13, "class", "fs-affix");
            builder.AddContent(14, Suffix);
            builder.CloseElement();
        }
        builder.CloseElement();
        if (HelpText is not null)
        {
            builder.OpenElement(15, "div");
            builder.AddAttribute(16, "id", $"{ControlId}-help");
            builder.AddAttribute(17, "class", "fs-help");
            builder.AddContent(18, HelpText);
            builder.CloseElement();
        }
        if (messages.Length > 0)
        {
            builder.OpenElement(19, "div");
            builder.AddAttribute(20, "id", $"{ControlId}-error");
            builder.AddAttribute(21, "class", "fs-validation");
            builder.AddAttribute(22, "aria-live", "polite");
            foreach (var message in messages)
            {
                builder.OpenElement(23, "div");
                builder.AddContent(24, message);
                builder.CloseElement();
            }
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    protected abstract void RenderControl(RenderTreeBuilder builder);

    protected void AddControlAttributes(RenderTreeBuilder builder)
    {
        builder.AddMultipleAttributes(30, AdditionalAttributes);
        builder.AddAttribute(31, "id", ControlId);
        builder.AddAttribute(32, "class", $"fs-input {CssClass}");
        builder.AddAttribute(33, "disabled", Disabled);
        builder.AddAttribute(34, "readonly", ReadOnly);
        var invalid = _standaloneError is not null || EditContext?.GetValidationMessages(FieldIdentifier).Any() == true;
        builder.AddAttribute(35, "aria-invalid", invalid ? "true" : null);
        var descriptions = new List<string>();
        if (AdditionalAttributes?.TryGetValue("aria-describedby", out var describedBy) == true)
            descriptions.Add(Convert.ToString(describedBy, CultureInfo.InvariantCulture) ?? "");
        if (HelpText is not null) descriptions.Add($"{ControlId}-help");
        if (invalid) descriptions.Add($"{ControlId}-error");
        builder.AddAttribute(36, "aria-describedby", descriptions.Count > 0 ? string.Join(" ", descriptions) : null);
        builder.AddAttribute(37, "name", NameAttributeValue);
    }

    protected void AddTextBinding(RenderTreeBuilder builder)
    {
        builder.AddAttribute(50, "value", InputText);
        builder.AddAttribute(51, UpdateOnInput ? "oninput" : "onchange",
            EventCallback.Factory.CreateBinder<string?>(this, SetInputText, InputText));
        builder.SetUpdatesAttributeName("value");
    }

    protected virtual void SetInputText(string? value)
    {
        _standaloneError = null;
        CurrentValueAsString = value;
    }

    protected sealed override bool TryParseValueFromString(string? value, out TValue result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage)
    {
        var valid = TryParse(value, out result, out validationErrorMessage);
        if (EditContext is null) _standaloneError = validationErrorMessage;
        return valid;
    }

    protected abstract bool TryParse(string? value, out TValue result,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationErrorMessage);
}

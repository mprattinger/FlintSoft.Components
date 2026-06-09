using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FlintSoft.Components.Inputs;

public abstract class InputBase<TValue> : MyComponentBase, IDisposable
{
    internal readonly string UnknownBoundField = "(unknown)";

    private readonly EventHandler<ValidationStateChangedEventArgs> _validationStateChangedHandler;
    private bool _parsingFailed;
    private string? _incomingValueBeforeParsing;
    private ValidationMessageStore? _parsingValidationMessages;
    private Type? _nullableUnderlyingType;
    private bool _previousParsingAttemptFailed;
    private bool _hasInitializedParameters;

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Name { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public virtual string? AriaLabel { get; set; }

    [Parameter]
    public bool Required { get; set; }

    [Parameter]
    public virtual TValue? Value { get; set; }

    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    [Parameter]
    public FieldIdentifier? Field { get; set; }

    [Parameter]
    public virtual bool Autofocus { get; set; } = false;

    [Parameter]
    public virtual string? Placeholder { get; set; }

    [Parameter]
    public Expression<Func<TValue>>? ValidationFor { get; set; }

    [Parameter]
    public bool Hidden { get; set; }

    protected EditContext EditContext { get; set; } = default!;

    protected internal FieldIdentifier FieldIdentifier { get; set; }

    internal virtual bool FieldBound => Field is not null || ValueExpression is not null || ValueChanged.HasDelegate;

    protected virtual bool HasError => FieldBound && EditContext is not null && EditContext.GetValidationMessages(FieldIdentifier).Any();

    protected virtual string ErrorCssClass => HasError ? "has-error" : "";

    protected async Task SetCurrentValueAsync(TValue? value)
    {
        var hasChanged = !EqualityComparer<TValue>.Default.Equals(value, Value);
        if (!hasChanged)
        {
            return;

        }
        _parsingFailed = false;

        Value = value;
        if (ValueChanged.HasDelegate)
        {
            // Thread Safety: Force `ValueChanged` to be re-associated with the Dispatcher, prior to invocation.
            await InvokeAsync(async () => await ValueChanged.InvokeAsync(value));
        }
        if (FieldBound)
        {
            // Thread Safety: Force `EditContext` to be re-associated with the Dispatcher
            await InvokeAsync(() => EditContext?.NotifyFieldChanged(FieldIdentifier));
        }
    }

    protected virtual string HiddenCssClass => Hidden ? "hidden" : "";

    protected TValue? CurrentValue
    {
        get => Value;
        set => _ = SetCurrentValueAsync(value);
    }

    protected string? CurrentValueAsString
    {
        // InputBase-derived components can hold invalid states (e.g., an InputNumber being blank even when bound
        // to an int value). So, if parsing fails, we keep the rejected string in the UI even though it doesn't
        // match what's on the .NET model. This avoids interfering with typing, but still notifies the EditContext
        // about the validation error message.
        get => _parsingFailed ? _incomingValueBeforeParsing : FormatValueAsString(CurrentValue);
        set => _ = SetCurrentValueAsStringAsync(value);

    }

    protected async Task SetCurrentValueAsStringAsync(string? value)
    {
        _incomingValueBeforeParsing = value;
        _parsingValidationMessages?.Clear();

        if (_nullableUnderlyingType != null && string.IsNullOrEmpty(value))
        {
            // Assume if it's a nullable type, null/empty inputs should correspond to default(T)
            // Then all subclasses get nullable support almost automatically (they just have to
            // not reject Nullable<T> based on the type itself).
            _parsingFailed = false;
            CurrentValue = default!;
        }
        else if (TryParseValueFromString(value, out var parsedValue, out var validationErrorMessage))
        {
            _parsingFailed = false;
            await SetCurrentValueAsync(parsedValue);
        }
        else
        {
            _parsingFailed = true;

            // EditContext may be null if the input is not a child component of EditForm.
            if (EditContext is not null && FieldBound)
            {
                _parsingValidationMessages ??= new ValidationMessageStore(EditContext);
                _parsingValidationMessages.Add(FieldIdentifier, validationErrorMessage);

                // Since we're not writing to CurrentValue, we'll need to notify about modification from here
                EditContext.NotifyFieldChanged(FieldIdentifier);
            }
        }

        // We can skip the validation notification if we were previously valid and still are
        if (_parsingFailed || _previousParsingAttemptFailed)
        {
            EditContext?.NotifyValidationStateChanged();
            _previousParsingAttemptFailed = _parsingFailed;
        }
    }

    protected InputBase()
    {
        Id = Guid.CreateVersion7().ToString();
        _validationStateChangedHandler = OnValidateStateChanged;
    }

    protected virtual string? FormatValueAsString(TValue? value)
        => value?.ToString();

    protected abstract bool TryParseValueFromString(string? value, [MaybeNullWhen(false)] out TValue result, [NotNullWhen(false)] out string? validationErrorMessage);

    protected override string? ClassValue
    {
        get
        {
            return null;
        }
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);

        if (!_hasInitializedParameters)
        {
            // This is the first run
            // Could put this logic in OnInit, but its nice to avoid forcing people who override OnInit to call base.OnInit()

            if (Field is not null)
            {
                FieldIdentifier = (FieldIdentifier)Field;
            }
            else if (ValueExpression is not null)
            {
                FieldIdentifier = FieldIdentifier.Create(ValueExpression);
            }
            else if (ValueChanged.HasDelegate)
            {
                FieldIdentifier = FieldIdentifier.Create(() => Value);
            }

            if (CascadedEditContext != null)
            {
                EditContext = CascadedEditContext;
                EditContext.OnValidationStateChanged += _validationStateChangedHandler;
            }

            _nullableUnderlyingType = Nullable.GetUnderlyingType(typeof(TValue));
            _hasInitializedParameters = true;
        }
        else if (CascadedEditContext != EditContext)
        {
            // Not the first run

            // We don't support changing EditContext because it's messy to be clearing up state and event
            // handlers for the previous one, and there's no strong use case. If a strong use case
            // emerges, we can consider changing this.
            throw new InvalidOperationException($"{GetType()} does not support changing the " +
                $"{nameof(Microsoft.AspNetCore.Components.Forms.EditContext)} dynamically.");
        }

        UpdateAdditionalValidationAttributes();

        // For derived components, retain the usual lifecycle with OnInit/OnParametersSet/etc.
        return base.SetParametersAsync(ParameterView.Empty);
    }

    /// <summary>
    /// Handler for the OnChange event.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected virtual async Task ChangeHandlerAsync(ChangeEventArgs e)
    {
        var _notifyCalled = false;
        var isValid = TryParseValueFromString(e.Value?.ToString(), out TValue? result, out var validationErrorMessage);

        if (isValid)
        {
            await SetCurrentValueAsync(result ?? default);
            _notifyCalled = true;

            if (FieldBound && CascadedEditContext != null)
            {
                _parsingValidationMessages?.Clear(); // Clear any previous errors
            }
        }
        else
        {
            if (FieldBound && CascadedEditContext != null)
            {
                _parsingValidationMessages ??= new ValidationMessageStore(CascadedEditContext);

                _parsingValidationMessages.Clear();
                _parsingValidationMessages.Add(FieldIdentifier, validationErrorMessage ?? "Unknown parsing error");
            }
        }
        if (FieldBound && !_notifyCalled)
        {
            CascadedEditContext?.NotifyFieldChanged(FieldIdentifier);
        }
    }

    protected virtual async Task ChangeHandlerAsync(TValue e)
    {
        var _notifyCalled = false;
        var isValid = TryParseValueFromString(e?.ToString(), out TValue? result, out var validationErrorMessage);

        if (isValid)
        {
            await SetCurrentValueAsync(result ?? default);
            _notifyCalled = true;

            if (FieldBound && CascadedEditContext != null)
            {
                _parsingValidationMessages?.Clear(); // Clear any previous errors
            }
        }
        else
        {
            if (FieldBound && CascadedEditContext != null)
            {
                _parsingValidationMessages ??= new ValidationMessageStore(CascadedEditContext);

                _parsingValidationMessages.Clear();
                _parsingValidationMessages.Add(FieldIdentifier, validationErrorMessage ?? "Unknown parsing error");
            }
        }
        if (FieldBound && !_notifyCalled)
        {
            CascadedEditContext?.NotifyFieldChanged(FieldIdentifier);
        }
    }

    /// <summary>
    /// Handler for the OnInput event, with an optional delay to avoid to raise the <see cref="ValueChanged"/> event too often.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected virtual async Task InputHandlerAsync(ChangeEventArgs e) // TODO: To update in all Input fields
    {
        //if (!Immediate)
        //{
        //    return;
        //}

        //if (ImmediateDelay > 0)
        //{
        //    await _debounce.RunAsync(ImmediateDelay, async () => await ChangeHandlerAsync(e));
        //}
        //else
        //{
        //    await ChangeHandlerAsync(e);
        //}
        await ChangeHandlerAsync(e);
    }

    [SuppressMessage("Style", "VSTHRD200:Use `Async` suffix for async methods", Justification = "#vNext: To update in the next version")]
    public virtual async void FocusAsync()
    {
        await Element!.FocusAsync();
    }

    private void OnValidateStateChanged(object? sender, ValidationStateChangedEventArgs eventArgs)
    {
        UpdateAdditionalValidationAttributes();

        InvokeAsync(StateHasChanged);
    }

    private void UpdateAdditionalValidationAttributes()
    {
        //if (EditContext is null)
        //{
        //    return;
        //}

        //var hasAriaInvalidAttribute = AdditionalAttributes != null && AdditionalAttributes.ContainsKey("aria-invalid");
        //if (FieldBound && EditContext.GetValidationMessages(FieldIdentifier).Any())
        //{
        //    if (hasAriaInvalidAttribute)
        //    {
        //        // Do not overwrite the attribute value
        //        return;
        //    }

        //    if (ConvertToDictionary(AdditionalAttributes, out var additionalAttributes))
        //    {
        //        AdditionalAttributes = additionalAttributes;
        //    }

        //    // To make the `Input` components accessible by default
        //    // we will automatically render the `aria-invalid` attribute when the validation fails
        //    // value must be "true" see https://www.w3.org/TR/wai-aria-1.1/#aria-invalid
        //    additionalAttributes["aria-invalid"] = "true";
        //}
        //else if (hasAriaInvalidAttribute)
        //{
        //    // No validation errors. Need to remove `aria-invalid` if it was rendered already

        //    if (AdditionalAttributes!.Count == 1)
        //    {
        //        // Only aria-invalid argument is present which we don't need any more
        //        AdditionalAttributes = null;
        //    }
        //    else
        //    {
        //        if (ConvertToDictionary(AdditionalAttributes, out var additionalAttributes))
        //        {
        //            AdditionalAttributes = additionalAttributes;
        //        }

        //        additionalAttributes.Remove("aria-invalid");
        //    }
        //}
    }

    private static bool ConvertToDictionary(IReadOnlyDictionary<string, object>? source, out Dictionary<string, object> result)
    {
        var newDictionaryCreated = true;
        if (source == null)
        {
            result = [];
        }
        else if (source is Dictionary<string, object> currentDictionary)
        {
            result = currentDictionary;
            newDictionaryCreated = false;
        }
        else
        {
            result = [];
            foreach (var item in source)
            {
                result.Add(item.Key, item.Value);
            }
        }

        return newDictionaryCreated;
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    protected void NotifyFieldChange()
    {
        if (FieldBound)
        {
            EditContext?.NotifyFieldChanged(FieldIdentifier);
        }
    }

    void IDisposable.Dispose()
    {
        // When initialization in the SetParametersAsync method fails, the EditContext property can remain equal to null
        if (EditContext is not null)
        {
            EditContext.OnValidationStateChanged -= _validationStateChangedHandler;
        }

        //_debounce.Dispose();

        Dispose(disposing: true);
    }
}

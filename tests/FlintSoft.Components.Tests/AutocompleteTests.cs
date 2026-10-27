using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class AutocompleteTests : BunitContext
{
    private Contact? _value;
    private string? _stringValue;
    private Contact? _selected;
    private static readonly Contact[] Contacts = [new(1, "Alex", "Vienna"), new(2, "Jamie", "Berlin")];

    public AutocompleteTests()
    {
        var module = JSInterop.SetupModule("./_content/FlintSoft.Components/flintsoft.js");
        module.SetupVoid("attachAutocomplete", _ => true).SetVoidResult();
        module.SetupVoid("detachAutocomplete", _ => true).SetVoidResult();
        module.SetupVoid("scrollAutocompleteOption", _ => true).SetVoidResult();
    }

    private IRenderedComponent<FsAutocomplete<Contact>> Create(
        Func<string, CancellationToken, Task<IReadOnlyList<Contact>>>? search = null,
        bool allowNew = false, int debounce = 0, int minimum = 1,
        Func<string, Contact>? createItemFromText = null) =>
        Render<FsAutocomplete<Contact>>(p => p
            .Add(c => c.Label, "Contact")
            .Add(c => c.Value, _value)
            .Add(c => c.ValueChanged, value => _value = value)
            .Add(c => c.ValueExpression, () => _value)
            .Add(c => c.SearchAsync, search ?? ((_, _) => Task.FromResult<IReadOnlyList<Contact>>(Contacts)))
            .Add(c => c.GetText, item => item.Name)
            .Add(c => c.CreateItemFromText, createItemFromText)
            .Add(c => c.AllowNewValues, allowNew)
            .Add(c => c.DebounceMilliseconds, debounce)
            .Add(c => c.MinimumLength, minimum)
            .Add(c => c.ItemSelected, item => _selected = item));

    [Fact]
    public void KeyboardSelectsTypedObjectsWithoutCommittingWhileTyping()
    {
        var input = Create();
        input.Find("input").Focus();
        input.Find("input").Input("a");
        Assert.Null(_value);
        Assert.Equal(2, input.FindAll("[role=option]").Count);
        Assert.Empty(input.FindAll("[aria-selected=true]"));
        Assert.Equal(input.Find("input").Id, input.Find("label").GetAttribute("for"));
        input.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("Alex", input.Find("[aria-selected=true]").TextContent.Trim());
        Assert.Equal(input.Find("[aria-selected=true]").Id, input.Find("input").GetAttribute("aria-activedescendant"));
        input.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        input.Find("input").KeyDown(Key.Enter);
        Assert.Equal(Contacts[1], _value);
        Assert.Equal(Contacts[1], _selected);
        Assert.Empty(input.FindAll("[role=listbox]"));
    }

    [Fact]
    public async Task RestrictedBlurRestoresPreviousValueAndEscapeIgnoresLateResults()
    {
        _value = Contacts[0];
        var pending = new TaskCompletionSource<IReadOnlyList<Contact>>();
        var input = Create((_, _) => pending.Task);
        input.Find("input").Focus();
        input.Find("input").Input("other");
        input.Find("input").KeyDown(Key.Escape);
        Assert.Empty(input.FindAll("[role=listbox]"));
        await input.InvokeAsync(() => pending.SetResult(Contacts));
        Assert.Empty(input.FindAll("[role=listbox]"));
        input.Find("input").Blur();
        Assert.Equal(Contacts[0], _value);
        input.WaitForAssertion(() => Assert.Equal("Alex", input.Find("input").GetAttribute("value")));
        Assert.Null(_selected);
    }

    [Fact]
    public void FreeTextCommitsOnEnterOrBlurButTabDoesNotChooseHighlightedObject()
    {
        var input = Create(allowNew: true, createItemFromText: text => new Contact(99, text, "New"));
        input.Find("input").Focus();
        input.Find("input").Input("New person");
        Assert.Null(_value);
        input.Find("input").KeyDown(Key.Enter);
        Assert.Equal(new Contact(99, "New person", "New"), _value);
        Assert.Null(_selected);
        input.Find("input").Input("Other name");
        input.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        input.Find("input").KeyDown(Key.Tab);
        input.Find("input").Blur();
        Assert.Equal(new Contact(99, "Other name", "New"), _value);
        Assert.Null(_selected);
    }

    [Fact]
    public void StringItemsCanStillCommitFreeTextDirectly()
    {
        var input = Render<FsAutocomplete<string>>(p => p
            .Add(c => c.Value, _stringValue)
            .Add(c => c.ValueChanged, value => _stringValue = value)
            .Add(c => c.ValueExpression, () => _stringValue)
            .Add(c => c.SearchAsync, (_, _) => Task.FromResult<IReadOnlyList<string>>([]))
            .Add(c => c.GetText, item => item)
            .Add(c => c.AllowNewValues, true)
            .Add(c => c.DebounceMilliseconds, 0));
        input.Find("input").Focus();
        input.Find("input").Input("New person");
        input.Find("input").KeyDown(Key.Enter);
        Assert.Equal("New person", _stringValue);
    }

    [Fact]
    public async Task ObsoleteSearchesAreCanceledAndCannotReplaceNewerSuggestions()
    {
        var old = new TaskCompletionSource<IReadOnlyList<Contact>>();
        CancellationToken oldToken = default;
        var input = Create((text, cancellationToken) =>
        {
            if (text == "old") { oldToken = cancellationToken; return old.Task; }
            return Task.FromResult<IReadOnlyList<Contact>>([Contacts[1]]);
        });
        input.Find("input").Focus();
        input.Find("input").Input("old");
        Assert.Contains("Loading", input.Find("[role=status]").TextContent);
        input.Find("input").Input("new");
        Assert.True(oldToken.IsCancellationRequested);
        Assert.Equal("Jamie", input.Find("[role=option]").TextContent.Trim());
        await input.InvokeAsync(() => old.SetResult([Contacts[0]]));
        Assert.Equal("Jamie", input.Find("[role=option]").TextContent.Trim());
    }

    [Fact]
    public async Task DebounceMinimumLengthAndOutsideDismissalCancelPendingWork()
    {
        var queries = new List<string>();
        var input = Create((text, _) =>
        {
            queries.Add(text);
            return Task.FromResult<IReadOnlyList<Contact>>(Contacts);
        }, debounce: 40, minimum: 2);
        input.Find("input").Focus();
        input.Find("input").Input("a");
        Assert.Empty(input.FindAll("[role=listbox]"));
        input.Find("input").Input("al");
        input.Find("input").Input("ale");
        input.WaitForAssertion(() => Assert.Single(queries));
        Assert.Equal("ale", queries[0]);
        input.Find("input").Input("alex");
        await input.InvokeAsync(() => input.Instance.DismissSuggestionsAsync());
        await Task.Delay(70);
        Assert.Single(queries);
        Assert.Empty(input.FindAll("[role=listbox]"));
    }

    [Fact]
    public void TemplatesAndPointerSelectionUseGetTextForBinding()
    {
        var input = Create();
        input.Render(p => p.Add(c => c.ItemTemplate, item => builder => builder.AddContent(0, $"{item.Name} / {item.City}")));
        input.Find("input").Focus();
        input.Find("input").Input("a");
        Assert.Contains("Alex / Vienna", input.Markup);
        input.FindAll("[role=option]")[0].Click();
        Assert.Equal(Contacts[0], _value);
        Assert.Equal(Contacts[0], _selected);
    }

    [Fact]
    public void EmptyFocusSearchAndUpArrowStartAtLastSuggestion()
    {
        var queries = new List<string>();
        var input = Create((text, _) =>
        {
            queries.Add(text);
            return Task.FromResult<IReadOnlyList<Contact>>(Contacts);
        }, minimum: 0);
        input.Find("input").Focus();
        Assert.Equal("", Assert.Single(queries));
        input.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("Jamie", input.Find("[aria-selected=true]").TextContent.Trim());
        input.Find("input").KeyDown(Key.Enter);
        Assert.Equal(Contacts[1], _value);
    }

    [Fact]
    public void RestrictedTextWithoutSelectionClearsOnBlur()
    {
        var input = Create();
        input.Find("input").Focus();
        input.Find("input").Input("Alex");
        input.Find("input").KeyDown(Key.Enter);
        Assert.Null(_value);
        input.Find("input").Blur();
        input.WaitForAssertion(() => Assert.Equal("", input.Find("input").GetAttribute("value")));
        Assert.Null(_selected);
    }

    [Fact]
    public void SearchErrorsAreShownAndTheNextSearchCanRecover()
    {
        var input = Create((_, _) => throw new InvalidOperationException("Database unavailable"));
        input.Find("input").Focus();
        input.Find("input").Input("a");
        Assert.Contains("could not be loaded", input.Find("[role=alert]").TextContent);
        input.Render(p => p.Add(c => c.SearchAsync, (_, _) => Task.FromResult<IReadOnlyList<Contact>>([])));
        input.Find("input").Input("b");
        Assert.Empty(input.FindAll("[role=alert]"));
        Assert.Contains("No suggestions", input.Find("[role=status]").TextContent);
    }

    [Fact]
    public void ExternalValueChangesAndDisabledStatesResetTheEditor()
    {
        var input = Create();
        input.Find("input").Focus();
        input.Find("input").Input("draft");
        input.Render(p => p.Add(c => c.Value, Contacts[0]));
        Assert.Equal("Alex", input.Find("input").GetAttribute("value"));
        Assert.Empty(input.FindAll("[role=listbox]"));
        input.Render(p => p.Add(c => c.ReadOnly, true));
        input.Find("input").Input("blocked");
        Assert.Equal("Alex", input.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task DisposingCancelsSearchAndSuppressesLateResults()
    {
        CancellationToken token = default;
        var pending = new TaskCompletionSource<IReadOnlyList<Contact>>();
        var input = Create((_, cancellationToken) => { token = cancellationToken; return pending.Task; });
        input.Find("input").Focus();
        input.Find("input").Input("a");
        await input.InvokeAsync(() => input.Instance.DisposeAsync().AsTask());
        Assert.True(token.IsCancellationRequested);
        await input.InvokeAsync(() => pending.SetResult(Contacts));
        Assert.Null(_value);
    }

    [Fact]
    public async Task EditFormValidationUpdatesWhenSuggestionIsCommitted()
    {
        var model = new Profile();
        var editContext = new EditContext(model);
        var form = Render<EditForm>(p => p
            .Add(c => c.EditContext, editContext)
            .Add(c => c.ChildContent, _ => builder =>
            {
                builder.OpenComponent<DataAnnotationsValidator>(0);
                builder.CloseComponent();
                builder.OpenComponent<FsAutocomplete<Contact>>(1);
                builder.AddAttribute(2, "Label", "Contact");
                builder.AddAttribute(3, "Value", model.Customer);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<Contact?>(this, value => model.Customer = value));
                builder.AddAttribute(5, "ValueExpression", (Expression<Func<Contact?>>)(() => model.Customer));
                builder.AddAttribute(6, "SearchAsync", (Func<string, CancellationToken, Task<IReadOnlyList<Contact>>>)((_, _) => Task.FromResult<IReadOnlyList<Contact>>(Contacts)));
                builder.AddAttribute(7, "GetText", (Func<Contact, string>)(item => item.Name));
                builder.AddAttribute(8, "DebounceMilliseconds", 0);
                builder.CloseComponent();
            }));
        await form.InvokeAsync(() => editContext.Validate());
        Assert.Contains("required", form.Find(".fs-validation").TextContent);
        Assert.Equal("true", form.Find("input").GetAttribute("aria-invalid"));
        form.Find("input").Focus();
        form.Find("input").Input("al");
        form.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        form.Find("input").KeyDown(Key.Enter);
        Assert.Equal(Contacts[0], model.Customer);
        Assert.Empty(form.FindAll(".fs-validation"));
        Assert.True(editContext.IsModified(editContext.Field(nameof(Profile.Customer))));
    }

    private sealed record Contact(int Id, string Name, string City);
    private sealed class Profile
    {
        [Required] public Contact? Customer { get; set; }
    }
}

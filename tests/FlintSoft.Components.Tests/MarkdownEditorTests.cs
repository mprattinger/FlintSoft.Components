using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class MarkdownEditorTests : BunitContext
{
    private string? _value;

    public MarkdownEditorTests()
    {
        var module = JSInterop.SetupModule("./_content/FlintSoft.Components/markdown-editor.js");
        module.SetupVoid("attach", _ => true).SetVoidResult();
        module.SetupVoid("detach", _ => true).SetVoidResult();
        module.SetupVoid("sync", _ => true).SetVoidResult();
        module.SetupVoid("apply", _ => true).SetVoidResult();
    }

    [Fact]
    public void DefaultToolbarAndInputBindWithLabelAndHelp()
    {
        var editor = Render<FsMarkdownEditor>(p => p
            .Add(c => c.Label, "Document")
            .Add(c => c.HelpText, "Write here")
            .Add(c => c.Value, _value)
            .Add(c => c.ValueChanged, value => _value = value)
            .Add(c => c.ValueExpression, () => _value));
        Assert.Equal(10, editor.FindAll(".fs-markdown-toolbar button").Count);
        Assert.Equal(editor.Find("textarea").Id, editor.Find("label").GetAttribute("for"));
        Assert.Equal(editor.Find(".fs-help").Id, editor.Find("textarea").GetAttribute("aria-describedby"));
        editor.Find("textarea").Input("**hello**");
        Assert.Equal("**hello**", _value);
        editor.Render(p => p.Add(c => c.Value, "external"));
        Assert.Equal("external", editor.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public async Task CustomToolbarReplacesDefaultsAndFormattingRejectsInvalidHeading()
    {
        var editor = Render<FsMarkdownEditor>(p => p
            .Add(c => c.ValueExpression, () => _value)
            .Add(c => c.Toolbar, builder => builder.AddMarkupContent(0, "<button type=\"button\">Mine</button>")));
        Assert.Single(editor.FindAll(".fs-markdown-toolbar button"));
        Assert.Equal("Mine", editor.Find(".fs-markdown-toolbar button").TextContent);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => editor.InvokeAsync(() => editor.Instance.HeadingAsync(4)));
        await editor.InvokeAsync(() => editor.Instance.BoldAsync());
    }

    [Fact]
    public async Task DisabledAndReadonlyDisableDefaultActions()
    {
        var editor = Render<FsMarkdownEditor>(p => p.Add(c => c.ValueExpression, () => _value).Add(c => c.Disabled, true));
        Assert.NotNull(editor.Find("textarea").GetAttribute("disabled"));
        Assert.All(editor.FindAll(".fs-markdown-toolbar button"), button => Assert.NotNull(button.GetAttribute("disabled")));
        await editor.InvokeAsync(() => editor.Instance.BoldAsync());
        editor.Render(p => p.Add(c => c.Disabled, false).Add(c => c.ReadOnly, true));
        Assert.NotNull(editor.Find("textarea").GetAttribute("readonly"));
        Assert.All(editor.FindAll(".fs-markdown-toolbar button"), button => Assert.NotNull(button.GetAttribute("disabled")));
    }

    [Fact]
    public async Task RequiredValidationAndModificationFollowInput()
    {
        var model = new Model();
        var context = new EditContext(model);
        var form = Render<EditForm>(p => p.Add(c => c.EditContext, context).Add(c => c.ChildContent, _ => builder =>
        {
            builder.OpenComponent<DataAnnotationsValidator>(0);
            builder.CloseComponent();
            builder.OpenComponent<FsMarkdownEditor>(1);
            builder.AddAttribute(2, "Label", "Document");
            builder.AddAttribute(3, "Value", model.Document);
            builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, value => model.Document = value));
            builder.AddAttribute(5, "ValueExpression", (Expression<Func<string?>>)(() => model.Document));
            builder.CloseComponent();
        }));
        await form.InvokeAsync(() => context.Validate());
        Assert.Contains("required", form.Find(".fs-validation").TextContent);
        form.Find("textarea").Input("hello");
        Assert.Equal("hello", model.Document);
        Assert.True(context.IsModified(context.Field(nameof(Model.Document))));
        Assert.Empty(form.FindAll(".fs-validation"));
    }

    private sealed class Model
    {
        [Required] public string? Document { get; set; }
    }
}

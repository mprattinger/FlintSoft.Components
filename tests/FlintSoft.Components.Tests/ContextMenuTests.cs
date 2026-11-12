using Bunit;
using FlintSoft.Components.Icons;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class ContextMenuTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public ContextMenuTests()
    {
        _module = JSInterop.SetupModule("./_content/FlintSoft.Components/flintsoft.js");
        _module.SetupVoid("attachContextMenu", _ => true).SetVoidResult();
        _module.SetupVoid("detachContextMenu", _ => true).SetVoidResult();
        _module.SetupVoid("positionContextMenu", _ => true).SetVoidResult();
    }

    [Fact]
    public void TextTriggerTogglesChildContentAndForwardsAttributes()
    {
        var menu = Render<FsContextMenu>(p => p
            .Add(c => c.Text, "More options")
            .Add(c => c.Class, "custom-menu")
            .AddUnmatched("id", "options")
            .AddChildContent("<a href=\"profile\">View profile</a>"));
        var trigger = menu.Find("button");
        Assert.Contains("custom-menu", menu.Find("#options").ClassList);
        Assert.Equal("button", trigger.GetAttribute("type"));
        Assert.Equal("More options", trigger.TextContent.Trim());
        Assert.Equal("More options", trigger.GetAttribute("aria-label"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.Empty(menu.FindAll("section"));
        trigger.Click();
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.Equal(menu.Find("section").Id, trigger.GetAttribute("aria-controls"));
        Assert.Equal("View profile", menu.Find("section a").TextContent);
        Assert.Equal("More options", menu.Find("section").GetAttribute("aria-label"));
        Assert.Contains(_module.Invocations, call => call.Identifier == "positionContextMenu");
        trigger.Click();
        Assert.Empty(menu.FindAll("section"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Options")]
    public void CustomIconWorksAloneOrWithText(string? text)
    {
        var menu = Render<FsContextMenu>(p => p
            .Add(c => c.Text, text)
            .Add(c => c.AccessibleLabel, "Project actions")
            .Add<Gear>(c => c.Icon)
            .AddChildContent("<p>Custom content</p>"));
        Assert.Equal(text ?? "", menu.Find("button").TextContent.Trim());
        Assert.Equal("Project actions", menu.Find("button").GetAttribute("aria-label"));
        Assert.Equal("true", menu.Find(".fs-context-menu-icon").GetAttribute("aria-hidden"));
        menu.Find("svg").Click();
        Assert.Equal("Custom content", menu.Find("section p").TextContent);
    }

    [Fact]
    public void ChildControlsRemainInteractiveWithoutClosing()
    {
        var clicks = 0;
        var menu = Render<FsContextMenu>(p => p.AddChildContent<FsButton>(button => button
            .Add(c => c.OnClick, () => clicks++)
            .AddChildContent("Save")));
        menu.Find("button").Click();
        menu.Find("section button").Click();
        Assert.Equal(1, clicks);
        Assert.Single(menu.FindAll("section"));
        Assert.Equal("true", menu.Find("button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task DismissalAndDisposalAreIdempotent()
    {
        var menu = Render<FsContextMenu>();
        menu.Find("button").Click();
        await menu.InvokeAsync(() => menu.Instance.DismissContextMenuAsync());
        await menu.InvokeAsync(() => menu.Instance.DismissContextMenuAsync());
        Assert.Empty(menu.FindAll("section"));
        Assert.Equal("false", menu.Find("button").GetAttribute("aria-expanded"));
        await menu.InvokeAsync(async () => await menu.Instance.DisposeAsync());
        await menu.InvokeAsync(async () => await menu.Instance.DisposeAsync());
        Assert.Single(_module.Invocations, call => call.Identifier == "detachContextMenu");
        await menu.InvokeAsync(() => menu.Instance.DismissContextMenuAsync());
        Assert.Empty(menu.FindAll("section"));
    }

    [Fact]
    public void InstancesHaveUniqueIdsAndUsableDefaults()
    {
        var first = Render<FsContextMenu>();
        var second = Render<FsContextMenu>();
        Assert.Equal("Actions", first.Find("button").GetAttribute("aria-label"));
        Assert.Equal("Actions", first.Find("button").TextContent.Trim());
        Assert.NotEqual(first.Find("button").GetAttribute("aria-controls"),
            second.Find("button").GetAttribute("aria-controls"));
        first.Find("button").Click();
        Assert.Single(first.FindAll("section"));
        Assert.Empty(second.FindAll("section"));
    }
}

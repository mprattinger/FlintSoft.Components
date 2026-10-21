using Bunit;
using FlintSoft.Components.Icons;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class SettingsTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public SettingsTests()
    {
        _module = JSInterop.SetupModule("./_content/FlintSoft.Components/flintsoft.js");
        _module.SetupVoid("attachSettings", _ => true).SetVoidResult();
        _module.SetupVoid("detachSettings", _ => true).SetVoidResult();
        _module.SetupVoid("positionSettings", _ => true).SetVoidResult();
    }

    [Fact]
    public void GearTogglesFlyoutAndForwardsAttributes()
    {
        var settings = Render<FsSettings>(p => p
            .Add(c => c.AccessibleLabel, "Preferences")
            .Add(c => c.Class, "custom-settings")
            .AddUnmatched("id", "settings")
            .AddChildContent("<a href=\"profile\">Profile settings</a>"));
        Assert.NotNull(settings.FindComponent<Gear>());
        Assert.Contains("custom-settings", settings.Find("#settings").ClassList);
        Assert.Equal("button", settings.Find("button").GetAttribute("type"));
        Assert.Equal("Preferences", settings.Find("button").GetAttribute("aria-label"));
        Assert.Equal("false", settings.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(settings.FindAll("section"));
        settings.Find("svg").Click();
        Assert.Equal("true", settings.Find("button").GetAttribute("aria-expanded"));
        Assert.Equal(settings.Find("section").Id, settings.Find("button").GetAttribute("aria-controls"));
        Assert.Equal("Preferences", settings.Find("section").GetAttribute("aria-label"));
        Assert.Equal("Profile settings", settings.Find("section a").TextContent);
        settings.Find("button").Click();
        Assert.Empty(settings.FindAll("section"));
    }

    [Fact]
    public void ChildButtonsRemainInteractiveWithoutClosingFlyout()
    {
        var clicks = 0;
        var settings = Render<FsSettings>(p => p.AddChildContent<FsButton>(button => button
            .Add(c => c.OnClick, () => clicks++)
            .AddChildContent("Save")));
        settings.Find(".fs-settings-trigger").Click();
        settings.Find("section button").Click();
        Assert.Equal(1, clicks);
        Assert.Single(settings.FindAll("section"));
    }

    [Fact]
    public async Task DismissalAndDisposalAreIdempotent()
    {
        var settings = Render<FsSettings>();
        settings.Find("button").Click();
        await settings.InvokeAsync(() => settings.Instance.DismissSettingsAsync());
        await settings.InvokeAsync(() => settings.Instance.DismissSettingsAsync());
        Assert.Empty(settings.FindAll("section"));
        await settings.InvokeAsync(async () => await settings.Instance.DisposeAsync());
        await settings.InvokeAsync(async () => await settings.Instance.DisposeAsync());
        Assert.Single(_module.Invocations, call => call.Identifier == "detachSettings");
    }

    [Fact]
    public void InstancesHaveUniqueFlyoutIdsAndDefaultLabels()
    {
        var first = Render<FsSettings>();
        var second = Render<FsSettings>();
        Assert.Equal("Settings", first.Find("button").GetAttribute("aria-label"));
        Assert.NotEqual(first.Find("button").GetAttribute("aria-controls"),
            second.Find("button").GetAttribute("aria-controls"));
    }
}

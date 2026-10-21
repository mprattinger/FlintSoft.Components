using Bunit;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class AdornerTests : BunitContext
{
    [Fact]
    public void HiddenByDefaultAndFollowsLoadingState()
    {
        var adorner = Render<FsAdorner>();
        Assert.Empty(adorner.Markup);
        adorner.Render(p => p.Add(c => c.IsLoading, true));
        Assert.Equal("Loading...", adorner.Find("[role=status]").TextContent.Trim());
        Assert.Equal("polite", adorner.Find("[role=status]").GetAttribute("aria-live"));
        Assert.Equal("true", adorner.Find("[role=status]").GetAttribute("aria-atomic"));
        adorner.Render(p => p.Add(c => c.IsLoading, false));
        Assert.Empty(adorner.Markup);
        adorner.Render(p => p.Add(c => c.IsLoading, true));
        Assert.Single(adorner.FindAll(".fs-adorner"));
    }

    [Fact]
    public void LoadingTextIsCustomizableAndEncoded()
    {
        var adorner = Render<FsAdorner>(p => p
            .Add(c => c.IsLoading, true)
            .Add(c => c.LoadingText, "<strong>Please wait</strong>")
            .Add(c => c.Class, "custom-adorner")
            .AddUnmatched("id", "loading-overlay"));
        Assert.Contains("custom-adorner", adorner.Find("#loading-overlay").ClassList);
        Assert.Equal("<strong>Please wait</strong>", adorner.Find("[role=status]").TextContent.Trim());
        Assert.Empty(adorner.FindAll("strong"));
    }

    [Fact]
    public void ChildContentReplacesDefaultAndSupportsComponents()
    {
        var clicks = 0;
        var adorner = Render<FsAdorner>(p => p
            .Add(c => c.IsLoading, true)
            .AddChildContent<FsButton>(button => button
                .Add(c => c.OnClick, () => clicks++)
                .AddChildContent("Cancel")));
        Assert.DoesNotContain("Loading...", adorner.Markup);
        adorner.Find("button").Click();
        Assert.Equal(1, clicks);
        Assert.Single(adorner.FindAll(".fs-adorner"));
    }

    [Fact]
    public void ChildContentIsNotRenderedWhileHidden()
    {
        var adorner = Render<FsAdorner>(p => p.AddChildContent("<p>Custom loading</p>"));
        Assert.Empty(adorner.Markup);
    }
}

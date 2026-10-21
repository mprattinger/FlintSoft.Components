using Bunit;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class PersonaTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public PersonaTests()
    {
        _module = JSInterop.SetupModule("./_content/FlintSoft.Components/flintsoft.js");
        _module.SetupVoid("attachPersona", _ => true).SetVoidResult();
        _module.SetupVoid("detachPersona", _ => true).SetVoidResult();
        _module.SetupVoid("positionPersona", _ => true).SetVoidResult();
    }

    [Theory]
    [InlineData("Alex Morgan", "AM")]
    [InlineData("  Alex   Robin Morgan  ", "AM")]
    [InlineData("Jamie", "J")]
    [InlineData("Émile Zola", "ÉZ")]
    [InlineData(null, "?")]
    [InlineData("  ", "?")]
    public void CollapsedAvatarDerivesInitials(string? name, string expected)
    {
        var persona = Render<FsPersona>(p => p.Add(c => c.Name, name));
        Assert.Equal(expected, persona.Find("button").TextContent.Trim());
        Assert.Equal("button", persona.Find("button").GetAttribute("type"));
        Assert.Equal("false", persona.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(persona.FindAll(".fs-persona-card"));
    }

    [Fact]
    public void ToggleRendersDetailsAndActions()
    {
        var persona = Render<FsPersona>(p => p
            .Add(c => c.Name, "Alex Morgan")
            .Add(c => c.Description, "Designer")
            .Add(c => c.Initials, "AX")
            .Add(c => c.Class, "custom-persona")
            .AddUnmatched("id", "profile")
            .Add(c => c.Actions, builder => builder.AddMarkupContent(0, "<a href=\"profile\">View profile</a>"))
            .AddChildContent("<p>Vienna</p>"));
        Assert.Contains("custom-persona", persona.Find("#profile").ClassList);
        Assert.Equal("AX", persona.Find("button").TextContent.Trim());
        persona.Find("button").Click();
        Assert.Equal("true", persona.Find("button").GetAttribute("aria-expanded"));
        Assert.Equal(persona.Find("section").Id, persona.Find("button").GetAttribute("aria-controls"));
        Assert.Equal("Alex Morgan", persona.Find("section").GetAttribute("aria-label"));
        Assert.Equal("Designer", persona.Find(".fs-persona-heading p").TextContent);
        Assert.Equal("Vienna", persona.Find(".fs-persona-details").TextContent);
        Assert.Equal("View profile", persona.Find(".fs-persona-actions a").TextContent);
        persona.Find("button").Click();
        Assert.Empty(persona.FindAll("section"));
    }

    [Fact]
    public async Task DismissalClosesAndIsIdempotent()
    {
        var persona = Render<FsPersona>();
        persona.Find("button").Click();
        await persona.InvokeAsync(() => persona.Instance.DismissPersonaAsync());
        await persona.InvokeAsync(() => persona.Instance.DismissPersonaAsync());
        Assert.Equal("false", persona.Find("button").GetAttribute("aria-expanded"));
        Assert.Empty(persona.FindAll(".fs-persona-card"));
    }

    [Fact]
    public void ImageErrorsFallBackAndNewUrlRetries()
    {
        var persona = Render<FsPersona>(p => p
            .Add(c => c.Name, "Jamie Lee")
            .Add(c => c.ImageUrl, "first.jpg"));
        Assert.Equal("first.jpg", persona.Find("img").GetAttribute("src"));
        Assert.Equal("", persona.Find("img").GetAttribute("alt"));
        persona.Find("img").TriggerEvent("onerror", EventArgs.Empty);
        Assert.Empty(persona.FindAll("img"));
        Assert.Equal("JL", persona.Find("button").TextContent.Trim());
        persona.Render(p => p.Add(c => c.Description, "Engineer"));
        Assert.Empty(persona.FindAll("img"));
        persona.Render(p => p.Add(c => c.ImageUrl, "second.jpg"));
        Assert.Equal("second.jpg", persona.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void OptionalSectionsAreOmittedAndContentIsEncoded()
    {
        var first = Render<FsPersona>(p => p.Add(c => c.AccessibleLabel, "Account"));
        var second = Render<FsPersona>(p => p.Add(c => c.Name, "<script>alert(1)</script>"));
        first.Find("button").Click();
        second.Find("button").Click();
        Assert.Equal("Account", first.Find("button").GetAttribute("aria-label"));
        Assert.Empty(first.FindAll(".fs-persona-heading, .fs-persona-details, .fs-persona-actions"));
        Assert.NotEqual(first.Find("section").Id, second.Find("section").Id);
        Assert.Empty(second.FindAll("script"));
        Assert.Equal("<script>alert(1)</script>", second.Find("strong").TextContent);
    }

    [Fact]
    public async Task DisposalDetachesBrowserListeners()
    {
        var persona = Render<FsPersona>();
        await persona.InvokeAsync(async () => await persona.Instance.DisposeAsync());
        Assert.Single(_module.Invocations, call => call.Identifier == "detachPersona");
    }
}

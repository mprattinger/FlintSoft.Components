using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace FlintSoft.Components.BrowserTests;

public sealed class BrowserFixture : IAsyncLifetime
{
    private readonly List<Process> _hosts = [];
    private readonly List<StringBuilder> _hostOutput = [];
    public string HostOutput => string.Join(Environment.NewLine, _hostOutput.Select(output =>
    {
        lock (output) return output.ToString();
    }));
    private IPlaywright _playwright = default!;
    public IBrowser Browser { get; private set; } = default!;
    public string ServerUrl { get; private set; } = "";
    public string WasmUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
        ServerUrl = await StartAsync("Demo\\Demo.csproj");
        WasmUrl = await StartAsync("tests\\WasmHost\\WasmHost.csproj");
    }

    private async Task<string> StartAsync(string project)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FlintSoft.Components.slnx")))
            root = root.Parent;
        if (root is null) throw new InvalidOperationException("Cannot locate repository root.");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var url = $"http://127.0.0.1:{port}";
        var output = new StringBuilder();
        _hostOutput.Add(output);
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root.FullName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        foreach (var argument in new[] { "run", "--no-build", "--no-launch-profile", "--configuration", configuration, "--project", project, "--urls", url })
            start.ArgumentList.Add(argument);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start host.");
        _hosts.Add(process);
        process.OutputDataReceived += (_, args) => { lock (output) output.AppendLine(args.Data); };
        process.ErrorDataReceived += (_, args) => { lock (output) output.AppendLine(args.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var http = new HttpClient();
        for (var attempt = 0; attempt < 120; attempt++)
        {
            if (process.HasExited) throw new InvalidOperationException($"Host exited: {output}");
            try { if ((await http.GetAsync(url)).IsSuccessStatusCode) return url; }
            catch (HttpRequestException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Host did not become ready: {output}");
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
        foreach (var host in _hosts)
        {
            if (!host.HasExited) host.Kill(entireProcessTree: true);
            host.Dispose();
        }
    }
}

public sealed class BrowserTests(BrowserFixture fixture) : IClassFixture<BrowserFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdornerCentersDefaultAndCustomContentAcrossViewports(bool wasm)
    {
        var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(wasm ? fixture.WasmUrl : fixture.ServerUrl + "/adorners");
        await Expect(page.Locator(wasm ? "#ready" : "[data-interactive=true]")).ToBeVisibleAsync(new() { Timeout = 60000 });
        var overlay = page.Locator(".fs-adorner");
        await Expect(overlay).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Show loading", Exact = true }).ClickAsync();
        await Expect(overlay.GetByRole(AriaRole.Status)).ToContainTextAsync("Loading...");
        if (wasm)
            await overlay.GetByRole(AriaRole.Button, new() { Name = "Finish loading" }).ClickAsync();
        await Expect(overlay).ToHaveCountAsync(0);
        foreach (var theme in new[] { "light", "dark" })
        {
            if (wasm)
                await page.Locator("main").EvaluateAsync("(element, theme) => element.dataset.fsTheme = theme", theme);
            else if (await page.Locator("[data-fs-theme]").GetAttributeAsync("data-fs-theme") != theme)
                await page.GetByRole(AriaRole.Button, new() { Name = "Toggle dark theme" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Show custom content" }).ClickAsync();
            await Expect(overlay).ToContainTextAsync("Preparing your workspace");
            await Expect(overlay).Not.ToContainTextAsync("Loading...");
            foreach (var width in new[] { 1280, 390, 320 })
            {
                await page.SetViewportSizeAsync(width, 844);
                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                Assert.True(await overlay.EvaluateAsync<bool>("""
                    element => {
                        const r = element.getBoundingClientRect();
                        const c = element.querySelector(".fs-adorner-content").getBoundingClientRect();
                        const width = document.documentElement.clientWidth;
                        return r.left === 0 && r.top === 0 &&
                            Math.abs(r.width - width) < 1 &&
                            Math.abs(r.height - window.innerHeight) < 1 &&
                            Math.abs(c.left + c.width / 2 - width / 2) < 1 &&
                            Math.abs(c.top + c.height / 2 - window.innerHeight / 2) < 1;
                    }
                    """));
            }
            await overlay.ClickAsync(new() { Position = new() { X = 5, Y = 5 } });
            await page.Keyboard.PressAsync("Escape");
            await Expect(overlay).ToBeVisibleAsync();
            await overlay.GetByRole(AriaRole.Button, new() { Name = "Cancel loading" }).ClickAsync();
            await Expect(overlay).ToHaveCountAsync(0);
        }
        Assert.Empty(errors);
        await context.CloseAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettingsSupportsGearToggleContentAndDismissal(bool wasm)
    {
        var context = await fixture.Browser.NewContextAsync(new()
        {
            HasTouch = true,
            ViewportSize = new() { Width = 390, Height = 844 }
        });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(wasm ? fixture.WasmUrl : fixture.ServerUrl + "/settings");
        await Expect(page.Locator(wasm ? "#ready" : "[data-interactive=true]")).ToBeVisibleAsync(new() { Timeout = 60000 });
        var trigger = page.GetByRole(AriaRole.Button, new() { Name = "Settings", Exact = true });
        var flyout = page.Locator(".fs-settings-flyout");
        await Expect(flyout).ToHaveCountAsync(0);
        await trigger.Locator("svg").TapAsync();
        await Expect(flyout).ToBeVisibleAsync();
        await flyout.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();
        await Expect(flyout).ToBeVisibleAsync();
        await trigger.ClickAsync();
        await Expect(flyout).ToHaveCountAsync(0);
        await trigger.PressAsync("Enter");
        await Expect(flyout).ToBeVisibleAsync();
        await trigger.PressAsync("Tab");
        await Expect(flyout.Locator("a").First).ToBeFocusedAsync();
        await flyout.Locator("a").First.PressAsync("Escape");
        await Expect(flyout).ToHaveCountAsync(0);
        await Expect(trigger).ToBeFocusedAsync();
        await trigger.PressAsync("Space");
        await Expect(flyout).ToBeVisibleAsync();
        await page.Locator("h1").TapAsync();
        await Expect(flyout).ToHaveCountAsync(0);
        await trigger.ClickAsync();
        await Expect(flyout).ToBeVisibleAsync();
        await page.SetViewportSizeAsync(320, 844);
        await page.WaitForFunctionAsync("""
            () => {
                const r = document.querySelector(".fs-settings-flyout").getBoundingClientRect();
                return r.left >= 0 && r.right <= window.innerWidth;
            }
            """);
        await page.GetByRole(AriaRole.Button, new() { Name = wasm ? "Save" : "Toggle dark theme", Exact = true }).FocusAsync();
        await Expect(flyout).ToHaveCountAsync(0);
        if (!wasm)
        {
            await trigger.ClickAsync();
            await flyout.GetByRole(AriaRole.Link, new() { Name = "Profile settings" }).ClickAsync();
            await Expect(flyout).ToHaveCountAsync(0);
            await Expect(page.Locator("h1")).ToContainTextAsync("A familiar face.");
        }
        Assert.Empty(errors);
        await context.CloseAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersonaSupportsToggleOutsideTouchKeyboardAndActions(bool wasm)
    {
        var context = await fixture.Browser.NewContextAsync(new()
        {
            HasTouch = true,
            ViewportSize = new() { Width = 390, Height = 844 }
        });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(wasm ? fixture.WasmUrl : fixture.ServerUrl + "/personas");
        await Expect(page.Locator(wasm ? "#ready" : "[data-interactive=true]")).ToBeVisibleAsync(new() { Timeout = 60000 });
        var avatar = page.GetByRole(AriaRole.Button, new() { Name = "Alex Morgan", Exact = true });
        var card = page.Locator(".fs-persona-card");
        await Expect(card).ToHaveCountAsync(0);
        await avatar.TapAsync();
        await Expect(card).ToBeVisibleAsync();
        await Expect(card).ToContainTextAsync("Product designer");
        await card.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Expect(card).ToBeVisibleAsync();
        await avatar.ClickAsync();
        await Expect(card).ToHaveCountAsync(0);
        await avatar.PressAsync("Enter");
        await Expect(card).ToBeVisibleAsync();
        await avatar.PressAsync("Tab");
        await Expect(card.Locator("a").First).ToBeFocusedAsync();
        await card.Locator("a").First.PressAsync("Escape");
        await Expect(card).ToHaveCountAsync(0);
        await Expect(avatar).ToBeFocusedAsync();
        await avatar.PressAsync("Space");
        await Expect(card).ToBeVisibleAsync();
        await page.Locator("h1").TapAsync();
        await Expect(card).ToHaveCountAsync(0);
        await avatar.ClickAsync();
        await Expect(card).ToBeVisibleAsync();
        foreach (var width in new[] { 320, 390 })
        {
            await page.SetViewportSizeAsync(width, 844);
            await Expect(card).ToBeVisibleAsync();
            await page.WaitForFunctionAsync("""
                () => {
                    const r = document.querySelector(".fs-persona-card").getBoundingClientRect();
                    return r.left >= 0 && r.right <= window.innerWidth;
                }
                """);
        }
        await page.GetByRole(AriaRole.Button, new() { Name = wasm ? "Save" : "Toggle dark theme", Exact = true }).FocusAsync();
        await Expect(card).ToHaveCountAsync(0);
        if (!wasm)
        {
            var imageAvatar = page.GetByRole(AriaRole.Button, new() { Name = "Jamie Lee", Exact = true });
            Assert.True(await imageAvatar.Locator("img").EvaluateAsync<bool>("image => image.complete && image.naturalWidth > 0"));
            await imageAvatar.ClickAsync();
            await Expect(card).ToContainTextAsync("Software engineer");
            await page.GetByRole(AriaRole.Link, new() { Name = "Panels", Exact = false }).ClickAsync();
            await Expect(card).ToHaveCountAsync(0);
        }
        Assert.Empty(errors);
        await context.CloseAsync();
    }

    [Fact]
    public async Task AutocompleteSupportsKeyboardPointerDismissalAndNewValues()
    {
        var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(fixture.ServerUrl + "/inputs");
        try
        {
            await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync();
        }
        catch (PlaywrightException exception)
        {
            throw new InvalidOperationException(fixture.HostOutput, exception);
        }
        var input = page.GetByRole(AriaRole.Combobox, new() { Name = "Existing contact" });
        await input.FillAsync("Vienna");
        var options = page.GetByRole(AriaRole.Listbox).GetByRole(AriaRole.Option);
        await Expect(options).ToHaveCountAsync(2);
        await Expect(options.First).ToContainTextAsync("alex@example.com");
        await Expect(input).Not.ToHaveAttributeAsync("aria-activedescendant", new System.Text.RegularExpressions.Regex(".+"));
        await input.PressAsync("ArrowDown");
        await Expect(options.First).ToHaveAttributeAsync("aria-selected", "true");
        await input.PressAsync("ArrowDown");
        await Expect(options.Last).ToHaveAttributeAsync("aria-selected", "true");
        await input.PressAsync("ArrowUp");
        await input.PressAsync("Enter");
        await Expect(input).ToHaveValueAsync("Alex Morgan");
        await Expect(options).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Selected object" })).ToContainTextAsync("1: Alex Morgan, Vienna");
        await input.FillAsync("Jamie");
        await Expect(options).ToHaveCountAsync(1);
        await input.PressAsync("Escape");
        await Expect(options).ToHaveCountAsync(0);
        await Expect(input).ToHaveValueAsync("Jamie");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Find the right match" }).ClickAsync();
        await Expect(input).ToHaveValueAsync("Alex Morgan");
        await input.FillAsync("Jamie");
        await Expect(options).ToHaveCountAsync(1);
        await options.First.ClickAsync();
        await Expect(input).ToHaveValueAsync("Jamie Lee");
        await input.FillAsync("Vienna");
        await Expect(options).ToHaveCountAsync(2);
        await input.PressAsync("ArrowDown");
        await input.PressAsync("Tab");
        await Expect(options).ToHaveCountAsync(0);
        await Expect(input).ToHaveValueAsync("Jamie Lee");
        var free = page.GetByRole(AriaRole.Combobox, new() { Name = "Contact or new name" });
        await free.FillAsync("New contact");
        await free.PressAsync("Enter");
        await Expect(page.GetByText("Free-text binding: New contact", new() { Exact = true })).ToBeVisibleAsync();
        await free.FillAsync("Another new contact");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Find the right match" }).ClickAsync();
        await Expect(page.GetByText("Free-text binding: Another new contact", new() { Exact = true })).ToBeVisibleAsync();
        await input.FillAsync("Vienna");
        await Expect(options).ToHaveCountAsync(2);
        Assert.False(await page.Locator(".fs-autocomplete-popup").EvaluateAsync<bool>("element => element.getBoundingClientRect().right > window.innerWidth"));
        Assert.Empty(errors);
        await context.CloseAsync();
    }

    [Fact]
    public async Task WebAssemblyAutocompletePreventsSubmitAndDismissesInsideModal()
    {
        var context = await fixture.Browser.NewContextAsync(new() { HasTouch = true });
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.WasmUrl);
        await Expect(page.Locator("#ready")).ToBeVisibleAsync(new() { Timeout = 60000 });
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Alex");
        await page.GetByLabel("Budget").FillAsync("1250,50");
        var input = page.GetByRole(AriaRole.Combobox, new() { Name = "Assignee" });
        await input.FillAsync("Al");
        await Expect(page.GetByRole(AriaRole.Listbox).GetByRole(AriaRole.Option)).ToHaveCountAsync(1);
        await input.PressAsync("ArrowDown");
        await input.PressAsync("Enter");
        await Expect(page.Locator("#bound-assignee")).ToHaveTextAsync("Alex");
        await Expect(page.Locator(".fs-toast")).ToHaveCountAsync(0);
        await input.EvaluateAsync("""
            element => {
                element.dispatchEvent(new CompositionEvent("compositionstart", { bubbles: true }));
                element.value = "Ja";
                element.dispatchEvent(new InputEvent("input", { bubbles: true, isComposing: true }));
            }
            """);
        await Expect(page.GetByRole(AriaRole.Listbox)).ToHaveCountAsync(0);
        await input.EvaluateAsync("element => element.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }))");
        await Expect(page.GetByRole(AriaRole.Listbox).GetByRole(AriaRole.Option)).ToContainTextAsync("Jamie");
        await input.FillAsync("Ja");
        await page.GetByRole(AriaRole.Listbox).GetByRole(AriaRole.Option).TapAsync();
        await Expect(page.Locator("#bound-assignee")).ToHaveTextAsync("Jamie");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open modal" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        var project = dialog.GetByRole(AriaRole.Combobox);
        await project.FillAsync("Al");
        await Expect(dialog.GetByRole(AriaRole.Listbox)).ToBeVisibleAsync();
        await project.PressAsync("Escape");
        await Expect(dialog.GetByRole(AriaRole.Listbox)).ToHaveCountAsync(0);
        await Expect(dialog).ToBeVisibleAsync();
        await project.PressAsync("Escape");
        await Expect(dialog).Not.ToBeVisibleAsync();
        await context.CloseAsync();
    }

    [Fact]
    public async Task ServerPrerendersAndAllPagesWorkInBothThemesOnMobile()
    {
        using var http = new HttpClient();
        var prerender = await http.GetStringAsync(fixture.ServerUrl + "/inputs");
        Assert.Contains("The everyday essentials", prerender);
        var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        foreach (var route in new[] { "", "inputs", "buttons", "modals", "toasts", "panels", "personas", "settings", "adorners", "tables" })
        {
            await page.GotoAsync($"{fixture.ServerUrl}/{route}");
            await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync();
            await Expect(page.Locator("h1")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Toggle dark theme" }).ClickAsync();
            await Expect(page.Locator("[data-fs-theme]")).ToHaveAttributeAsync("data-fs-theme", "dark");
            var overflow = await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth");
            Assert.False(overflow, $"Page /{route} overflows on mobile.");
        }
        var table = page.Locator(".fs-table-scroll");
        foreach (var width in new[] { 320, 390, 600 })
        {
            await page.SetViewportSizeAsync(width, 844);
            foreach (var theme in new[] { "light", "dark" })
            {
                if (await page.Locator("[data-fs-theme]").GetAttributeAsync("data-fs-theme") != theme)
                    await page.GetByRole(AriaRole.Button, new() { Name = "Toggle dark theme" }).ClickAsync();
                Assert.False(await table.EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"),
                    $"Table overflows at {width}px in {theme} theme.");
                await Expect(page.Locator("tbody tr")).ToHaveCountAsync(4);
                await Expect(page.Locator(".fs-table-mobile-label").First).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Columnheader, new() { Name = "Member" })).ToHaveCountAsync(1);
                await Expect(page.Locator("tbody tr").First.Locator("td")).ToHaveCountAsync(5);
                await Expect(page.Locator("tbody tr").First.GetByRole(AriaRole.Button, new() { Name = "View" })).ToBeVisibleAsync();
            }
        }
        await page.Locator(".demo-person small").First.EvaluateAsync(
            "element => element.textContent = 'averylongunbrokenemailaddresswithoutspaces@example.com'");
        Assert.False(await table.EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
        await page.Locator("tbody tr").First.GetByRole(AriaRole.Button, new() { Name = "View" }).ClickAsync();
        await Expect(page.Locator(".fs-toast")).ToContainTextAsync("Alex Morgan");
        await page.GetByLabel("Show loading state").CheckAsync();
        await Expect(table.GetByRole(AriaRole.Status)).ToHaveTextAsync("Loading...");
        Assert.False(await table.EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
        await page.GetByLabel("Show loading state").UncheckAsync();
        await page.GetByLabel("Show empty state").CheckAsync();
        await Expect(table).ToContainTextAsync("No team members yet.");
        Assert.False(await table.EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
        await page.GetByLabel("Show empty state").UncheckAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await Expect(page.Locator(".fs-table-mobile-label").First).Not.ToBeVisibleAsync();
        Assert.Equal("table", await page.Locator(".fs-table").EvaluateAsync<string>("element => getComputedStyle(element).display"));
        Assert.False(await table.EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
        Assert.Empty(errors);
        await context.CloseAsync();
    }

    [Fact]
    public async Task TableActionsCreateEntriesFromEmptyStateOnMobile()
    {
        var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 320, Height = 844 } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.ServerUrl + "/tables");
        await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync();
        var actions = page.GetByRole(AriaRole.Group, new() { Name = "Studio team actions" });
        await page.GetByLabel("Show loading state").CheckAsync();
        await Expect(actions.GetByRole(AriaRole.Button, new() { Name = "New member" })).ToBeVisibleAsync();
        await page.GetByLabel("Show loading state").UncheckAsync();
        await page.GetByLabel("Show empty state").CheckAsync();
        await actions.GetByRole(AriaRole.Button, new() { Name = "New member" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create member" }).ClickAsync();
        await Expect(dialog.Locator(".fs-validation")).ToHaveCountAsync(2);
        await dialog.GetByLabel("Member name").FillAsync("Casey Jones");
        await dialog.GetByLabel("Member email").FillAsync("casey@example.com");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create member" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(page.Locator("tbody tr")).ToHaveCountAsync(5);
        await Expect(page.Locator("tbody tr").Last).ToContainTextAsync("Casey Jones");
        await Expect(page.Locator(".fs-toast")).ToContainTextAsync("Member created");
        Assert.False(await page.Locator(".fs-table-container").EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
        await context.CloseAsync();
    }

    [Fact]
    public async Task PanelsWorkWithoutDemoStylesAndKeepActionsUsableOnMobile()
    {
        var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.ServerUrl + "/panels");
        await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync();
        await page.EvaluateAsync("""
            document.querySelectorAll('link[rel="stylesheet"]').forEach(link => {
                if (!link.href.includes('flintsoft')) link.disabled = true;
            });
            """);
        var panel = page.GetByRole(AriaRole.Region, new() { Name = "Project details", Exact = true });
        foreach (var width in new[] { 320, 390, 1280 })
        {
            await page.SetViewportSizeAsync(width, 900);
            foreach (var theme in new[] { "light", "dark" })
            {
                await page.Locator("[data-fs-theme]").EvaluateAsync("(element, theme) => element.dataset.fsTheme = theme", theme);
                await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Reset" })).ToBeVisibleAsync();
                await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Save project" })).ToBeVisibleAsync();
                Assert.False(await panel.EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
                Assert.Equal("solid", await panel.EvaluateAsync<string>("element => getComputedStyle(element).borderTopStyle"));
                Assert.Equal(theme == "dark" ? "rgb(25, 43, 57)" : "rgb(255, 255, 255)",
                    await panel.EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor"));
            }
        }
        await panel.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        await Expect(panel.GetByLabel("Project name")).ToHaveValueAsync("");
        await panel.GetByLabel("Project name").FillAsync("Panel project");
        await panel.GetByRole(AriaRole.Button, new() { Name = "Save project" }).ClickAsync();
        await Expect(page.Locator(".fs-toast")).ToContainTextAsync("Panel project");
        await context.CloseAsync();
    }

    [Fact]
    public async Task ServerFormsModalFocusAndToastsWork()
    {
        var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.ServerUrl + "/inputs");
        await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync();
        var amount = page.GetByLabel("Amount", new() { Exact = true });
        await amount.FillAsync("");
        await amount.PressSequentiallyAsync("12,50");
        await Expect(amount).ToHaveValueAsync("12,50");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save profile" }).ClickAsync();
        await Expect(page.Locator(".fs-validation").First).ToBeVisibleAsync();
        await page.GetByLabel("Display name", new() { Exact = true }).FillAsync("Alex");
        await page.GetByLabel("Work email").FillAsync("alex@example.com");
        await page.GetByLabel("Project budget").FillAsync("1250,50");
        await page.GetByLabel("Delivery date").FillAsync("2026-10-20");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save profile" }).ClickAsync();
        await Expect(page.Locator(".fs-validation")).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Changes saved" })).ToBeVisibleAsync();
        await page.GotoAsync(fixture.ServerUrl + "/modals");
        await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync();
        var trigger = page.GetByRole(AriaRole.Button, new() { Name = "Create a project", Exact = true });
        await trigger.ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByLabel("Project name")).ToHaveValueAsync("Untitled project");
        Assert.Equal("hidden", await page.EvaluateAsync<string>("document.body.style.overflow"));
        Assert.True(await page.Locator(".demo-sidebar").EvaluateAsync<bool>("element => { element.querySelector('a').focus(); return !element.contains(document.activeElement); }"));
        for (var i = 0; i < 8; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            Assert.True(await dialog.EvaluateAsync<bool>("element => element.contains(document.activeElement) || document.activeElement === document.body"));
        }
        await page.Keyboard.PressAsync("Escape");
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(trigger).ToBeFocusedAsync();
        Assert.Equal("", await page.EvaluateAsync<string>("document.body.style.overflow"));
        await page.GetByLabel("Close with Escape").UncheckAsync();
        await trigger.ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeVisibleAsync();
        await page.Mouse.ClickAsync(5, 5);
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Backdrop");
        await page.GotoAsync(fixture.ServerUrl + "/toasts");
        await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Keep it until dismissed" }).ClickAsync();
        await Expect(page.Locator(".fs-toast")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Dismiss notification" }).ClickAsync();
        await Expect(page.Locator(".fs-toast")).ToHaveCountAsync(0);
        await context.CloseAsync();
    }

    [Fact]
    public async Task WebAssemblyBindsValidatesAndOpensModalWithoutServerCircuit()
    {
        var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(fixture.WasmUrl);
        await Expect(page.Locator("#ready")).ToBeVisibleAsync(new() { Timeout = 60000 });
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Expect(page.Locator(".fs-validation")).ToHaveCountAsync(2);
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("Alex");
        await page.GetByLabel("Budget").FillAsync("1250,50");
        await page.GetByLabel("Date", new() { Exact = true }).FillAsync("2026-10-20");
        await page.GetByLabel("Time", new() { Exact = true }).FillAsync("09:30");
        await page.GetByLabel("Updates").CheckAsync();
        await page.GetByLabel("Team").SelectOptionAsync("Engineering");
        await page.GetByLabel("Two", new() { Exact = true }).CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Expect(page.Locator(".fs-toast")).ToContainTextAsync("WASM success");
        await Expect(page.Locator("#bound-budget")).ToContainTextAsync("1250,50");
        await Expect(page.Locator(".fs-validation")).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open modal" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(page.GetByLabel("Project", new() { Exact = true })).ToHaveValueAsync("Initialized");
        await page.Keyboard.PressAsync("Escape");
        await Expect(page.Locator("#modal-result")).ToHaveTextAsync("Escape");
        await Expect(page.Locator("tbody tr")).ToHaveCountAsync(2);
        Assert.Empty(errors);
        await context.CloseAsync();
    }
}

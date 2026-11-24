using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace FlintSoft.Components.BrowserTests;

public sealed class TreeBrowserTests(BrowserFixture fixture) : IClassFixture<BrowserFixture>
{
    private static ILocator Node(IPage page, string id) => page.Locator($"[data-fs-tree-id='{id}']");
    private static ILocator Row(IPage page, string id) => Node(page, id).Locator(":scope > .fs-tree-row");

    private async Task<IPage> OpenServerAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.ServerUrl + "/trees");
        await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync(new() { Timeout = 60000 });
        return page;
    }

    [Fact]
    public async Task PointerAndKeyboardShareOverridePolicyAndControlledSelection()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenServerAsync(context);
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await Row(page, "override").Locator(".fs-tree-toggle").ClickAsync();
        await Expect(Node(page, "override-child")).ToBeVisibleAsync();
        await Expect(page.Locator("#tree-events")).ToContainTextAsync("Expanded changed: override = True");
        await Row(page, "override").Locator(".fs-tree-label").ClickAsync();
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: override");
        await Expect(page.Locator("#tree-events")).ToContainTextAsync("Clicked: override");
        await Row(page, "restricted").Locator(".fs-tree-toggle").ClickAsync();
        await Expect(Node(page, "restricted-child")).ToHaveCountAsync(0);
        await Row(page, "restricted").Locator(".fs-tree-label").ClickAsync();
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: override");
        await Node(page, "restricted").FocusAsync();
        await Node(page, "restricted").PressAsync("ArrowRight");
        await Node(page, "restricted").PressAsync("Enter");
        await Expect(Node(page, "restricted-child")).ToHaveCountAsync(0);
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: override");
        await Node(page, "override").FocusAsync();
        await Node(page, "override").PressAsync("ArrowLeft");
        await Expect(Node(page, "override-child")).ToHaveCountAsync(0);
        await Node(page, "override").PressAsync("ArrowRight");
        await Expect(Node(page, "override-child")).ToBeVisibleAsync();
        await Node(page, "override").PressAsync("ArrowRight");
        await Expect(Node(page, "override-child")).ToBeFocusedAsync();
        await Node(page, "override-child").PressAsync("ArrowLeft");
        await Expect(Node(page, "override")).ToBeFocusedAsync();
        await Node(page, "override").PressAsync("Home");
        await Expect(Node(page, "product")).ToBeFocusedAsync();
        await Node(page, "product").PressAsync("d");
        await Expect(Node(page, "delivery")).ToBeFocusedAsync();
        await Node(page, "delivery").PressAsync("Space");
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: delivery");
        await Node(page, "delivery").PressAsync("Tab");
        Assert.False(await Node(page, "delivery").EvaluateAsync<bool>("node => node === document.activeElement"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task NativeDropUpdatesDataSourceAndPreservesSubtreeState()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenServerAsync(context);
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await Expect(Row(page, "product")).ToHaveAttributeAsync("draggable", "true");
        await Expect(Row(page, "delivery")).ToHaveAttributeAsync("draggable", "false");
        await Expect(Row(page, "pinned")).ToHaveAttributeAsync("draggable", "false");
        await Row(page, "brief").Locator(".fs-tree-label").ClickAsync();
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: brief");
        await page.Locator(".fs-tree").First.ScrollIntoViewIfNeededAsync();
        await Row(page, "product").DragToAsync(Row(page, "delivery"));
        await Expect(page.Locator("#tree-save")).ToHaveTextAsync("Saved product in delivery.");
        await Expect(page.Locator("#tree-data")).ToContainTextAsync("product -> delivery");
        await Expect(page.Locator("#tree-data")).ToContainTextAsync("brief -> product");
        await Expect(page.Locator("#tree-events")).ToContainTextAsync("Drop: product, root[0] -> delivery[0]");
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: brief");
        await Expect(page.Locator(".fs-tree-drop-target")).ToHaveCountAsync(0);
        await Row(page, "delivery").Locator(".fs-tree-toggle").ClickAsync();
        await Expect(Node(page, "brief")).ToBeVisibleAsync();
        await Expect(Node(page, "product")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Node(page, "brief")).ToHaveAttributeAsync("aria-selected", "true");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task RejectedSaveDoesNotMoveAndAccessibleMoveUsesSameCallback()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await OpenServerAsync(context);
        await page.GetByLabel("Reject moves (simulate a failed save)").CheckAsync();
        await page.Locator(".fs-tree").First.ScrollIntoViewIfNeededAsync();
        await page.EvaluateAsync("""
            () => {
                window.treeDragEvents = [];
                for (const name of ['dragstart', 'dragover', 'drop', 'dragend']) {
                    document.addEventListener(name, event => window.treeDragEvents.push(
                        name + ':' + event.target.closest('[data-fs-tree-id]')?.dataset.fsTreeId + ':' + event.defaultPrevented));
                }
            }
            """);
        await Expect(Row(page, "brief")).ToHaveAttributeAsync("draggable", "true");
        await Row(page, "brief").DragToAsync(Row(page, "delivery"));
        var nativeEvents = await page.EvaluateAsync<string[]>("window.treeDragEvents");
        Assert.Contains("drop:delivery:true", nativeEvents);
        await Expect(page.Locator("#tree-save")).ToHaveTextAsync("Save rejected; source data unchanged.");
        await Expect(page.Locator("#tree-data")).ToContainTextAsync("brief -> product");
        await Expect(Node(page, "brief")).ToBeVisibleAsync();
        await page.GetByLabel("Reject moves (simulate a failed save)").UncheckAsync();
        var tree = page.Locator(".fs-tree").First;
        await tree.Locator("summary").ClickAsync();
        await tree.GetByLabel("Item to move").SelectOptionAsync("brief");
        await tree.GetByLabel("Destination").SelectOptionAsync("delivery");
        await tree.GetByRole(AriaRole.Button, new() { Name = "Move", Exact = true }).PressAsync("Enter");
        await Expect(page.Locator("#tree-save")).ToHaveTextAsync("Saved brief in delivery.");
        await Expect(page.Locator("#tree-data")).ToContainTextAsync("brief -> delivery");
        await Expect(page.Locator("#tree-events")).ToContainTextAsync("Drop: brief, product[0] -> delivery[0]");
        await page.GetByRole(AriaRole.Button, new() { Name = "Toggle dark theme" }).ClickAsync();
        var overflow = await tree.EvaluateAsync<bool>("tree => tree.scrollWidth > tree.clientWidth");
        Assert.False(overflow);
    }

    [Fact]
    public async Task InvalidNativeDragsAndExternalPayloadsDoNotRaiseDropEvent()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenServerAsync(context);
        await Row(page, "product").DragToAsync(Row(page, "brief"));
        await Row(page, "brief").DragToAsync(Row(page, "product"));
        await Expect(page.Locator("#tree-save")).ToHaveTextAsync("No moves saved.");
        await Expect(page.Locator("#tree-events")).Not.ToContainTextAsync("Drop:");
        await Row(page, "delivery").EvaluateAsync("""
            row => {
                const dataTransfer = new DataTransfer();
                dataTransfer.setData('application/x-flintsoft-tree-item', 'external');
                row.dispatchEvent(new DragEvent('dragover', { bubbles: true, cancelable: true, dataTransfer }));
                row.dispatchEvent(new DragEvent('drop', { bubbles: true, cancelable: true, dataTransfer }));
            }
            """);
        await Expect(page.Locator("#tree-events")).Not.ToContainTextAsync("Drop:");
        await Expect(page.Locator(".fs-tree-drop-target")).ToHaveCountAsync(0);
        await page.GetByLabel("Disable tree").CheckAsync();
        await Expect(Row(page, "product")).ToHaveAttributeAsync("draggable", "false");
        await Expect(Row(page, "product").Locator(".fs-tree-toggle")).ToBeDisabledAsync();
        await Expect(Row(page, "delivery").Locator(".fs-tree-toggle")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task LazyLoadingTemplatesAndCollapseRestoreFocusWithoutActivating()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenServerAsync(context);
        await Expect(Row(page, "lazy").Locator(".fs-tree-toggle")).ToHaveCountAsync(0);
        await Row(page, "lazy").GetByRole(AriaRole.Button, new() { Name = "Load children for Lazy" }).ClickAsync();
        await Expect(Node(page, "loaded")).ToBeVisibleAsync();
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: none");
        await Node(page, "loaded").FocusAsync();
        await Row(page, "lazy").Locator(".fs-tree-toggle").ClickAsync();
        await Expect(Node(page, "loaded")).ToHaveCountAsync(0);
        await Expect(Node(page, "lazy")).ToBeFocusedAsync();
        await Node(page, "custom").GetByRole(AriaRole.Button, new() { Name = "Details" }).ClickAsync();
        await Expect(page.Locator("#tree-events")).ToContainTextAsync("Details: custom");
        await Expect(Node(page, "custom")).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: none");
        await Row(page, "lazy").Locator(".fs-tree-toggle").ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Overview", Exact = false }).ClickAsync();
        await Expect(page.Locator(".fs-tree")).ToHaveCountAsync(0);
        await page.GotoAsync(fixture.ServerUrl + "/trees");
        await Expect(Node(page, "product")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task WebAssemblySupportsOverridesKeyboardAndNativeMove()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(fixture.WasmUrl);
        await Expect(page.Locator("#ready")).ToBeVisibleAsync(new() { Timeout = 60000 });
        await Node(page, "source").FocusAsync();
        await Node(page, "source").PressAsync("ArrowRight");
        await Expect(Node(page, "child")).ToBeVisibleAsync();
        await Node(page, "source").PressAsync("Enter");
        await Expect(page.Locator("#tree-result")).ToHaveTextAsync("source");
        await Row(page, "child").DragToAsync(Row(page, "target"));
        await Expect(page.Locator("#tree-result")).ToHaveTextAsync("Moved child: source -> target");
        await Row(page, "target").Locator(".fs-tree-toggle").ClickAsync();
        await Expect(Node(page, "target").Locator(Selector("child"))).ToBeVisibleAsync();
        await Expect(Node(page, "source")).ToHaveAttributeAsync("aria-selected", "true");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ItemActionsAddChildrenAndOpenMenusWithoutActivatingOrDraggingRows()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenServerAsync(context);
        await Expect(Row(page, "product").Locator(".fs-tree-toggle path")).ToHaveAttributeAsync("d", "m6 9 6 6 6-6");
        await Expect(Row(page, "brief").Locator(".fs-tree-toggle")).ToHaveCountAsync(0);
        await Expect(Row(page, "pinned").Locator(".fs-tree-actions")).ToHaveCountAsync(0);
        await Expect(Row(page, "delivery").Locator(".fs-tree-toggle")).ToHaveCountAsync(0);
        await Row(page, "delivery").GetByRole(AriaRole.Button, new() { Name = "Add child to Delivery" }).ClickAsync();
        await Expect(Row(page, "delivery").Locator(".fs-tree-toggle path")).ToHaveAttributeAsync("d", "m9 18 6-6-6-6");
        await Expect(page.Locator("#tree-data")).ToContainTextAsync("added-1 -> delivery");
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: none");
        await Row(page, "delivery").Locator(".fs-tree-toggle").ClickAsync();
        await Expect(Node(page, "added-1")).ToBeVisibleAsync();
        var more = Row(page, "brief").GetByRole(AriaRole.Button, new() { Name = "More actions for Brief" });
        await more.ClickAsync();
        await Expect(Row(page, "brief").Locator(".fs-context-menu-flyout")).ToBeVisibleAsync();
        await Row(page, "brief").GetByRole(AriaRole.Button, new() { Name = "Details", Exact = true }).ClickAsync();
        await Expect(page.Locator("#tree-events")).ToContainTextAsync("Details: brief");
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: none");
        var action = Row(page, "brief").GetByRole(AriaRole.Button, new() { Name = "Add child to Brief" });
        var dragPrevented = await action.EvaluateAsync<bool>("""
            button => !button.dispatchEvent(new DragEvent('dragstart', {
                bubbles: true, cancelable: true, dataTransfer: new DataTransfer()
            }))
            """);
        Assert.True(dragPrevented);
        await more.PressAsync("Escape");
        await Expect(more).ToBeFocusedAsync();
        await action.PressAsync("Enter");
        await Expect(page.Locator("#tree-data")).ToContainTextAsync("added-2 -> brief");
        await Expect(page.Locator("#tree-selection")).ToHaveTextAsync("Selected: none");
        await page.GetByLabel("Disable tree").CheckAsync();
        await Expect(action).ToBeDisabledAsync();
        await Expect(more).ToBeDisabledAsync();
    }

    private static string Selector(string id) => $"[data-fs-tree-id='{id}']";
}

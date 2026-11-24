using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace FlintSoft.Components.BrowserTests;

public sealed class MarkdownEditorBrowserTests(BrowserFixture fixture) : IClassFixture<BrowserFixture>
{
    [Fact]
    public async Task ServerEditorSupportsDefaultAndCustomActionsAndImagePaste()
    {
        var page = await fixture.Browser.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        try
        {
            await page.GotoAsync(fixture.ServerUrl + "/markdown");
            await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync(new() { Timeout = 60000 });
            var editor = page.GetByLabel("Document", new() { Exact = true });
            await editor.FillAsync("hello");
            await editor.EvaluateAsync("e => { e.focus(); e.setSelectionRange(0, 5); e.dispatchEvent(new Event('select')); }");
            await page.GetByRole(AriaRole.Button, new() { Name = "Bold", Exact = true }).First.ClickAsync();
            await Expect(editor).ToHaveValueAsync("**hello**");
            await Expect(page.Locator("#markdown-value")).ToHaveTextAsync("**hello**");
            var custom = page.GetByLabel("Custom document");
            await custom.FillAsync("custom");
            await custom.EvaluateAsync("e => { e.focus(); e.setSelectionRange(0, 6); e.dispatchEvent(new Event('select')); }");
            await page.GetByRole(AriaRole.Button, new() { Name = "Bold", Exact = true }).Nth(1).ClickAsync();
            await Expect(custom).ToHaveValueAsync("**custom**");
            await Expect(page.Locator("#custom-markdown-value")).ToHaveTextAsync("**custom**");
            await Expect(editor).ToHaveValueAsync("**hello**");
            var native = await editor.EvaluateAsync<bool>("e => { const d = new DataTransfer(); d.setData('text/plain', 'text'); return e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
            Assert.True(native);
            await editor.EvaluateAsync("e => { e.focus(); e.setSelectionRange(e.value.length, e.value.length); const d = new DataTransfer(); d.items.add(new File(['a'.repeat(100000)], 'shot.png', { type: 'image/png' })); e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
            await Expect(editor).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(@"^\*\*hello\*\*!\[Image\]\(blob:http.*\)$"));
            var value = await editor.InputValueAsync();
            await Expect(page.Locator("#markdown-value")).ToHaveTextAsync(value);
            var unhandledImage = await custom.EvaluateAsync<bool>("e => { const d = new DataTransfer(); d.items.add(new File(['image'], 'shot.png', { type: 'image/png' })); return e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
            Assert.True(unhandledImage);
            Assert.Empty(errors);
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task InlineFormattingLeavesSelectedWhitespaceOutsideMarkers()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync(fixture.ServerUrl + "/markdown");
            await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync(new() { Timeout = 60000 });
            var editor = page.GetByLabel("Document", new() { Exact = true });
            page.Dialog += async (_, dialog) => await dialog.AcceptAsync("https://example.test/");
            var cases = new (string Action, string Input, int Start, int End, string Expected)[]
            {
                ("bold", "word next", 0, 5, "**word** next"),
                ("italic", "  two words \tend", 0, 13, "  *two words* \tend"),
                ("link", " word next", 0, 6, " [word](https://example.test/) next"),
                ("bold", " \t ", 0, 3, " \t "),
                ("italic", "abc", 1, 1, "a**bc"),
                ("bold", "\nword\n", 0, 6, "\n**word**\n")
            };
            foreach (var (action, input, start, end, expected) in cases)
            {
                await editor.FillAsync(input);
                await editor.EvaluateAsync("(e, selection) => { e.focus(); e.setSelectionRange(selection[0], selection[1]); e.dispatchEvent(new Event('select')); }", new[] { start, end });
                await page.Locator($"[data-fs-markdown-action='{action}']").First.ClickAsync();
                await Expect(editor).ToHaveValueAsync(expected);
            }
            var custom = page.GetByLabel("Custom document");
            await custom.FillAsync("word next");
            await custom.EvaluateAsync("e => { e.focus(); e.setSelectionRange(0, 5); e.dispatchEvent(new Event('select')); }");
            await page.GetByRole(AriaRole.Button, new() { Name = "Bold", Exact = true }).Nth(1).ClickAsync();
            await Expect(custom).ToHaveValueAsync("**word** next");
            await Expect(page.Locator("#custom-markdown-value")).ToHaveTextAsync("**word** next");
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task DefaultToolbarAndKeyboardEditExpectedMarkdown()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync(fixture.ServerUrl + "/markdown");
            await Expect(page.Locator("[data-interactive=true]")).ToBeVisibleAsync(new() { Timeout = 60000 });
            var editor = page.GetByLabel("Document", new() { Exact = true });
            page.Dialog += async (_, dialog) => await dialog.AcceptAsync("https://example.test/");
            var cases = new (string Action, string Expected)[]
            {
                ("bold", "**abc**"), ("italic", "*abc*"), ("heading-1", "# abc"),
                ("heading-2", "## abc"), ("heading-3", "### abc"), ("list", "* abc"),
                ("numbered-list", "1. abc"), ("code", "```\nabc\n```"),
                ("link", "[abc](https://example.test/)"), ("quote", "> abc")
            };
            foreach (var (action, expected) in cases)
            {
                await editor.FillAsync("abc");
                await Expect(page.Locator("#markdown-value")).ToHaveTextAsync("abc");
                await editor.EvaluateAsync("e => { e.focus(); e.setSelectionRange(0, 3); e.dispatchEvent(new Event('select')); }");
                var button = page.Locator($"[data-fs-markdown-action='{action}']").First;
                if (action is "bold" or "italic") await button.ClickAsync();
                else await button.Locator("svg").ClickAsync();
                await Expect(editor).ToHaveValueAsync(expected);
                await Expect(page.Locator("#markdown-value")).ToHaveTextAsync(expected);
            }
            await editor.FillAsync("1. one");
            await editor.PressAsync("End");
            await editor.PressAsync("Enter");
            await Expect(editor).ToHaveValueAsync("1. one\n2. ");
            await editor.PressAsync("Enter");
            await Expect(editor).ToHaveValueAsync("1. one\n");
            await editor.PressAsync("Control+]");
            await Expect(editor).ToHaveValueAsync("1. one\n\t");
            await editor.PressAsync("Tab");
            Assert.False(await editor.EvaluateAsync<bool>("e => document.activeElement === e"));
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task WasmEditorBindsAndProcessesImagePaste()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync(fixture.WasmUrl);
            await Expect(page.Locator("#ready")).ToBeVisibleAsync(new() { Timeout = 60000 });
            var editor = page.GetByLabel("WASM Markdown");
            await editor.FillAsync("hello");
            await editor.EvaluateAsync("e => { e.focus(); e.setSelectionRange(0, 5); e.dispatchEvent(new Event('select')); }");
            await page.GetByRole(AriaRole.Button, new() { Name = "Italic", Exact = true }).ClickAsync();
            await Expect(editor).ToHaveValueAsync("*hello*");
            await editor.EvaluateAsync("e => { e.focus(); e.setSelectionRange(e.value.length, e.value.length); const d = new DataTransfer(); d.items.add(new File(['image'], 'shot.png', { type: 'image/png' })); e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
            await Expect(editor).ToHaveValueAsync("*hello*![Image](/images/test.png)");
            await Expect(page.Locator("#bound-markdown")).ToHaveTextAsync("*hello*![Image](/images/test.png)");
            await editor.EvaluateAsync("e => { e.focus(); const d = new DataTransfer(); d.items.add(new File(['image'], 'shot.png', { type: 'image/png' })); e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
            await editor.FillAsync("changed while uploading");
            await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("could not be inserted");
            await Expect(editor).ToHaveValueAsync("changed while uploading");
            await Expect(page.Locator("#bound-markdown")).ToHaveTextAsync("changed while uploading");
            await editor.EvaluateAsync("e => { e.focus(); e.setSelectionRange(e.value.length, e.value.length); const d = new DataTransfer(); d.items.add(new File(['image'], 'shot.png', { type: 'image/png' })); e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
            await Expect(page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            await Expect(editor).ToHaveValueAsync("changed while uploading![Image](/images/test.png)");
            await editor.EvaluateAsync("e => { const d = new DataTransfer(); d.items.add(new File(['a'.repeat(17)], 'large.png', { type: 'image/png' })); e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: d, bubbles: true, cancelable: true })); }");
            await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("could not be inserted");
            await Expect(editor).ToHaveValueAsync("changed while uploading![Image](/images/test.png)");
        }
        finally { await page.CloseAsync(); }
    }
}

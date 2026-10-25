using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlintSoft.Components.Tests;

public sealed class ComponentTests : BunitContext
{
    [Fact]
    public void StandaloneTextBindsOnChangeAndConnectsLabel()
    {
        var model = new Model();
        var input = Render<FsText>(p => p
            .Add(c => c.Label, "Name")
            .Add(c => c.HelpText, "Your name")
            .Add(c => c.Value, model.Name)
            .Add(c => c.ValueChanged, value => model.Name = value)
            .Add(c => c.ValueExpression, () => model.Name));
        input.Find("input").Change("Alex");
        Assert.Equal("Alex", model.Name);
        Assert.Equal(input.Find("input").Id, input.Find("label").GetAttribute("for"));
        Assert.Equal(input.Find(".fs-help").Id, input.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void NumericInputPreservesInvalidTextAndUsesDecimalComma()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var model = new Model { Budget = 42 };
            var input = Render<FsNumber<decimal?>>(p => p
                .Add(c => c.Label, "Budget")
                .Add(c => c.UpdateOnInput, true)
                .Add(c => c.Value, model.Budget)
                .Add(c => c.ValueChanged, value => model.Budget = value)
                .Add(c => c.ValueExpression, () => model.Budget));
            input.Find("input").Input("1");
            input.Find("input").Input("1,");
            Assert.Equal("1,", input.Find("input").GetAttribute("value"));
            input.Find("input").Input("1250,50");
            Assert.Equal(1250.50m, model.Budget);
            input.Find("input").Input("-");
            Assert.Equal(1250.50m, model.Budget);
            Assert.Equal("-", input.Find("input").GetAttribute("value"));
            Assert.Equal("true", input.Find("input").GetAttribute("aria-invalid"));
            Assert.Contains("valid number", input.Find(".fs-validation").TextContent);
            input.Find("input").Input("");
            Assert.Null(model.Budget);
            Assert.Empty(input.FindAll(".fs-validation"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task EditFormDisplaysDataAnnotationsAndParseErrors()
    {
        var model = new Model();
        var editContext = new EditContext(model);
        var form = Render<EditForm>(p => p
            .Add(c => c.EditContext, editContext)
            .Add(c => c.ChildContent, _ => builder =>
            {
                builder.OpenComponent<DataAnnotationsValidator>(0);
                builder.CloseComponent();
                builder.OpenComponent<FsNumber<decimal?>>(1);
                builder.AddAttribute(2, "Label", "Budget");
                builder.AddAttribute(3, "Value", model.Budget);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, value => model.Budget = value));
                builder.AddAttribute(5, "ValueExpression", (System.Linq.Expressions.Expression<Func<decimal?>>)(() => model.Budget));
                builder.CloseComponent();
            }));
        await form.InvokeAsync(() => editContext.Validate());
        Assert.Contains("required", form.Find(".fs-validation").TextContent);
        form.Find("input").Change("not a number");
        Assert.Contains("valid number", form.Find(".fs-validation").TextContent);
        Assert.True(editContext.IsModified(editContext.Field(nameof(Model.Budget))));
        form.Find("input").Change("500");
        Assert.Equal(500m, model.Budget);
        Assert.Empty(form.FindAll(".fs-validation"));
    }

    [Fact]
    public void DatesTimesAndNullableSelectBind()
    {
        var model = new Model();
        var date = Render<FsDate>(p => p
            .Add(c => c.ValueChanged, value => model.Date = value)
            .Add(c => c.ValueExpression, () => model.Date));
        date.Find("input").Change("2026-10-20");
        Assert.Equal(new DateOnly(2026, 10, 20), model.Date);
        date.Find("input").Change("");
        Assert.Null(model.Date);
        var time = Render<FsTime>(p => p
            .Add(c => c.ValueChanged, value => model.Time = value)
            .Add(c => c.ValueExpression, () => model.Time));
        time.Find("input").Change("09:30:15");
        Assert.Equal(new TimeOnly(9, 30, 15), model.Time);
        var select = Render<FsSelect<int?>>(p => p
            .Add(c => c.ValueChanged, value => model.Selection = value)
            .Add(c => c.ValueExpression, () => model.Selection)
            .AddChildContent("<option value=\"\">None</option><option value=\"2\">Two</option>"));
        select.Find("select").Change("2");
        Assert.Equal(2, model.Selection);
        select.Find("select").Change("");
        Assert.Null(model.Selection);
    }

    [Fact]
    public void CheckboxAndRadioGroupBindTypedValues()
    {
        var model = new Model();
        var checkbox = Render<FsCheckbox>(p => p
            .Add(c => c.Label, "Updates")
            .Add(c => c.ValueChanged, value => model.Updates = value)
            .Add(c => c.ValueExpression, () => model.Updates));
        checkbox.Find("input").Change(true);
        Assert.True(model.Updates);
        var radio = Render<FsRadioGroup<int?>>(p => p
            .Add(c => c.Label, "Plan")
            .Add(c => c.Options, [new(1, "One"), new(2, "Two")])
            .Add(c => c.ValueChanged, value => model.Selection = value)
            .Add(c => c.ValueExpression, () => model.Selection));
        radio.FindAll("input")[1].Change("1");
        Assert.Equal(2, model.Selection);
        Assert.Equal("Plan", radio.Find("legend").TextContent);
        Assert.Single(radio.FindAll("input").Select(element => element.GetAttribute("name")).Distinct());
    }

    [Fact]
    public void ButtonDefaultsToNonSubmitAndForwardsClick()
    {
        var clicked = false;
        var button = Render<FsButton>(p => p
            .Add(c => c.OnClick, () => clicked = true)
            .Add(c => c.Appearance, FsAppearance.Danger)
            .AddChildContent("Delete"));
        Assert.Equal("button", button.Find("button").GetAttribute("type"));
        Assert.Contains("fs-button-danger", button.Find("button").ClassList);
        button.Find("button").Click();
        Assert.True(clicked);
    }

    [Fact]
    public void PanelRendersOptionalSectionsAndForwardsAttributes()
    {
        var panel = Render<FsPanel>(p => p
            .Add(c => c.Title, "Project details")
            .Add(c => c.Description, "A little context.")
            .Add(c => c.Badge, "DRAFT")
            .Add(c => c.Class, "custom-panel")
            .AddUnmatched("id", "project-panel")
            .Add(c => c.HeaderActions, builder => builder.AddMarkupContent(0, "<button>Reset</button>"))
            .Add(c => c.Footer, builder => builder.AddMarkupContent(0, "<button>Save</button>"))
            .AddChildContent("<p>Project content</p>"));
        Assert.Equal("project-panel", panel.Find("section").Id);
        Assert.Contains("fs-panel", panel.Find("section").ClassList);
        Assert.Contains("custom-panel", panel.Find("section").ClassList);
        Assert.Equal(panel.Find("h2").Id, panel.Find("section").GetAttribute("aria-labelledby"));
        Assert.Equal("A little context.", panel.Find(".fs-panel-heading p").TextContent);
        Assert.Equal("DRAFT", panel.Find(".fs-panel-badge").TextContent);
        Assert.Equal("Reset", panel.Find(".fs-panel-actions button").TextContent);
        Assert.Equal("Project content", panel.Find(".fs-panel-body").TextContent);
        Assert.Equal("Save", panel.Find(".fs-panel-footer button").TextContent);
    }

    [Fact]
    public void ContentOnlyPanelsHaveNoEmptySectionsOrDanglingLabels()
    {
        var panel = Render<FsPanel>(p => p
            .AddUnmatched("aria-label", "Simple content")
            .AddChildContent("<p>Content only</p>"));
        Assert.Empty(panel.FindAll(".fs-panel-header, .fs-panel-footer, h2"));
        Assert.Null(panel.Find("section").GetAttribute("aria-labelledby"));
        Assert.Equal("Simple content", panel.Find("section").GetAttribute("aria-label"));
        Assert.Equal("Content only", panel.Find(".fs-panel-body").TextContent);
    }

    [Fact]
    public void PanelTitlesAreEncodedAndLabelIdsAreUnique()
    {
        var first = Render<FsPanel>(p => p.Add(c => c.Title, "<script>alert(1)</script>"));
        var second = Render<FsPanel>(p => p.Add(c => c.Title, "Another panel"));
        Assert.Empty(first.FindAll("script"));
        Assert.Equal("<script>alert(1)</script>", first.Find("h2").TextContent);
        Assert.NotEqual(first.Find("h2").Id, second.Find("h2").Id);
    }

    [Fact]
    public void TableRendersTemplatesEmptyAndLoadingStates()
    {
        var columns = new FsColumn<string>[] { new("Name", value => builder => builder.AddContent(0, value)) };
        var table = Render<FsTable<string>>(p => p
            .Add(c => c.Items, ["Alex"])
            .Add(c => c.Columns, columns)
            .Add(c => c.Caption, "People"));
        Assert.Equal("col", table.Find("th").GetAttribute("scope"));
        Assert.Equal("Alex", table.Find("tbody td .fs-table-cell-content").TextContent);
        Assert.Equal("Name", table.Find(".fs-table-mobile-label").TextContent);
        Assert.Equal("true", table.Find(".fs-table-mobile-label").GetAttribute("aria-hidden"));
        table.Render(p => p.Add(c => c.Items, []));
        Assert.Contains("No items", table.Find("tbody").TextContent);
        table.Render(p => p.Add(c => c.Loading, true));
        Assert.Contains("Loading", table.Find("tbody").TextContent);
        Assert.Equal("true", table.Find("table").GetAttribute("aria-busy"));
        Assert.Empty(table.FindAll(".fs-table-actions"));
    }

    [Fact]
    public void TableActionsRemainAvailableInEmptyAndLoadingStates()
    {
        var clicks = 0;
        var table = Render<FsTable<string>>(p => p
            .Add(c => c.Items, [])
            .Add(c => c.Columns, [])
            .Add(c => c.Caption, "People")
            .Add(c => c.Actions, builder =>
            {
                builder.OpenComponent<FsButton>(0);
                builder.AddAttribute(1, "OnClick", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => clicks++));
                builder.AddAttribute(2, "ChildContent", (RenderFragment)(content => content.AddContent(0, "New")));
                builder.CloseComponent();
            }));
        Assert.Equal("People actions", table.Find(".fs-table-actions").GetAttribute("aria-label"));
        Assert.Single(table.FindAll("caption .fs-table-actions button"));
        Assert.Equal("People", table.Find(".fs-table-title").TextContent);
        Assert.Equal(table.Find(".fs-table-title").Id, table.Find("table").GetAttribute("aria-labelledby"));
        table.Find(".fs-table-actions button").Click();
        table.Render(p => p.Add(c => c.Loading, true));
        table.Find(".fs-table-actions button").Click();
        Assert.Equal(2, clicks);
        Assert.Equal("button", table.Find(".fs-table-actions button").GetAttribute("type"));
    }

    [Fact]
    public void TableSupportsDuplicateItemsAndHeaderTemplates()
    {
        var columns = new FsColumn<string>[]
        {
            new("Name", value => builder => builder.AddContent(0, value),
                HeaderTemplate: builder => builder.AddMarkupContent(0, "<abbr title=\"Full name\">Name</abbr>"))
        };
        var table = Render<FsTable<string>>(p => p
            .Add(c => c.Items, ["Alex", "Alex"])
            .Add(c => c.Columns, columns));
        Assert.Equal(2, table.FindAll("tbody tr").Count);
        Assert.Equal("Full name", table.Find("th abbr").GetAttribute("title"));
        Assert.All(table.FindAll(".fs-table-mobile-label"), label => Assert.Equal("Name", label.TextContent));
    }

    [Fact]
    public async Task ModalCoordinatesInitializationClosingFailureAndAwaitableClose()
    {
        Services.AddFlintSoftComponents();
        var module = JSInterop.SetupModule("./_content/FlintSoft.Components/flintsoft.js");
        module.SetupVoid("captureModalFocus", _ => true).SetVoidResult();
        module.SetupVoid("openModal", _ => true).SetVoidResult();
        module.SetupVoid("closeModal", _ => true).SetVoidResult();
        var initialized = false;
        FsCloseReason? reason = null;
        var modal = Render<FsModal>(p => p
            .Add(c => c.Title, "Edit")
            .Add(c => c.OnOpening, () => initialized = true)
            .Add(c => c.OnClosed, value => reason = value));
        await modal.InvokeAsync(() => modal.Instance.OpenAsync());
        Assert.True(initialized);
        var other = Render<FsModal>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.InvokeAsync(() => other.Instance.OpenAsync()));
        await modal.InvokeAsync(() => modal.Instance.DismissAsync("escape"));
        Assert.Equal(FsCloseReason.Escape, reason);
        await other.InvokeAsync(() => other.Instance.OpenAsync());
        await other.InvokeAsync(() => other.Instance.CloseAsync());
        var failed = Render<FsModal>(p => p.Add(c => c.OnOpening, () => throw new InvalidOperationException("load failed")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failed.InvokeAsync(() => failed.Instance.OpenAsync()));
        await modal.InvokeAsync(() => modal.Instance.OpenAsync());
        await modal.InvokeAsync(() => modal.Instance.CloseAsync());

        var closeReasonTask = modal.InvokeAsync(() => modal.Instance.OpenAndWaitForCloseAsync());
        await modal.InvokeAsync(() => modal.Instance.CloseAsync(FsCloseReason.CloseButton));
        Assert.Equal(FsCloseReason.CloseButton, await closeReasonTask);

        var pending = modal.InvokeAsync(() => modal.Instance.OpenAndWaitForCloseAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => modal.InvokeAsync(() => modal.Instance.OpenAndWaitForCloseAsync()));
        await modal.InvokeAsync(() => modal.Instance.CloseAsync());
        await pending;
    }

    [Fact]
    public async Task ToastHostShowsContentAndExpiresOrDismissesItems()
    {
        Services.AddFlintSoftComponents();
        var service = Services.GetRequiredService<FsToastService>();
        var host = Render<FsToastHost>();
        await host.InvokeAsync(() => service.Show("Saved", FsToastStyle.Success, "Done", TimeSpan.FromMilliseconds(40)));
        Assert.Equal("Saved", host.Find(".fs-toast-body div").TextContent);
        host.WaitForAssertion(() => Assert.Empty(host.FindAll(".fs-toast")));
        await host.InvokeAsync(() => service.Show("Persistent", duration: TimeSpan.Zero));
        Assert.Single(host.FindAll(".fs-toast"));
        await host.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Empty(service.Items);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Show("Invalid", duration: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task ToastPausesWhileHoveredAndRestartsWhenLeaving()
    {
        Services.AddFlintSoftComponents();
        var service = Services.GetRequiredService<FsToastService>();
        var host = Render<FsToastHost>();
        await host.InvokeAsync(() => service.Show("Pause me", duration: TimeSpan.FromMilliseconds(200)));
        host.Find(".fs-toast").MouseEnter();
        await Task.Delay(250);
        Assert.Single(service.Items);
        host.Find(".fs-toast").MouseLeave();
        host.WaitForAssertion(() => Assert.Empty(service.Items));
    }

    private sealed class Model
    {
        public string? Name { get; set; }
        [Required, Range(100, 100000)] public decimal? Budget { get; set; }
        public DateOnly? Date { get; set; }
        public TimeOnly? Time { get; set; }
        public int? Selection { get; set; }
        public bool Updates { get; set; }
    }
}

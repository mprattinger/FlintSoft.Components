# FlintSoft.Components v2

A .NET 10 Razor component library with standalone styling, light and dark themes,
and no Bootstrap dependency. This is a clean v2 API, not a compatibility layer for v1.

## Quick start

Reference the library project or install the `FlintSoft.Components` NuGet package.
Add `@using FlintSoft.Components` to your `_Imports.razor` and register the services:

```csharp
builder.Services.AddFlintSoftComponents();
```

Reference the stylesheet in your host document:

```html
<link rel="stylesheet" href="_content/FlintSoft.Components/flintsoft.css" />
```

Place a single `<FsToastHost />` inside your interactive layout. Components support
Interactive Server and WebAssembly. In a Blazor Web App, use an interactive render
mode for pages or routes that contain these components. Prerendering does not call
browser APIs; modal JavaScript loads after interactive rendering.

Run the showcase with:

```powershell
dotnet run --project Demo
```

## Themes

Set `data-fs-theme="light"` or `data-fs-theme="dark"` on an ancestor of the components.
All design tokens start with `--fs-`. Override them after the library stylesheet:

```css
:root {
    --fs-color-primary: #087eaa;
    --fs-color-primary-soft: #e8f5fa;
    --fs-color-on-primary: #fff;
    --fs-radius: .75rem;
    --fs-font-family: "Segoe UI", system-ui, sans-serif;
}
```

Semantic color families include primary, success, danger and warning (each with a
soft variant), plus text, muted, surface, background, border, overlay and focus.
Spacing, font size and shadow tokens are also available in `flintsoft.css`.
Override dark-theme tokens in `[data-fs-theme="dark"]` when customizing that theme.

## Inputs

```razor
<FsText Label="Name" @bind-Value="model.Name" />
<FsText Label="Email" Type="email" @bind-Value="model.Email" />
<FsNumber TValue="decimal?" Label="Budget" Prefix="EUR" @bind-Value="model.Budget" />
<FsDate Label="Date" @bind-Value="model.Date" />
<FsTime Label="Time" @bind-Value="model.Time" />
<FsTextArea Label="Notes" @bind-Value="model.Notes" />
<FsSelect TValue="string" Label="Team" @bind-Value="model.Team">
    <option value="">Choose a team</option>
    <option>Design</option>
</FsSelect>
<FsCheckbox Label="Send updates" @bind-Value="model.Updates" />
<FsRadioGroup TValue="string" Label="Plan" Options="plans" @bind-Value="model.Plan" />
```

Use `IReadOnlyList<FsOption<TValue>>` for radio options. Numeric types are `short`,
`int`, `long`, `float`, `double`, `decimal`, and their nullable equivalents.
Date and time bind to `DateOnly?` and `TimeOnly?`. Checkbox values are `bool`.
Text inputs support text, password, email, tel, url and search.

All inputs support `Label`, `HelpText`, `Id`, `Disabled`, `ReadOnly`, `Prefix`,
`Suffix` and additional HTML attributes. Binding works standalone and inside
`EditForm` with `DataAnnotationsValidator`. When not using `@bind-Value`, supply
`ValueExpression` alongside `Value` and `ValueChanged`, as with Blazor's built-in inputs.

Updates use `onchange` by default. Set `UpdateOnInput="true"` on text, textarea
or numeric inputs for keystroke updates. Invalid numeric text remains visible while
the last valid model value is preserved. Parsing errors are shown inside forms.
Nullable inputs map empty text to null. Numeric formatting/parsing uses the current
app culture; the demo uses `de-DE` to demonstrate decimal commas. Native date/time
values use browser-required invariant formats and are localized by the browser.
Use the application's normal Blazor localization configuration in Server or
WebAssembly; the library does not change the application's culture.

## Autocomplete

`FsAutocomplete<TItem>` binds a **string** with `@bind-Value`. Supply an asynchronous
search callback and a function that returns the input text for a selected item:

```razor
<FsAutocomplete TItem="Customer" Label="Customer" @bind-Value="customerName"
                SearchAsync="SearchCustomersAsync" GetText="customer => customer.Name"
                ItemSelected="CustomerSelected" AllowNewValues="false">
    <ItemTemplate Context="customer">
        <strong>@customer.Name</strong>
        <div>@customer.City</div>
    </ItemTemplate>
</FsAutocomplete>

@code {
    private string? customerName;
    private Customer? selectedCustomer;
    private void CustomerSelected(Customer customer) => selectedCustomer = customer;
    private async Task<IReadOnlyList<Customer>> SearchCustomersAsync(
        string text, CancellationToken cancellationToken)
    {
        return await customerRepository.SearchAsync(text, cancellationToken);
    }
}
```

`ItemTemplate` is optional; suggestions display `GetText(item)` by default.
Templates should contain display content, not interactive controls. `ItemSelected`
fires only for an explicitly chosen suggestion, not for newly typed text.
The callback receives the selected object; the bound value receives its `GetText`.

Search defaults to a 250ms debounce and one character minimum, configurable with
`DebounceMilliseconds` and `MinimumLength`. Focus searches the current text when it
meets the minimum; set `MinimumLength="0"` to search on empty focus. Honor the supplied
cancellation token for database/API calls. Obsolete results are ignored even if the
callback ignores cancellation. Search failures are logged and shown in the popup;
they are not treated as an empty result. Loading and empty messages can be customized
with `LoadingText`, `NoResultsText` and `SearchErrorText`.

No suggestion is automatically selected. Up/Down activates an entry, Enter commits
it, and mouse/touch selection also commits it. Escape or an outside click closes the
popup and cancels pending searches. Tab leaves without choosing a highlighted item.
With `AllowNewValues="true"`, Enter or leaving the field commits the typed text.
Otherwise (the default), leaving restores the previous committed value, or clears
the input when there was none. Typing does not update the model on each keystroke;
`UpdateOnInput` is not used for this control. An exact text match is not an implicit
object selection. Clear the bound value from the parent to reset a restricted field.
Changing the bound value externally also resets the editor and pending search.

The component uses the existing floating label, affixes, help text and EditForm
validation, along with ARIA combobox/listbox semantics. Suggestions are rendered
after interactive events; no search or browser API runs during prerendering.

## Panels

`FsPanel` provides the same themed surface, border, rounded corners and responsive
padding used throughout the demo. It works without any demo styles or services.

```razor
<FsPanel Title="Project details" Description="Give your idea a name." Badge="DRAFT">
    <HeaderActions>
        <FsButton Appearance="FsAppearance.Action" OnClick="Reset">Reset</FsButton>
    </HeaderActions>
    <ChildContent>
        <FsText Label="Project name" @bind-Value="name" />
    </ChildContent>
    <Footer>
        <FsButton OnClick="Save">Save project</FsButton>
    </Footer>
</FsPanel>
```

`Title`, `Description`, `Badge`, `HeaderActions` and `Footer` are optional.
Use inline child content when not supplying named slots. A content-only panel
does not render an empty header or footer. Titles render as level-two headings
and label their section; titleless panels can supply `aria-label`.
`Class` adds custom CSS classes and additional HTML attributes are forwarded to
the section. The panel does not impose outer margins or create a form: place it
inside an `EditForm`, or place an `EditForm` in its body, when needed.

## Buttons

```razor
<FsButton OnClick="Save">Save</FsButton>
<FsButton Type="submit">Submit</FsButton>
<FsButton Appearance="FsAppearance.Danger" Disabled="busy">Delete</FsButton>
```

Appearances: `Primary`, `Secondary`, `Action`, `Danger`, `Success`.
The default HTML type is `button`, not submit.

## Modals

```razor
<FsModal @ref="modal" Title="Edit project" OnOpening="LoadAsync" OnClosed="Closed">
    <FsText Label="Name" @bind-Value="name" />
</FsModal>

@code {
    private FsModal modal = default!;
    private string? name;
    private Task LoadAsync() { name = "Example"; return Task.CompletedTask; }
    private void Closed(FsCloseReason reason) { }
    private Task Open() => modal.OpenAsync();
}
```

`OpenAsync` awaits initialization before scheduling the dialog for display.
Initialization errors propagate to the caller. A second concurrent open throws;
close the active modal first. Call `CloseAsync()` to close programmatically.
`OnClosed` reports `Programmatic`, `CloseButton`, `Escape` or `Backdrop`.
`CloseOnEscape` and `CloseOnBackdrop` default to true.
For a titleless modal, provide `AccessibleLabel`.

Native dialogs provide modal background isolation and keyboard focus containment.
The JavaScript module locks body scrolling and restores focus/scroll on closure or
component removal. Modern browsers with native `<dialog>` support are required.

## Toasts

```csharp
Toasts.Show("Saved.", FsToastStyle.Success, title: "All done");
var id = Toasts.Show("Close this when ready.", duration: TimeSpan.Zero);
Toasts.Dismiss(id);
```

Inject `FsToastService`; render one `FsToastHost` per interactive application scope.
Styles: `Info`, `Success`, `Danger`, `Warning`. An overload accepts `RenderFragment`.
The default duration is five seconds; zero means manual dismissal only. Negative
durations throw. Hover and focus pause dismissal; leaving restarts the full duration.
Timers start after interactive rendering and are canceled when toast items are removed.
In Interactive Server, invoke the service from the circuit's renderer context
(use `InvokeAsync` when publishing from background work).

## Tables

`FsTable<TItem>` accepts `IReadOnlyList<TItem> Items` and
`IReadOnlyList<FsColumn<TItem>> Columns`. Each column has a header, a
`RenderFragment<TItem>` cell template, an optional CSS class and an optional
`RenderFragment HeaderTemplate`.
Put buttons or other components in cell templates for row actions.

```razor
<FsTable TItem="Person" Items="people" Columns="columns" Caption="Team"
         RowKey="person => person.Id" Loading="loading">
    <Actions>
        <FsButton OnClick="OpenCreate">New member</FsButton>
    </Actions>
    <EmptyContent>No team members yet.</EmptyContent>
</FsTable>
```

The optional `Actions` fragment renders beside the title inside the table caption,
right-aligned and wrapping on narrow screens. Use it for table-level actions such as creating
an entry, refreshing or exporting. Actions remain available in loading and empty
states; the caller controls their click handlers and disabled state. Without
`Actions`, no toolbar is rendered.

Use stable, unique `RowKey` values for mutable data. `EmptyContent` and
`LoadingContent` customize state messages. At widths up to 600px, each row becomes
a stacked card with column labels, all values and row actions visible without
horizontal scrolling. `Header` supplies the mobile label even when using a
`HeaderTemplate`. The same table markup and accessible column headers are retained;
desktop keeps the normal table layout. Cell templates should allow their content
to wrap instead of imposing fixed widths. Sorting, filtering, paging, selection, virtualization and
server-side data management are intentionally not included.

## Release checks

Install the browser once after building the browser-test project:

```powershell
dotnet build tests\FlintSoft.Components.BrowserTests
pwsh tests\FlintSoft.Components.BrowserTests\bin\Debug\net10.0\playwright.ps1 install chromium
```

The browser suite starts and stops its own local demo and WebAssembly hosts.

```powershell
dotnet test FlintSoft.Components.slnx
dotnet pack src --configuration Release
```

Review the demo in both themes and on narrow screens before publishing the preview.

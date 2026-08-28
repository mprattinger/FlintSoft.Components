# FlintSoft.Components

A comprehensive, modern Blazor component library built for .NET 10. FlintSoft.Components provides a rich collection of reusable, professionally-designed UI components for building responsive web applications.

## Features

- **30+ UI Components** - Ready-to-use Razor components for common UI patterns
- **.NET 10 Support** - Built on the latest .NET framework with modern C# features
- **Nullable Reference Types** - Full nullable reference type support for safer code
- **CSS Styling** - Each component includes scoped CSS for consistent styling
- **Theme Support** - Built-in theme toggle capability
- **Accessibility** - Components designed with accessibility in mind

## Components

### Input Components
- **TextInput** - Single-line text input field
- **NumberInput** - Numeric input with validation
- **Checkbox** - Single checkbox control
- **Radio & RadioGroup** - Radio button options
- **Dropdown** - Select from predefined options
- **AutoComplete** - Text input with autocomplete suggestions
- **DatePicker** - Intuitive date selection
- **DateTimePicker** - Combined date and time selection

### Buttons
- **Button** - Versatile button component with styling options
- **ThemeToggle** - Built-in light/dark theme switcher

### Modals & Dialogs
- **Modal** - Flexible modal dialog container
- **Message** - Simple message modal
- **YesNo** - Confirmation dialog

### Tables
- **Table** - Data table container
- **Column** - Table column definition
- **Row** - Table row component

### Notifications
- **Toast** - Non-intrusive notification messages

### Authentication
- **Persona** - User persona/profile component

### Container
- Layout and container components for structural organization

## Getting Started

### Installation

Add the FlintSoft.Components NuGet package to your Blazor project:

```bash
dotnet add package FlintSoft.Components
```

### Setup

1. Add the necessary namespace imports to your `_Imports.razor` (one `@using` per namespace you use):

```razor
@using FlintSoft.Components
@using FlintSoft.Components.Buttons
@using FlintSoft.Components.Container
@using FlintSoft.Components.Icons
@using FlintSoft.Components.Inputs
@using FlintSoft.Components.Modals
@using FlintSoft.Components.Tables
@using FlintSoft.Components.Toasts
@using FlintSoft.Components.Authentication
```

2. Link the library's design-tokens stylesheet **and** your app's scoped-CSS bundle in `App.razor` (the scoped-CSS bundle, e.g. `YourApp.styles.css`, automatically includes every component's `.razor.css` — you don't need to reference the library's CSS separately for that part):

```html
<link rel="stylesheet" href="_content/FlintSoft.Components/tokens.css" />
<link rel="stylesheet" href="@Assets["css/app.css"]" /> <!-- your own app CSS, optional overrides -->
<link rel="stylesheet" href="@Assets["YourApp.styles.css"]" />
```

`tokens.css` must be loaded before your own stylesheet if you want to override any of its `--fs-*` variables (see [Styling & Design Tokens](#styling--design-tokens) below).

## Usage Examples

### Basic Button

```razor
<Button>Click me</Button>
```

### Text Input

```razor
<TextInput 
    @bind-Value="@email" 
    Placeholder="Enter email address" 
    Class="my-custom-class" />
```

### Modal

```razor
<Modal IsVisible="@showModal" OnClose="@(() => showModal = false)" Title="Modal Title" HasCloseButton="true">
    <ChildContent>
        <p>Modal content goes here</p>
    </ChildContent>
    <Footer>
        <Button ButtonAppearance="Button.ButtonAppearanceEnum.Primary" OnClick="@(() => showModal = false)">Close</Button>
    </Footer>
</Modal>
```

### Data Table

`Table`/`Row`/`Column` are composable layout components — you build the table structure directly in markup (there's no `Items`/`Render` data-binding API):

```razor
<Table>
    <HeaderContent>
        <Column>Name</Column>
        <Column>Email</Column>
        <Column>Status</Column>
    </HeaderContent>
    @foreach (var item in items)
    {
        <Row>
            <Column>@item.Name</Column>
            <Column>@item.Email</Column>
            <Column>@item.Status</Column>
        </Row>
    }
</Table>
```

### Theme Toggle

```razor
<ThemeToggle />
```

### Toast Notification

```razor
<Toast IsVisible="@showToast"
       OnClose="@(() => showToast = false)"
       Title="Success"
       ToastAppearance="Toast.ToastAppearanceEnum.Success">
    Operation completed successfully
</Toast>
```

## Component Base Class

All components inherit from `MyComponentBase`, which provides:

- **Element Reference** - Direct access to underlying DOM element
- **Id Parameter** - Custom ID for component identification
- **Class Parameter** - CSS class customization
- **Style Parameter** - Inline style customization

Example with custom styling:

```razor
<TextInput 
    Id="user-email"
    Class="form-control"
    Style="margin-bottom: 10px;" />
```

## Utilities

### Extension Methods

The library includes utility extension methods:

- **ToAttributeValue()** - Convert enum values to HTML attribute values
- **GetDescription()** - Retrieve enum descriptions with optional case conversion

Example:

```csharp
public enum Priority { High, Medium, Low }
var value = Priority.High.ToAttributeValue(); // "high"
```

## Architecture

- **Namespace**: `FlintSoft.Components`
- **Target Framework**: .NET 10.0
- **Nullable Types**: Enabled
- **Implicit Usings**: Enabled
- **Browser Support**: Web browser platform

## Styling & Design Tokens

Components use scoped CSS modules (`.razor.css` files) to prevent style conflicts and keep each component visually consistent. All colors, radii, shadows, spacing and z-index values are read from a single set of CSS custom properties prefixed with `--fs-` (FlintSoft), shipped in **`_content/FlintSoft.Components/tokens.css`**.

There are no hardcoded colors or per-property fallback values inside component styles — if a `--fs-*` variable isn't defined somewhere in scope (usually on `:root`), the properties that use it are simply ignored by the browser. So one of the following is required:

- **Recommended:** link `tokens.css` (see [Setup](#setup)) to get a complete, ready-to-use default palette, then override only the variables you want to rebrand in your own stylesheet (loaded *after* `tokens.css`).
- **Alternative:** skip `tokens.css` entirely and define all `--fs-*` variables yourself on `:root` in your app's CSS.

### Token reference

| Category | Variables |
|---|---|
| Layout | `--fs-color-background`, `--fs-color-surface`, `--fs-color-surface-text`, `--fs-color-text`, `--fs-color-text-muted`, `--fs-color-border` |
| Brand | `--fs-color-primary`, `--fs-color-primary-foreground`, `--fs-color-secondary` |
| Form controls | `--fs-color-control-text`, `--fs-color-control-text-inverse`, `--fs-color-control-filter` (icon filter for native date/time pickers), `--fs-color-disabled` |
| Semantic | `--fs-color-action`, `--fs-color-success`, `--fs-color-success-hover`, `--fs-color-danger`, `--fs-color-danger-hover`, `--fs-color-white`, `--fs-color-black` |
| Avatar / Persona | `--fs-color-avatar-bg`, `--fs-color-avatar-initials-bg`, `--fs-color-avatar-fg` |
| Elevation | `--fs-overlay`, `--fs-shadow-sm`, `--fs-shadow-md`, `--fs-shadow-lg`, `--fs-shadow-xl` |
| Shape | `--fs-radius-sm`, `--fs-radius-md`, `--fs-radius-lg` |
| Spacing | `--fs-space-1` … `--fs-space-6` |
| Z-index | `--fs-z-flyout`, `--fs-z-modal` |

### Re-branding example

```css
/* your app's own CSS, loaded after tokens.css */
:root {
    --fs-color-primary: #6d28d9;
    --fs-color-action: #f59e0b;
    --fs-radius-sm: .5rem;
}
```

### Dark mode

`<ThemeToggle />` switches themes by toggling a `dark-theme` class on `<html>`. `tokens.css` ships a matching `.dark-theme` override block that redefines the relevant `--fs-*` variables — no extra setup required. If you fully replace `tokens.css` with your own variable definitions, make sure to add your own `.dark-theme { ... }` overrides too.

## JavaScript Interop

Select components include JavaScript interop for enhanced functionality:
- AutoComplete suggestions
- DatePicker interactions
- Theme persistence
- Modal animations

## Requirements

- .NET 10.0 or later
- ASP.NET Core 10.0 or later
- Modern web browser with WebAssembly support

## License

[Add your license information here]

## Contributing

Contributions are welcome! Please ensure:
- Code follows the existing style conventions
- Components include both `.razor` and `.razor.css` files
- JavaScript interop is properly typed
- Components extend `MyComponentBase`

## Support

For issues, feature requests, or questions, please open an issue in the repository.

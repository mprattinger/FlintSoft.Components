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

1. Add the necessary imports to your `_Imports.razor`:

```razor
@using FlintSoft.Components
```

2. Include the component CSS in your `App.razor`:

```html
<link href="_framework/blazor.web.css" rel="stylesheet" />
<link href="flintsoft-components.bundle.css" rel="stylesheet" />
```

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
<Modal IsOpen="@showModal" OnClose="@OnModalClose">
    <h3>Modal Title</h3>
    <p>Modal content goes here</p>
</Modal>
```

### Data Table

```razor
<Table Items="@items">
    <Column Header="Name" Render="@((item) => item.Name)" />
    <Column Header="Email" Render="@((item) => item.Email)" />
    <Column Header="Status" Render="@((item) => item.Status)" />
</Table>
```

### Theme Toggle

```razor
<ThemeToggle />
```

### Toast Notification

```razor
<Toast Message="Operation completed successfully" Type="Success" />
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

## Styling

Components use scoped CSS modules (`.razor.css` files) to:
- Prevent style conflicts
- Maintain component isolation
- Provide consistent theming
- Support light/dark mode

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

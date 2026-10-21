# FlintSoft.Components v2 Implementation Plan

## Scope

Build a clean, .NET 10-only v2 API without a v1 compatibility layer.
Support Blazor Interactive Server and WebAssembly, including prerendering.

Use `ideas/index.html` and `ideas/style.css` as the visual baseline.
The library currently contains the project skeleton, and Demo contains the
Blazor template.

## 1. Foundation

- Structure the Razor Class Library into inputs, buttons, modals, toasts and tables.
- Keep lifecycle handling prerender-safe.
- Provide standalone, scoped or prefixed styling without a Bootstrap dependency.
- Define `--fs-*` design tokens for colors, spacing, typography, borders and focus states.
- Provide light and dark themes through token overrides.
- Establish accessibility conventions: associated labels, keyboard interaction,
  visible focus and appropriate announcements.

## 2. Inputs and Form Integration

Provide separate typed controls for:

- Text, including password and email.
- Numeric values, including integers and decimals.
- Date and time.
- Textarea.
- Single select.
- Checkbox.
- Radio groups.
- Autocomplete with async search, custom suggestion templates, keyboard selection
  and optional new text values.

File upload is outside the initial release scope.

Share the field layout rather than implementing one universal input component.
The shared layout handles floating labels, prefixes and suffixes, help text and
validation. Typed controls handle parsing and behavior. Numeric inputs can use
a generic value type for integers and decimals.

### Binding and Validation

- Support `@bind-Value` both standalone and inside `EditForm`.
- Integrate with `EditContext` and DataAnnotations, including field modification
  tracking and validation messages.
- Support nullable values, disabled and read-only states, and unmatched HTML
  attributes where appropriate.
- Respect the current app culture, including decimal commas.
- Use format handling appropriate to native date and time inputs.
- Default to change-based updates, with optional input-based updates where meaningful.
- Preserve intermediate numeric text while typing instead of prematurely coercing
  or clearing it.
- Keep checkbox and radio styling distinct from floating-label fields.

## 3. Buttons

Provide the following appearances using theme tokens:

- Primary.
- Secondary.
- Action.
- Danger/failure.
- Success.

Support explicit button types, disabled states and click callbacks. Default to
`type="button"` to prevent accidental form submission; make submit behavior explicit.

## 4. Modals

- Expose `OpenAsync` and `CloseAsync` for use through a component reference.
- Await an initialization callback before showing content.
- Propagate initialization failures rather than opening a misleading empty modal.
- Support an optional title, `ChildContent`, a close button and close notifications
  containing the close reason.
- Make Escape and backdrop dismissal configurable.
- Allow one active modal at a time, with coordinated activation.
- Block background interaction and lock background scrolling.
- Trap focus while open and restore focus on close.
- Limit JavaScript to browser behavior that Blazor and CSS cannot reliably provide alone.

## 5. Toasts

Use an injected service and one toast host per interactive application scope.

- Provide info, success, danger and warning styles.
- Support optional titles and either text or `RenderFragment` content.
- Support configurable auto-dismiss and manual close.
- Position toasts at the top right on desktop and adapt to narrow screens.
- Provide accessible announcements.
- Define sensible timer behavior during interaction.
- Clean up timers and subscriptions on disposal.

## 6. Presentation-Only Table

- Use a generic items API with templated columns, headers and cells.
- Support row actions, loading states and empty states.
- Use semantic HTML `<table>` elements rather than a div-based grid.
- At widths up to 600px, display each row as a stacked card with column labels,
  all values and row actions visible without horizontal scrolling. Preserve table
  semantics and the normal desktop layout.

Sorting, paging, filtering, row selection, server-side data loading and
virtualization are outside the initial v2 release scope.

## 7. Demo, Verification and Release Documentation

### Demo

- Connect Demo to the library.
- Replace template pages with Inputs, Buttons, Modals, Toasts and Tables pages.
- Demonstrate standalone binding, validated forms and nullable values.
- Demonstrate decimal-comma culture, both themes and responsive layouts.

### Verification

- Add focused component tests for binding and validation, modal lifecycle,
  toast lifecycle and table rendering.
- Add browser-level coverage for keyboard and focus behavior, modal background
  blocking and responsive behavior.
- Verify Interactive Server and WebAssembly hosting, including prerendering.

### Documentation and Packaging

- Document installation and stylesheet inclusion.
- Document service and host registration.
- Document theme tokens and component usage examples.
- Prepare NuGet metadata.
- Review the public API before publishing a v2 preview.

## Implementation Order

1. Foundation.
2. Inputs and buttons.
3. Modals.
4. Toasts.
5. Tables.
6. Final API review, documentation and packaging.

Add demo examples and focused tests alongside each component family rather than
deferring them until the end. Finalize packaging after exercising the APIs through
the demo.

The scope decisions above are resolved. Implementation can proceed from this plan
without further requirements clarification.

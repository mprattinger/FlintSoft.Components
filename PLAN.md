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

## Markdown Editor Implementation Plan

### Scope and source

Add an `FsMarkdownEditor` Blazor input, using the existing editor in
`C:\dev\html\tryouts-controls\src\js\markdown-editor.js` and its browser tests
as the behavioral baseline. The component edits Markdown as text; rendering,
preview, Markdown parsing and HTML sanitization are outside this scope.

### 1. Component and form integration

- Follow the existing input conventions for `@bind-Value`, `ValueExpression`,
  labels, help text, additional attributes, disabled and read-only states,
  focus, and `EditForm` validation and field modification notifications.
- Keep the textarea under Blazor's control while using browser interop for
  selection-aware editing. Synchronize typing, programmatic formatting and
  external value changes without duplicate notifications, feedback loops or
  lost edits. Define how null and empty values map to the textarea.
- Initialize browser behavior after interactive rendering, support prerendering,
  Interactive Server and WebAssembly, and dispose listeners and references.
  Prevent late asynchronous work from updating a disposed editor.

### 2. Default and custom toolbars

- Provide the source editor's ten actions by default: bold, italic, headings
  1 through 3, bulleted list, numbered list, code block, link and quote.
- Add a named `Toolbar` render fragment that replaces the default toolbar when
  supplied. A custom toolbar can call public asynchronous methods on the editor
  reference for every default action, including a heading level and list type
  where appropriate. Both toolbars use the same editing implementation.
- Retain the selection when a toolbar control takes focus, including keyboard
  activation, and apply edits to the correct editor instance. Do not edit when
  disabled or read-only. Use non-submit buttons, accessible names and visible
  keyboard focus for the default toolbar.
- Preserve the source's list continuation, empty-list exit and indentation
  behavior while ensuring keyboard users can leave the editor. Document the
  chosen Tab and link-entry interaction.

### 3. Host-managed image paste

- Offer an optional asynchronous paste handler that receives a typed payload
  for the pasted image, including its bytes, MIME type and filename when
  available. The host processes or uploads the image and returns its path or
  URL; the editor inserts a Markdown image link with a documented alt-text and
  escaping convention. The component itself does not upload the image.
- Non-image paste uses the browser's normal behavior. When no image handler is
  configured, leave image paste to the browser as well. Choose whether to
  intercept synchronously, before starting the async handler: the browser's
  original default action cannot be resumed after an asynchronous decision.
- Preserve a meaningful insertion position while processing is pending. Define
  and test how intervening edits, external value changes, multiple pending
  pastes, read-only changes and disposal affect the result so stale positions
  cannot silently corrupt text. Surface read, interop and handler failures;
  never insert a success-shaped placeholder or silently discard an error.
- Document data transfer and size considerations for Interactive Server,
  where image bytes cross the circuit, and describe the native-paste behavior
  when the handler is not configured.

### 4. Styling, demo and documentation

- Use the existing `--fs-*` tokens for light and dark themes; make the toolbar
  wrap and the editor usable on narrow screens. Do not carry over the source
  demo's fixed width and viewport height as component defaults.
- Add a demo with default and custom toolbars, binding and form validation,
  plus a host-controlled image-paste example. Update the README with API
  examples, keyboard behavior, paste semantics and preview/sanitization scope.

### 5. Verification

- Add focused component tests for binding, external updates, validation,
  disabled/read-only behavior, default and custom toolbar rendering, and
  interop lifecycle and failure handling.
- Add browser tests for all actions, selection and focus, keyboard behavior,
  multiple independent editors, normal text paste, handled image paste,
  pending-paste races and disposal. Exercise Interactive Server and WebAssembly
  and check both themes and narrow layouts.
- Build the affected projects and run the focused component and browser tests;
  verify the packaged static assets are available to consuming applications.

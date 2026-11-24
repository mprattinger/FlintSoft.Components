# Generic tree component implementation plan

Status: implemented, including drag-and-drop, demo, documentation, and focused
verification. See README.md for the public usage contract and Demo's `/trees` page.

## 1. Goal and baseline

Add a reusable `FsTree<TItem>` to FlintSoft.Components, supporting arbitrary
application data rather than assuming files and folders.

Use these existing sources as the behavioral and visual starting point:

- `C:\dev\html\tryouts-controls\src\js\structure-tree.js`
- `C:\dev\html\tryouts-controls\src\css\structure-tree.css`
- `C:\dev\html\tryouts-controls\src\tests\drag-and-drop.test.html`
- `C:\dev\html\tryouts-controls\STRUCTURE-TREE.md`

The source already implements nested rendering, unique string IDs, per-item drag
permission, same-tree move validation, descendant-cycle prevention, highlighting,
and move notifications. It does NOT implement expansion/collapse, activation
policy, lazy loading, or accessible tree keyboard navigation.

Port the useful concepts, not the file/folder schema or DOM ownership. Blazor
should own the rendered tree and its state; do not let the original JavaScript
replace Blazor-managed DOM. Derive depth from nesting instead of storing `level`.
Follow this repository's `FlintSoft.Components` namespace, `Fs*` naming,
`EventCallback` conventions, theme tokens, and interactive hosting support.

## 2. Recommended public data model

Use a small generic presentation wrapper so applications do not need to inherit
from a library class or add UI-specific properties to their domain models:

```csharp
public sealed class FsTreeItem<TItem>
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required bool ShowActions { get; set; }
    public TItem? Value { get; init; }
    public IReadOnlyList<FsTreeItem<TItem>> Children { get; set; } = [];
    public bool? IsExpandable { get; set; }
    public bool? IsClickable { get; set; }
    public bool IsDraggable { get; set; }
    public bool CanAcceptDrop { get; set; }
}
```

- `Id` is non-empty, stable, case-sensitive, and unique across one tree instance.
  Different tree instances may reuse the same IDs.
- `Value` holds the application's original object. Text and structure remain
  usable without a payload.
- `Text` supplies the default label and keyboard-search text, even with templates.
- `ShowActions` is a required bool on every initializer: true renders the `Actions`
  slot for that node; false hides it. No container is rendered without a template.
- `Children` preserves caller-provided ordering. Empty children do not necessarily
  mean a leaf: a node may be expandable before its children have been loaded.
- Nullable policy properties mean "inherit the tree configuration" when null.
  Explicit false is a default policy, NOT an absolute prohibition: a request
  handler may override it for one interaction.
- Do not require `Type`, `Level`, parent pointers, URLs, or file/folder icons.
- Validate missing/duplicate IDs, cycles, and a node appearing under multiple
  parents. Report invalid input explicitly rather than hiding invalid nodes.
- The component does not mutate the supplied hierarchy. Applications may replace
  `Children` or supply a new item graph; stable IDs preserve applicable UI state.
- `IsDraggable` defaults to false and controls whether that item can start a move.
  This is independent of `IsClickable` and `IsExpandable`.
- `CanAcceptDrop` defaults to false and explicitly permits receiving children.
  A non-draggable node may accept drops, including when its children are empty.
  This replaces the source library's folder-only destination restriction without
  requiring a file/folder type. Expansion permission does not imply drop permission.

Recommendation: yes, include `IsClickable`. It expresses row activation policy
independently of expansion. It does not mean disabled, focusable, or selected.
Initially keep this model tree-specific; do not introduce a shared list-item base
class without another concrete component that needs it.

## 3. Component parameters and rendering

Proposed initial surface:

| Parameter                            | Purpose / default                                                                                                                |
| ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| `Items`                              | Required root nodes, `IReadOnlyList<FsTreeItem<TItem>>`.                                                                         |
| `DefaultIsExpandable`                | `bool?`, default null: infer from non-empty children. True permits lazy/empty branches; false denies by default.                 |
| `DefaultIsClickable`                 | `bool`, default true.                                                                                                            |
| `Disabled`                           | Hard interaction block, default false; request events cannot override it.                                                        |
| `ItemDropped`                        | Awaited callback for a valid drop; the application persists the move and updates the bound hierarchy. Moving requires a handler. |
| `ExpandedIds` / `ExpandedIdsChanged` | Optional controlled expansion state, bindable; internal state when omitted.                                                      |
| `SelectedId` / `SelectedIdChanged`   | Optional controlled single selection, bindable; internal state when omitted.                                                     |
| `ItemTemplate`                       | Optional `RenderFragment<FsTreeItem<TItem>>` for label content.                                                                  |
| `Actions`                            | Optional `RenderFragment<FsTreeItem<TItem>>` beside the label, rendered only when the node's required `ShowActions` is true. |
| `EmptyContent`                       | Optional content when there are no roots.                                                                                        |
| `EmptyChildrenContent`               | Optional content for an expanded branch with no children.                                                                        |
| `AccessibleLabel`                    | Accessible tree name; document a non-empty default.                                                                              |
| `Class` / additional attributes      | Follow existing component conventions.                                                                                           |

Keep expansion, selection, focus, and activation as distinct concepts:

- The disclosure affordance expands/collapses only; it does not activate or select.
- An accepted label click or keyboard activation selects the item and emits
  `ItemClicked`. It does not automatically expand it or navigate anywhere.
- Focus alone never selects or activates. Keyboard navigation can pass through
  non-clickable nodes, and their children may still be expandable/clickable.
- Provide `Actions`, an optional `RenderFragment<FsTreeItem<TItem>>` beside each
  label, gated by the node's required `ShowActions` property. It supports add-child
  icon buttons, vertical-dots context menus, and arbitrary application controls.
- Actions must not activate/select/expand a row or initiate a drag. Disable native
  slot controls when the tree is disabled, preserve normal keyboard interaction,
  and require accessible names on icon-only controls. Avoid empty action containers.
- Clicking a nested label-template action must not accidentally activate the row
  or an ancestor; use stopPropagation there, or prefer the dedicated Actions slot.
- Key rows by stable node ID, not object reference or array position.
- Use component-instance-prefixed DOM IDs rather than raw application IDs.

Do not add multi-selection, checkboxes, filtering, sorting, virtualization, or
routing to the first implementation. These can be separate features later.

## 4. Request events: allow, deny, or use configuration

Use one explicit tri-state enum shared by tree request event arguments:

```csharp
public enum FsTreeDecision
{
    Default,
    Allow,
    Deny
}
```

Expose awaited request events with mutable `Decision`, initially `Default`:

- `ExpansionRequested`: arguments include `Item`, `Id`, `ParentId`,
  `IsExpanded` (current state), `RequestedExpanded` (target state), `Decision`,
  and a cancellation token for obsolete/disposed requests.
- `ItemClickRequested`: arguments include `Item`, `Id`, `ParentId`, `Decision`,
  and a cancellation token. `Item` is the clicked node; its `Value` is the
  application payload. Use the same event for keyboard activation.

Use concrete generic argument types such as
`FsTreeExpansionRequestEventArgs<TItem>` and
`FsTreeItemClickRequestEventArgs<TItem>`, exposed through `EventCallback<TArgs>`.
A request handler can await authorization, confirmation, or loading, then set the
final decision. A handler that leaves the decision unchanged defers to config.

### Exact policy precedence

For an expansion attempt, resolve the configured policy as:

`Item.IsExpandable ?? DefaultIsExpandable ?? (Item.Children.Count > 0)`

For activation, resolve it as:

`Item.IsClickable ?? DefaultIsClickable`

Then apply the request decision:

| Handler decision         | Effective result                                              |
| ------------------------ | ------------------------------------------------------------- |
| `Default`, or no handler | Use the configured policy above.                              |
| `Allow`                  | Permit, even when the item property or tree default is false. |
| `Deny`                   | Reject, even when the item property or tree default is true.  |

Critical: raise the request event BEFORE rejecting an interaction because its
configured policy is false. Otherwise the event could never override that false.
An override applies only to that request; do not rewrite item properties.

Hard guards run first: a disabled/disposed tree, missing node, duplicate pending
request, or action already at its target state does not start another request.
Policy denial is an expected outcome, not an error. Exceptions are failures,
not implicit approval or a silent fallback to configuration.

### Disclosure chevrons and unloaded branches

Populated branches use a right chevron when collapsed and a down chevron when
expanded. A handler can still override `IsExpandable == false` through the same
request pipeline. Childless items never display a disclosure chevron, regardless
of configured policy, handler presence, or current expansion state.

Keep expansion semantics and keyboard requests for configured/unloaded branches:
Right/Left arrows or `RequestExpansionAsync` from an application action can still
request expansion/collapse and trigger lazy loading. Show the chevron after children
are loaded, and remove it when the last child is removed. An allowed expansion may
still have no children; display optional empty-children content without inventing
child nodes. Empty branches can use an explicit Load children action in `Actions`.

### Collapse behavior

Use `ExpansionRequested` for both expansion and collapse, distinguished by
`RequestedExpanded`. For collapse, `Default` permits collapse regardless of
`IsExpandable`; otherwise a policy change could trap an already-open node.
`Deny` can explicitly veto collapse, and `Allow` explicitly permits it.

## 5. Completed events and ordering

Expose separate notifications so consumers can distinguish intent from success:

- `ExpandedChanged`: `Item`, `Id`, and final `IsExpanded` after an accepted change.
- `ItemClicked`: `Item`, `Id`, and the selected ID after accepted activation.

The clicked item is exposed as `Item`, not an ambiguous `Clicked` boolean.
If "clicked property" means something other than the clicked node/payload,
confirm that before implementation. Do not persist a transient `IsClicked` flag.
Use `SelectedId` for persistent selected state.

Expansion ordering:

1. Resolve the live item and validate hard guards.
2. Await `ExpansionRequested` without changing expansion state.
3. Revalidate request identity and resolve its decision against current config.
4. Commit internal state, or await `ExpandedIdsChanged` in controlled mode.
5. Emit `ExpandedChanged` only when the accepted state is actually applied.

Activation ordering:

1. Resolve the live item and validate hard guards.
2. Await `ItemClickRequested`, then revalidate and resolve the decision.
3. If allowed, update/request selection; emit `SelectedIdChanged` only if changed.
4. Emit `ItemClicked` once per accepted activation, including repeated activation
   of an already-selected item. In controlled mode report the actual selected ID;
   a parent may choose not to accept a selection change.

A denied request does not alter selection/expansion or emit a success event.
Request callbacks are decision hooks; irreversible application actions belong
in completed callbacks. External side effects cannot be undone by this component.
Keyboard, pointer, and any future public interaction methods must share these
pipelines. External parameter updates are authoritative state synchronization,
not user requests, and must not emit request events or notification loops.

## 6. State ownership, async work, and lazy children

- When a state parameter is supplied, the parent owns it: do not mutate a supplied
  collection or keep a competing optimistic copy. Use `OnParametersSet` to
  reconcile accepted updates and pending expansion notifications.
- Without the parameter, maintain the corresponding state internally. Start
  collapsed and unselected. Null `ExpandedIds` means uncontrolled; an empty ID
  collection means controlled with all nodes collapsed. Supplying `SelectedId`,
  including null (no selection), means controlled selection; omit it for internal state.
- Preserve state across replacement objects with the same IDs. Prune internal
  state for removed nodes; ignore stale IDs from controlled state for rendering
  without rewriting the parent's parameters.
- Collapsing an ancestor keeps descendant expansion and selection state. If focus
  would become hidden, move focus to the collapsed ancestor. If a node is removed,
  restore focus to the nearest surviving ancestor or first visible node.
- Support lazy loading inside `ExpansionRequested`: the application awaits its
  data source, updates the node's children or the bound graph, and decides whether
  to allow expansion. No separate data-provider abstraction in the first version.
- A handler can leave `Decision` at `Default` after loading; evaluate configuration
  after it completes. Set `Allow` to override an explicit false policy.
- Track pending work per node; suppress duplicate/conflicting operations on that
  node while permitting independent nodes to work. Show pending expansion state
  and `aria-busy` without falsely showing the branch as already expanded.
- Cancel/invalidate pending work on disposal, node removal/replacement during the
  request, disabling, or superseding authoritative state updates. Revalidate after
  every await so stale work cannot expand a removed node or overwrite newer state.
  Ordinary replacement between completed interactions still preserves ID state.
- Clear pending indicators in `finally`. Surface callback failures through normal
  Blazor error handling/ErrorBoundary; never interpret an exception as success.
  An application may show its own recoverable loading error and deny the request.
- Instantiate no browser-only APIs during prerendering. Dispose any browser
  listeners and cancellation resources for Server and WebAssembly hosts.

## 7. Accessibility and presentation

- Implement the WAI-ARIA tree pattern: named `tree`, `treeitem`, nested `group`,
  truthful `aria-expanded`, and single-selection `aria-selected`.
- Use one roving tab stop, visible focus, and visible-node keyboard navigation:
  Up/Down, Home/End, Right to request expansion or move to the first visible child,
  Left to request collapse or move to the parent, and type-ahead using `Text`.
- Enter/Space activate through the same click-request policy. Prevent page scrolling
  for handled keys; Tab must leave the tree normally.
- Make disclosure interaction keyboard accessible without creating a tab stop for
  every icon. Avoid nested interactive elements with conflicting semantics.
- Do not map `IsClickable == false` to whole-node `aria-disabled`: expansion and
  focus may still be available, and an event may override the activation default.
- Adapt indentation and branch highlighting to `--fs-*` theme tokens, light/dark
  themes, long labels, and narrow layouts. Use logical CSS spacing for direction.
- Render labels as text by default; custom templates remain Razor-rendered content,
  not raw HTML copied from the original JavaScript.
- Keep JavaScript minimal and limited to focus and browser drag/drop behavior
  Blazor cannot reliably provide, such as synchronous `DataTransfer` handling.
  Never use JavaScript as a second hierarchy, expansion, or selection engine.

## 8. Drag-and-drop: required initial scope

Drag-and-drop is part of the initial implementation, based on the source library's
move behavior and `structure-move` event. The application must receive enough
information to update its actual data source, not just rearrange rendered rows.

### Permissions and validation

- A source needs `IsDraggable == true`; a destination needs `CanAcceptDrop == true`.
  A disabled tree or a tree without an `ItemDropped` handler cannot start moves.
  These drag/drop permissions are explicit guards, separate from the expansion
  and activation tri-state policies.
- Initially support moving a node (including its descendants) into a destination
  node, appended to its children, matching the source library. Moving a root item
  into a node is supported; dropping onto the root background is not initial scope.
- Reject self-drops, moving a node into its descendants, same-parent drops,
  missing/stale IDs, external drag payloads, and cross-tree drops. Resolve nodes
  from the current tree rather than trusting serialized client data.
- Highlight only valid destinations; clear highlights and drag state on leave,
  drop, drag cancellation, removal, disable, navigation, and disposal.
- Browser drag feedback may use minimal JavaScript, but all move validation is
  repeated in .NET immediately before invoking the application callback.

### Drop event and data-source ownership

Expose `EventCallback<FsTreeDropEventArgs<TItem>> ItemDropped`. Raise it once after
an actual, valid drop (not during drag-over), with this proposed read-only payload:

| Property                          | Meaning                                                                                 |
| --------------------------------- | --------------------------------------------------------------------------------------- |
| `Items`                           | Current complete root collection, corresponding to the source event's `structure.root`. |
| `Item` / `Id`                     | Dragged node and stable ID; `Item.Value` is its domain object.                          |
| `SourceParent` / `SourceParentId` | Original parent; both null for a root item.                                             |
| `TargetParent` / `TargetParentId` | Destination node and stable ID.                                                         |
| `SourceIndex`                     | Original zero-based index in the source parent's children or root collection.           |
| `TargetIndex`                     | Proposed zero-based insertion index: the destination's current child count.             |
| `CancellationToken`               | Signals disposal or invalidation while the callback is pending.                         |

This preserves the useful information in the JavaScript event (`structure`,
`item`, `sourceParent`, `targetParent`) and adds IDs and positions for updating a
remote or immutable data source. Payload objects are references, not immutable
snapshots; IDs and index values capture the validated drop context.

The event deliberately reports a valid drop intent, not successful persistence:

1. Validate the move, capture its context, and clear native drag feedback.
2. Await `ItemDropped`. The handler may validate application rules, save the new
   parent/order in its data source, and update/reload the bound `Items`/`Children`.
3. Render the hierarchy provided by the application. Do not mutate caller-owned
   collections, create a competing moved copy, or optimistically claim success.
4. If the application rejects the operation, it leaves the hierarchy unchanged
   and presents its reason. If persistence fails, it handles/reports the failure
   or lets normal Blazor error handling surface it; the tree must not silently
   report a successful move. An unchanged hierarchy remains in its original place.

The source JavaScript mutates before emitting `structure-move`; this is an
intentional adaptation for a generic, application-owned Blazor data source.
Document the ordering difference prominently. `ItemDropped` is not named
`ItemMoved` because a valid drop does not guarantee a completed database update.
No additional request/completed move event pair is needed for this first API.

While a drop callback is pending, block further moves in that tree, show a pending
state, and release it in `finally`. Cancel/invalidate pending work on disposal or
relevant external hierarchy changes; the handler must honor cancellation and
revalidate data-source constraints before committing. Its own authoritative graph
update is expected, not a second move. Authorization belongs in the application
or server, never in the browser's draggable flag.

Preserve expansion and selection by ID after moves; derive new depth from nesting.
Do not implicitly activate a dragged item, auto-expand a collapsed destination,
or emit expansion/click events. Suppress the synthetic click following a drag.
Restore focus to the moved node if visible, otherwise to a visible destination or
ancestor. Provide a keyboard/non-drag move alternative that uses the same guards
and `ItemDropped` callback, with accessible pending/result announcements.

Cross-tree moves, sibling reordering, dropping onto the root background, and
hover-to-expand remain future extensions, not requirements for initial parity.

## 9. Implementation phases and affected surfaces

### Phase 1: API and policy core

- Finalize the proposed model, event names, precedence table, and state ownership,
  including `IsDraggable`, `CanAcceptDrop`, and the `ItemDropped` payload/contract.
- Add `src\Trees` with `FsTreeItem<TItem>`, `FsTreeDecision`, event args, and the
  component's shared interaction logic. Keep helper types internal unless public
  consumers actually need them.
- Add focused tests for the policy matrix, ID/structure validation, and move guards.

### Phase 2: Rendering and interaction

- Implement `src\Trees\FsTree.razor`; introduce an internal recursive node
  component only if needed to keep the Razor implementation clear.
- Implement disclosure, activation, selection, controlled/uncontrolled state,
  async guards, lazy-child updates, templates, and explicit error behavior.
- Add theme-aware styles using the repository's existing stylesheet conventions.

### Phase 3: Drag-and-drop and data-source integration

- Implement property-controlled dragging and destination acceptance, instance
  ownership checks, cycle/no-op rejection, highlighting, and cleanup.
- Wire the awaited `ItemDropped` callback with complete item/parent/index context;
  keep hierarchy mutation and persistence in the application.
- Add pending/error behavior and protect against duplicate and stale operations.
- Port relevant source-library scenarios to the existing component/browser tests.

### Phase 4: Accessibility and browser lifecycle

- Implement tree semantics, keyboard navigation, focus restoration, type-ahead,
  pending announcements, minimal browser interop, and a non-drag move alternative.
- Verify nested content, multiple instances, prerendering, and disposal.

### Phase 5: Showcase and documentation

- Add `Demo\Components\Pages\Trees.razor` and a navigation link in
  `Demo\Components\Layout\NavMenu.razor`.
- Demonstrate a domain model unrelated to files, custom labels, lazy children,
  independent expand/click policies, allow/deny/default handlers, and state binding.
- Show a small event log proving denied attempts do not emit completed events.
- Demonstrate draggable/non-draggable sources, a non-draggable destination that
  accepts drops, root and nested moves, and an awaited data-source update using
  the event's item, parents, and positions. Include a rejected/failed save example.
- Update `README.md` with API examples, policy precedence, event ordering,
  controlled-state rules, the disclosure-affordance trade-off, and the drop-event
  payload and application-owned persistence contract.
- Add coverage in `tests\WasmHost\App.razor` for the relevant browser scenarios.

All five phases, including drag-and-drop, belong to the initial tree release.

## 10. Verification and acceptance criteria

Use the existing xUnit/bUnit component project and Playwright browser project;
no new test framework or test project is needed.

Component tests should verify:

- Required/duplicate IDs, cycles, duplicate placement, multiple roots, and ordering.
- The full cross-product of item policy true/false/null, tree defaults, handler
  absent/Default/Allow/Deny, for both expansion and activation. For expansion,
  also test inferred empty/non-empty children and nullable tree defaults.
- In particular, false + Allow succeeds and true + Deny fails through real UI events.
- A false policy still permits invoking its override handler; a disabled tree does not.
- Collapse defaults to allowed even after expand policy becomes false; explicit veto works.
- Exact callback ordering and counts, clicked item/payload identity, repeat clicks,
  unchanged-state no-ops, and disclosure clicks not causing activation.
- Controlled state accepted/rejected by the parent, uncontrolled state, object
  replacement with stable IDs, stale IDs, and programmatic updates without loops.
- Lazy load success, empty results, callback exceptions, duplicate interactions,
  concurrent independent nodes, removal/replacement/disable/disposal during awaits,
  and external state updates superseding pending requests.
- Plain-text encoding, custom templates, additional attributes, and isolated instances.
- `IsDraggable` and `CanAcceptDrop` independently control sources/destinations;
  non-draggable destinations may receive moves and click/expand policy is unrelated.
- Self/descendant, same-parent, cross-tree, external, stale, and disabled drops do
  not emit `ItemDropped`; a missing callback does not enable unusable dragging.
- Valid root/nested and subtree drops emit exactly one callback with the correct
  root collection, item/payload, nullable source parent, destination, and indices.
- The component never mutates supplied collections. Application updates render
  the move, stable IDs preserve state, and rejected/failed saves retain the original
  hierarchy without false success notifications.
- Pending callbacks suppress duplicate moves, release pending state on failure,
  and handle graph invalidation, cancellation, and disposal safely.

Browser tests should verify:

- Keyboard paths use the same allow/deny/default policies as pointer paths.
- Roving focus, Tab escape, type-ahead, collapse/removal focus recovery, and ARIA state.
- Embedded actions do not activate ancestors; disclosure and activation remain separate.
- Pending UX, narrow layout, long labels, both themes, and multiple instances.
- Real browser drag/drop honors source/destination properties and instance isolation;
  highlights clear on leave/drop/cancel, and invalid drops never reach the callback.
- A valid drop updates the demo's actual data source through `ItemDropped` and
  remains correct after rerendering. Failed persistence does not visually move it.
- The non-drag move alternative uses the same payload and validation. Moving does
  not accidentally activate/select/expand nodes, and focus recovers correctly.
- Interactive Server and WebAssembly behavior, including prerender-safe initialization
  and cleanup when navigating away during async work.

Build changed projects and run the smallest relevant test selections first, then
broader validation only where the changes warrant it.

## Implementation outcome

All five implementation phases are present:

- `src\Trees` contains the generic component, node model, tri-state decisions,
  event args, controlled/internal state, and shared interaction pipelines.
- `src\wwwroot\tree.js` handles browser keyboard/focus and native drag transport;
  the existing main stylesheet contains theme-aware tree styles.
- Demo's `/trees` page demonstrates policies, lazy loading, application-owned
  moves, rejected saves, bound state, and custom labels. The WebAssembly host
  also exercises the tree.
- `README.md` documents the API, callback order, state ownership, and the intentional
  difference from the source library's mutate-before-notify move event.

Verified on implementation:

- Solution build succeeded, including isolated validation while the app was debugged.
- All 193 component regression tests passed, including 129 tree cases.
- All seven focused tree browser tests passed across Interactive Server and
  WebAssembly, covering native moves, rejected persistence, keyboard/policy
  behavior, lazy loading, templates, and narrow-screen non-drag moving.
- Chevron direction and childless visibility, add-child actions, context menus,
  per-node action visibility, disabled controls, and action isolation are covered.

Sibling reordering, cross-tree moves, root-background drops, hover-to-expand,
virtualization, and multi-selection remain outside this initial implementation.

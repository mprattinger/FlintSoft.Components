const trees = new WeakMap();
const dragType = "application/x-flintsoft-tree-item";

export function attachTree(root, reference) {
    if (trees.has(root)) return;
    let drag = null;
    let highlighted = null;
    let suppressClickUntil = 0;
    let lastFocused = null;
    let sessionSequence = 0;
    const owns = element => element instanceof Element && element.closest(".fs-tree") === root;
    const enabled = () => root.dataset.fsTreeEnabled === "true" && root.dataset.fsTreeDropping !== "true";
    const rowOf = target => owns(target) ? target.closest(".fs-tree-row") : null;
    const nodeOf = row => row?.closest("[data-fs-tree-id]");
    const highlight = row => {
        if (row === highlighted) return;
        highlighted?.classList.remove("fs-tree-drop-target");
        highlighted = row;
        highlighted?.classList.add("fs-tree-drop-target");
    };
    const clear = (cancel = true) => {
        highlight(null);
        const previous = drag;
        drag = null;
        if (cancel && previous) {
            previous.started.then(accepted => {
                if (accepted && root.isConnected) return reference.invokeMethodAsync("EndTreeDragAsync", previous.session);
            });
        }
    };
    const canDrop = row => {
        const target = nodeOf(row);
        return enabled() && drag && drag.row.isConnected && drag.row.draggable &&
            target && owns(target) && target.dataset.fsTreeAccept === "true" && target.getAttribute("aria-busy") !== "true" &&
            !drag.node.contains(target) && drag.node.parentElement.closest("[data-fs-tree-id]") !== target;
    };
    const interactive = target => target.closest(".fs-tree-actions,button,a[href],input,select,textarea,[contenteditable]:not([contenteditable=false]),[role=button]");
    const start = event => {
        const row = rowOf(event.target);
        if (!row || !enabled() || !row.draggable || interactive(event.target)) {
            if (owns(event.target)) event.preventDefault();
            return;
        }
        clear();
        const node = nodeOf(row);
        const session = String(++sessionSequence);
        const id = node.dataset.fsTreeId;
        drag = { row, node, id, session, started: reference.invokeMethodAsync("BeginTreeDrag", id, session) };
        event.dataTransfer.setData(dragType, session);
        event.dataTransfer.effectAllowed = "move";
        suppressClickUntil = Date.now() + 700;
    };
    const over = event => {
        const row = rowOf(event.target);
        if (!event.dataTransfer.types.includes(dragType) || !canDrop(row)) { highlight(null); return; }
        event.preventDefault();
        event.dataTransfer.dropEffect = "move";
        highlight(row);
    };
    const leave = event => {
        if (highlighted && !(event.relatedTarget instanceof Node && highlighted.contains(event.relatedTarget))) highlight(null);
    };
    const drop = async event => {
        const row = rowOf(event.target);
        if (!canDrop(row) || event.dataTransfer.getData(dragType) !== drag.session) {
            if (owns(event.target)) clear();
            return;
        }
        event.preventDefault();
        const current = drag;
        const targetId = nodeOf(row).dataset.fsTreeId;
        clear(false);
        suppressClickUntil = Date.now() + 700;
        if (await current.started && root.isConnected)
            await reference.invokeMethodAsync("DropTreeAsync", current.id, targetId, current.session);
    };
    const end = () => { if (drag) suppressClickUntil = Date.now() + 700; clear(); };
    const click = event => {
        if (owns(event.target) && event.detail !== 0 && Date.now() < suppressClickUntil) {
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    };
    const pointer = event => {
        if (!drag) suppressClickUntil = 0;
        const row = rowOf(event.target);
        if (row && !interactive(event.target) && root.dataset.fsTreeEnabled === "true") nodeOf(row).focus({ preventScroll: true });
    };
    const focus = event => { if (owns(event.target)) lastFocused = event.target; };
    const keydown = event => {
        if (!owns(event.target) || event.target.getAttribute("role") !== "treeitem" ||
            event.isComposing || event.altKey || event.ctrlKey || event.metaKey || root.dataset.fsTreeEnabled !== "true") return;
        if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End", "Enter", " "].includes(event.key) || event.key.length === 1) {
            event.preventDefault();
            event.stopPropagation();
            reference.invokeMethodAsync("TreeKeyAsync", event.target.dataset.fsTreeId, event.key);
        }
    };
    const cleanup = () => {
        clear(false);
        root.removeEventListener("dragstart", start);
        root.removeEventListener("dragover", over);
        root.removeEventListener("dragleave", leave);
        root.removeEventListener("drop", drop);
        root.removeEventListener("dragend", end);
        root.removeEventListener("click", click, true);
        root.removeEventListener("pointerdown", pointer);
        root.removeEventListener("focusin", focus);
        root.removeEventListener("keydown", keydown);
        observer.disconnect();
        trees.delete(root);
    };
    const observer = new MutationObserver(() => {
        if (!root.isConnected) { cleanup(); return; }
        if (drag && (!enabled() || !drag.row.isConnected || !drag.row.draggable)) clear();
        if (highlighted && (!highlighted.isConnected || !canDrop(highlighted))) highlight(null);
    });
    root.addEventListener("dragstart", start);
    root.addEventListener("dragover", over);
    root.addEventListener("dragleave", leave);
    root.addEventListener("drop", drop);
    root.addEventListener("dragend", end);
    root.addEventListener("click", click, true);
    root.addEventListener("pointerdown", pointer);
    root.addEventListener("focusin", focus);
    root.addEventListener("keydown", keydown);
    observer.observe(document.body, { childList: true, subtree: true, attributes: true,
        attributeFilter: ["data-fs-tree-enabled", "data-fs-tree-dropping", "data-fs-tree-accept", "aria-busy", "draggable"] });
    trees.set(root, { cleanup, focus: (id, force) => {
        const target = document.getElementById(id);
        if (!target || !owns(target)) return;
        if (force || root.contains(document.activeElement) ||
            (lastFocused && !lastFocused.isConnected && document.activeElement === document.body)) target.focus();
    } });
}

export function focusTreeItem(root, id, force) {
    trees.get(root)?.focus(id, force);
}

export function detachTree(root) {
    trees.get(root)?.cleanup();
}

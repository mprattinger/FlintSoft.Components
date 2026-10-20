const dialogs = new Map();
const openingFocus = new WeakMap();

export function captureModalFocus(dialog) {
    openingFocus.set(dialog, document.activeElement);
}

export function openModal(dialog, reference, escape, backdrop) {
    if (dialogs.has(dialog)) return;
    const previousFocus = openingFocus.get(dialog) ?? document.activeElement;
    openingFocus.delete(dialog);
    const previousOverflow = document.body.style.overflow;
    const cancel = event => {
        event.preventDefault();
        if (escape) reference.invokeMethodAsync("DismissAsync", "escape");
    };
    const click = event => {
        const bounds = dialog.getBoundingClientRect();
        if (backdrop && event.target === dialog &&
            (event.clientX < bounds.left || event.clientX > bounds.right ||
             event.clientY < bounds.top || event.clientY > bounds.bottom)) {
            reference.invokeMethodAsync("DismissAsync", "backdrop");
        }
    };
    const cleanup = () => {
        dialog.removeEventListener("cancel", cancel);
        dialog.removeEventListener("click", click);
        observer.disconnect();
        document.body.style.overflow = previousOverflow;
        if (previousFocus instanceof HTMLElement && previousFocus.isConnected) previousFocus.focus();
        dialogs.delete(dialog);
    };
    const observer = new MutationObserver(() => {
        if (!dialog.isConnected) cleanup();
    });
    dialog.showModal();
    document.body.style.overflow = "hidden";
    dialog.addEventListener("cancel", cancel);
    dialog.addEventListener("click", click);
    observer.observe(document.body, { childList: true, subtree: true });
    dialogs.set(dialog, cleanup);
}

export function closeModal(dialog) {
    if (dialog.open) dialog.close();
    dialogs.get(dialog)?.();
}

const autocompletes = new WeakMap();

export function attachAutocomplete(input, reference) {
    if (autocompletes.has(input)) return;
    const control = input.closest(".fs-input-control");
    let composing = false;
    const compositionStart = () => { composing = true; };
    const compositionEnd = () => {
        composing = false;
        input.dispatchEvent(new Event("input", { bubbles: true }));
    };
    const inputEvent = event => {
        if (composing || event.isComposing) event.stopPropagation();
    };
    const outside = event => {
        if (input.getAttribute("aria-expanded") === "true" && !control.contains(event.target))
            reference.invokeMethodAsync("DismissSuggestionsAsync");
    };
    const keydown = event => {
        if (composing || event.isComposing) {
            event.stopPropagation();
            return;
        }
        if (input.disabled || input.readOnly) return;
        const open = input.getAttribute("aria-expanded") === "true";
        if (event.key === "ArrowDown" || event.key === "ArrowUp" ||
            (event.key === "Escape" && open) ||
            (event.key === "Enter" && (open || input.dataset.fsAllowNew === "true"))) {
            event.preventDefault();
            // Escape should dismiss the suggestions, not an enclosing modal.
            if (event.key === "Escape") {
                event.stopPropagation();
                reference.invokeMethodAsync("DismissSuggestionsAsync");
            }
        }
    };
    const cleanup = () => {
        document.removeEventListener("pointerdown", outside, true);
        input.removeEventListener("keydown", keydown);
        input.removeEventListener("input", inputEvent);
        input.removeEventListener("compositionstart", compositionStart);
        input.removeEventListener("compositionend", compositionEnd);
        observer.disconnect();
        autocompletes.delete(input);
    };
    const observer = new MutationObserver(() => {
        if (!input.isConnected) cleanup();
    });
    document.addEventListener("pointerdown", outside, true);
    input.addEventListener("keydown", keydown);
    input.addEventListener("input", inputEvent);
    input.addEventListener("compositionstart", compositionStart);
    input.addEventListener("compositionend", compositionEnd);
    observer.observe(document.body, { childList: true, subtree: true });
    autocompletes.set(input, cleanup);
}

export function detachAutocomplete(input) {
    autocompletes.get(input)?.();
}

export function scrollAutocompleteOption(input) {
    const id = input.getAttribute("aria-activedescendant");
    if (id) document.getElementById(id)?.scrollIntoView({ block: "nearest", inline: "nearest" });
}

let lockCount = 0;
let previousOverflow = "";

function preventBackgroundEnter(event) {
    if (event.key !== "Enter" || lockCount === 0) {
        return;
    }

    if (event.target instanceof Element && event.target.closest(".overlay .container")) {
        return;
    }

    event.preventDefault();
    event.stopPropagation();
}

export function lockBodyScroll() {
    if (lockCount === 0) {
        previousOverflow = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        document.addEventListener("keydown", preventBackgroundEnter, true);
    }

    lockCount++;
}

export function unlockBodyScroll() {
    if (lockCount === 0) {
        return;
    }

    lockCount--;

    if (lockCount === 0) {
        document.body.style.overflow = previousOverflow;
        document.removeEventListener("keydown", preventBackgroundEnter, true);
    }
}

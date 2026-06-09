export function initializeAutocomplete(autoCompleteRef, dotnetRef) {
    console.log("Initializing" + autoCompleteRef.id);
    //autoCompleteRef.addEventListener("onkeyup", (e) => handleKeyUp(e, dotnetRef));
    document.addEventListener("click", (e) => possibleOutsideClickDetected(e, autoCompleteRef, dotnetRef));
    autoCompleteRef.addEventListener("keydown", (e) => handleKeyDown(e, dotnetRef));
}

export function preventAndStop(event) {
    event.preventDefault();
    event.stopPropagation();

}

export function disposeAutocomplete(autoCompleteRef) {
    document.removeEventListener("click", possibleOutsideClick);
    autoCompleteRef.removeEventListener("keydown", handleKeyDown);
    console.log("Disposed autocomplete " + autoCompleteRef.id);
}

function possibleOutsideClickDetected(evt, autoCompleteRef, dotnetRef) {
    console.log("Outside click detected " + autoCompleteRef.id);

    if (!(autoCompleteRef.current === evt.target || autoCompleteRef.current?.contains(evt.target))) {
        dotnetRef.invokeMethodAsync("PossibleOutsideClickDetected");
    }
}

function handleKeyDown(event, dotnetRef) {
    console.log("Key down detected: " + event.key);
    if (event.key === "Enter") {
        dotnetRef.invokeMethodAsync("HandleEnterKeyDown");
        event.preventDefault();
        event.stopPropagation();
        return;
    }

    if (event.key === "Tab") {
        dotnetRef.invokeMethodAsync("HandleTabKeyDown");
        event.preventDefault();
        event.stopPropagation();
        return;
    }
}

export function selectText(inputRef) {
    inputRef.select();
}

export function scrollIntoView(id) {
    const element = document.getElementById(id);
    if (element) {
        element.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }
}
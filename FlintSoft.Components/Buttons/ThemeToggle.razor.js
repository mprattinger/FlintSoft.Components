export function initializeThemeModule(dotnetRef) {
    const mql = window.matchMedia('(prefers-color-scheme: dark)');

    mql.addEventListener("change", (e) => themePreferenceChanged(e, dotnetRef));

    return mql.matches;
}

function themePreferenceChanged(event, dotnetRef) {
    dotnetRef.invokeMethodAsync("SystemThemePreferenceChanged", event.matches);
}

export function disposeThemeTogglers() {
    const mql = window.matchMedia('(prefers-color-scheme: dark)');

    mql.removeEventListener("change", themePreferenceChanged);
}

export function themeClass(themeClass) {
    document.documentElement.className = themeClass;
}
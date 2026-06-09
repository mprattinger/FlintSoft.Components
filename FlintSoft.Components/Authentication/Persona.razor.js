window.persona = (function () {
    let registeredRef = null;
    let dotNetRef = null;

    function outSideClick(event) {
        if (!registeredRef) return;
        if (!registeredRef.contains(event.target)) {
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('CloseFlyout');
            }
        }
    }

    return {
        register: function (element, dotNetObject) {
            registeredRef = element;
            dotNetRef = dotNetObject;
            document.addEventListener('click', outSideClick);
        },
        unregister() {
            registeredRef = null;
            dotNetRef = null;
            document.removeEventListener('click', outSideClick);
        }
    }
})();
(() => {
    const modal = document.getElementById("components-reconnect-modal");
    // data-permanent conserva el nodo y sus listeners durante navegación mejorada.
    if (!modal || modal.dataset.reconnectInitialized) return;
    modal.dataset.reconnectInitialized = "true";
    const retryButton = document.getElementById("reconnect-retry");
    const reloadButton = document.getElementById("reconnect-reload");
    const states = ["show", "hide", "failed", "rejected"];
    let previousState = "hide";
    let retrying = false;

    // Observar solo la presentación que administra Blazor; no sustituir su reconexión.
    function syncPresentation() {
        const state = states.find(value => modal.classList.contains(`components-reconnect-${value}`)) || "hide";
        modal.setAttribute("aria-busy", String(state === "show"));
        if (state === "hide") {
            if (modal.open) modal.close();
        } else {
            modal.setAttribute("aria-labelledby", `reconnect-title-${state}`);
            modal.setAttribute("aria-describedby", `reconnect-description-${state}`);
            // El diálogo nativo bloquea el fondo, contiene el foco y restaura el foco al cerrar.
            if (!modal.open) modal.showModal();
            if (state !== previousState) {
                modal.querySelector(`.reconnect-state-${state}`).focus({ preventScroll: true });
            }
        }
        previousState = state;
    }

    function showState(state) {
        modal.classList.remove(...states.map(value => `components-reconnect-${value}`));
        modal.classList.add(`components-reconnect-${state}`);
        syncPresentation();
    }

    new MutationObserver(syncPresentation).observe(modal, { attributes: true, attributeFilter: ["class"] });
    modal.addEventListener("cancel", event => event.preventDefault());

    // Debe funcionar sin circuito: no usar un @onclick de Blazor para estas acciones.
    retryButton.addEventListener("click", async () => {
        if (retrying) return;
        retrying = true;
        retryButton.disabled = true;
        // Los contadores pertenecen a los intentos automáticos de Blazor, no a este clic.
        modal.dataset.manualRetry = "true";
        showState("show");
        try {
            const restored = await window.Blazor.reconnect();
            showState(restored ? "hide" : "rejected");
        } catch {
            showState("failed");
        } finally {
            retrying = false;
            retryButton.disabled = false;
            delete modal.dataset.manualRetry;
        }
    });

    reloadButton.addEventListener("click", () => window.location.reload());
    syncPresentation();
})();

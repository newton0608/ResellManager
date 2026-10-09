(() => {
    const modal = document.getElementById("components-reconnect-modal");
    // data-permanent conserva el nodo y sus listeners durante navegación mejorada.
    if (!modal || modal.dataset.reconnectInitialized) return;
    modal.dataset.reconnectInitialized = "true";
    const retryButton = document.getElementById("reconnect-retry");
    const reloadButton = document.getElementById("reconnect-reload");
    const notice = document.getElementById("store-reconnect");
    const noticeMessage = document.getElementById("store-reconnect-message");
    const noticeRetry = document.getElementById("store-reconnect-retry");
    const noticeReload = document.getElementById("store-reconnect-reload");
    const states = ["show", "hide", "failed", "rejected"];
    let previousState = "hide";
    let retrying = false;
    let previousPublicCatalog;
    const isPublicCatalog = () => !!document.querySelector?.('[data-public-catalog="true"]');

    // Observar solo la presentación que administra Blazor; no sustituir su reconexión.
    function syncPresentation() {
        const state = states.find(value => modal.classList.contains(`components-reconnect-${value}`)) || "hide";
        modal.setAttribute("aria-busy", String(state === "show"));
        const publicCatalog = isPublicCatalog();
        previousPublicCatalog = publicCatalog;
        if (notice) {
            notice.hidden = !publicCatalog || state === "hide";
            notice.dataset.state = state;
            noticeMessage.textContent = state === "failed" ? "No se pudo restablecer la conexión." :
                state === "rejected" ? "La sesión ya no está disponible." : "Reconectando…";
            noticeRetry.hidden = state !== "failed";
            noticeReload.hidden = state !== "rejected";
        }
        if (publicCatalog) {
            if (modal.open) modal.close();
        } else if (state === "hide") {
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
    const retry = async () => {
        if (retrying) return;
        retrying = true;
        retryButton.disabled = true;
        if (noticeRetry) noticeRetry.disabled = true;
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
            if (noticeRetry) noticeRetry.disabled = false;
            delete modal.dataset.manualRetry;
        }
    };
    retryButton.addEventListener("click", retry);
    noticeRetry?.addEventListener("click", retry);
    noticeReload?.addEventListener("click", () => window.location.reload());
    // El modal es permanente, pero el layout cambia durante navegación mejorada.
    // enhancedload pertenece a Blazor, no a document. Observar la marca también
    // cubre reemplazos del DOM y restauración de historial sin depender de rutas.
    const syncLayout = () => {
        if (isPublicCatalog() !== previousPublicCatalog) syncPresentation();
    };
    new MutationObserver(syncLayout).observe(document.body, {
        childList: true, subtree: true, attributes: true, attributeFilter: ["data-public-catalog"]
    });
    const registerEnhancedNavigation = () => window.Blazor?.addEventListener?.("enhancedload", syncPresentation);
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", registerEnhancedNavigation, { once: true });
    } else {
        registerEnhancedNavigation();
    }
    window.addEventListener?.("pageshow", syncPresentation);
    window.addEventListener?.("popstate", syncLayout);

    reloadButton.addEventListener("click", () => window.location.reload());
    syncPresentation();
})();

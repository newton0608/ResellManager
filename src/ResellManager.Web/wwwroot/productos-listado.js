// Mismo patrón de sentinel/cursor que el catálogo público, limitado al listado administrativo.
const instancias = new WeakMap();

export function iniciar(root, dotnet) {
    if (!root || root.isConnected === false || instancias.has(root)) return;
    const state = { root, dotnet, observer: null, sentinel: null, pending: false, alive: true };
    if (typeof IntersectionObserver !== 'undefined') {
        state.observer = new IntersectionObserver(async entries => {
            if (!state.alive || state.pending || !entries.some(e => e.target === state.sentinel && e.isIntersecting)) return;
            if (state.sentinel?.dataset.enabled !== 'true') return;
            const anterior = state.sentinel;
            state.pending = true;
            let completed = false;
            try { await dotnet.invokeMethodAsync('CargarMasDesdeScrollAsync'); completed = true; }
            catch { state.observer.unobserve(anterior); /* Reanudación manual tras desconexión. */ }
            finally {
                state.pending = false;
                if (completed && state.alive && anterior !== state.sentinel && state.sentinel?.dataset.enabled === 'true') {
                    state.observer.unobserve(state.sentinel);
                    state.observer.observe(state.sentinel);
                }
            }
        }, { rootMargin: '250px 0px' });
    }
    instancias.set(root, state);
    actualizar(root);
}

export function actualizar(root) {
    const state = instancias.get(root);
    if (!state) return;
    const siguiente = root.querySelector('[data-productos-sentinel]');
    if (siguiente === state.sentinel) return;
    if (state.sentinel) state.observer?.unobserve(state.sentinel);
    state.sentinel = siguiente;
    if (siguiente) state.observer?.observe(siguiente);
}

export function destruir(root) {
    const state = instancias.get(root);
    if (!state) return;
    state.alive = false;
    state.observer?.disconnect();
    instancias.delete(root);
}

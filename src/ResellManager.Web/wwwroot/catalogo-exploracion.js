const instancias = new WeakMap();

export function iniciar(root, dotnet) {
    if (!root || root.isConnected === false || instancias.has(root)) return;
    const abort = new AbortController();
    const state = { root, dotnet, observer: null, sentinel: null, pending: false, abort, alive: true };
    function scrollCarousel(event) {
        const button = event.target.closest('[data-store-carousel-delta]');
        if (!button || !root.contains(button)) return;
        const carousel = button.closest('[data-store-section]').querySelector('[data-store-carousel]');
        carousel.scrollBy({ left: Number(button.dataset.storeCarouselDelta) * carousel.clientWidth * .8,
            behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    }
    function controls(event) {
        const carousel = event?.target?.matches?.('[data-store-carousel]') ? event.target : null;
        const carousels = carousel ? [carousel] : [...root.querySelectorAll('[data-store-carousel]')];
        for (const row of carousels) {
            const section = row.closest('[data-store-section]');
            section.querySelector('[data-store-carousel-delta="-1"]').disabled = row.scrollLeft <= 1;
            section.querySelector('[data-store-carousel-delta="1"]').disabled = row.scrollLeft + row.clientWidth >= row.scrollWidth - 1;
        }
    }
    root.addEventListener('click', scrollCarousel, { signal: abort.signal });
    root.addEventListener('scroll', controls, { capture: true, signal: abort.signal });
    window.addEventListener('resize', controls, { signal: abort.signal });
    state.controls = controls;
    if (typeof IntersectionObserver !== 'undefined') {
        state.observer = new IntersectionObserver(async entries => {
            if (!state.alive || state.pending || !entries.some(e => e.target === state.sentinel && e.isIntersecting)) return;
            if (state.sentinel?.dataset.enabled !== 'true') return;
            const sentinel = state.sentinel;
            state.pending = true;
            let completed = false;
            try { await state.dotnet.invokeMethodAsync('CargarMasDesdeScrollAsync'); completed = true; }
            catch { /* El componente muestra errores de lectura; navegación/desmontaje puede cortar el circuito. */ }
            finally {
                state.pending = false;
                if (completed && state.alive && state.sentinel !== sentinel && state.sentinel?.dataset.enabled === 'true') {
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
    state.controls();
    // Un nodo nuevo por cursor permite una sola petición, aun ante callbacks repetidos.
    const next = root.querySelector('[data-store-sentinel]');
    if (next !== state.sentinel) {
        if (state.sentinel) state.observer?.unobserve(state.sentinel);
        state.sentinel = next;
        if (next) state.observer?.observe(next);
    }
}

export function destruir(root) {
    const state = instancias.get(root);
    if (!state) return;
    state.alive = false;
    state.observer?.disconnect(); state.abort.abort(); instancias.delete(root);
}

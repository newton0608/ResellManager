(() => {
    const selected = new WeakSet();
    const reported = new WeakMap();
    let pending = false;
    let firstPointerFocus = null;
    document.addEventListener("focusin", event => {
        const input = event.target;
        if (!input.matches?.("input[data-select-once]") || selected.has(input) || !input.value) return;
        selected.add(input);
        firstPointerFocus = input;
        // Keep the native numeric control; do not reselect during later edits.
        input.select();
    });
    document.addEventListener("mouseup", event => {
        if (event.target === firstPointerFocus) event.preventDefault();
        firstPointerFocus = null;
    });
    document.addEventListener("focusout", () => { firstPointerFocus = null; });

    function revealErrors() {
        pending = false;
        const modal = document.querySelector("dialog[open]");
        let handled = false;
        document.querySelectorAll(".mensaje-error, .validation-summary-errors, .validation-summary, .direct-sale-partial-warning").forEach(error => {
            const text = error.textContent.trim();
            if (!text) { reported.delete(error); return; }
            if (!error.getClientRects().length || (modal && !modal.contains(error))) return;
            if (reported.get(error) === text) return;
            reported.set(error, text);
            if (handled) return;
            handled = true;
            const bounds = error.getBoundingClientRect();
            const visible = bounds.top >= 0 && bounds.bottom <= window.innerHeight;
            if (!visible) {
                error.scrollIntoView({ block: "center", behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth" });
                error.setAttribute("tabindex", "-1");
                error.focus({ preventScroll: true });
            }
        });
    }
    function schedule() {
        if (pending) return;
        pending = true;
        requestAnimationFrame(revealErrors);
    }
    new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true, characterData: true });
    schedule();
})();

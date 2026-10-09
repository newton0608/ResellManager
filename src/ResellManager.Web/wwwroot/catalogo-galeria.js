const instancias = new WeakMap();

export function iniciar(root) {
    if (!root || instancias.has(root)) return;
    const dialog = root.querySelector('[data-gallery-dialog]');
    if (!dialog) return;
    const main = root.querySelector('[data-gallery-main]');
    const image = root.querySelector('[data-lightbox-image]');
    const stage = root.querySelector('[data-gallery-stage]');
    const thumbs = [...root.querySelectorAll('[data-gallery-index]')];
    const abort = new AbortController();
    const listen = (target, name, action) => target.addEventListener(name, action, { signal: abort.signal });
    let index = Number(root.dataset.selectedIndex) || 0, scale = 1, x = 0, y = 0, distance = 0, restoreFocus, oldOverflow;
    const pointers = new Map();
    let start, last, gestured = false;
    function paint() {
        const maxX = Math.max(0, (image.clientWidth * scale - stage.clientWidth) / 2);
        const maxY = Math.max(0, (image.clientHeight * scale - stage.clientHeight) / 2);
        x = Math.min(maxX, Math.max(-maxX, x)); y = Math.min(maxY, Math.max(-maxY, y));
        image.style.transform = `translate(${x}px, ${y}px) scale(${scale})`;
        root.querySelector('[data-gallery-zoom]').textContent = `${Math.round(scale * 100)}%`;
    }
    function zoom(value) { scale = Math.max(1, Math.min(4, value)); if (scale === 1) x = y = 0; paint(); }
    function select(value) {
        index = (value + thumbs.length) % thumbs.length;
        const src = thumbs[index].dataset.gallerySrc;
        for (const img of [main, image]) { img.hidden = false; img.src = src; img.alt = `${root.dataset.name} — fotografía ${index + 1}`; }
        root.querySelector('[data-gallery-fallback]').hidden = true;
        root.querySelector('[data-lightbox-fallback]').hidden = true;
        thumbs.forEach((thumb, i) => thumb.setAttribute('aria-pressed', String(i === index)));
        root.querySelectorAll('[data-gallery-count]').forEach(node => node.textContent = `${index + 1} / ${thumbs.length}`);
        pointers.clear(); distance = 0; zoom(1);
    }
    function close() {
        if (!dialog.open) return;
        dialog.close();
        cleanup();
    }
    function cleanup() {
        if (oldOverflow === undefined) return;
        document.body.style.overflow = oldOverflow;
        oldOverflow = undefined;
        pointers.clear(); zoom(1);
        if (restoreFocus?.isConnected) restoreFocus.focus({ preventScroll: true });
    }
    listen(dialog, 'close', () => { if (!dialog.open) cleanup(); });
    listen(dialog, 'cancel', cleanup);
    listen(root, 'click', event => {
        const button = event.target.closest('button');
        if (!button || !root.contains(button)) return;
        if (button.hasAttribute('data-gallery-open')) {
            restoreFocus = button; oldOverflow = document.body.style.overflow;
            document.body.style.overflow = 'hidden';
            // El visor carga solo la imagen seleccionada al abrirlo.
            image.src = thumbs[index].dataset.gallerySrc;
            dialog.showModal(); root.querySelector('[data-gallery-close]').focus({ preventScroll: true });
        } else if (button.hasAttribute('data-gallery-close')) close();
        else if (button.hasAttribute('data-gallery-prev')) select(index - 1);
        else if (button.hasAttribute('data-gallery-next')) select(index + 1);
        else if (button.hasAttribute('data-gallery-index')) select(Number(button.dataset.galleryIndex));
        else if (button.hasAttribute('data-gallery-plus')) zoom(scale + .5);
        else if (button.hasAttribute('data-gallery-minus')) zoom(scale - .5);
        else if (button.hasAttribute('data-gallery-reset')) zoom(1);
    });
    listen(dialog, 'keydown', event => {
        if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); select(index + (event.key === 'ArrowLeft' ? -1 : 1)); }
        // Escape se resuelve con cancel/close nativos; el diálogo contiene el foco.
    });
    for (const [img, fallback] of [[main, '[data-gallery-fallback]'], [image, '[data-lightbox-fallback]']]) {
        listen(img, 'error', () => { img.hidden = true; root.querySelector(fallback).hidden = false; });
        listen(img, 'load', () => { img.hidden = false; root.querySelector(fallback).hidden = true; paint(); });
        if (img.complete && img.src && img.naturalWidth === 0) { img.hidden = true; root.querySelector(fallback).hidden = false; }
    }
    for (const thumb of thumbs) {
        const img = thumb.querySelector('img');
        listen(img, 'error', () => { img.hidden = true; thumb.querySelector('span').hidden = false; });
        if (img.complete && img.naturalWidth === 0) { img.hidden = true; thumb.querySelector('span').hidden = false; }
    }
    // Deslizar la imagen del detalle también permite recorrer las fotografías.
    let mainStart;
    listen(main, 'pointerdown', e => mainStart = { x: e.clientX, y: e.clientY });
    listen(main, 'pointerup', e => {
        if (mainStart && Math.abs(e.clientX - mainStart.x) > 50 && Math.abs(e.clientY - mainStart.y) < 40) {
            e.preventDefault(); select(index + (e.clientX < mainStart.x ? 1 : -1));
            // Evitar que el click generado después de swipe abra el visor.
            root.querySelector('[data-gallery-open]').addEventListener('click', e => e.stopPropagation(), { once: true });
        }
        mainStart = null;
    });
    listen(stage, 'pointerdown', e => {
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        stage.setPointerCapture(e.pointerId);
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pointers.size === 1) { start = last = { x: e.clientX, y: e.clientY }; gestured = scale > 1; }
        if (pointers.size === 2) { distance = 0; gestured = true; }
    });
    listen(stage, 'pointermove', e => {
        if (!pointers.has(e.pointerId)) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pointers.size === 2) {
            const [a, b] = [...pointers.values()];
            const next = Math.hypot(a.x - b.x, a.y - b.y);
            if (distance > 0) zoom(scale * next / distance);
            distance = next; gestured = true;
        } else if (scale > 1 && last) {
            x += e.clientX - last.x; y += e.clientY - last.y; paint(); gestured = true;
        }
        last = { x: e.clientX, y: e.clientY };
    });
    function release(e, cancelled = false) {
        if (!pointers.has(e.pointerId)) return;
        pointers.delete(e.pointerId); distance = 0;
        if (pointers.size !== 0) { last = [...pointers.values()][0]; return; }
        const dx = e.clientX - start.x, dy = e.clientY - start.y;
        if (!cancelled && !gestured && scale === 1 && Math.abs(dx) > 50 && Math.abs(dx) > Math.abs(dy) * 1.4) select(index + (dx < 0 ? 1 : -1));
        else if (!cancelled && !gestured && e.pointerType === 'mouse' && Math.hypot(dx, dy) < 8) zoom(scale === 1 ? 2 : 1);
        start = last = null;
    }
    listen(stage, 'pointerup', e => release(e));
    listen(stage, 'pointercancel', e => release(e, true));
    instancias.set(root, () => { close(); cleanup(); abort.abort(); });
}

export function destruir(root) { instancias.get(root)?.(); instancias.delete(root); }
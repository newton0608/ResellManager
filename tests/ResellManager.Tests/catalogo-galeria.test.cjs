const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname, '../../src/ResellManager.Web/wwwroot/catalogo-galeria.js'), 'utf8').replaceAll('export ', '');
function fixture() {
    function node() {
        const listeners = new Map(), attrs = new Set();
        return { dataset: {}, style: {}, hidden: false, isConnected: true, clientWidth: 100, clientHeight: 100,
            addEventListener(name, callback, options) { const item = { callback, signal: options?.signal }; const list = listeners.get(name) || []; list.push(item); listeners.set(name, list); },
            fire(name, event = {}) { for (const l of listeners.get(name) || []) if (!l.signal?.aborted) l.callback(event); },
            hasAttribute: key => attrs.has(key), setAttribute: (key, val) => { attrs.add(key); },
            closest() { return this; }, focus() { document.activeElement = this; }, setPointerCapture() {} };
    }
    const document = { body: { style: { overflow: 'auto' } }, activeElement: null };
    const root = node(), dialog = node(), main = node(), image = node(), stage = node(), zoom = node(), count = node();
    root.dataset = { name: 'Producto', selectedIndex: '1' };
    dialog.showModal = () => dialog.open = true;
    // close event ocurre después; destruir debe limpiar incluso si listeners son abortados antes.
    dialog.close = () => dialog.open = false;
    const close = node(); close.setAttribute('data-gallery-close', '');
    const open = node(); open.setAttribute('data-gallery-open', '');
    const thumbs = ['first', 'cover'].map((src, i) => { const n = node(); n.dataset = { gallerySrc: src, galleryIndex: String(i) }; n.querySelector = () => node(); return n; });
    const map = { '[data-gallery-dialog]': dialog, '[data-gallery-main]': main, '[data-lightbox-image]': image,
        '[data-gallery-stage]': stage, '[data-gallery-zoom]': zoom, '[data-gallery-close]': close, '[data-gallery-open]': open,
        '[data-gallery-fallback]': node(), '[data-lightbox-fallback]': node() };
    root.querySelector = selector => map[selector];
    root.querySelectorAll = selector => selector === '[data-gallery-index]' ? thumbs : [count];
    root.contains = () => true;
    const context = { document, AbortController }; vm.runInNewContext(source, context);
    context.iniciar(root);
    return { context, root, dialog, main, image, stage, zoom, count, open, document, map,
        click(attr) { const button = node(); button.setAttribute(attr, ''); root.fire('click', { target: button }); },
        point(type, id, x, y) { stage.fire(type, { pointerId: id, pointerType: 'touch', clientX: x, clientY: y }); } };
}
test('visor selecciona portada, zoom no navega y destruir restaura scroll/foco sin close event', () => {
    const f = fixture();
    f.root.fire('click', { target: f.open });
    assert.equal(f.image.src, 'cover'); assert.equal(f.dialog.open, true);
    assert.equal(f.document.body.style.overflow, 'hidden');
    f.click('data-gallery-plus'); assert.equal(f.zoom.textContent, '150%');
    f.context.destruir(f.root);
    assert.equal(f.dialog.open, false); assert.equal(f.document.body.style.overflow, 'auto');
    assert.equal(f.document.activeElement, f.open);
});
test('pinch/pan no cambia imagen; swipe a escala 1 navega y reinicia zoom', () => {
    const f = fixture();
    f.point('pointerdown', 1, 100, 100); f.point('pointerdown', 2, 200, 100);
    f.point('pointermove', 2, 210, 100); f.point('pointermove', 2, 320, 100);
    assert.equal(f.zoom.textContent, '200%');
    f.point('pointerup', 2, 320, 100); f.point('pointermove', 1, 80, 100); f.point('pointerup', 1, 80, 100);
    assert.equal(f.count.textContent, undefined); // ninguna navegación durante pinch/pan
    f.click('data-gallery-reset');
    f.point('pointerdown', 3, 300, 100); f.point('pointerup', 3, 180, 100);
    assert.equal(f.count.textContent, '1 / 2'); assert.equal(f.zoom.textContent, '100%');
});
test('fallo de imagen activa placeholder y otro load lo recupera', () => {
    const f = fixture();
    f.main.fire('error'); assert.equal(f.main.hidden, true); assert.equal(f.map['[data-gallery-fallback]'].hidden, false);
    f.main.fire('load'); assert.equal(f.main.hidden, false); assert.equal(f.map['[data-gallery-fallback]'].hidden, true);
});
test('Escape libera scroll inmediatamente sin depender del close encolado', () => {
    const f = fixture(); f.root.fire('click', { target: f.open });
    f.dialog.fire('cancel');
    assert.equal(f.document.body.style.overflow, 'auto');
    assert.equal(f.document.activeElement, f.open);
});
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs'); const path = require('node:path'); const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname, '../../src/ResellManager.Web/wwwroot/catalogo-exploracion.js'),'utf8').replaceAll('export ', '');
function fixture({ observer = true, reduced = false } = {}) {
    const listeners = {}, observed = [], unobserved = [], scrolls = [];
    const previous = {}, next = {};
    const carousel = { clientWidth: 400, scrollWidth: 1000, scrollLeft: 0, scrollBy: p => scrolls.push(p),
        closest: () => section, matches: () => true };
    const section = { querySelector: s => s === '[data-store-carousel]' ? carousel : s.includes('="-1"') ? previous : next };
    const root = { sentinel: { dataset: { enabled: 'true' } }, contains: () => true,
        querySelector: () => root.sentinel, querySelectorAll: () => [carousel],
        addEventListener: (name, fn, opts) => listeners[name] = { fn, opts } };
    let callback, calls = 0, complete, fail, disconnected = false;
    const dotnet = { invokeMethodAsync: name => { assert.equal(name, 'CargarMasDesdeScrollAsync'); calls++; return new Promise((r, reject) => { complete = r; fail = reject; }); } };
    const context = { WeakMap, AbortController, matchMedia: () => ({ matches: reduced }), window: { addEventListener() {} } };
    if (observer) context.IntersectionObserver = class { constructor(cb, opts) { callback=cb; assert.equal(opts.rootMargin,'250px 0px'); }
        observe(x) { observed.push(x); } unobserve(x) { unobserved.push(x); } disconnect() { disconnected = true; } };
    vm.runInNewContext(source,context); context.iniciar(root,dotnet);
    return { context, root, listeners, observed, unobserved, scrolls, carousel, section, previous, next,
        fire: () => callback([{ target: root.sentinel, isIntersecting: true }]), finish: () => complete(), reject: () => fail(new Error('Circuito caído')), stale: target => callback([{ target, isIntersecting: true }]), get calls() { return calls; }, get disconnected() { return disconnected; } };
}
test('observer evita solicitudes simultáneas y se desactiva ante carga/error', async () => {
    const f = fixture(); const first = f.fire(); await f.fire(); assert.equal(f.calls, 1);
    f.root.sentinel.dataset.enabled = 'false'; f.finish(); await first; await f.fire(); assert.equal(f.calls, 1);
    const next = { dataset: { enabled: 'true' } }; f.root.sentinel = next; f.context.actualizar(f.root);
    assert.equal(f.observed.at(-1), next); const second = f.fire(); assert.equal(f.calls,2);
    f.context.destruir(f.root); f.finish(); await second;
    assert.equal(f.disconnected,true); assert.equal(f.listeners.click.opts.signal.aborted,true);
});
test('sin IntersectionObserver mantiene controles y respeta movimiento reducido', () => {
    const f = fixture({ observer: false, reduced: true });
    assert.equal(f.observed.length,0); assert.equal(f.previous.disabled,true); assert.equal(f.next.disabled,false);
    f.listeners.click.fn({ target: { closest: () => ({ dataset: { storeCarouselDelta: '1' }, closest: () => f.section }) } });
    assert.equal(f.scrolls[0].left,320); assert.equal(f.scrolls[0].behavior,'auto');
    f.carousel.scrollLeft = 600; f.context.actualizar(f.root); assert.equal(f.next.disabled,true);
    f.context.destruir(f.root);
});


test('circuito caído no rearma observer ni produce reintentos permanentes', async () => {
    const f = fixture(); const pending = f.fire(); f.reject(); await pending;
    assert.equal(f.calls,1); assert.equal(f.observed.length,1); assert.equal(f.unobserved.length,0);
    f.context.destruir(f.root);
});
test('entradas de un sentinel anterior no solicitan otra página', async () => {
    const f = fixture(); const old = f.root.sentinel; f.root.sentinel = { dataset: { enabled: 'true' } };
    f.context.actualizar(f.root); await f.stale(old); assert.equal(f.calls,0);
    f.context.destruir(f.root);
});

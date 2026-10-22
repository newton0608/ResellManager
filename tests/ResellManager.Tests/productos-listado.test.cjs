const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname, '../../src/ResellManager.Web/wwwroot/productos-listado.js'), 'utf8').replaceAll('export ', '');

function fixture({ observer = true } = {}) {
    const root = { sentinel: { dataset: { enabled: 'true' } }, querySelector: () => root.sentinel };
    const observed = [], unobserved = [];
    let callback, complete, reject, disconnected = false, calls = 0;
    const dotnet = { invokeMethodAsync: name => {
        assert.equal(name, 'CargarMasDesdeScrollAsync');
        calls++;
        return new Promise((ok, fail) => { complete = ok; reject = fail; });
    } };
    const context = { WeakMap };
    if (observer) context.IntersectionObserver = class {
        constructor(cb, options) { callback = cb; assert.equal(options.rootMargin, '250px 0px'); }
        observe(node) { observed.push(node); }
        unobserve(node) { unobserved.push(node); }
        disconnect() { disconnected = true; }
    };
    vm.runInNewContext(source, context);
    context.iniciar(root, dotnet);
    return { root, context, observed, unobserved,
        fire: (target = root.sentinel) => callback([{ target, isIntersecting: true }]),
        finish: () => complete(), fail: () => reject(new Error('Circuito cerrado')),
        get calls() { return calls; }, get disconnected() { return disconnected; } };
}

test('scroll pide una sola página mientras hay una solicitud pendiente', async () => {
    const f = fixture();
    const pending = f.fire();
    await f.fire();
    assert.equal(f.calls, 1);
    f.finish(); await pending;
    f.root.sentinel.dataset.enabled = 'false';
    await f.fire();
    assert.equal(f.calls, 1);
});

test('nuevo cursor durante una carga se rearma al terminar; entradas viejas se ignoran', async () => {
    const f = fixture();
    const old = f.root.sentinel;
    const pending = f.fire();
    f.root.sentinel = { dataset: { enabled: 'true' } };
    f.context.actualizar(f.root);
    await f.fire();
    assert.equal(f.calls, 1);
    f.finish(); await pending;
    assert.equal(f.observed.at(-1), f.root.sentinel);
    await f.fire(old);
    assert.equal(f.calls, 1);
    const next = f.fire(); f.finish(); await next;
    assert.equal(f.calls, 2);
});

test('fin de lista elimina el sentinel; desmontaje desconecta sin invocar un componente viejo', async () => {
    const f = fixture();
    const old = f.root.sentinel;
    f.root.sentinel = null;
    f.context.actualizar(f.root);
    assert.equal(f.unobserved.at(-1), old);
    await f.fire(old);
    assert.equal(f.calls, 0);
    f.context.destruir(f.root);
    assert.equal(f.disconnected, true);
    await f.fire(old);
    assert.equal(f.calls, 0);
});

test('navegar durante una carga impide rearmar el observer al finalizar', async () => {
    const f = fixture();
    const pending = f.fire();
    f.context.destruir(f.root);
    f.finish(); await pending;
    assert.equal(f.disconnected, true);
    assert.equal(f.observed.length, 1);
});

test('desconexión retira la observación; no rearma un reintento automático', async () => {
    const f = fixture();
    const pending = f.fire(); f.fail(); await pending;
    assert.equal(f.calls, 1);
    assert.deepEqual(f.unobserved, [f.root.sentinel]);
    f.context.actualizar(f.root);
    assert.equal(f.observed.length, 1);
});

test('sin IntersectionObserver conserva el fallback manual y permite desmontar', () => {
    const f = fixture({ observer: false });
    f.context.actualizar(f.root);
    f.context.destruir(f.root);
    assert.equal(f.calls, 0);
});

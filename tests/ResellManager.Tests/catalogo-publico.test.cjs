const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname, '../../src/ResellManager.Web/wwwroot/catalogo-publico.js'), 'utf8').replaceAll('export ', '');
function fixture(fetch) {
    const calls = [], history = [];
    const context = { URL, URLSearchParams, AbortController, document: { baseURI: 'https://preview.example/' },
        fetch: (...args) => { calls.push(args); return fetch(...args); },
        window: { location: { href: 'https://preview.example/catalogo' }, history: { state: {}, replaceState: (...args) => history.push(args) } } };
    vm.runInNewContext(source, context);
    return { context, calls, history };
}
test('listado combina tres filtros codificados y omite credenciales/cache', async () => {
    const f = fixture(async () => ({ ok: true, json: async () => [] }));
    await f.context.listar(' vitamina & Á ', 4, ' Acmé + B ');
    const [url, options] = f.calls[0];
    assert.equal(url.searchParams.get('termino'), 'vitamina & Á');
    assert.equal(url.searchParams.get('categoriaId'), '4');
    assert.equal(url.searchParams.get('marca'), 'Acmé + B');
    assert.equal(options.credentials, 'omit'); assert.equal(options.cache, 'no-store');
    f.context.actualizarUrl('vitamina & Á', 4, 'Acmé + B');
    assert.equal(f.history[0][2].searchParams.get('marca'), 'Acmé + B');
    f.context.actualizarUrl('', null, null);
    assert.equal(f.history[1][2].search, '');
});
test('lectura nueva cancela request anterior; 404 detalle es estado no disponible', async () => {
    const f = fixture(async url => ({ ok: true, status: url.pathname.endsWith('/999') ? 404 : 200, json: async () => [] }));
    const first = f.context.listar('vieja', null, null);
    await f.context.listar('nueva', null, 'Marca');
    assert.equal(f.calls[0][1].signal.aborted, true);
    await first;
    assert.equal(await f.context.detalle(999), null);
    assert.throws(() => f.context.detalle('../file'), /Producto no válido/);
});
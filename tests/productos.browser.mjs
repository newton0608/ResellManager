// QA real de Productos con SQLite, usuarios y fotos sintéticos, aislados de producción.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const { chromium } = await import(process.env.PRODUCTOS_PLAYWRIGHT_MODULE
    ? pathToFileURL(process.env.PRODUCTOS_PLAYWRIGHT_MODULE).href : 'playwright');
const root = process.env.PRODUCTOS_PROJECT_ROOT || fileURLToPath(new URL('../', import.meta.url));
const baseline = process.env.PRODUCTOS_QA_BASELINE === '1';
const artifacts = process.env.PRODUCTOS_QA_DIRECTORY || resolve(root, '.artifacts/productos', String(Date.now()));
await mkdir(artifacts, { recursive: true });
const reservation = createServer();
await new Promise(r => reservation.listen(0, '127.0.0.1', r));
const port = reservation.address().port;
await new Promise(r => reservation.close(r));
const address = `http://127.0.0.1:${port}`;
const app = spawn(process.env.PRODUCTOS_DOTNET || 'dotnet',
    [resolve(root, 'src/ResellManager.Web/bin/Debug/net10.0/ResellManager.Web.dll')], {
        cwd: resolve(root, 'src/ResellManager.Web'),
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: address,
            ConnectionStrings__ResellManager: `Data Source=${resolve(artifacts, 'qa.db')}`,
            AlmacenamientoImagenesProducto__DirectorioBase: resolve(artifacts, 'productos'),
            AlmacenamientoComprobantes__DirectorioBase: resolve(artifacts, 'comprobantes'),
            DataProtection__KeysPath: resolve(artifacts, 'keys'),
            UsuarioInicial__Correo: 'productos-qa@example.test', UsuarioInicial__Contrasena: 'QaOnly!Productos22',
            ProductoLookup__OpenFactsHabilitado: 'false', ProductoLookup__UpcitemdbHabilitado: 'false' }
    });
let output = '', browser;
app.stdout.on('data', d => output += d);
app.stderr.on('data', d => output += d);
const checks = [], errors = [];
try {
    for (let i = 0; i < 150; i++) {
        if (app.exitCode !== null) throw new Error(`Proceso ${app.exitCode}: ${output}`);
        try { if ((await fetch(address + '/health')).ok) break; } catch { }
        if (i === 149) throw new Error('No arrancó: ' + output);
        await new Promise(r => setTimeout(r, 200));
    }
    browser = await chromium.launch({ headless: true, executablePath: process.env.PRODUCTOS_BROWSER_EXECUTABLE });
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    const cdp = await context.newCDPSession(page);
    const respuestasMiniatura = [];
    await cdp.send('Network.enable');
    cdp.on('Network.responseReceived', ({ response }) => {
        if (response.url.includes('/miniatura?v='))
            respuestasMiniatura.push({ url: response.url, cache: response.fromDiskCache || response.fromPrefetchCache });
    });
    page.on('pageerror', e => errors.push(e.message));
    async function open(path) { await page.goto(address + path); await page.waitForLoadState('networkidle'); }
    async function fill(id, value) { await page.locator('#' + id).fill(value); await page.locator('#' + id).blur(); }
    await open('/login');
    await fill('correo', 'productos-qa@example.test');
    await fill('contrasena', 'QaOnly!Productos22');
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await page.waitForURL(url => !url.pathname.includes('login'));
    await open('/categorias/nueva');
    await fill('categoria-nombre', 'Categoría QA Productos');
    await page.getByRole('button', { name: 'Guardar categoría', exact: true }).click();
    await page.waitForURL(/\/categorias\?/);
    const png = await page.evaluate(() => {
        const c = document.createElement('canvas'); c.width = 600; c.height = 900;
        const x = c.getContext('2d'); x.fillStyle = '#2360ad'; x.fillRect(60, 90, 480, 720);
        return c.toDataURL('image/png').split(',')[1];
    });
    for (const name of ['Producto QA Azul', 'Producto QA Verde']) {
        await open('/productos/nuevo');
        await fill('producto-nombre', name);
        await fill('producto-precio-sugerido', '120');
        await page.locator('#producto-categoria').selectOption({ label: 'Categoría QA Productos' });
        await page.locator('#producto-imagen').setInputFiles({ name: 'foto.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') });
        await page.getByText('1 de 8 fotografías preparadas', { exact: true }).waitFor();
        await page.getByRole('button', { name: 'Guardar producto', exact: true }).click();
        await page.waitForURL(/\/productos(?:\?|$)/);
    }
    for (const width of [390, 1440]) {
        await page.setViewportSize({ width, height: 1000 });
        for (const entry of ['nombre', 'boton']) {
            await open('/productos');
            const card = page.locator('article[aria-label="Producto QA Azul"]');
            const image = card.locator('img');
            await image.waitFor();
            await image.scrollIntoViewIfNeeded();
            await page.waitForFunction(() => [...document.querySelectorAll('article img')].every(i => i.complete && i.naturalWidth > 0));
            if (!baseline) {
                assert.match(await image.getAttribute('src'), /\/miniatura\?v=/);
                assert.ok(await image.evaluate(i => i.naturalWidth <= 240 && i.naturalHeight <= 240));
            }
            await page.screenshot({ path: resolve(artifacts, `listado-${width}-${entry}.png`), fullPage: true });
            if (entry === 'nombre') await card.getByRole('heading', { name: 'Producto QA Azul', exact: true }).click();
            else await card.getByRole('link', { name: 'Ver detalle', exact: true }).click();
            await page.waitForURL(/\/productos\/\d+$/);
            await page.getByRole('heading', { level: 1, name: 'Producto QA Azul', exact: true }).waitFor({ timeout: 10_000 });
            const full = page.locator('figure img');
            await full.waitFor();
            assert.match(await full.getAttribute('src'), /\/imagen/);
            await page.waitForFunction(() => document.querySelector('figure img')?.naturalHeight > 240);
            assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
            await page.screenshot({ path: resolve(artifacts, `detalle-${width}-${entry}.png`), fullPage: true });
            const descargasAntes = respuestasMiniatura.filter(x => !x.cache).length;
            await page.getByRole('link', { name: 'Volver', exact: true }).click();
            await page.getByRole('heading', { level: 1, name: 'Productos', exact: true }).waitFor();
            await page.waitForLoadState('networkidle');
            if (!baseline) assert.equal(respuestasMiniatura.filter(x => !x.cache).length, descargasAntes,
                'Volver debe reutilizar las miniaturas en caché');
            checks.push(`${width}px: ${entry}, ${baseline ? 'imagen original' : 'miniatura'}, detalle y volver sin recarga`);
        }
    }
    if (!baseline) {
        let solicitudes = 0, reintentos = 0;
        const patron = '**/productos/*/miniatura?*';
        await page.route(patron, async route => {
            solicitudes++;
            if (route.request().url().includes('reintento=1')) reintentos++;
            if (!route.request().url().includes('reintento=1'))
                await route.fulfill({ status: 503, headers: { 'cache-control': 'no-store' }, body: '' });
            else await route.continue();
        });
        await open('/productos');
        await page.waitForFunction(() => [...document.querySelectorAll('article img')].length === 2
            && [...document.querySelectorAll('article img')].every(i => i.complete && i.naturalWidth > 0 && i.src.includes('reintento=1')));
        assert.equal(reintentos, 2);
        // Prerender e hidratación pueden solicitar por separado la URL inicial fallida.
        assert.ok(solicitudes <= 6);
        await page.unroute(patron);
        solicitudes = 0;
        reintentos = 0;
        await page.route(patron, async route => {
            solicitudes++;
            if (route.request().url().includes('reintento=1')) reintentos++;
            await route.fulfill({ status: 404, headers: { 'cache-control': 'no-store' }, body: '' });
        });
        await open('/productos');
        await page.getByRole('img', { name: 'Imagen no disponible', exact: true }).first().waitFor();
        assert.equal(await page.getByRole('img', { name: 'Imagen no disponible', exact: true }).count(), 2);
        assert.equal(reintentos, 2);
        assert.ok(solicitudes <= 6);
        await page.unroute(patron);
        checks.push('Error transitorio: un reintento; error permanente: fallback sin bucle');

        await open('/categorias/nueva');
        await fill('categoria-nombre', 'Otra categoría QA');
        await page.getByRole('button', { name: 'Guardar categoría', exact: true }).click();
        await page.waitForURL(/\/categorias\?/);
        for (let i = 1; i <= 41; i++) {
            await open('/productos/nuevo');
            await fill('producto-nombre', `Producto QA Lote ${String(i).padStart(2, '0')}`);
            await fill('producto-precio-sugerido', '120');
            await page.locator('#producto-categoria').selectOption({ label: i <= 35 ? 'Categoría QA Productos' : 'Otra categoría QA' });
            await page.getByRole('button', { name: 'Guardar producto', exact: true }).click();
            await page.waitForURL(/\/productos(?:\?|$)/);
        }
        const count = async n => page.waitForFunction(n => document.querySelectorAll('article[aria-label]').length === n, n);
        async function siguiente(n) {
            await page.locator('[data-productos-sentinel]').evaluate(node => node.scrollIntoView({ block: 'end', behavior: 'instant' }));
            await count(n);
            await page.waitForLoadState('networkidle');
        }
        for (const width of [390, 1440]) {
            await page.setViewportSize({ width, height: 1000 });
            await open('/productos');
            await count(20);
            await siguiente(40);
            await siguiente(43);
            assert.equal(await page.locator('[data-productos-sentinel]').count(), 0);
            const nombres = await page.locator('article[aria-label]').evaluateAll(items => items.map(i => i.getAttribute('aria-label')));
            assert.equal(new Set(nombres).size, 43);
            assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
            await page.screenshot({ path: resolve(artifacts, `scroll-${width}.png`) });
            checks.push(`${width}px: scroll real 20 → 40 → 43, sin duplicados y con fin de resultados`);
        }
        await fill('buscar-producto', 'Lote');
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        await count(20);
        assert.ok((await page.locator('article[aria-label]').evaluateAll(items => items.map(i => i.getAttribute('aria-label')))).every(name => name.includes('Lote')));
        await siguiente(40); await siguiente(41);
        await page.getByRole('button', { name: 'Limpiar', exact: true }).click();
        await count(20);
        await page.locator('#categoria-productos').selectOption({ label: 'Categoría QA Productos' });
        const cursorAnterior = await page.locator('[data-productos-sentinel]').elementHandle();
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        await page.waitForFunction(node => !node.isConnected, cursorAnterior);
        await count(20); await siguiente(37);
        assert.equal(await page.locator('article[aria-label="Producto QA Lote 36"]').count(), 0);
        const codigoVerde = await page.locator('article[aria-label="Producto QA Verde"] span.font-mono').innerText();
        await fill('buscar-producto', codigoVerde);
        await page.getByRole('button', { name: 'Buscar', exact: true }).click();
        await count(1);
        await page.locator('article[aria-label="Producto QA Verde"]').waitFor();
        await page.getByRole('button', { name: 'Limpiar', exact: true }).click();
        await count(20);
        checks.push('Búsqueda global por nombre/código, filtro de categoría y Limpiar reinician la paginación');

        const manual = await browser.newContext({ storageState: await context.storageState(), viewport: { width: 390, height: 1000 } });
        await manual.addInitScript(() => { window.IntersectionObserver = undefined; });
        const fallback = await manual.newPage();
        await fallback.goto(address + '/productos');
        await fallback.waitForLoadState('networkidle');
        assert.equal(await fallback.locator('article[aria-label]').count(), 20);
        await fallback.getByRole('button', { name: 'Cargar más productos', exact: true }).click();
        await fallback.waitForFunction(() => document.querySelectorAll('article[aria-label]').length === 40);
        await fallback.getByRole('button', { name: 'Cargar más productos', exact: true }).click();
        await fallback.waitForFunction(() => document.querySelectorAll('article[aria-label]').length === 43);
        assert.equal(await fallback.getByRole('button', { name: 'Cargar más productos', exact: true }).count(), 0);
        await manual.close();
        checks.push('Sin IntersectionObserver: botón manual conserva carga 20 → 40 → 43');
    }
    assert.deepEqual(errors, []);
    console.log(JSON.stringify({ artifacts, checks }, null, 2));
} finally {
    if (browser) await browser.close();
    app.kill('SIGTERM');
    await writeFile(resolve(artifacts, 'server.log'), output);
    await writeFile(resolve(artifacts, 'resultado.json'), JSON.stringify({ checks, errors }, null, 2));
}

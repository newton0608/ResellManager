// QA reproducible contra la aplicación real, DB temporal y respuestas públicas sintéticas.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';
const { chromium } = await import(process.env.CATALOGO_PLAYWRIGHT_MODULE ? pathToFileURL(process.env.CATALOGO_PLAYWRIGHT_MODULE).href : 'playwright');
const root = fileURLToPath(new URL('../', import.meta.url));
const artifacts = resolve(root, '.artifacts/catalogo-qa', String(Date.now()));
await mkdir(artifacts, { recursive: true });
const reservation = createServer();
await new Promise(r => reservation.listen(0, '127.0.0.1', r));
const port = reservation.address().port;
await new Promise(r => reservation.close(r));
const address = `http://127.0.0.1:${port}`;
let output = '';
const app = spawn('dotnet', [resolve(root, 'src/ResellManager.Web/bin/Debug/net10.0/ResellManager.Web.dll')], {
    cwd: resolve(root, 'src/ResellManager.Web'), windowsHide: true,
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: address,
        ConnectionStrings__ResellManager: `Data Source=${resolve(artifacts, 'qa.db')}`,
        AlmacenamientoImagenesProducto__DirectorioBase: resolve(artifacts, 'productos'),
        AlmacenamientoComprobantes__DirectorioBase: resolve(artifacts, 'comprobantes'),
        CatalogoContacto__WhatsAppNumero: '50255550123', CatalogoContacto__OrigenPublico: 'https://tienda.example',
        UsuarioInicial__Correo: 'catalogo-qa@example.test', UsuarioInicial__Contrasena: 'QaOnly!Catalog14',
        DataProtection__KeysPath: resolve(artifacts, 'keys') }
});
app.stdout.on('data', d => output += d); app.stderr.on('data', d => output += d);
let browser, page;
const checks = [], errors = [];
function check(name, value) { assert.ok(value, name); checks.push(name); }
try {
    for (let i = 0; i < 100; i++) {
        if (typeof app.exitCode === 'number') throw new Error(`Proceso ${app.exitCode}: ${output}`);
        try { if ((await fetch(address + '/health')).ok) break; } catch { }
        await new Promise(r => setTimeout(r, 200));
        if (i === 99) throw new Error('No arrancó aplicación: ' + output);
    }
    browser = await chromium.launch({ channel: process.env.CATALOGO_BROWSER_CHANNEL || 'msedge', headless: true });
    const context = await browser.newContext();
    page = await context.newPage();
    page.on('pageerror', e => errors.push(e.message));
    const png = await page.evaluate(() => {
        const canvas = document.createElement('canvas'); canvas.width = 1200; canvas.height = 1500;
        const ctx = canvas.getContext('2d'); ctx.fillStyle = 'white'; ctx.fillRect(0, 0, 1200, 1500);
        ctx.fillStyle = '#173221'; ctx.font = 'bold 64px sans-serif'; ctx.fillText('VITAMINA Á & B', 100, 150);
        ctx.font = '48px sans-serif'; ctx.fillText('Información nutricional', 100, 360);
        for (let i = 0; i < 7; i++) { ctx.font = '32px sans-serif'; ctx.fillText(`Nutriente ${i + 1}                 10 mg`, 100, 500 + i * 100); }
        return canvas.toDataURL('image/png').split(',')[1];
    });
    const photos = [{ id: 'a', orden: 0, esPortada: true }, { id: 'b', orden: 1, esPortada: false }];
    const product = { id: 7, nombre: 'Vitamina Á & B', categoriaId: 4, categoria: 'Suplementos', categoriaPadreId: 3,
        categoriaPadreNombre: 'Salud y bienestar', precioPublico: 149, tieneImagenPrincipal: true, disponible: true, marca: 'Acmé',
        descripcion: 'Producto sintético para QA. No corresponde a inventario real.', imagenes: photos };
    const requests = [];
    await page.route('**/api/catalogo/productos**', async route => {
        const url = new URL(route.request().url()); requests.push(url.pathname + url.search);
        if (url.pathname.includes('/imagen')) return route.fulfill({ status: 200, contentType: 'image/png', body: Buffer.from(png, 'base64') });
        if (url.pathname.endsWith('/7')) return route.fulfill({ json: product });
        const match = !url.searchParams.get('marca') || url.searchParams.get('marca').toLowerCase() === 'acmé';
        return route.fulfill({ json: match ? [product] : [] });
    });
    for (const width of [320, 390, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(address + '/catalogo');
        await page.getByRole('link', { name: /Ver producto/ }).waitFor();
        check(`listado ${width} sin overflow`, await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        check(`listado ${width} no descarga galería`, !requests.some(r => r.includes('/imagenes/')));
        await page.selectOption('#catalogo-categoria', '3');
        await page.selectOption('#catalogo-marca', 'Acmé');
        await page.locator('#catalogo-busqueda').fill('Vitamina');
        await page.waitForURL(/termino=Vitamina/);
        check(`URL tres filtros ${width}`, new URL(page.url()).searchParams.get('marca') === 'Acmé' && new URL(page.url()).searchParams.get('categoriaId') === '3');
        await page.goto(page.url());
        await page.getByRole('link', { name: /Ver producto/ }).waitFor();
        check(`restaura filtros ${width}`, await page.locator('#catalogo-marca').inputValue() === 'Acmé');
        await page.screenshot({ path: resolve(artifacts, `listado-${width}.png`), fullPage: true });
        await page.goto(address + '/catalogo/7');
        await page.getByRole('heading', { name: product.nombre, exact: true }).waitFor();
        check(`detalle ${width} sin overflow`, await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        const wa = await page.getByRole('link', { name: 'Consultar por WhatsApp' }).getAttribute('href');
        check(`WhatsApp canónico codificado ${width}`, new URL(wa).searchParams.get('text').includes('Vitamina Á & B. https://tienda.example/producto/7'));
        check(`indicador verde ${width}`, await page.locator('.store-availability').evaluate(e => getComputedStyle(e, '::before').backgroundColor === 'rgb(35, 116, 60)'));
        await page.screenshot({ path: resolve(artifacts, `detalle-${width}.png`), fullPage: true });
        await page.getByRole('button', { name: 'Ampliar fotografías de ' + product.nombre }).click();
        await page.locator('[data-gallery-dialog]').evaluate(d => { if (!d.open) throw new Error('visor cerrado'); });
        check(`bloquea scroll ${width}`, await page.evaluate(() => document.body.style.overflow === 'hidden'));
        await page.getByRole('button', { name: 'Ampliar zoom', exact: true }).click();
        check(`zoom ${width}`, await page.locator('[data-gallery-zoom]').textContent() === '150%');
        await page.keyboard.press('ArrowRight');
        check(`flechas y reset ${width}`, await page.locator('[data-gallery-zoom]').textContent() === '100%');
        await page.screenshot({ path: resolve(artifacts, `visor-${width}.png`) });
        await page.keyboard.press('Escape');
        check(`foco restaurado ${width}`, await page.locator('[data-gallery-open]').evaluate(e => document.activeElement === e));
        check(`scroll restaurado ${width}`, await page.evaluate(() => document.body.style.overflow !== 'hidden'));
        // El marker DOM funciona igual con ruta limpia pública reescrita.
        await page.evaluate(() => history.replaceState({}, '', '/producto/7'));
        await page.evaluate(() => document.getElementById('components-reconnect-modal').className = 'components-reconnect-show');
        await page.locator('#store-reconnect').waitFor({ state: 'visible' });
        check(`reconexión pública ${width} sin modal`, await page.locator('#components-reconnect-modal').evaluate(e => !e.open));
        await page.evaluate(() => document.getElementById('components-reconnect-modal').className = 'components-reconnect-rejected');
        await page.getByRole('button', { name: 'Recargar', exact: true }).waitFor();
        await page.evaluate(() => document.getElementById('components-reconnect-modal').className = 'components-reconnect-hide');
        requests.length = 0;
    }
    // Pan/pinch/swipe sintéticos. No sustituyen Safari/iPhone/Android físicos.
    await page.locator('[data-gallery-index="0"]').click();
    await page.locator('[data-gallery-open]').click();
    await page.locator('[data-gallery-stage]').evaluate(stage => {
        const send = (type, id, x, y) => stage.dispatchEvent(new PointerEvent(type, { bubbles: true, pointerId: id, pointerType: 'touch', clientX: x, clientY: y }));
        // Dispatch no activa captura nativa de punteros; sustituir solo ese método en QA sintético.
        stage.setPointerCapture = () => {};
        send('pointerdown', 1, 200, 300); send('pointerdown', 2, 300, 300);
        send('pointermove', 2, 310, 300); send('pointermove', 2, 420, 300);
        send('pointerup', 2, 420, 300); send('pointermove', 1, 140, 300); send('pointerup', 1, 140, 300);
    });
    check('pinch amplía sin cambiar fotografía', await page.locator('[data-gallery-zoom]').textContent() === '200%');
    check('pinch no navega fotografía', await page.locator('[data-gallery-count]').first().textContent() === '1 / 2');
    await page.getByRole('button', { name: 'Restablecer zoom' }).click();
    await page.locator('[data-gallery-stage]').evaluate(stage => {
        for (const [type, x] of [['pointerdown', 300], ['pointermove', 200], ['pointerup', 180]]) stage.dispatchEvent(new PointerEvent(type, { pointerId: 1, pointerType: 'touch', clientX: x, clientY: 300, bubbles: true }));
    });
    check('swipe sin zoom cambia fotografía', await page.locator('[data-gallery-count]').first().textContent() === '2 / 2');
    await page.keyboard.press('Escape');
    await page.route('**/api/catalogo/productos/7/imagenes/b', route => route.fulfill({ status: 404 }));
    await page.goto(address + '/catalogo/7'); await page.locator('[data-gallery-open]').waitFor();
    await page.getByRole('button', { name: /Ver fotografía 2/ }).click();
    await page.locator('[data-gallery-fallback]').waitFor({ state: 'visible' });
    check('imagen fallida mantiene galería y placeholder', await page.locator('[data-gallery-open]').isVisible());
    await page.goto(address + '/login');
    await page.evaluate(() => document.getElementById('components-reconnect-modal').classList.replace('components-reconnect-hide', 'components-reconnect-show'));
    check('administración conserva modal', await page.locator('#components-reconnect-modal').evaluate(e => e.open));
    check('administración oculta aviso público', await page.locator('#store-reconnect').isHidden());
    // Administración real sobre la misma DB temporal, identidad ficticia, sin inventario real.
    await page.goto(address + '/login');
    await page.locator('#correo').fill('catalogo-qa@example.test');
    await page.locator('#contrasena').fill('QaOnly!Catalog14');
    await page.getByRole('button', { name: 'Entrar' }).click();
    await page.waitForURL(address + '/');
    await page.goto(address + '/categorias/nueva');
    await page.waitForLoadState('networkidle');
    await page.locator('#categoria-nombre').fill('Salud QA');
    await page.getByRole('button', { name: 'Guardar categoría' }).click();
    await page.waitForURL(/\/categorias\?/);
    await page.goto(address + '/categorias/nueva');
    await page.waitForLoadState('networkidle');
    await page.locator('#categoria-nombre').fill('Vitaminas QA');
    await page.selectOption('#categoria-padre', '1');
    await page.getByRole('button', { name: 'Guardar categoría' }).click();
    await page.waitForURL(/\/categorias\?/);
    await page.goto(address + '/productos/nuevo');
    await page.waitForLoadState('networkidle');
    await page.locator('#producto-nombre').fill('Producto sintético QA');
    await page.selectOption('#producto-categoria', '2');
    await page.locator('#producto-precio-sugerido').fill('49.95');
    const files = [1, 2].map(i => ({ name: `foto-${i}.png`, mimeType: 'image/png', buffer: Buffer.from(png, 'base64') }));
    await page.locator('#producto-imagen').setInputFiles(files);
    await page.getByRole('button', { name: 'Usar como portada' }).waitFor();
    await page.getByRole('button', { name: 'Usar como portada' }).click();
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click();
    await page.waitForURL(/\/productos\?/);
    const saved = await (await context.request.get(address + '/productos/1/imagenes')).json();
    check('alta real dos fotografías y portada independiente del orden', saved.length === 2 && saved[1].esPortada);
    for (const width of [320, 390, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(address + '/productos/1/editar');
    await page.waitForLoadState('networkidle');
        await page.getByRole('button', { name: 'Portada seleccionada' }).waitFor();
        check(`admin fotografías ${width} sin overflow`, await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        check(`admin galería persistida ${width}`, await page.locator('ol[aria-label="Fotografías ordenadas"] > li').count() === 2);
        await page.screenshot({ path: resolve(artifacts, `admin-producto-${width}.png`), fullPage: true });
        await page.goto(address + '/categorias/1/editar');
        check(`admin raíz con hijas ${width} restringida`, await page.locator('#categoria-padre').isDisabled());
        check(`admin categoría ${width} sin overflow`, await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.screenshot({ path: resolve(artifacts, `admin-categoria-${width}.png`), fullPage: true });
    }
    await page.goto(address + '/productos/1/editar');
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Mover fotografía 2 antes' }).click();
    await page.waitForFunction(() => document.querySelector('ol[aria-label="Fotografías ordenadas"] > li p')?.textContent.includes('Portada'));
    await page.getByRole('button', { name: 'Eliminar fotografía 2' }).click();
    await page.waitForFunction(() => document.querySelectorAll('ol[aria-label="Fotografías ordenadas"] > li').length === 1);
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click();
    await page.waitForURL(/\/productos\?/);
    const edited = await (await context.request.get(address + '/productos/1/imagenes')).json();
    check('edición real reordena/elimina y conserva portada', edited.length === 1 && edited[0].esPortada && edited[0].id === saved[1].id);
    assert.deepEqual(errors, []);
    await writeFile(resolve(artifacts, 'report.json'), JSON.stringify({ checks, errors }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, artifacts }, null, 2));
} catch (error) {
    await page?.screenshot({ path: resolve(artifacts, 'failure.png'), fullPage: true });
    await writeFile(resolve(artifacts, 'failure.json'), JSON.stringify({ checks, errors, message: error.message }, null, 2));
    throw error;
} finally {
    await browser?.close(); app.kill();
    await writeFile(resolve(artifacts, 'app.log'), output);
}
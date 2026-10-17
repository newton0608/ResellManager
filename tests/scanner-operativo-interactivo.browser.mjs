// QA del circuito Blazor real, con base/archivos temporales y fotografías de fixtures.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const modulePath = process.env.SCANNER_PLAYWRIGHT_MODULE;
const { chromium } = await import(modulePath ? pathToFileURL(modulePath).href : 'playwright');
const { expect } = await import(modulePath ? new URL('test.mjs', pathToFileURL(modulePath)).href : 'playwright/test');
const root = fileURLToPath(new URL('../', import.meta.url));
const artifacts = resolve(root, '.artifacts/scanner-operativo-interactivo', String(Date.now()));
await mkdir(artifacts, { recursive: true });
const reservation = createServer();
await new Promise(r => reservation.listen(0, '127.0.0.1', r));
const port = reservation.address().port;
await new Promise(r => reservation.close(r));
const address = `http://127.0.0.1:${port}`;
const channel = process.env.SCANNER_BROWSER_CHANNEL || 'msedge';
const codigo = '3574660239843';
const app = spawn('dotnet', [resolve(root, 'src/ResellManager.Web/bin/Debug/net10.0/ResellManager.Web.dll')], {
    cwd: resolve(root, 'src/ResellManager.Web'),
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: address,
        ConnectionStrings__ResellManager: `Data Source=${resolve(artifacts, 'qa.db')}`,
        AlmacenamientoImagenesProducto__DirectorioBase: resolve(artifacts, 'productos'),
        AlmacenamientoComprobantes__DirectorioBase: resolve(artifacts, 'comprobantes'),
        DataProtection__KeysPath: resolve(artifacts, 'keys'),
        UsuarioInicial__Correo: 'scanner-qa@example.test', UsuarioInicial__Contrasena: 'QaOnly!Scanner21',
        ProductoLookup__OpenFactsHabilitado: 'false', ProductoLookup__UpcitemdbHabilitado: 'false' }
});
let output = '', browser, page;
app.stdout.on('data', d => output += d);
app.stderr.on('data', d => output += d);
const errors = [], checks = [];
function passed(name) { checks.push(name); console.log(name); }
async function open(path, target = page) {
    await target.goto(address + path);
    // El HTML prerenderizado aparece antes de que el circuito conecte sus eventos.
    await target.waitForLoadState('networkidle');
}
async function fill(id, value) {
    const input = page.locator('#' + id);
    await input.fill(value);
    await input.blur();
}
async function scan(target = page) {
    await target.getByRole('button', { name: 'Escanear código de barras con la cámara' }).first().click();
    const dialog = target.locator('dialog.rm-barcode-scanner[open]');
    await expect(dialog).toBeVisible();
    await dialog.locator('[data-barcode-file]').setInputFiles(resolve(root, 'tests/fixtures/barcodes/ean.jpg'));
    await expect(dialog.locator('[data-barcode-code]')).toHaveText(codigo, { timeout: 30_000 });
    await dialog.getByRole('button', { name: 'Usar código', exact: true }).click();
    await expect(dialog).toHaveCount(0);
}
async function views(name) {
    for (const width of [390, 1440]) {
        await page.setViewportSize({ width, height: 1000 });
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${name}: overflow a ${width}`);
        await page.screenshot({ path: resolve(artifacts, `${name}-${width}.png`), fullPage: true });
    }
    await page.setViewportSize({ width: 390, height: 1000 });
}
try {
    for (let i = 0; i < 150; i++) {
        if (app.exitCode !== null) throw new Error(`Proceso ${app.exitCode}: ${output}`);
        try { if ((await fetch(address + '/health')).ok) break; } catch { }
        if (i === 149) throw new Error('No arrancó aplicación: ' + output);
        await new Promise(r => setTimeout(r, 200));
    }
    browser = await chromium.launch({ channel, headless: true });
    const context = await browser.newContext({ viewport: { width: 390, height: 1000 } });
    page = await context.newPage();
    context.on('page', p => p.on('pageerror', e => errors.push(e.message)));
    page.on('pageerror', e => errors.push(e.message));
    await open('/login');
    await fill('correo', 'scanner-qa@example.test');
    await fill('contrasena', 'QaOnly!Scanner21');
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await expect(page).not.toHaveURL(/\/login/);
    // Preparación sólo mediante formularios reales sobre la DB temporal.
    await open('/categorias/nueva');
    await fill('categoria-nombre', 'Raíz sintética');
    await page.getByRole('button', { name: 'Guardar categoría', exact: true }).click();
    await expect(page).toHaveURL(/\/categorias\?/);
    await open('/categorias/nueva');
    await fill('categoria-nombre', 'Hija sintética');
    await page.locator('#categoria-padre').selectOption({ label: 'Raíz sintética' });
    await page.getByRole('button', { name: 'Guardar categoría', exact: true }).click();
    await expect(page).toHaveURL(/\/categorias\?/);
    await open('/proveedores/nuevo');
    await fill('proveedor-nombre', 'Proveedor sintético');
    await page.getByRole('button', { name: 'Guardar proveedor', exact: true }).click();
    await expect(page).toHaveURL(/\/proveedores(?:\?|$)/);
    await open('/clientes/nuevo');
    await fill('cliente-nombres', 'Cliente sintético');
    await fill('cliente-telefono', '55550123');
    await page.getByRole('button', { name: 'Guardar cliente', exact: true }).click();
    await expect(page).toHaveURL(/\/clientes\?/);
    passed('Preparación sintética y autenticación reales');

    await open('/compras/nueva');
    await page.locator('#compra-origen').selectOption('CompraLocal');
    await fill('compra-proveedor', 'Proveedor sintético');
    await page.getByRole('button', { name: 'Proveedor sintético', exact: true }).click();
    await fill('cantidad-compra-1', '5');
    await fill('costo-compra-1', '8.25');
    await fill('compra-observaciones', 'Compra sintética que debe conservarse');
    const png = await page.evaluate(() => {
        const c = document.createElement('canvas'); c.width = 32; c.height = 48;
        const x = c.getContext('2d'); x.fillStyle = '#0066d6'; x.fillRect(0, 0, 32, 48);
        return c.toDataURL('image/png').split(',')[1];
    });
    await page.getByRole('checkbox', { name: 'Adjuntar comprobante' }).check();
    await fill('comprobante-numero', 'QA-SINTETICO');
    await page.locator('#comprobante-archivo').setInputFiles({ name: 'comprobante.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') });
    await page.getByRole('button', { name: 'Escanear código de barras con la cámara' }).click();
    await page.locator('dialog.rm-barcode-scanner[open]').getByRole('button', { name: 'Cancelar', exact: true }).click();
    await expect(page.locator('#cantidad-compra-1')).toHaveValue('5');
    await scan();
    await page.getByRole('button', { name: 'Registrar producto', exact: true }).click();
    await expect(page.locator('#producto-codigo-barras')).toHaveValue(codigo);
    const alta = page.locator('section[aria-labelledby="titulo-alta-producto"]');
    await alta.getByRole('button', { name: 'Cancelar', exact: true }).click();
    await expect(page.locator('#cantidad-compra-1')).toHaveValue('5');
    await scan();
    await page.getByRole('button', { name: 'Registrar producto', exact: true }).click();
    await fill('producto-nombre', 'Producto sintético scanner');
    await fill('producto-precio-sugerido', '120');
    await page.locator('#producto-categoria').selectOption({ label: 'Raíz sintética' });
    await page.locator('#producto-subcategoria').selectOption({ label: 'Hija sintética' });
    await page.locator('#producto-imagen').setInputFiles([
        { name: 'primera.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') },
        { name: 'portada.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') }
    ]);
    await expect(alta.getByText('2 de 8 fotografías preparadas', { exact: true })).toBeVisible();
    await alta.getByRole('button', { name: 'Usar como portada', exact: true }).click();
    await views('compra-alta-galeria');
    await alta.getByRole('button', { name: 'Guardar producto', exact: true }).click();
    await expect(alta).toHaveCount(0);
    await expect(page.locator('#producto-compra-1-estado')).toContainText('Producto sintético scanner');
    await expect(page.locator('#cantidad-compra-1')).toHaveValue('5');
    await expect(page.locator('#costo-compra-1')).toHaveValue('8.25');
    await expect(page.locator('#comprobante-numero')).toHaveValue('QA-SINTETICO');
    assert.equal(await page.locator('#comprobante-archivo').evaluate(e => e.files[0].name), 'comprobante.png');
    passed('Compra: fotografía → alta con galería/subcategoría → retorno con comprobante intacto');
    const inventory = await context.newPage();
    await open('/inventario', inventory);
    await scan(inventory);
    await expect(inventory).toHaveURL(/\/productos\/\d+$/);
    await expect(inventory.getByRole('heading', { name: 'Producto sintético scanner', exact: true })).toBeVisible();
    await inventory.close();
    passed('Inventario: fotografía abre Producto antes de existir unidades de Compra');
    await page.getByRole('button', { name: 'Revisar compra', exact: true }).click();
    await expect(page.locator('dialog[open]')).toContainText('5 × Q 8.25');
    await page.getByRole('button', { name: 'Confirmar compra', exact: true }).click();
    await expect(page).toHaveURL(/\/compras\/\d+(?:\?|$)/);
    passed('Compra confirmada con unidades y comprobante mediante servicios reales');

    await open('/ventas/nueva');
    await page.getByRole('button', { name: 'Venta directa', exact: true }).click();
    await fill('venta-directa-cliente', 'Cliente sintético');
    await page.getByRole('button', { name: /Cliente sintético.*55550123/ }).click();
    await scan();
    await expect(page.locator('#directa-cantidad-scanner')).toHaveAttribute('max', '5');
    await fill('directa-cantidad-scanner', '2');
    await page.getByRole('button', { name: 'Agregar unidades', exact: true }).click();
    await expect(page.locator('.rm-sale-lot')).toHaveCount(1);
    await scan();
    await expect(page.locator('#directa-cantidad-scanner')).toHaveAttribute('max', '3');
    await page.getByRole('button', { name: 'Agregar unidades', exact: true }).click();
    await expect(page.locator('.rm-sale-lot')).toHaveCount(2);
    await page.locator('[id^="precio-directo-"]').nth(0).fill('75');
    await page.locator('[id^="precio-directo-"]').nth(1).fill('65');
    await page.locator('[id^="precio-directo-"]').nth(1).blur();
    await views('venta-lotes');
    await page.getByRole('button', { name: 'Revisar venta', exact: true }).click();
    await expect(page.locator('dialog[open] .rm-sale-review-lot')).toHaveCount(2);
    await expect(page.locator('dialog[open]')).toContainText('Q 215.00');
    await page.getByRole('button', { name: 'Confirmar venta', exact: true }).click();
    await expect(page).toHaveURL(/\/ventas\/\d+(?:\?|$)/);
    await expect(page.locator('main')).toContainText('Q 215.00');
    passed('Venta Directa: fotografía, exclusión de seleccionadas, lotes/precios y persistencia real');
    assert.deepEqual(errors, []);
    await writeFile(resolve(artifacts, 'report.json'), JSON.stringify({ browser: await browser.version(), channel,
        liveCircuit: true, hardwareCamera: false, opticalFixtures: true, syntheticDatabase: true, checks, errors }, null, 2));
    console.log(`Informe: ${artifacts}`);
} catch (error) {
    if (page && !page.isClosed()) {
        await page.screenshot({ path: resolve(artifacts, 'failure.png'), fullPage: true }).catch(() => {});
        await writeFile(resolve(artifacts, 'failure.html'), await page.content());
    }
    throw error;
} finally {
    await browser?.close();
    app.kill();
    await writeFile(resolve(artifacts, 'app.log'), output);
}

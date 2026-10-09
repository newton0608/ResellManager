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
    // El flujo principal certifica el botón de fallback. Un segundo contexto usa el observer real.
    await context.addInitScript(() => { window.IntersectionObserver = undefined; });
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
    const roots = [3, 5, 6, 8, ...Array.from({ length: 15 }, (_, i) => i + 10)].map(id => ({
        id, nombre: id === 3 ? 'Salud y bienestar' : id === 5 ? 'Un artículo' : id === 6 ? 'Diez artículos' : id === 8 ? 'Once artículos' : `Raíz QA ${id}`, categoriaPadreId: null
    }));
    const children = [{ id: 4, nombre: 'Suplementos', categoriaPadreId: 3 },
        ...Array.from({ length: 17 }, (_, i) => ({ id: 100 + i, nombre: `Subcategoría QA ${i + 1}`, categoriaPadreId: 3 }))];
    const brands = ['Acmé', ...Array.from({ length: 17 }, (_, i) => `Marca QA ${String(i + 1).padStart(2, '0')}`)];
    const rootProducts = Array.from({ length: 40 }, (_, i) => ({ ...product, id: i + 7,
        nombre: i === 0 ? product.nombre : `Producto QA ${String(i + 7).padStart(3, '0')}`,
        categoriaId: i % 3 === 0 ? 4 : 3, categoria: i % 3 === 0 ? 'Suplementos' : 'Salud y bienestar',
        categoriaPadreId: i % 3 === 0 ? 3 : null, categoriaPadreNombre: i % 3 === 0 ? 'Salud y bienestar' : null
    }));
    const childProducts = children.slice(1).map((category, i) => ({ ...product, id: 1000 + i,
        nombre: `Producto QA subcategoría ${i + 1}`, categoriaId: category.id, categoria: category.nombre,
        categoriaPadreId: 3, categoriaPadreNombre: 'Salud y bienestar' }));
    const otherProducts = roots.slice(1).flatMap(category => Array.from({ length: category.id === 6 ? 10 : category.id === 8 ? 11 : 1 }, (_, i) => ({
        ...product, id: category.id * 10000 + i, nombre: `Producto QA raíz ${category.id} foto ${i + 1}`,
        categoriaId: category.id, categoria: category.nombre, categoriaPadreId: null, categoriaPadreNombre: null,
        marca: brands[category.id % brands.length]
    })));
    const allProducts = [...rootProducts, ...childProducts, ...otherProducts].sort((a, b) => a.id - b.id);
    const requests = [], payloads = [];
    let failNextPage = false, slowStarted, signalSlow;
    const smallPage = (items, cursor, size = 16) => {
        const available = items.filter(item => item.id > (Number(cursor) || 0));
        const selected = available.slice(0, size);
        return { items: selected, hasMore: available.length > size, nextCursor: available.length > size ? selected.at(-1).id : null };
    };
    const forCategory = (items, id) => !id ? items : items.filter(item => item.categoriaId === Number(id) || item.categoriaPadreId === Number(id));
    const cover = ({ descripcion, imagenes, ...item }) => item;
    async function publicRoute(route) {
        const url = new URL(route.request().url()), endpoint = url.pathname.replace('/api/catalogo/productos', '');
        requests.push(url.pathname + url.search);
        if (endpoint.includes('/imagen')) return route.fulfill({ status: 200, contentType: 'image/png', body: Buffer.from(png, 'base64') });
        if (endpoint === '/7') return route.fulfill({ json: product });
        if (endpoint === '') throw new Error('La nueva UI solicitó el listado completo heredado.');
        let result;
        if (endpoint === '/raices') result = smallPage(roots, url.searchParams.get('cursor'));
        else if (endpoint === '/marcas') {
            const available = brands.filter(brand => !url.searchParams.get('cursor') || brand > url.searchParams.get('cursor'));
            const items = available.slice(0, 16);
            result = { items, hasMore: available.length > 16, nextCursor: available.length > 16 ? items.at(-1) : null };
        } else if (endpoint === '/escaparate') {
            const rootPage = smallPage(roots, url.searchParams.get('cursor'), 3);
            result = { secciones: rootPage.items.map(categoria => ({ categoria,
                productos: forCategory(allProducts, categoria.id).slice(0, 10).map(cover) })), hasMore: rootPage.hasMore, nextCursor: rootPage.nextCursor };
        } else if (endpoint === '/pagina') {
            if (failNextPage && url.searchParams.has('cursor')) { failNextPage = false; return route.fulfill({ status: 503, json: { error: 'Fallo sintético de la siguiente página' } }); }
            const term = (url.searchParams.get('termino') || '').toLocaleLowerCase();
            if (term === 'lento') {
                signalSlow?.();
                await new Promise(resolve => setTimeout(resolve, 900));
                result = { items: [{ ...cover(product), id: 9001, nombre: 'Resultado obsoleto' }], hasMore: false, nextCursor: null };
            } else if (term === 'nuevo') result = { items: [{ ...cover(product), id: 9002, nombre: 'Resultado nuevo' }], hasMore: false, nextCursor: null };
            else {
                let available = forCategory(allProducts, url.searchParams.get('categoriaId'));
                if (term) available = available.filter(item => item.nombre.toLocaleLowerCase().includes(term));
                const brand = url.searchParams.get('marca')?.trim().toLocaleLowerCase();
                if (brand) available = available.filter(item => item.marca.trim().toLocaleLowerCase() === brand);
                result = smallPage(available, url.searchParams.get('cursor'));
            }
        } else {
            const category = /^\/categorias\/(\d+)\/(contexto|hijas)$/.exec(endpoint);
            if (!category) throw new Error(`Lectura pública inesperada: ${url}`);
            if (category[2] === 'hijas') result = smallPage(children, url.searchParams.get('cursor'));
            else {
                const selected = [...roots, ...children].find(item => item.id === Number(category[1]));
                if (!selected) return route.fulfill({ status: 404 });
                const raiz = selected.categoriaPadreId ? roots.find(item => item.id === selected.categoriaPadreId) : selected;
                result = { raiz, seleccionada: selected, subcategorias: smallPage(raiz.id === 3 ? children : [], null) };
            }
        }
        payloads.push({ endpoint, cursor: url.searchParams.get('cursor'), size: result.items?.length ?? result.secciones?.length });
        try { return await route.fulfill({ json: result }); }
        catch (error) { if (endpoint !== '/pagina' || url.searchParams.get('termino') !== 'Lento') throw error; }
    }
    await page.route('**/api/catalogo/productos**', publicRoute);
    const cards = () => page.locator('.store-product-card');
    const waitCards = count => page.waitForFunction(expected => document.querySelectorAll('.store-product-card').length === expected, count);
    const noOverflow = () => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth);
    const hasOnlyCover = () => !requests.some(request => request.includes('/imagenes/'));
    const rootUrl = address + '/catalogo?categoriaId=3';
    for (const width of [320, 390, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        requests.length = 0;
        await page.goto(address + '/catalogo');
        await page.locator('[data-store-section]').first().waitFor();
        check(`portada ${width} inicial sólo tres raíces`, await page.locator('[data-store-section]').count() === 3);
        const carouselSizes = await page.locator('[data-store-carousel]').evaluateAll(rows => rows.map(row => row.querySelectorAll('.store-product-card').length));
        check(`portada ${width} carruseles 10/1/10`, JSON.stringify(carouselSizes) === '[10,1,10]');
        check(`portada ${width} categoría vacía y niñas excluidas`, !await page.getByRole('heading', { name: 'Suplementos', exact: true }).count() && !await page.getByRole('heading', { name: 'Vacía QA', exact: true }).count());
        check(`portada ${width} sin overflow`, await noOverflow());
        check(`portada ${width} sin listado total ni galería`, hasOnlyCover() && !requests.some(request => /^\/api\/catalogo\/productos(?:\?|$)/.test(request)));
        check(`portada ${width} carga acotada`, requests.filter(request => request.includes('/escaparate')).length === 1);
        check(`portada ${width} tarjetas 4:5 contain lazy`, await page.locator('.store-product-card img').first().evaluate(image => image.loading === 'lazy' && getComputedStyle(image).objectFit === 'contain' && getComputedStyle(image.closest('.store-media')).aspectRatio === '4 / 5'));
        check(`portada ${width} carrusel tarjetas legibles con overflow interno`, await page.locator('[data-store-carousel]').first().evaluate(row => row.querySelector('.store-product-card').getBoundingClientRect().width >= 150 && row.scrollWidth > row.clientWidth));
        await page.locator('[data-store-section]').first().getByRole('button', { name: 'Productos siguientes de Salud y bienestar', exact: true }).click();
        await page.waitForFunction(() => document.querySelector('[data-store-carousel]').scrollLeft > 0);
        check(`portada ${width} controles accesibles desplazan carrusel`, await page.locator('[data-store-section]').first().getByRole('button', { name: 'Productos anteriores de Salud y bienestar', exact: true }).isEnabled());
        await page.screenshot({ path: resolve(artifacts, `portada-${width}.png`), fullPage: true });
        await page.getByRole('button', { name: 'Cargar más categorías', exact: true }).click();
        await page.waitForFunction(() => document.querySelectorAll('[data-store-section]').length === 6);
        check(`portada ${width} siguiente grupo mantiene anteriores`, await page.locator('[data-store-section]').count() === 6 && (await page.locator('[data-store-carousel]').evaluateAll(rows => rows.map(row => row.querySelectorAll('.store-product-card').length))).every(count => count <= 10));
        await page.locator('[data-store-section]').first().getByRole('link', { name: /Ver todos/ }).click();
        await page.waitForURL(/categoriaId=3/); await waitCards(16);
        check(`raíz ${width} inicia 16`, await cards().count() === 16);
        check(`raíz ${width} chips dependientes`, await page.getByRole('button', { name: 'Todos', exact: true }).getAttribute('aria-pressed') === 'true' && await page.getByRole('button', { name: 'Suplementos', exact: true }).count() === 1);
        check(`raíz ${width} selector sólo raíces`, !await page.locator('#catalogo-categoria option[value="4"]').count());
        await page.getByRole('button', { name: 'Suplementos', exact: true }).click();
        await page.waitForURL(/categoriaId=4/); await waitCards(14);
        check(`hija ${width} productos exclusivos`, await cards().evaluateAll(items => items.every(item => item.querySelector('.store-product-category').textContent === 'Suplementos')));
        check(`hija ${width} raíz seleccionada`, await page.locator('#catalogo-categoria').inputValue() === '3' && await page.getByRole('button', { name: 'Suplementos', exact: true }).getAttribute('aria-pressed') === 'true');
        await page.selectOption('#catalogo-marca', 'Acmé');
        await page.locator('#catalogo-busqueda').fill('Vitamina');
        await page.waitForURL(/termino=Vitamina/); await waitCards(1);
        check(`URL tres filtros ${width}`, new URL(page.url()).searchParams.get('marca') === 'Acmé' && new URL(page.url()).searchParams.get('categoriaId') === '4');
        await page.goto(page.url()); await waitCards(1);
        check(`restaura raíz/hija/marca ${width}`, await page.locator('#catalogo-marca').inputValue() === 'Acmé' && await page.locator('#catalogo-categoria').inputValue() === '3' && await page.getByRole('button', { name: 'Suplementos', exact: true }).getAttribute('aria-pressed') === 'true');
        check(`listado/chips ${width} sin overflow`, await noOverflow());
        await page.screenshot({ path: resolve(artifacts, `listado-${width}.png`), fullPage: true });
        await page.getByRole('button', { name: 'Limpiar', exact: true }).click();
        await page.waitForFunction(() => document.querySelectorAll('[data-store-section]').length === 3);
        check(`Limpiar ${width} regresa portada sin filtros`, !new URL(page.url()).searchParams.size);
        await page.goto(address + '/catalogo/7');
        await page.getByRole('heading', { name: product.nombre, exact: true }).waitFor();
        check(`detalle ${width} sin overflow`, await noOverflow());
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
        // Misma marca DOM con rutas reescritas; sin asumir hosts ni prefijos.
        await page.evaluate(() => history.replaceState({}, '', '/producto/7'));
        await page.evaluate(() => document.getElementById('components-reconnect-modal').classList.replace('components-reconnect-hide', 'components-reconnect-show'));
        await page.locator('#store-reconnect').waitFor({ state: 'visible' });
        check(`reconexión pública ${width} sin modal/foco`, await page.locator('#components-reconnect-modal').evaluate(e => !e.open) && await page.locator('[data-gallery-open]').evaluate(e => document.activeElement === e));
        check(`reconexión pública ${width} pequeña con spinner`, await page.locator('#store-reconnect').evaluate(e => e.getBoundingClientRect().height < 36 && e.getBoundingClientRect().width < 180 && getComputedStyle(e.querySelector('.store-reconnect-spinner')).display !== 'none'));
        check(`reconexión pública ${width} sin contador`, !/Intento|servidor/.test(await page.locator('#store-reconnect').innerText()));
        await page.screenshot({ path: resolve(artifacts, `reconexion-${width}.png`) });
        await page.evaluate(() => document.getElementById('components-reconnect-modal').classList.replace('components-reconnect-show', 'components-reconnect-rejected'));
        await page.getByRole('button', { name: 'Recargar', exact: true }).waitFor();
        check(`reconexión persistente ${width} acciones visibles`, await page.locator('#store-reconnect').isVisible() && await page.getByRole('button', { name: 'Recargar', exact: true }).isVisible());
        await page.evaluate(() => document.getElementById('components-reconnect-modal').classList.replace('components-reconnect-rejected', 'components-reconnect-hide'));
    }
    // Páginas reales del cliente HTTP: retener 16 al fallar y avanzar 16/16/fin.
    await page.goto(rootUrl); await waitCards(16);
    const loaded = await cards().locator('a').evaluateAll(links => links.map(link => link.getAttribute('href')));
    failNextPage = true;
    await page.getByRole('button', { name: 'Cargar más productos', exact: true }).click();
    await page.getByText(/Los resultados anteriores siguen disponibles/).waitFor();
    check('fallo de página conserva las primeras 16', JSON.stringify(await cards().locator('a').evaluateAll(links => links.map(link => link.getAttribute('href')))) === JSON.stringify(loaded));
    await page.getByRole('button', { name: 'Reintentar carga', exact: true }).click(); await waitCards(32);
    check('reintentar agrega segunda página sin duplicados', (new Set(await cards().locator('a').evaluateAll(links => links.map(link => link.href)))).size === 32);
    await page.getByRole('button', { name: 'Cargar más productos', exact: true }).click(); await waitCards(48);
    check('tercera página incrementa de 16 en 16', await cards().count() === 48);
    await page.getByRole('button', { name: 'Cargar más productos', exact: true }).click(); await waitCards(57);
    check('última página y fin fiables', await cards().count() === 57 && !await page.getByRole('button', { name: 'Cargar más productos', exact: true }).count() && await page.getByText('Has visto todos los resultados.', { exact: true }).isVisible());
    check('payloads páginas/carruseles acotados', payloads.filter(p => p.endpoint === '/pagina').every(p => p.size <= 16) && payloads.filter(p => p.endpoint === '/escaparate').every(p => p.size <= 3));
    await page.selectOption('#catalogo-marca', 'Marca QA 01');
    await page.waitForFunction(() => document.querySelectorAll('.store-product-card').length === 0);
    check('cambio de filtro reinicia cursor/resultados', await cards().count() === 0);
    await page.goto(rootUrl); await waitCards(16);
    await page.getByRole('button', { name: 'Más subcategorías', exact: true }).click();
    await page.getByRole('button', { name: 'Subcategoría QA 17', exact: true }).waitFor();
    check('opciones de hijas incrementales', await page.getByRole('button', { name: 'Subcategoría QA 17', exact: true }).count() === 1);
    await page.getByRole('button', { name: 'Más categorías disponibles', exact: true }).click();
    await page.locator('#catalogo-categoria option[value="24"]').waitFor({ state: 'attached' });
    check('opciones de raíces incrementales', await page.locator('#catalogo-categoria option[value="24"]').count() === 1);
    await page.getByRole('button', { name: 'Más marcas disponibles', exact: true }).click();
    await page.locator('#catalogo-marca option[value="Marca QA 17"]').waitFor({ state: 'attached' });
    check('opciones de marcas incrementales', await page.locator('#catalogo-marca option[value="Marca QA 17"]').count() === 1);
    // Navegación de historial reconstruye una hija compartida y su raíz.
    await page.goto(address + '/catalogo?categoriaId=4&marca=Acm%C3%A9'); await waitCards(14);
    await page.goto(rootUrl); await waitCards(16);
    await page.goBack(); await waitCards(14);
    check('atrás reconstruye contexto de hija', await page.locator('#catalogo-categoria').inputValue() === '3' && await page.getByRole('button', { name: 'Suplementos', exact: true }).getAttribute('aria-pressed') === 'true');
    await page.goForward(); await waitCards(16);
    check('adelante restaura raíz y página inicial', await page.getByRole('button', { name: 'Todos', exact: true }).getAttribute('aria-pressed') === 'true');
    // La búsqueda llega a artículos no incluidos en el carrusel inicial.
    await page.goto(address + '/catalogo'); await page.locator('[data-store-section]').first().waitFor();
    await page.locator('#catalogo-busqueda').fill('subcategoría 17'); await waitCards(1);
    check('búsqueda global fuera de carruseles iniciales', await cards().first().innerText().then(text => text.includes('subcategoría 17')));
    slowStarted = new Promise(resolve => { signalSlow = resolve; });
    await page.locator('#catalogo-busqueda').fill('Lento'); await slowStarted;
    await page.locator('#catalogo-busqueda').fill('Nuevo');
    await page.getByRole('heading', { name: 'Resultado nuevo', exact: true }).waitFor();
    await page.waitForTimeout(1100);
    check('respuesta obsoleta nunca mezcla resultados', await cards().count() === 1 && !await page.getByRole('heading', { name: 'Resultado obsoleto', exact: true }).count());
    await page.locator('#catalogo-busqueda').fill('sin coincidencia sintética');
    await page.getByRole('heading', { name: 'No encontramos productos con estos filtros', exact: true }).waitFor();
    check('búsqueda sin resultados conserva recuperación', await cards().count() === 0);
    // Otro contexto conserva IntersectionObserver nativo: scroll real activa una página acotada.
    const scrollingContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const scrolling = await scrollingContext.newPage();
    scrolling.on('pageerror', error => errors.push(error.message));
    await scrolling.route('**/api/catalogo/productos**', publicRoute);
    await scrolling.goto(rootUrl);
    await scrolling.waitForFunction(() => document.querySelectorAll('.store-product-card').length === 16);
    await scrolling.locator('[data-store-sentinel]').scrollIntoViewIfNeeded();
    await scrolling.waitForFunction(() => document.querySelectorAll('.store-product-card').length === 32);
    check('IntersectionObserver real carga siguiente bloque', await scrolling.locator('.store-product-card').count() === 32);
    await scrolling.screenshot({ path: resolve(artifacts, 'scroll-real-390.png'), fullPage: true });
    await scrolling.goto(address + '/catalogo');
    await scrolling.waitForFunction(() => document.querySelectorAll('[data-store-section]').length === 3);
    await scrolling.locator('[data-store-sentinel]').scrollIntoViewIfNeeded();
    await scrolling.waitForFunction(() => document.querySelectorAll('[data-store-section]').length === 6);
    check('IntersectionObserver real difiere grupos de categorías', await scrolling.locator('[data-store-section]').count() === 6);
    await scrollingContext.close();
    await page.goto(address + '/catalogo/7'); await page.locator('[data-gallery-open]').waitFor();
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
    // Reproducir el defecto de layout conservando el estado, sin invocar showModal artificialmente.
    await page.evaluate(() => document.querySelector('main').setAttribute('data-public-catalog', 'true'));
    await page.waitForFunction(() => !document.getElementById('components-reconnect-modal').open);
    check('layout público sobre estado existente cierra modal y muestra aviso', await page.locator('#store-reconnect').isVisible());
    await page.evaluate(() => document.querySelector('main').removeAttribute('data-public-catalog'));
    await page.waitForFunction(() => document.getElementById('components-reconnect-modal').open);
    check('retorno al layout administrativo restaura modal', await page.locator('#store-reconnect').isHidden());
    await page.evaluate(() => document.getElementById('components-reconnect-modal').classList.replace('components-reconnect-show', 'components-reconnect-hide'));
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
    await page.goto(address + '/categorias/nueva');
    await page.waitForLoadState('networkidle');
    await page.locator('#categoria-nombre').fill('Ropa QA');
    await page.getByRole('button', { name: 'Guardar categoría' }).click();
    await page.waitForURL(/\/categorias\?/);
    await page.goto(address + '/productos/nuevo');
    await page.waitForLoadState('networkidle');
    await page.locator('#producto-nombre').fill('Producto sintético QA');
    await page.selectOption('#producto-categoria', '1');
    await page.selectOption('#producto-subcategoria', '2');
    await page.locator('#producto-precio-sugerido').fill('49.95');
    const photoProgress = [];
    await page.exposeFunction('capturarProgresoQA', value => photoProgress.push(value));
    await page.evaluate(() => {
        new MutationObserver(() => {
            for (const status of document.querySelectorAll('[role="status"][aria-live="polite"]')) {
                const text = status.innerText.trim();
                if (/^(Preparando foto|Subiendo foto|Guardando fotografías)/.test(text)) window.capturarProgresoQA(text);
            }
        }).observe(document.body, { childList: true, characterData: true, subtree: true });
    });
    // PNG válido con contenido determinista para que la transferencia real no sea instantánea.
    const uploadPng = await page.evaluate(() => {
        const canvas = document.createElement('canvas'); canvas.width = 600; canvas.height = 800;
        const ctx = canvas.getContext('2d'), image = ctx.createImageData(600, 800);
        let seed = 14;
        for (let i = 0; i < image.data.length; i += 4) {
            for (let c = 0; c < 3; c++) { seed = (1664525 * seed + 1013904223) >>> 0; image.data[i + c] = seed >>> 24; }
            image.data[i + 3] = 255;
        }
        ctx.putImageData(image, 0, 0);
        return canvas.toDataURL('image/png').split(',')[1];
    });
    const file = (i, large = false) => ({ name: `foto-${i}.png`, mimeType: 'image/png', buffer: Buffer.from(large ? uploadPng : png, 'base64') });
    const galleryRows = () => page.locator('ol[aria-label="Fotografías ordenadas"] > li');
    const waitGallery = count => page.waitForFunction(expected => document.querySelectorAll('ol[aria-label="Fotografías ordenadas"] > li').length === expected, count);
    await page.locator('#producto-imagen').setInputFiles(Array.from({ length: 5 }, (_, i) => file(i + 1, true)));
    await waitGallery(5);
    check('preparación cinco fotos muestra pasos reales en DOM', [1, 2, 3, 4, 5].every(step => photoProgress.some(text => new RegExp(`(?:Preparando|Subiendo) foto ${step} de 5`).test(text))));
    check('preparación distingue transferencia SignalR', photoProgress.some(text => /^Subiendo foto \d de 5/.test(text)));
    await page.getByRole('button', { name: 'Usar como portada', exact: true }).first().click();
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click();
    await page.waitForURL(/\/productos\?/);
    const saved = await (await context.request.get(address + '/productos/1/imagenes')).json();
    check('alta real cinco fotografías y portada independiente del orden', saved.length === 5 && saved[1].esPortada);
    check('guardado atómico muestra texto honesto sin contador', photoProgress.some(text => text === 'Guardando fotografías…') && photoProgress.every(text => !/^Guardando foto \d/.test(text)));
    for (const width of [320, 390, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(address + '/productos/1/editar'); await page.waitForLoadState('networkidle');
        await page.getByRole('button', { name: 'Portada seleccionada' }).waitFor();
        check(`admin fotografías ${width} sin overflow`, await noOverflow());
        check(`admin galería persistida ${width}`, await galleryRows().count() === 5);
        check(`admin hija ${width} raíz/hija preseleccionadas`, await page.locator('#producto-categoria').inputValue() === '1' && await page.locator('#producto-subcategoria').inputValue() === '2');
        check(`admin categoría principal ${width} excluye hija`, !await page.locator('#producto-categoria option[value="2"]').count());
        await page.screenshot({ path: resolve(artifacts, `admin-producto-${width}.png`), fullPage: true });
        await page.selectOption('#producto-categoria', '3');
        await page.waitForFunction(() => document.getElementById('producto-subcategoria').value === '' && document.getElementById('producto-subcategoria').disabled);
        check(`admin cambiar raíz ${width} limpia hija`, await page.locator('#producto-subcategoria').inputValue() === '' && await page.locator('#producto-subcategoria').isDisabled());
        await page.goto(address + '/categorias/1/editar');
        check(`admin raíz con hijas ${width} restringida`, await page.locator('#categoria-padre').isDisabled());
        check(`admin categoría ${width} sin overflow`, await noOverflow());
        await page.screenshot({ path: resolve(artifacts, `admin-categoria-${width}.png`), fullPage: true });
    }
    await page.goto(address + '/productos/1/editar'); await page.waitForLoadState('networkidle');
    await page.locator('#producto-imagen').setInputFiles(Array.from({ length: 4 }, (_, i) => file(i + 6)));
    await page.getByText('Un producto admite hasta 8 fotografías en total. No se añadió ninguna de esta selección.', { exact: true }).waitFor();
    check('novena foto cancela lote entero preservando cinco', await galleryRows().count() === 5);
    await page.locator('#producto-imagen').setInputFiles([{ name: 'invalida.png', mimeType: 'image/png', buffer: Buffer.from('archivo inválido de QA') }]);
    await page.getByText('Selecciona imágenes JPEG, PNG o WebP válidas. No se añadió ninguna de esta selección.', { exact: true }).waitFor();
    check('archivo inválido preserva la galería previa', await galleryRows().count() === 5);
    await page.locator('#producto-imagen').setInputFiles([file(6), file(7), file(8)]); await waitGallery(8);
    check('ocho fotos bloquean nuevas selecciones', await page.locator('#producto-imagen').isDisabled());
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click(); await page.waitForURL(/\/productos\?/);
    check('ocho fotos guardadas en servicio real', (await (await context.request.get(address + '/productos/1/imagenes')).json()).length === 8);
    await page.goto(address + '/productos/1/editar'); await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Mover fotografía 2 antes' }).click();
    await page.waitForFunction(() => document.querySelector('ol[aria-label="Fotografías ordenadas"] > li p')?.textContent.includes('Portada'));
    for (let remaining = 8; remaining > 1; remaining--) {
        await page.getByRole('button', { name: 'Eliminar fotografía 2' }).click(); await waitGallery(remaining - 1);
    }
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click(); await page.waitForURL(/\/productos\?/);
    const edited = await (await context.request.get(address + '/productos/1/imagenes')).json();
    check('edición real reordena/elimina y conserva portada', edited.length === 1 && edited[0].esPortada && edited[0].id === saved[1].id);
    await page.goto(address + '/productos/nuevo'); await page.waitForLoadState('networkidle');
    await page.locator('#producto-nombre').fill('Producto QA sin fotografía');
    await page.selectOption('#producto-categoria', '1'); await page.locator('#producto-precio-sugerido').fill('10');
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click(); await page.waitForURL(/\/productos\?/);
    check('alta raíz sin hija y sin fotografías', (await (await context.request.get(address + '/productos/2/imagenes')).json()).length === 0);
    await page.goto(address + '/productos/2/editar'); await page.waitForLoadState('networkidle');
    check('edición de raíz deja hija opcional vacía', await page.locator('#producto-categoria').inputValue() === '1' && await page.locator('#producto-subcategoria').inputValue() === '');
    await page.locator('#producto-imagen').setInputFiles([file(1)]); await waitGallery(1);
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click(); await page.waitForURL(/\/productos\?/);
    check('una fotografía sigue siendo válida', (await (await context.request.get(address + '/productos/2/imagenes')).json()).length === 1);
    // El formulario reutilizado de Compra persiste hija y foto sin registrar una compra ficticia.
    await page.goto(address + '/compras/nueva'); await page.waitForLoadState('networkidle');
    await page.locator('#producto-compra-1').fill('inexistente-para-alta-qa');
    await page.locator('.rm-product-picker').getByRole('button', { name: 'Agregar producto', exact: true }).click();
    await page.locator('#producto-nombre').fill('Producto QA desde Compra');
    await page.selectOption('#producto-categoria', '1'); await page.selectOption('#producto-subcategoria', '2');
    await page.locator('#producto-precio-sugerido').fill('12');
    await page.locator('#producto-imagen').setInputFiles([file(1)]); await waitGallery(1);
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click();
    await page.getByText('Producto Producto QA desde Compra registrado y seleccionado. Continúa con cantidad y costo unitario.', { exact: true }).waitFor();
    check('alta desde Compra conserva contexto sin salir', new URL(page.url()).pathname === '/compras/nueva' && await page.locator('#producto-compra-1').inputValue() === 'Producto QA desde Compra');
    check('alta desde Compra persiste fotografía', (await (await context.request.get(address + '/productos/3/imagenes')).json()).length === 1);
    await page.goto(address + '/productos/3/editar'); await page.waitForLoadState('networkidle');
    check('alta desde Compra persiste hija real', await page.locator('#producto-categoria').inputValue() === '1' && await page.locator('#producto-subcategoria').inputValue() === '2');
    // Firma PNG válida, cuerpo indecodificable: falla el guardado real y conserva selección.
    await page.goto(address + '/productos/nuevo'); await page.waitForLoadState('networkidle');
    await page.locator('#producto-nombre').fill('Producto QA reintento');
    await page.selectOption('#producto-categoria', '1'); await page.locator('#producto-precio-sugerido').fill('7');
    await page.locator('#producto-imagen').setInputFiles([{ name: 'png-indecodificable.png', mimeType: 'image/png', buffer: Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3, 4]) }]); await waitGallery(1);
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click();
    await page.locator('.mensaje-error[role="alert"]').first().waitFor();
    check('guardado fallido conserva foto preparada y formulario', new URL(page.url()).pathname === '/productos/nuevo' && await galleryRows().count() === 1);
    await page.getByRole('button', { name: 'Eliminar fotografía 1' }).click(); await waitGallery(0);
    await page.locator('#producto-imagen').setInputFiles([file(1)]); await waitGallery(1);
    await page.getByRole('button', { name: 'Guardar producto', exact: true }).click(); await page.waitForURL(/\/productos\?/);
    check('reintento tras guardar fallido no duplica fotografías', (await (await context.request.get(address + '/productos/4/imagenes')).json()).length === 1);
    await writeFile(resolve(artifacts, 'photo-progress.json'), JSON.stringify(photoProgress, null, 2));
    assert.deepEqual(errors, []);
    await writeFile(resolve(artifacts, 'report.json'), JSON.stringify({ checks, errors, requests, payloads }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, artifacts }, null, 2));
} catch (error) {
    await page?.screenshot({ path: resolve(artifacts, 'failure.png'), fullPage: true });
    const layout = await page?.evaluate(() => ({ viewport: innerWidth, document: document.documentElement.scrollWidth,
        elements: [...document.querySelectorAll('body *')].map(element => {
            const rect = element.getBoundingClientRect(), style = getComputedStyle(element);
            return { tag: element.tagName, class: element.className?.toString(), left: rect.left, right: rect.right,
                width: rect.width, scroll: element.scrollWidth, client: element.clientWidth, overflow: style.overflowX,
                display: style.display, minWidth: style.minWidth, columns: style.gridAutoColumns };
        }).filter(element => element.right > innerWidth + 1 || element.scroll > element.client + 1) }));
    await writeFile(resolve(artifacts, 'failure.json'), JSON.stringify({ checks, errors, message: error.message, layout }, null, 2));
    throw error;
} finally {
    await browser?.close(); app.kill();
    await writeFile(resolve(artifacts, 'app.log'), output);
}
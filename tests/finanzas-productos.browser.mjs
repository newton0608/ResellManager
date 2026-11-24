// Circuito Blazor real, cuentas y SQLite exclusivamente sintéticos en .artifacts.
import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
const modulePath=process.env.SCANNER_PLAYWRIGHT_MODULE;
const {chromium}=await import(modulePath?pathToFileURL(modulePath).href:'playwright');
const {expect}=await import(modulePath?new URL('test.mjs',pathToFileURL(modulePath)).href:'playwright/test');
const root=fileURLToPath(new URL('../',import.meta.url));
const artifacts=resolve(root,'.artifacts/finanzas-productos',String(Date.now()));await mkdir(artifacts,{recursive:true});
const reservation=createServer();await new Promise(r=>reservation.listen(0,'127.0.0.1',r));const port=reservation.address().port;await new Promise(r=>reservation.close(r));
const address=`http://127.0.0.1:${port}`,db=resolve(artifacts,'qa.db');
let app,browser,page,log='';const checks=[],errors=[];
async function start(admin){
 app=spawn('dotnet',[resolve(root,'src/ResellManager.Web/bin/Debug/net10.0/ResellManager.Web.dll')],{
  cwd:resolve(root,'src/ResellManager.Web'),env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:address,
   ConnectionStrings__ResellManager:`Data Source=${db}`,AlmacenamientoImagenesProducto__DirectorioBase:resolve(artifacts,'productos'),
   AlmacenamientoComprobantes__DirectorioBase:resolve(artifacts,'comprobantes'),DataProtection__KeysPath:resolve(artifacts,'keys'),
   UsuarioInicial__Correo:'finanzas-qa@example.test',UsuarioInicial__Contrasena:'QaOnly!Finanzas22',
   ...(admin?{SeguridadOperaciones__AdministradoresIds__0:admin}:{}),ProductoLookup__OpenFactsHabilitado:'false',ProductoLookup__UpcitemdbHabilitado:'false'}});
 app.stdout.on('data',d=>log+=d);app.stderr.on('data',d=>log+=d);
 for(let i=0;i<150;i++){if(app.exitCode!==null)throw Error(log);try{if((await fetch(address+'/health')).ok)return;}catch{}await new Promise(r=>setTimeout(r,200));}throw Error('No arrancó: '+log);
}
async function stop(){if(app&&app.exitCode===null){app.kill();await new Promise(r=>app.once('exit',r));}}
async function open(path){await page.goto(address+path);await page.waitForLoadState('networkidle');}
async function fill(id,value){await page.locator('#'+id).fill(value);await page.locator('#'+id).blur();}
async function field(label,value,scope=page){await scope.getByLabel(label,{exact:true}).fill(value);await scope.getByLabel(label,{exact:true}).blur();}
async function views(name){for(const width of [390,1440]){await page.setViewportSize({width,height:1000});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`${name}: overflow ${width}`);await page.screenshot({path:resolve(artifacts,`${name}-${width}.png`),fullPage:true});}await page.setViewportSize({width:390,height:1000});}
function passed(name){checks.push(name);console.log(name);}
async function scan(cantidad){
 await page.getByRole('button',{name:'Escanear código de barras con la cámara'}).first().click();
 const dialog=page.locator('dialog.rm-barcode-scanner[open]');await expect(dialog).toBeVisible();
 await dialog.locator('[data-barcode-file]').setInputFiles(resolve(root,'tests/fixtures/barcodes/ean.jpg'));
 await expect(dialog.locator('[data-barcode-code]')).toHaveText('3574660239843',{timeout:30000});await dialog.getByRole('button',{name:'Usar código',exact:true}).click();
 await fill('directa-cantidad-scanner',String(cantidad));await page.getByRole('button',{name:'Agregar unidades',exact:true}).click();
}
try{
 await start();
 // Solo lee el ID de la cuenta creada en esta DB de QA; no modifica Identity mediante SQL.
 const admin=execFileSync('python3',['-c','import sqlite3,sys; print(sqlite3.connect(sys.argv[1]).execute("SELECT Id FROM AspNetUsers").fetchone()[0])',db],{encoding:'utf8'}).trim();
 await stop();await start(admin);
 browser=await chromium.launch({headless:true,channel:process.env.SCANNER_BROWSER_CHANNEL||'chromium'});
 const context=await browser.newContext({viewport:{width:390,height:1000}});page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 await open('/login');await fill('correo','finanzas-qa@example.test');await fill('contrasena','QaOnly!Finanzas22');await page.getByRole('button',{name:'Entrar',exact:true}).click();await expect(page).not.toHaveURL(/\/login/);
 await open('/categorias/nueva');await fill('categoria-nombre','Categoría financiera QA');await page.getByRole('button',{name:'Guardar categoría',exact:true}).click();await expect(page).toHaveURL(/\/categorias\?/);
 await open('/proveedores/nuevo');await fill('proveedor-nombre','Proveedor financiero QA');await page.getByRole('button',{name:'Guardar proveedor',exact:true}).click();await expect(page).toHaveURL(/\/proveedores(?:\?|$)/);
 await open('/clientes/nuevo');await fill('cliente-nombres','Ana QA');await fill('cliente-telefono','55551234');await page.getByRole('button',{name:'Guardar cliente',exact:true}).click();await expect(page).toHaveURL(/\/clientes\?/);
 const png=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=300;c.height=400;const x=c.getContext('2d');x.fillStyle='#4e6476';x.fillRect(0,0,300,400);return c.toDataURL('image/png').split(',')[1];});
 await open('/productos/nuevo');await fill('producto-nombre','Producto financiero QA');await fill('producto-codigo-barras','3574660239843');await fill('producto-precio-sugerido','0');await page.locator('#producto-categoria').selectOption({label:'Categoría financiera QA'});
 await page.locator('#producto-imagen').setInputFiles({name:'producto.png',mimeType:'image/png',buffer:Buffer.from(png,'base64')});await page.getByText('1 de 8 fotografías preparadas',{exact:true}).waitFor();await page.getByRole('button',{name:'Guardar producto',exact:true}).click();await expect(page).toHaveURL(/\/productos\?/);
 await page.getByLabel('Sin precio asignado',{exact:true}).check();await page.getByRole('button',{name:'Buscar',exact:true}).click();
 await page.getByRole('button',{name:'Asignar precio',exact:true}).click();await expect(page.locator('dialog[open] img')).toBeVisible();await field('Precio de venta Q','100',page.locator('dialog[open]'));
 await views('precio-movil');await page.getByRole('button',{name:'Guardar precio',exact:true}).click();await expect(page.locator('dialog[open]')).toHaveCount(0);await expect(page.getByRole('button',{name:'Asignar precio',exact:true})).toHaveCount(0);await expect(page.getByLabel('Sin precio asignado',{exact:true})).toBeChecked();passed('Filtro backend y diálogo de precio conservan contexto y retiran el producto valorado');
 await open('/compras/nueva');await page.locator('#compra-origen').selectOption('CompraLocal');await fill('compra-proveedor','Proveedor financiero QA');await page.getByRole('button',{name:'Proveedor financiero QA',exact:true}).click();
 await fill('producto-compra-1','Producto financiero QA');await page.getByRole('button',{name:/Producto financiero QA/}).click();await fill('cantidad-compra-1','5');await page.getByLabel('Costo conocido',{exact:true}).uncheck();await page.getByRole('button',{name:'Revisar compra',exact:true}).click();await page.getByRole('button',{name:'Confirmar compra',exact:true}).click();await expect(page).toHaveURL(/\/compras\/\d+/);
 await open('/clientes/apertura');await field('Cliente ID existente (opcional)','1');await field('Nombre','Ana QA');await field('Teléfono obligatorio','55551234');await field('Deuda Q','1250');await page.getByRole('button',{name:'Agregar a vista previa',exact:true}).click();await expect(page.getByText('Total de cartera: Q 1,250.00')).toBeVisible();await views('apertura-previa');
 await page.getByLabel('He revisado la cartera completa.',{exact:false}).check();await page.getByRole('button',{name:'Confirmar apertura global',exact:true}).click();await expect(page.getByRole('heading',{name:'Apertura confirmada y cerrada'})).toBeVisible();
 for(const ext of ['xlsx','pdf']){const r=await context.request.get(address+'/informes/apertura/'+ext);assert.equal(r.status(),200);assert.ok((await r.body()).length>100);}
 passed('Apertura global con corte automático y exportaciones reales XLSX/PDF');
 await open('/movimientos?cliente=1');await expect(page.getByText('Saldo actual global: Q 1,250.00')).toBeVisible();await views('movimientos');
 const ajuste=page.locator('section').filter({has:page.getByRole('heading',{name:'Crear ajuste',exact:true})});await field('Importe con signo en Q','-50',ajuste);await field('Motivo obligatorio','Corrección sintética',ajuste);await ajuste.getByRole('button',{name:'Revisar ajuste',exact:true}).click();await ajuste.getByRole('button',{name:'Confirmar operación',exact:true}).click();await expect(page.getByText('Saldo actual global: Q 1,200.00')).toBeVisible();passed('Ajuste revisado y confirmado en backend mantiene el saldo global');
 await open('/ventas/nueva?modo=directa');await expect(page.getByRole('radio',{name:'Consumidor Final (CF)',exact:true})).toBeChecked();await scan(2);await field('Nombre opcional','Comprador ocasional');
 await field('Importe aplicado Q','200');await field('Importe entregado Q','250');await page.getByRole('button',{name:'Revisar venta',exact:true}).click();await expect(page.locator('dialog[open]')).toContainText('Cambio: Q 50.00');await views('cf-revision');await page.getByRole('button',{name:'Confirmar venta',exact:true}).click();await expect(page).toHaveURL(/\/ventas\/\d+$/);await expect(page.getByRole('heading',{name:'Consumidor Final',exact:true})).toBeVisible();
 await field('Motivo de solicitud','Devolución completa sintética');await page.getByRole('button',{name:'Solicitar anulación completa',exact:true}).click();await field('Motivo de decisión','Aceptada por administrador');await page.getByRole('button',{name:'Aprobar solicitud',exact:true}).click();
 await field('Importe Q','200');await page.getByLabel('Método de devolución',{exact:true}).selectOption('Transferencia');await field('Referencia o constancia','QA devolución real simulada');await page.getByRole('button',{name:'Confirmar devolución efectuada',exact:true}).click();
 const unidades=await page.getByLabel('Unidad',{exact:true}).locator('option').evaluateAll(xs=>xs.map(x=>x.value).filter(x=>x!=='0'));assert.equal(unidades.length,2);
 for(let i=0;i<unidades.length;i++){await page.getByLabel('Unidad',{exact:true}).selectOption(unidades[i]);await page.getByLabel('Apta para reventa',{exact:true}).setChecked(i===0);await field('Condición y observación',i===0?'Apta, recibida':'Dañada, no apta');await page.getByRole('button',{name:'Confirmar artículo recibido',exact:true}).click();await expect(page.getByText(`Artículos recibidos ${i+1} de 2`,{exact:false})).toBeVisible();}
 await views('cf-anulacion');await page.getByRole('button',{name:'Finalizar anulación completa',exact:true}).click();await expect(page.getByRole('heading',{name:/Completada/})).toBeVisible();passed('CF con cambio y anulación aprobada, reembolso por otro método y recepción apta/no apta');
 await open('/costos?unidad='+unidades[0]);await field('Costo real Q','25');await field('Motivo','Costo real comprobado sintético');await page.getByLabel('Confirmo el costo real.',{exact:false}).check();await page.getByRole('button',{name:'Registrar nota de corrección',exact:true}).click();await expect(page.getByText(/Costo real comprobado sintético/)).toBeVisible();
 await page.getByRole('button',{name:'Consultar informe',exact:true}).click();await expect(page.getByText('Utilidad calculable:',{exact:false})).toBeVisible();await views('costos-informe');passed('Nota de costo e informe revisado con corte explícito');
 await open('/productos/etiquetas');await fill('producto-etiqueta','Producto financiero QA');await page.getByRole('button',{name:/Producto financiero QA/}).click();await field('Copias','2');await page.getByRole('button',{name:'Agregar al lote',exact:true}).click();await field('Ancho mm','120');await field('Alto mm','40');await page.getByRole('button',{name:'Preparar vista previa',exact:true}).click();await page.getByRole('button',{name:'Verificar perfil y PDF a tamaño real',exact:true}).click();await expect(page.getByRole('link',{name:'Abrir PDF y hoja de prueba'})).toBeVisible();await views('etiquetas');passed('Etiquetas con código completo, perfil validado y sin presumir impresión física');
 const prod=execFileSync('python3',['-c','import sqlite3,sys; print(sqlite3.connect(sys.argv[1]).execute("SELECT Id FROM Productos").fetchone()[0])',db],{encoding:'utf8'}).trim();
 await open('/catalogo/'+prod);await expect(page.getByRole('heading',{name:'Producto financiero QA',exact:true})).toBeVisible();await views('catalogo-publico');
 await open('/movimientos?cliente=1');await expect(page.getByText('Saldo actual global: Q 1,200.00')).toBeVisible();assert.deepEqual(errors,[]);
 await writeFile(resolve(artifacts,'report.json'),JSON.stringify({browser:await browser.version(),liveCircuit:true,syntheticDatabase:true,checks,errors},null,2));console.log('Informe: '+artifacts);
}catch(e){if(page&&!page.isClosed()){await page.screenshot({path:resolve(artifacts,'failure.png'),fullPage:true}).catch(()=>{});await writeFile(resolve(artifacts,'failure.html'),await page.content());}throw e;}
finally{await browser?.close();await stop();await writeFile(resolve(artifacts,'app.log'),log);}

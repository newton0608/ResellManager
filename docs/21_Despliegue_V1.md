# Despliegue V1 — Runbook de preparación

## Estado y límites

Este documento prepara el go-live; no afirma que exista producción ni una release/tag 1.0.0. La referencia funcional es [Cierre V1, sección 19](18_Fase510_CierreV1.md#19-cierre-operativo-reservas-recepción-y-actividad-del-cliente-13092026). La verificación técnica allí registrada (405 tests .NET, 17 JS, build sin errores/warnings) no sustituye pruebas en el VPS ni QA visual real.

Objetivo: VPS con Ubuntu LTS soportado, Docker y Docker Compose, Caddy como reverse proxy y una instancia de ResellManager con SQLite persistente. El repositorio incluye Dockerfile, compose.yml, Caddyfile y ajustes de hosting. El despliegue real, bootstrap, QA detrás de Caddy, backup y prueba REAL de restauración siguen pendientes. No se agregan migraciones ni cambios de esquema.

## Arquitectura objetivo

```text
Internet → HTTPS → Caddy → ResellManager ASP.NET Core / Blazor InteractiveServer
                                           └→ SQLite persistente
```

- Solo Caddy publica el servicio web hacia Internet; el puerto de la aplicación queda en una red privada de Compose, sin publicación directa en el host. Restringir aparte el acceso administrativo al VPS y verificar firewall efectivo, también para puertos publicados por Docker.
- Resolver dominio y DNS antes de emitir/verificar certificados. El Caddyfile usa app.resellmanager.tech; configurar sus registros DNS hacia el VPS antes del arranque. No registrar credenciales en este documento.
- Configurar HTTPS, renovación y almacenamiento persistente de certificados/estado de Caddy. Validar redirección HTTP a HTTPS y el host permitido. Caddy admite proxy de WebSockets; comprobar la conexión interactiva de Blazor y su reconexión detrás del proxy. Ver [reverse proxy de Caddy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy).
- V1 no promete operación multiinstancia ni concurrencia fuerte multiusuario. No desplegar réplicas escribiendo sobre la misma SQLite como supuesto escalamiento automático.

## Hosting implementado y validación operativa pendiente

El arranque actual aplica las migraciones existentes antes del bootstrap de usuario, usa redirección HTTPS y protege las rutas de negocio/comprobantes con autenticación. Eso no configura por sí solo la frontera HTTPS de un reverse proxy.

Program.cs registra Forwarded Headers antes de HSTS, redirección y autenticación. DataProtection:KeysPath configura PersistKeysToFileSystem con ApplicationName estable ResellManager; sin esa ruta se conserva el comportamiento local de desarrollo. Las rutas productivas se proporcionan por entorno, no en appsettings.json.

### Forwarded Headers y proxy confiable

Configurar el procesamiento de `X-Forwarded-Proto` y `X-Forwarded-For` antes de redirección HTTPS y autenticación. Definir explícitamente el proxy o red de confianza de Caddy según la topología real de Compose y un límite acorde a los saltos reales; restringir hosts aceptados. No aceptar cabeceras de cualquier origen ni vaciar listas de confianza como atajo.

Probar que ASP.NET Core reconoce el esquema HTTPS externo, que login/logout y antiforgery funcionan, que las cookies de sesión recibidas por HTTPS son seguras y que no hay bucles de redirección. La cookie usa CookieSecurePolicy.Always en Production y SameAsRequest en Development; conserva HttpOnly, SameSite=Lax, expiración de ocho horas, sliding expiration y lockout. Incluir pruebas de cabeceras falsificadas desde fuentes no confiables. Consultar [configuración de proxies ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0) y [restricción de proxies desconocidos en .NET 8](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/8.0/forwarded-headers-unknown-proxies).

## Persistencia, permisos y configuración

Antes de arrancar con datos reales, definir y comprobar estos montajes persistentes:

| Recurso | Requisito |
| --- | --- |
| SQLite | Directorio persistente privado y ruta absoluta en `ConnectionStrings__ResellManager`. Montar el directorio, no solo el archivo, para permitir archivos auxiliares de SQLite. No dejar la BD en la capa efímera del contenedor. |
| Comprobantes | Directorio privado persistente mediante `AlmacenamientoComprobantes__DirectorioBase`, fuera de `wwwroot`. Conservar estructura y rutas relativas registradas en la BD; nunca servir este volumen como archivos estáticos desde Caddy. |
| Data Protection | Configurar y verificar el key ring en un volumen persistente, con identidad de aplicación estable entre recreaciones. No depender de un directorio efímero ni eliminar claves antiguas. Evaluar protección en reposo; conservar también los medios necesarios para descifrarlas durante restauración. |
| Caddy | Conservar su estado/certificados en almacenamiento persistente y restringido, con renovación comprobada. |

La persistencia de Data Protection permite conservar material criptográfico entre recreaciones; no conserva circuitos Blazor ni borradores no guardados. Ver [Data Protection en contenedores](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview#persisting-keys-when-hosting-in-a-docker-container).

- Ejecutar la aplicación con usuario no privilegiado y permisos mínimos: escritura solo en sus directorios de datos necesarios. Verificar propietario y permisos de volúmenes; no usar permisos abiertos a todos como solución.
- Configurar entorno Production, rutas, host y secretos fuera del repositorio y de la imagen. Si se usan archivos de entorno, mantenerlos fuera de Git y restringidos. Variables de entorno no son una bóveda; limitar quién puede inspeccionar contenedores. No asumir soporte automático de convenciones de secrets que la app no lea.
- Mantener secretos reales en un gestor/almacenamiento operativo protegido; este documento solo contiene nombres de claves. No imprimir contraseñas, cookies, tokens ni valores completos de configuración en logs o tickets.
- Revisar zona horaria del host/contenedor y fechas por defecto con la usuaria, sin cambiar los DateOnly del dominio.

## Bootstrap de acceso

1. Preparar una base nueva y persistente, o una restauración verificada; comprobar la ruta exacta antes del arranque.
2. Solo si hace falta la primera cuenta, configurar temporalmente `UsuarioInicial__Correo` y `UsuarioInicial__Contrasena` desde el mecanismo privado de secretos. Cumplir la política vigente de Identity documentada en la decisión 016.
3. Arrancar una única instancia, revisar migraciones/errores y verificar acceso con esa cuenta.
4. Retirar ambas credenciales de bootstrap del entorno y recrear el contenedor sin ellas. Verificar que el acceso sigue funcionando: la cuenta permanece en SQLite. El seed es idempotente y no cambia la contraseña de una cuenta existente; no sirve como recuperación de contraseña.
5. Mantener un procedimiento administrativo seguro de recuperación de acceso. No habilitar autorregistro ni dejar credenciales de bootstrap como mecanismo permanente.

## Respaldo y restauración: requisito de salida

Definir responsable, frecuencia, retención, pérdida máxima de datos aceptable (RPO) y tiempo de recuperación objetivo (RTO) antes de abrir el servicio. Guardar copias cifradas fuera del VPS; probar acceso a ellas y alertar ante fallos. Proteger credenciales y claves de descifrado por separado.

El conjunto mínimo respaldado es **SQLite + comprobantes + Data Protection**, acompañado de versión/commit de la imagen, fecha, inventario de archivos y configuración no secreta necesaria para reproducir el entorno. Mantener recuperación protegida de los secretos operativos y del estado de Caddy.

Procedimiento previsto para una copia consistente de V1:

1. Abrir una ventana de mantenimiento, impedir nuevas operaciones y detener ordenadamente la aplicación; verificar que no quedan escritores ni cargas de comprobantes en curso.
2. Obtener una copia SQLite consistente con herramientas soportadas. No copiar solo el archivo .db de una base activa ni omitir su WAL: una copia incompleta puede perder datos. La [API de backup de SQLite](https://www.sqlite.org/backup.html) es una alternativa para la BD; por sí sola no sincroniza los archivos de comprobantes.
3. Respaldar comprobantes y key ring del mismo punto operativo. Conservar permisos, rutas relativas y material de descifrado aplicable; comprobar integridad del paquete.
4. Reiniciar y verificar el servicio. No borrar el respaldo anterior hasta validar el nuevo según la retención acordada.

**Prueba REAL de restauración, no solo de creación de backup:** restaurar el conjunto en un entorno aislado sin sobrescribir el original; usar la imagen compatible, rutas y permisos correctos. Verificar integridad SQLite y relaciones, login, saldos, pedidos/reservas, lectura de comprobantes y funcionamiento de Data Protection. Si existen claves protegidas en reposo, comprobar su descifrado. Ensayar recuperación tras recreación del contenedor. Registrar fecha, copia usada, tiempos, responsable y resultado sin incluir secretos. Un backup no restaurado con éxito no satisface el go-live.

## Actualización y rollback

1. Identificar imagen/commit exactos actual y candidato; conservar la imagen anterior. Auditar dependencias y ejecutar verificaciones técnicas/funcionales en staging. No basar recuperación en una etiqueta mutable sin registrar su digest.
2. Revisar migraciones incluidas en la versión candidata, compatibilidad de datos y espacio libre. El host aplica migraciones al arrancar: no iniciar una imagen desconocida contra la BD real como prueba.
3. Realizar y comprobar el respaldo consistente previo, anunciar mantenimiento y detener la app. Actualizar la imagen/configuración conservando los volúmenes; no eliminar volúmenes ni inicializar una BD vacía sobre datos existentes.
4. Iniciar una instancia, revisar arranque y ejecutar las comprobaciones posteriores antes de reabrir operaciones.
5. Si falla: detener escrituras y guardar evidencia/copia del estado fallido. Volver a la imagen anterior solo si es compatible con el esquema/datos actuales. Si no lo es, restaurar el conjunto previo en una ubicación controlada junto con su versión/configuración; no asumir downgrade automático de migraciones.
6. Una restauración anterior puede perder operaciones posteriores al respaldo: requiere decisión explícita, identificación y conciliación de esas operaciones. No sobrescribirlas silenciosamente. Repetir las verificaciones antes de reabrir.

Los reinicios interrumpen circuitos InteractiveServer y pueden perder formularios no confirmados; avisar a la usuaria y no prometer despliegue sin interrupciones.

## Logs y verificaciones posteriores

- Configurar captura/rotación/retención de logs de app y proxy. Revisar errores de arranque, permisos, SQLite, espacio libre, uploads, reconexiones y certificados; no registrar cuerpos de comprobantes, credenciales ni datos comerciales innecesarios. Restringir acceso a logs/backups.
- Desde fuera del VPS: validar DNS y HTTPS, ausencia de exposición directa de la app, redirecciones y rechazo de hosts/cabeceras no confiables.
- Verificar login/logout, rutas privadas, acceso anónimo denegado a comprobantes, antiforgery, estilos aislados, WebSockets y reconexión.
- Recrear controladamente el contenedor y comprobar persistencia de datos, comprobantes y claves; verificar permisos de escritura y lectura sin abrir directorios privados al público.
- En staging con datos de prueba: compra y recepción parcial por compra con reservas conservadas; pedido con reserva y venta sustituta sin reservas sobrantes; pago y saldo; actividad mensual del cliente. No contaminar contabilidad real con operaciones ficticias.
- Completar QA real móvil ~390px y escritorio, incluidos formularios de fecha, select de reserva inicial/expandido, confirmaciones, recepción y desplegables del cliente. Guardar evidencia operativa privada y resultado.

## Pendientes antes del go-live

- [x] Preparar Dockerfile multi-stage .NET 8, Compose, Caddyfile y configuración explícita de hosting en el repositorio.
- [ ] Definir VPS Ubuntu LTS soportado e instalar/verificar Docker Compose; construir y probar la imagen Linux.
- [ ] Configurar dominio/DNS, HTTPS y renovación de certificados.
- [ ] Cerrar y probar Forwarded Headers/proxy confiable, restricciones de red y hosts.
- [ ] Verificar persistencia de SQLite, comprobantes, Data Protection y Caddy, con permisos mínimos y secretos externos.
- [ ] Completar bootstrap y retirar sus credenciales.
- [ ] Auditar paquetes NuGet directos y transitivos por vulnerabilidades, incluidas dependencias nativas; resolver hallazgos antes de habilitar producción. Registrar la auditoría real, no asumirla por un build exitoso.
- [ ] Verificar runtime/patch .NET y componentes base soportados en la fecha real del despliegue. El proyecto usa `net8.0`: .NET 8 termina soporte el **10/11/2026**, por lo que requiere un plan de migración y plazo antes de ese límite. No se implementa aquí la migración a .NET 10. Si el go-live ocurre después del fin de soporte, resolver el runtime soportado antes de abrir servicio. Revalidar la [política oficial de soporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- [ ] Ejecutar build/tests .NET y JS de la versión a desplegar y pruebas del runtime Linux, incluido procesamiento de imágenes/comprobantes.
- [ ] Completar validación final móvil/escritorio y pruebas detrás del proxy real.
- [ ] Respaldar y **restaurar realmente** SQLite + comprobantes + Data Protection; registrar evidencia y responsables de recuperación.
- [ ] Ensayar actualización/rollback, rotación de logs y alertas operativas mínimas.
- [ ] Aprobar go-live con la usuaria y registrar versión exacta. Solo una release/tag real permite fechar 1.0.0 en CHANGELOG.

Los checks de implementación no acreditan el despliegue real ni el go-live. [ROADMAP](../ROADMAP.md) resume versiones futuras; [V2](19_V2_Pendientes.md) y [V3](20_V3_Pendientes.md) no se implementan en este runbook.

## Construcción y arranque en Ubuntu

Desde la raíz del checkout de la versión revisada:

```bash
docker build --pull -t resellmanager:v1 .
```

El runtime copia solo el resultado de publish Release, usa ASP.NET Core .NET 8 sobre Debian/glibc
y ejecuta como el usuario app (UID/GID 1654 de la imagen .NET 8). El proyecto ya incluye
SkiaSharp.NativeAssets.Linux.NoDependencies; no se cambian dependencias ni reglas de comprobantes.
La prueba real de procesamiento de imágenes en Linux continúa siendo un requisito previo al go-live.
Compose fija Caddy en `caddy:2.11.4-alpine`, sin actualización automática. Registrar además el digest de las imágenes .NET y Caddy utilizadas y conservar las imágenes para rollback.

Compose usa la subred 172.29.213.0/28 y reserva 172.29.213.2 para Caddy.
Antes de crearla, comprobar rutas del host, VPN y redes Docker:

```bash
ip -4 route show table all
docker network ls
docker network inspect $(docker network ls -q) --format '{{.Name}} {{json .IPAM.Config}}'
```

Si hay solapamiento, cambiar conjuntamente subnet, ipv4_address de Caddy y
ReverseProxy__KnownProxy en compose.yml. Ninguna subred privada garantiza ausencia de conflictos.
La red bridge es privada entre contenedores, con salida a Internet para ACME; no usa internal:true.
Solo Caddy publica 80/TCP y 443/TCP+UDP. ResellManager únicamente declara expose:8080,
sin ports, sin publicación de SQLite ni acceso estático a comprobantes.

ReverseProxy:KnownProxy acepta una IP; ReverseProxy:KnownNetwork acepta un CIDR como alternativa.
Production exige al menos uno; valores inválidos y redes /0 detienen el arranque antes de SQLite/bootstrap.
Si se configuran ambos se confía en su unión. No ampliar la confianza innecesariamente:
este Compose confía solo en la IP de Caddy. Se sustituyen las entradas implícitas de loopback,
se limita a un salto y únicamente se procesan X-Forwarded-For y X-Forwarded-Proto.
No configurar ASPNETCORE_FORWARDEDHEADERS_ENABLED ni mecanismos alternativos que amplíen esa confianza.

Usar los directorios existentes del VPS bajo `/opt/resellmanager/data/`; no crear otra estructura bajo `/srv`. Deben permitir lectura/escritura al UID/GID del usuario `app` de la imagen (1654:1654 actualmente; verificar si cambia la imagen), incluidos los archivos ya existentes. Comprobar o ajustar los directorios sin borrar su contenido:

```bash
sudo install -d -m 0700 -o 1654 -g 1654 /opt/resellmanager/data/database /opt/resellmanager/data/comprobantes /opt/resellmanager/data/dataprotection
umask 077
touch .env
chmod 600 .env
```

Configurar localmente .env con estos valores no secretos, sin versionarlo. Compose rechaza variables ausentes o vacías para host y las tres rutas persistentes:

```dotenv
AllowedHosts=app.resellmanager.tech
ConnectionStrings__ResellManager="Data Source=/app/data/database/resellmanager.db"
AlmacenamientoComprobantes__DirectorioBase=/app/data/comprobantes
DataProtection__KeysPath=/app/data/dataprotection
```

Compose fija ASPNETCORE_ENVIRONMENT=Production, ASPNETCORE_HTTP_PORTS=8080,
ASPNETCORE_HTTPS_PORT=443 y ReverseProxy__KnownProxy=172.29.213.2.
appsettings.json conserva AllowedHosts=* para desarrollo: Production debe sobrescribirlo
con el host concreto. Si cambia el dominio, actualizar también Caddyfile; no está fijado en código.
Las claves de Data Protection persisten en disco sin cifrado en reposo configurado por la app:
proteger permisos, disco y backups según la política operativa.

Añadir temporalmente UsuarioInicial__Correo y UsuarioInicial__Contrasena mediante edición privada.
No hay valores de bootstrap en archivos versionados. Tras crear la cuenta, retirar ambas claves
y recrear la app; el bootstrap existente no cambia cuentas ni contraseñas ya creadas.
Evitar imprimir docker compose config o docker inspect completos cuando haya secretos.

```bash
docker compose config --quiet
docker compose run --rm --no-deps caddy caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
docker compose up -d --build
curl --fail https://app.resellmanager.tech/health
# Tras retirar las credenciales de bootstrap del .env:
docker compose up -d --force-recreate resellmanager
docker compose exec resellmanager id
docker compose ps
```

/health devuelve únicamente OK cuando el host terminó su arranque; no expone configuración ni datos,
ni comprueba disponibilidad continua de SQLite/disco. No se agrega un framework ni sondeo automático.
Caddy conserva /data y /config en los volúmenes nombrados caddy_data y caddy_config (no utiliza la carpeta preexistente /opt/resellmanager/data/caddy) y monta Caddyfile read-only; administra TLS
automáticamente y reverse_proxy admite WebSockets. Verificar SignalR, reconexión, login y antiforgery
en el VPS real. Nunca usar docker compose down -v durante una actualización.

## Hardening de la superficie web

Solo `POST /account/login` usa la política nativa `Login`: ventana fija de un minuto,
10 solicitudes por IP de cliente, sin cola y HTTP 429 al agotarse. Lee `RemoteIpAddress`
tras Forwarded Headers; no confía en XFF de proxies desconocidos. Los contadores son locales
al proceso y se reinician al recrearlo. Identity conserva su lockout por cuenta.
No hay limiter global ni límites para SignalR, archivos estáticos o /health.

Orden relevante: Forwarded Headers → headers (OnStarting) → exception handler/HSTS en Production
→ redirección HTTPS → estáticos/páginas de estado → routing → rate limiter → autenticación
→ autorización → antiforgery → endpoints. El limiter va después de routing porque la política
se selecciona por endpoint, y siempre después de procesar la IP del proxy confiable.

Headers básicos: X-Content-Type-Options=nosniff, Referrer-Policy=no-referrer, X-Frame-Options=DENY
y CSP frame-ancestors 'none'. La CSP global se añade solo si no existe otra: comprobantes conserva
sandbox. No se restringen scripts ni estilos. Validar estos controles detrás de Caddy en el VPS;
esto no completa el go-live.

## Verificaciones de esta preparación

Las pruebas de hosting cubren proxy/IP o CIDR explícitos, fuentes desconocidas (incluido loopback),
límite de un salto, rechazo de configuración inválida, cabeceras de host ignoradas,
variables de entorno, persistencia de claves entre proveedores, arranque y cookies por entorno.
La suite existente comprueba bootstrap idempotente y ausencia de cambios pendientes del modelo.

```bash
dotnet build
dotnet test
node --test tests/ResellManager.Tests/*.test.cjs
git diff --check
```

También se verificó publish Release para linux-x64 (framework-dependent), con libSkiaSharp.so y libe_sqlite3.so en el resultado. Esto verifica empaquetado, no ejecución nativa.

La ejecución local de estas comprobaciones no sustituye construir/arrancar la imagen Linux:
el entorno de preparación no dispone de Docker. Siguen pendientes validación de Compose/Caddy
con sus binarios, SkiaSharp dentro del contenedor, persistencia tras recreación real, QA detrás de
Caddy, bootstrap operativo, backup y restauración REAL.

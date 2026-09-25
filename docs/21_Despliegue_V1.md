# Despliegue V1 — Runbook operativo

## Estado y límites

ResellManager está en producción. Esta revisión documental del 21/09/2026 toma como referencia versionada `v1.0.1` y separa las fuentes de evidencia:

| Fuente | Estado demostrado o confirmado |
| --- | --- |
| Repositorio | Tags `v1.0.0 → 3f2d264` y `v1.0.1 → 97da64e`; Dockerfile, Compose, Caddyfile, hosting/seguridad, corrección de claves duplicadas Blazor en venta directa, script de backup y unidades systemd versionados. |
| Confirmación operativa del responsable del proyecto | Producción, dominio y HTTPS funcionando, cuentas Identity separadas y pruebas funcionales de cliente, compra y venta directa. |
| Confirmación operativa de recuperación | Backup manual real y restore real verificado; timer systemd instalado, activo y con al menos una ejecución correcta; retención automática. Las copias permanecen en el mismo VPS. |
| Pendiente confirmado | Copia automática externa hacia Raspberry/otro equipo y validación completa del rollback de versión de aplicación. |
| Sin evidencia detallada incorporada | Imagen/commit/digest exactos desplegados, fechas y registros de las comprobaciones, alcance pormenorizado del restore y verificaciones adicionales de seguridad, persistencia y QA indicadas abajo. |

La existencia de los tags no demuestra qué imagen ejecuta el VPS. Restore probado no equivale a rollback de aplicación validado ni certifica todos los controles. Las cuentas actuales no implican roles/permisos finos o concurrencia fuerte V2.

La referencia funcional vigente es [Alcance V1](14_Alcance_V1.md). [Cierre V1, sección 19](18_Fase510_CierreV1.md#19-cierre-operativo-reservas-recepción-y-actividad-del-cliente-13092026) conserva evidencia histórica (405 tests .NET, 17 JS y build sin errores/warnings); esos resultados no sustituyen pruebas en el VPS ni QA visual real. Esta sincronización no agrega migraciones ni cambios de esquema.

## Arquitectura de operación V1

```text
Internet → HTTPS → Caddy → ResellManager ASP.NET Core / Blazor InteractiveServer
                                           └→ SQLite persistente
```

- Solo Caddy publica el servicio web hacia Internet; el puerto de la aplicación queda en una red privada de Compose, sin publicación directa en el host. Restringir aparte el acceso administrativo al VPS y verificar firewall efectivo, también para puertos publicados por Docker.
- Dominio y HTTPS están operativos según confirmación del responsable. El Caddyfile usa app.resellmanager.tech; mantener DNS alineado con el VPS y comprobarlo al cambiar de host. No registrar credenciales en este documento.
- Mantener HTTPS y el almacenamiento persistente de certificados/estado de Caddy. La renovación comprobada, la redirección HTTP a HTTPS y el rechazo de hosts no autorizados requieren evidencia propia; no se dan por validados solo porque HTTPS funcione. Caddy admite proxy de WebSockets; comprobar la conexión interactiva de Blazor y su reconexión detrás del proxy. Ver [reverse proxy de Caddy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy).
- V1 no promete operación multiinstancia ni concurrencia fuerte multiusuario. No desplegar réplicas escribiendo sobre la misma SQLite como supuesto escalamiento automático.

## Hosting implementado y verificaciones operativas

El arranque actual aplica las migraciones existentes antes del bootstrap de usuario, usa redirección HTTPS y protege las rutas de negocio/comprobantes con autenticación. Eso no configura por sí solo la frontera HTTPS de un reverse proxy.

Program.cs registra Forwarded Headers antes de HSTS, redirección y autenticación. DataProtection:KeysPath configura PersistKeysToFileSystem con ApplicationName estable ResellManager; sin esa ruta se conserva el comportamiento local de desarrollo. Las rutas productivas se proporcionan por entorno, no en appsettings.json.

### Forwarded Headers y proxy confiable

Configurar el procesamiento de `X-Forwarded-Proto` y `X-Forwarded-For` antes de redirección HTTPS y autenticación. Definir explícitamente el proxy o red de confianza de Caddy según la topología real de Compose y un límite acorde a los saltos reales; restringir hosts aceptados. No aceptar cabeceras de cualquier origen ni vaciar listas de confianza como atajo.

Probar que ASP.NET Core reconoce el esquema HTTPS externo, que login/logout y antiforgery funcionan, que las cookies de sesión recibidas por HTTPS son seguras y que no hay bucles de redirección. La cookie usa CookieSecurePolicy.Always en Production y SameAsRequest en Development; conserva HttpOnly, SameSite=Lax, expiración de ocho horas, sliding expiration y lockout. Incluir pruebas de cabeceras falsificadas desde fuentes no confiables. Consultar [configuración de proxies ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0) y [restricción de proxies desconocidos en .NET 8](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/8.0/forwarded-headers-unknown-proxies).

## Persistencia, permisos y configuración

Conservar y verificar estos montajes persistentes en la instalación operativa y en cada restauración o actualización:

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

Procedimiento para una instalación que necesite crear su primera cuenta. Las cuentas actuales ya funcionan; su existencia no acredita la retirada de credenciales temporales, que debe verificarse por separado.

1. Preparar una base nueva y persistente, o una restauración verificada; comprobar la ruta exacta antes del arranque.
2. Solo si hace falta la primera cuenta, configurar temporalmente `UsuarioInicial__Correo` y `UsuarioInicial__Contrasena` desde el mecanismo privado de secretos. Cumplir la política vigente de Identity documentada en la decisión 016.
3. Arrancar una única instancia, revisar migraciones/errores y verificar acceso con esa cuenta.
4. Retirar ambas credenciales de bootstrap del entorno y recrear el contenedor sin ellas. Verificar que el acceso sigue funcionando: la cuenta permanece en SQLite. El seed es idempotente y no cambia la contraseña de una cuenta existente; no sirve como recuperación de contraseña.
5. Mantener un procedimiento administrativo seguro de recuperación de acceso. No habilitar autorregistro ni dejar credenciales de bootstrap como mecanismo permanente.

## Respaldo y restauración

Backup manual y restore real fueron realizados y verificados según la confirmación operativa del responsable del proyecto. El sistema automático systemd está instalado y activo, y el timer ya ejecutó correctamente al menos una vez. Esta confirmación no certifica cada control de la guía de restore ni el rollback completo de versión de aplicación.

### Automatización instalada y fuentes versionadas

- Fuente: [scripts/backup-v1.sh](../scripts/backup-v1.sh). Su copia operativa es `/opt/resellmanager/backup.sh`; esa es la ruta de `ExecStart` del servicio. Conservar la correspondencia entre la copia instalada y la versión revisada.
- Unidades: [resellmanager-backup.service](../deploy/systemd/resellmanager-backup.service) (`Type=oneshot`, requiere Docker) y [resellmanager-backup.timer](../deploy/systemd/resellmanager-backup.timer).
- Programación: `OnCalendar=*-*-* 03:30:00 America/Guatemala`, con `RandomizedDelaySec=10m`; la ejecución diaria ocurre alrededor de las 03:30, con hasta diez minutos de demora adicional. `Persistent=true` permite recuperar una activación de calendario perdida mientras el timer estuvo inactivo.
- El script exige root y necesita Bash, Docker Compose, `flock`, Python 3, `tar`, `sha256sum` y `curl`, además de las utilidades de shell usadas. Comprueba que exista `reselladmin` antes de detener la aplicación.

| Uso | Ruta o valor del script |
| --- | --- |
| Checkout de Compose | `/opt/resellmanager/source` |
| Datos | `/opt/resellmanager/data` |
| Backups | `/opt/resellmanager/backups` |
| Servicio Compose | `resellmanager` |
| Liveness | `https://app.resellmanager.tech/health` |
| Bloqueo | `/run/lock/resellmanager-backup.lock` |

### Secuencia de backup

1. Adquiere el bloqueo no bloqueante mediante `flock`. Si otra ejecución tiene el bloqueo, registra que no iniciará otro backup y termina sin crear una nueva copia.
2. Detiene `resellmanager` con `docker compose stop` para obtener un snapshot consistente de SQLite y sus archivos asociados. Hay una interrupción de servicio; los circuitos Blazor y formularios sin guardar no sobreviven necesariamente al reinicio.
3. Empaqueta los directorios completos `database`, `comprobantes` y `dataprotection` en `resellmanager-YYYYMMDD-HHMMSS.tar.gz`. Incluye el directorio de SQLite, no una copia aislada del `.db` mientras la app escribe.
4. Genera el archivo acompañante `.tar.gz.sha256`, con la suma SHA-256 del paquete.
5. Reinicia la aplicación mediante `docker compose start` y consulta `/health`, esperando una respuesta `OK`. Realiza hasta 20 intentos, con timeout de cinco segundos por petición y pausas de dos segundos entre intentos fallidos.
6. Tras recuperar liveness, asigna propietario `reselladmin:reselladmin` y permisos `600` al paquete y al checksum; después aplica la retención.

El manejador de salida intenta levantar la aplicación si el script termina anticipadamente y aún la considera detenida. Es un intento de recuperación, no una garantía de servicio restablecido. Si `/health` falla, el script termina con error aunque el paquete ya exista; no llega a aplicar los permisos finales ni la retención de esa ejecución.

SHA-256 sirve para comprobar integridad, **no cifra** el contenido. `/health` es **liveness** del host; no comprueba continuamente SQLite/disco ni certifica una recuperación completa o la integridad del backup.

### Retención automática

Se conserva la **unión**, sin duplicar archivos, de:

- Las 7 copias más recientes.
- La copia más reciente de cada una de las 4 semanas ISO más recientes disponibles.
- La copia más reciente de cada uno de los 3 meses más recientes disponibles.

Una misma copia puede satisfacer varios criterios. No se garantiza que existan exactamente 14 backups ni que las siete copias recientes correspondan a siete días distintos. Las ejecuciones manuales también entran en la selección por nombre y fecha. La rotación elimina los paquetes no seleccionados y sus checksums correspondientes.

### Operación y revisión de fallos

Para consultar el estado y las ejecuciones en el VPS:

```bash
sudo systemctl status resellmanager-backup.timer
sudo systemctl list-timers --all resellmanager-backup.timer
sudo systemctl status resellmanager-backup.service
sudo journalctl -u resellmanager-backup.service -n 100 --no-pager
```

Para una ejecución manual planificada, con la misma interrupción y retención que la automática:

```bash
sudo /opt/resellmanager/backup.sh
```

Revisar el journal y la presencia del paquete/checksum para confirmar una copia nueva; una salida correcta por bloqueo concurrente no crea otro backup. Ante fallo, comprobar `docker compose ps` y los logs del servicio desde `/opt/resellmanager/source`, recuperar la aplicación si sigue detenida e inspeccionar el respaldo antes de considerarlo válido. No eliminar copias anteriores para resolver un fallo.

### Alcance y pendientes de recuperación

Las copias actuales permanecen **en el mismo VPS**. La copia automática externa hacia Raspberry/otro equipo sigue pendiente; no existe todavía esa protección frente a pérdida completa del VPS. Definir destino, acceso y protección/cifrado de las copias externas, además de responsable, alertas y objetivos RPO/RTO. La programación y retención actuales están implementadas; su adecuación a esos objetivos requiere decisión operativa.

El paquete actual incluye **SQLite + comprobantes + Data Protection**. El script no incorpora imágenes de aplicación, un manifiesto de versión/commit, configuración, secretos ni los volúmenes de Caddy. Conservar por separado la identificación de imágenes, configuración necesaria y recuperación protegida de secretos y estado de Caddy; no asumir que están dentro del `.tar.gz`.

### Restore real: resultado y comprobaciones

El restore real desde backup está probado y verificado según evidencia operativa comunicada por el responsable. Falta incorporar su registro detallado: fecha, copia usada, imagen compatible, tiempos, responsable y comprobaciones realizadas, sin secretos. No se afirma que se hayan certificado individualmente todos los puntos siguientes.

Para repetir la validación: comprobar el checksum; restaurar el conjunto en un entorno aislado sin sobrescribir el original; usar imagen compatible, rutas y permisos correctos. Verificar integridad SQLite y relaciones, login, saldos, pedidos/reservas, lectura de comprobantes y funcionamiento de Data Protection. Si existen claves protegidas en reposo, comprobar su descifrado. Ensayar persistencia tras recreación del contenedor y registrar el resultado. Restaurar datos no valida por sí solo volver a una versión anterior de la aplicación.

## Actualización y rollback

**Pendiente: validación completa del rollback de versión de aplicación.** El restore de datos probado es una comprobación distinta. El procedimiento siguiente debe ensayarse con imágenes y datos compatibles; no se presenta como una operación ya validada.

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

## Estado operativo y verificaciones pendientes

Completado según la fuente indicada en «Estado y límites»:

- [x] Dockerfile multi-stage .NET 8, Compose, Caddyfile y hosting explícito versionados.
- [x] Producción, dominio y HTTPS funcionando; cuentas Identity separadas y pruebas funcionales de cliente, compra y venta directa, según confirmación operativa.
- [x] Tags `v1.0.0` y `v1.0.1` existentes; la corrección de claves duplicadas de Blazor en venta directa está incluida en `v1.0.1`.
- [x] Backup manual y restore real realizados y verificados, según confirmación operativa.
- [x] Timer systemd instalado y activo, con al menos una ejecución correcta y retención automática.

Pendientes confirmados y controles cuya evidencia detallada aún debe verificarse/incorporarse:

- [ ] Implementar y probar copia automática externa a Raspberry/otro equipo; las copias actuales siguen en el mismo VPS.
- [ ] Validar completamente el rollback de versión de aplicación.
- [ ] Registrar imagen/commit/digest exactos desplegados y conservar una imagen anterior compatible. Los tags no prueban qué ejecuta el VPS.
- [ ] Registrar la evidencia detallada del backup/restore, responsable y objetivos RPO/RTO.
- [ ] Verificar renovación de certificados y pruebas de Forwarded Headers/proxy confiable, restricciones de red y hosts.
- [ ] Verificar persistencia de SQLite, comprobantes, Data Protection y Caddy, con permisos mínimos y secretos externos.
- [ ] Verificar retirada de credenciales temporales de bootstrap.
- [ ] Auditar paquetes NuGet directos y transitivos por vulnerabilidades, incluidas dependencias nativas; registrar y resolver hallazgos. Un build exitoso no sustituye esa auditoría.
- [ ] Verificar runtime/patch .NET, Ubuntu y componentes base soportados en la instalación y en cada actualización. El proyecto usa `net8.0`: .NET 8 termina soporte el **10/11/2026**; mantener un plan de migración antes de ese límite. Esta fase documental no implementa la migración a .NET 10. Revalidar la [política oficial de soporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- [ ] Registrar pruebas de build/tests .NET y JS de la versión candidata y del runtime Linux, incluido procesamiento de imágenes/comprobantes.
- [ ] Completar o incorporar evidencia de QA móvil/escritorio y pruebas detalladas detrás del proxy real; las pruebas funcionales confirmadas no cubren automáticamente todos esos criterios.
- [ ] Verificar rotación/retención de logs y definir alertas operativas mínimas.

Las casillas de implementación no certifican los controles del VPS. [ROADMAP](../ROADMAP.md) resume versiones futuras; [V2](19_V2_Pendientes.md) y [V3](20_V3_Pendientes.md) siguen pendientes y no se implementan en este runbook.

## Construcción y arranque en Ubuntu

Procedimiento para nuevas instalaciones o recreaciones planificadas; no describe un despliegue inicial aún pendiente. Identificar la versión revisada y aplicar las precauciones de actualización anteriores. Desde la raíz de ese checkout:

```bash
docker build --pull -t resellmanager:v1 .
```

El runtime copia solo el resultado de publish Release, usa ASP.NET Core .NET 8 sobre Debian/glibc
y ejecuta como el usuario app (UID/GID 1654 de la imagen .NET 8). El proyecto ya incluye
SkiaSharp.NativeAssets.Linux.NoDependencies; no se cambian dependencias ni reglas de comprobantes.
La prueba real de procesamiento de imágenes en Linux requiere evidencia específica; las pruebas funcionales confirmadas no la acreditan por sí solas.
Compose fija Caddy en `caddy:2.11.4-alpine`, sin actualización automática. Registrar además el digest de las imágenes .NET y Caddy utilizadas y conservar las imágenes para rollback.

Compose usa la subred 172.29.213.0/28 y reserva 172.29.213.2 para Caddy.
Antes de crearla, comprobar rutas del host, VPN y redes Docker:

```bash
ip -4 route show table all
docker network ls
docker network inspect $(docker network ls -q) --format '{{.Name}} {{json .IPAM.Config}}'
```

Si hay solapamiento y es necesario cambiar la subred, actualizar conjuntamente subnet,
ipv4_address de Caddy, ipv4_address de ResellManager y ReverseProxy__KnownProxy en compose.yml.
Ambas IP estáticas deben pertenecer a la nueva subred, ser distintas entre sí y no entrar
en conflicto con otras direcciones utilizadas. ReverseProxy__KnownProxy debe seguir apuntando
a la IP de Caddy. Ninguna subred privada garantiza ausencia de conflictos.
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
ni comprueba disponibilidad continua de SQLite/disco. El script de backup consulta este endpoint después de reiniciar la app; no constituye un monitor continuo ni readiness avanzada.
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
su implementación no certifica todas las verificaciones de seguridad.

## Verificaciones de esta preparación

> **Registro histórico anterior al despliegue.** Los resultados y pendientes siguientes se conservan tal como se documentaron durante la preparación. No describen el estado operativo actual ni pruebas ejecutadas en esta sincronización Markdown; consultar «Estado y límites» y «Estado operativo y verificaciones pendientes» para la situación posterior.

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

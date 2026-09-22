# Seguridad de producción — checklist viva

## Objetivo

Este documento mantiene una lista de controles de seguridad y operación para ResellManager. No sustituye pruebas, auditorías ni el [runbook de despliegue V1](21_Despliegue_V1.md). Debe actualizarse cuando cambie la arquitectura (multiusuario, API pública, tienda, pagos, correo, IA, PostgreSQL, etc.).

Referencia histórica del paquete: rama `chore/v1-production-deploy`, hosting y hardening del login, headers, versión de Caddy y bind mounts, incorporados en `v1.0.1`. Implementado en código no significa validado en el VPS.

Estado operativo confirmado por el responsable del proyecto para la sincronización del 21/09/2026: ResellManager está en producción; funcionan dominio/HTTPS y cuentas Identity separadas; se probaron cliente, compra y venta directa. Backup manual y restore real fueron realizados y verificados. El timer systemd está instalado, activo y ya ejecutó correctamente al menos una vez, con retención automática. Las copias siguen en el mismo VPS; copia automática externa y validación completa del rollback de aplicación están pendientes.

Existen `v1.0.0 → 3f2d264` y `v1.0.1 → 97da64e`, pero no prueban qué imagen exacta está desplegada. Estar en producción, usar HTTPS o haber probado restore no certifica todos los controles de esta lista. Las cuentas separadas no implican roles/permisos finos ni concurrencia fuerte.

## Leyenda

- ✅ **Cubierto**: existe un control equivalente en la arquitectura actual; cada fila distingue implementación de confirmación operativa. No es una certificación global del VPS.
- 🟡 **Pendiente V1 / endurecimiento**: aplica a la instalación productiva actual; requiere completar el control o incorporar/verificar su evidencia operativa.
- 🔴 **Requisito de salida**: no debe considerarse operación real protegida sin comprobarlo.
- 🔵 **Futuro**: aplica cuando exista la funcionalidad indicada.
- ⚪ **No aplica hoy**: la arquitectura actual no expone esa superficie.

## Checklist principal

| # | Control | Estado V1 | Criterio actual / pendiente |
|---|---|---|---|
| 1 | RLS en tablas expuestas o control equivalente | ⚪ / 🔵 | SQLite no se expone al cliente y V1 no es multi-tenant. La autorización vive en servidor. Revaluar con PostgreSQL/API/tienda/multiusuario. |
| 2 | Cada fila con dueño cuando corresponda | ⚪ / 🔵 | Los datos actuales pertenecen al negocio y son compartidos. Incorporar ownership/tenant solo donde exista una frontera real de usuario/empresa. |
| 3 | Campos que el cliente no puede escribir | ✅ / 🟡 | Mantener DTO/InputModels y validación de casos de uso; evitar binding directo de entidades persistentes desde entrada no confiable. |
| 4 | Devolver solo los campos necesarios | ✅ / 🔵 | No existe una API JSON pública general. Una futura API/tienda debe exponer DTOs mínimos. |
| 5 | Claves públicas pueden exponerse; secretos jamás | ✅ | Secretos solo en servidor/configuración privada. Nunca incluir credenciales, tokens privados, claves de firma o bootstrap en repo, imagen, JS o logs. |
| 6 | Lo que llega a la app cliente no es secreto | ✅ | Cualquier HTML/JS/CSS/configuración enviada al navegador se considera pública. Blazor Server reduce código cliente, pero no cambia esta regla. |
| 7 | Ante una clave filtrada: rotar/revocar antes de purgar | ✅ | Una credencial filtrada se considera comprometida. Primero revocar/rotar; luego limpiar historial, logs o artefactos si procede. |
| 8 | Endpoint privado comprueba identidad y permiso | ✅ / 🟡 | V1 usa Identity, rutas autorizadas y endpoint de comprobantes protegido. Las cuentas actuales son separadas; roles/permisos finos y administración de usuarios desde la app siguen pendientes. |
| 9 | Tokens sensibles fuera de `localStorage` | ✅ | V1 no usa JWT de sesión en `localStorage`; Identity usa cookie HttpOnly. |
| 10 | Contraseñas correctamente hasheadas/delegadas | ✅ | ASP.NET Core Identity gestiona hashing. La política vigente exige longitud y complejidad y usa lockout por intentos fallidos. |
| 11 | Segundo factor disponible | 🔵 | Añadir 2FA como endurecimiento de cuentas administrativas, prioritario antes de ampliar usuarios o exposición. |
| 12 | Validación de inputs en servidor | ✅ / 🟡 | Las reglas críticas se validan en servicios/casos de uso. Mantener validación server-side aunque exista validación de UI. |
| 13 | Consultas parametrizadas | ✅ | EF Core es la vía normal de acceso. Cualquier SQL raw futuro debe parametrizarse; no concatenar entrada del usuario. |
| 14 | Codificar outputs según contexto | ✅ / 🟡 | Razor codifica salida por defecto. No usar HTML crudo con datos no confiables sin sanitización específica. |
| 15 | Validar links/redirecciones del usuario | ✅ / 🔵 | Login/logout actuales usan destinos locales controlados. Si aparece `returnUrl` u URLs externas, validar esquema/host y destino local. |
| 16 | Renombrar, validar y limitar uploads | ✅ | Comprobantes: límite de tamaño, detección por firma, nombres generados, límites de dimensiones para imágenes, reprocesado y almacenamiento privado fuera de `wwwroot`. |
| 17 | Limitar operaciones que generan costes | ⚪ / 🔵 | Aplicar cuotas/rate limit cuando existan SMS, email, IA, almacenamiento facturable, APIs de pago u otros proveedores de coste variable. |
| 18 | Anti-bot en creación de cuentas/envíos | ⚪ / 🔵 | V1 no tiene autorregistro ni envíos públicos. Aplicar en tienda/registro/contacto si se habilitan. |
| 19 | Pagos fallan cerrados | ⚪ / 🔵 | No hay pasarela online en V1. Futuro: webhook autenticado, idempotencia, verificación server-side y nunca marcar pagado ante estado dudoso. |
| 20 | IA no ejecuta acciones destructivas sin permiso | ⚪ / 🔵 | No hay agentes IA en V1. Si se integran, requerir autorización explícita, límites y auditoría para acciones destructivas/financieras. |
| 21 | CORS restringido y nada expuesto accidentalmente | ✅ / 🟡 | V1 no habilita CORS general. Solo Caddy publica 80/443; ResellManager usa `expose:8080` en la red Docker y SQLite no se publica. Verificarlo desde fuera del VPS. |
| 22 | Cabeceras de seguridad y HSTS | ✅ / 🟡 | Headers globales implementados mediante OnStarting: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY` y CSP `frame-ancestors 'none'` solo si no existe otra CSP. Comprobantes conserva `sandbox`; no se restringen scripts/estilos de Blazor. HSTS sigue en Production. Pendiente validación real en VPS. |
| 23 | Errores sin información sensible | ✅ / 🟡 | Production usa exception handler y mensajes operativos genéricos. Revisar que logs públicos/respuestas nunca incluyan rutas, secretos, SQL o stack traces. |
| 24 | Logs y alertas accionables | 🟡 | Se usa `ILogger` y Docker rota `json-file`. Falta decidir formato estructurado/correlación, retención, destino y alertas mínimas. |
| 25 | Restauración del backup probada | ✅ / 🟡 | Restore real realizado y verificado según confirmación operativa del responsable. Falta incorporar el registro detallado y alcance de comprobaciones; no se certifican todos los controles ni el rollback de aplicación. Véase el runbook. |

## Operación y observabilidad

### Backups y recuperación

El [runbook operativo](21_Despliegue_V1.md#respaldo-y-restauración) documenta `scripts/backup-v1.sh`, su copia `/opt/resellmanager/backup.sh` y el servicio/timer systemd instalados. El timer está activo y ya ejecutó correctamente; backup manual y restore real también están probados según confirmación operativa.

La retención automática conserva la unión de 7 copias más recientes, una por cada una de las 4 semanas ISO más recientes disponibles y una por cada uno de los 3 meses más recientes disponibles; no implica exactamente 14 archivos. Actualmente las copias permanecen en el mismo VPS y todavía no existe copia automática externa a Raspberry/otro equipo.

El paquete contiene `database`, `comprobantes` y `dataprotection`. SHA-256 comprueba integridad, no cifra; `/health` comprueba liveness después del reinicio, no una recuperación completa. Mantener como pendientes copia externa, política de recuperación/alertas y validación completa del rollback de aplicación. La evidencia de restore no certifica por sí sola permisos, descifrado de claves, todos los datos y operaciones o reconstrucción íntegra del VPS.

### Logs estructurados

V1 ya usa `ILogger`; no aplica Winston/Pino porque son herramientas del ecosistema Node. Antes de escalar observabilidad:

- Preferir salida estructurada (JSON) con timestamp, nivel, categoría y `TraceId`/request ID.
- Añadir identificadores de usuario solo cuando sean necesarios, evitando datos personales o comerciales innecesarios.
- No registrar contraseñas, cookies, tokens, contenido de comprobantes, connection strings completas ni `.env`.
- Mantener rotación y retención limitada en Docker y definir cómo extraer evidencias si el contenedor se recrea.

Estado: 🟡 endurecimiento posterior al primer despliegue.

### Monitoreo de errores

Sentry, Rollbar, Bugsnag u otra plataforma son opciones futuras; no son requisito técnico para arrancar una única instancia privada. Antes de conectarlas revisar privacidad, datos enviados y redacción de información sensible.

Estado: 🔵 futuro, incluido monitoreo externo/Sentry; no se incorpora en este cambio.

### Rate limiting

Implementado exclusivamente en `POST /account/login` con la política nativa `Login` de ASP.NET Core:

- Partición por `HttpContext.Connection.RemoteIpAddress` después de Forwarded Headers, normalizando IPv4/IPv4-mapped.
- Ventana fija de un minuto, 10 solicitudes por IP, `QueueLimit = 0`, respuesta HTTP 429 al excederse.
- Sin limiter global: no limita SignalR/Blazor, estáticos ni `/health`.
- `UseRateLimiter` después de `UseRouting` para seleccionar la política del endpoint y después de `UseForwardedHeaders` para usar la IP real solo desde el proxy confiable. Proxies desconocidos no pueden elegir su contador con XFF.
- Conserva el lockout por cuenta de Identity y `lockoutOnFailure=true`; no revela si existe la cuenta.
- Contadores en memoria por proceso; se reinician al recrear el contenedor. Usuarios que comparten una IP comparten el límite.

Estado: ✅ implementado y probado localmente; 🟡 validación real detrás de Caddy en el VPS pendiente.
Recuperación de contraseña, registro/tienda y otras superficies futuras requerirán su propia evaluación.

### Health checks

V1 implementa `/health` como **liveness** mínima: responde `OK` cuando el host terminó de arrancar y no expone datos. No comprueba continuamente SQLite, disco u otras dependencias.

Si en el futuro hay orquestador, balanceador o dependencias externas, separar liveness y readiness; una comprobación de readiness puede validar dependencias críticas sin revelar detalles al público.

Estado: ✅ liveness mínima; 🔵 readiness avanzada.

### Rollback

La instalación productiva aún necesita validar completamente una ruta de retorno de versión reproducible, distinta del restore de datos ya probado:

- Identificar commit/tag e imagen desplegada.
- Conservar la imagen anterior o poder reconstruirla exactamente.
- Backup consistente previo a despliegues que puedan afectar datos/esquema.
- No asumir que una imagen antigua es compatible con una base migrada.
- No usar `docker compose down -v` durante actualizaciones.

Blue/green no es necesario para V1 con una instancia y SQLite; primero priorizar un rollback simple y probado.

Estado: 🔴 validación completa del rollback de versión de aplicación pendiente. El restore real probado no cierra este control.

### Rotación de secretos

No aplicar una regla universal de “cada 90 días” a cualquier secreto o contraseña.

- Credencial filtrada: rotación/revocación inmediata.
- Bootstrap `UsuarioInicial`: retirar del entorno tras comprobar la cuenta inicial.
- API keys/tokens futuros: rotar según riesgo, proveedor y capacidad operativa.
- Contraseñas humanas: no forzar cambio periódico sin motivo; sí cambiar ante sospecha/compromiso y habilitar 2FA cuando corresponda.

Estado: ✅ política definida; procedimientos concretos se agregan cuando existan nuevos proveedores/secretos.

## Revisión del paquete de hosting V1

### Bien resuelto

- Dockerfile multi-stage y runtime .NET 8 no-root.
- ResellManager no publica `8080` al host; solo Caddy publica 80/443.
- Proxy confiable explícito: solo procesa `X-Forwarded-For` y `X-Forwarded-Proto`, con `ForwardLimit = 1`; Production falla si no se configura proxy/red válida.
- Cookie de autenticación `Secure` en Production, `HttpOnly` y `SameSite=Lax`.
- `AllowedHosts` se suministra por entorno para producción.
- Data Protection puede persistir keys en volumen y usa `ApplicationName = ResellManager`.
- SQLite, comprobantes y Data Protection se montan fuera de la capa efímera del contenedor.
- El Caddyfile expone solo `app.resellmanager.tech` y hace reverse proxy a la red privada.
- `.dockerignore` excluye `.env`, secretos, bases locales, App_Data, llaves y artefactos innecesarios.
- `/health` no devuelve configuración ni datos.
- Logs Docker tienen rotación básica configurada.
- Login tiene rate limiting nativo por IP y headers básicos globales sin una CSP completa.
- Caddy está fijado en `caddy:2.11.4-alpine`, sin latest ni auto-update; registrar además el digest utilizado.
- Bind mounts de la app alineados con `/opt/resellmanager/data/database`, `/opt/resellmanager/data/comprobantes` y `/opt/resellmanager/data/dataprotection`; usuario no-root conservado y Caddy mantiene sus volúmenes nombrados.

### Verificaciones pendientes en producción

1. **Validar rate limit implementado en el VPS**: 10 solicitudes por IP/minuto, siguiente solicitud 429 y contadores independientes detrás del proxy confiable; SignalR y health continúan disponibles.
2. **Validar headers implementados detrás de Caddy** en páginas/estáticos y comprobar que los comprobantes conservan CSP sandbox, sin romper Blazor.
3. **Completar evidencia detallada del restore ya probado** y **ensayar el rollback completo de versión de aplicación**, que sigue pendiente.
4. **Prueba Linux real** de SkiaSharp/comprobantes dentro del contenedor.
5. **Verificar permisos de bind mounts** con UID/GID del usuario `app` de la imagen.
6. **Validar red Docker elegida** y que la IP real de Caddy coincide con `ReverseProxy__KnownProxy`.
7. **Verificar desde Internet** que `8080` y SQLite no son accesibles.
8. **Retirar `UsuarioInicial__Correo` y `UsuarioInicial__Contrasena`** inmediatamente después del bootstrap y recrear el contenedor.
9. **Auditar paquetes NuGet directos/transitivos** y runtime de la instalación y de cada actualización; `net8.0` requiere plan de migración por fin de soporte el 10/11/2026.
10. **Reproducibilidad de imágenes**: Caddy está fijado en `2.11.4-alpine`; registrar además los digests de las imágenes usadas y conservar la desplegada/anterior para el ensayo real de rollback pendiente.
11. **Data Protection en reposo**: las keys persistidas en filesystem no quedan cifradas automáticamente por esa configuración; proteger permisos, disco y backups y evaluar protección adicional si aumenta el riesgo.
12. **Persistencia y recreaciones**: Compose usa `/opt/resellmanager/data/*` para la app y volúmenes nombrados para Caddy. Verificar escritura de los directorios/archivos por el UID/GID de `app` (1654:1654 en la imagen de referencia); no crear otra estructura en `/srv`. La carpeta `data/caddy` no sustituye los volúmenes nombrados. La indicación original de detener un Caddy de prueba correspondía a la transición inicial; no describe el estado productivo actual.
13. **Copia automática externa**: pendiente hacia Raspberry/otro equipo; comprobar acceso y recuperación cuando se implemente, sin considerar las copias locales como protección frente a pérdida completa del VPS.

## Futuro V2 / tienda / multiusuario

Revisar y ampliar esta checklist cuando aparezca cualquiera de estas superficies:

- PostgreSQL y/o RLS.
- Múltiples empresas/tenants o propiedad por usuario.
- Roles/permisos finos y 2FA.
- API pública, CORS y tokens.
- Tienda pública, registro, CAPTCHA/anti-bot y rate limiting ampliado.
- Pasarela de pagos y webhooks.
- Email/SMS/push y cuotas de proveedores.
- IA con herramientas/acciones.
- Observabilidad externa y alertas 24/7.
- Escalado horizontal, cache o múltiples instancias.

## Regla de cierre

Un control no se marca como ✅ por existir en documentación. Cuando dependa del entorno real (firewall, volúmenes, restore, exposición de puertos, certificados, logs, rollback), debe comprobarse en el VPS y registrar la evidencia operativa sin incluir secretos.

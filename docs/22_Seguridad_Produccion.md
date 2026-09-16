# Seguridad de producción — checklist viva

## Objetivo

Este documento mantiene una lista de controles de seguridad y operación para ResellManager. No sustituye pruebas, auditorías ni el [runbook de despliegue V1](21_Despliegue_V1.md). Debe actualizarse cuando cambie la arquitectura (multiusuario, API pública, tienda, pagos, correo, IA, PostgreSQL, etc.).

Referencia revisada para esta primera versión: rama `chore/v1-production-deploy`, paquete de hosting `f78d4bb` más este documento.

## Leyenda

- ✅ **Cubierto**: existe un control equivalente en la arquitectura actual.
- 🟡 **Pendiente V1 / endurecimiento**: aplica al despliegue actual y conviene cerrarlo antes o inmediatamente después del go-live.
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
| 8 | Endpoint privado comprueba identidad y permiso | ✅ / 🟡 | V1 usa Identity, rutas autorizadas y endpoint de comprobantes protegido. Roles/permisos finos quedan para multiusuario. |
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
| 22 | Cabeceras de seguridad y HSTS | 🟡 | HSTS existe en Production y comprobantes usan `nosniff` + CSP `sandbox`. Revisar cabeceras globales (`X-Content-Type-Options`, `Referrer-Policy`, anti-framing/CSP) sin romper Blazor. |
| 23 | Errores sin información sensible | ✅ / 🟡 | Production usa exception handler y mensajes operativos genéricos. Revisar que logs públicos/respuestas nunca incluyan rutas, secretos, SQL o stack traces. |
| 24 | Logs y alertas accionables | 🟡 | Se usa `ILogger` y Docker rota `json-file`. Falta decidir formato estructurado/correlación, retención, destino y alertas mínimas. |
| 25 | Restauración del backup probada | 🔴 | Requisito de salida. Debe restaurarse realmente SQLite + comprobantes + Data Protection en un entorno aislado y comprobar login y operaciones esenciales. |

## Operación y observabilidad

### Logs estructurados

V1 ya usa `ILogger`; no aplica Winston/Pino porque son herramientas del ecosistema Node. Antes de escalar observabilidad:

- Preferir salida estructurada (JSON) con timestamp, nivel, categoría y `TraceId`/request ID.
- Añadir identificadores de usuario solo cuando sean necesarios, evitando datos personales o comerciales innecesarios.
- No registrar contraseñas, cookies, tokens, contenido de comprobantes, connection strings completas ni `.env`.
- Mantener rotación y retención limitada en Docker y definir cómo extraer evidencias si el contenedor se recrea.

Estado: 🟡 endurecimiento posterior al primer despliegue.

### Monitoreo de errores

Sentry, Rollbar, Bugsnag u otra plataforma son opciones futuras; no son requisito técnico para arrancar una única instancia privada. Antes de conectarlas revisar privacidad, datos enviados y redacción de información sensible.

Estado: 🔵 / 🟡 según crecimiento y criticidad del negocio.

### Rate limiting

No aplicar una política global ciega sobre todo Blazor/SignalR. Proteger las superficies abusables de forma específica:

- `/account/login` — prioridad V1.
- Recuperación de contraseña futura.
- Registro/tienda pública futura.
- Uploads y endpoints costosos si se exponen públicamente.

El lockout de Identity protege cuentas concretas, pero no sustituye por completo un límite por origen para el endpoint de login.

Estado: 🟡 pendiente V1 para login.

### Health checks

V1 implementa `/health` como **liveness** mínima: responde `OK` cuando el host terminó de arrancar y no expone datos. No comprueba continuamente SQLite, disco u otras dependencias.

Si en el futuro hay orquestador, balanceador o dependencias externas, separar liveness y readiness; una comprobación de readiness puede validar dependencias críticas sin revelar detalles al público.

Estado: ✅ liveness mínima; 🔵 readiness avanzada.

### Rollback

Antes de datos reales debe existir una ruta de retorno reproducible:

- Identificar commit/tag e imagen desplegada.
- Conservar la imagen anterior o poder reconstruirla exactamente.
- Backup consistente previo a despliegues que puedan afectar datos/esquema.
- No asumir que una imagen antigua es compatible con una base migrada.
- No usar `docker compose down -v` durante actualizaciones.

Blue/green no es necesario para V1 con una instancia y SQLite; primero priorizar un rollback simple y probado.

Estado: 🔴 ensayo operativo pendiente.

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

### Pendientes antes de operación real

1. **Rate limit del login** por origen, sin afectar SignalR/Blazor globalmente.
2. **Cabeceras globales de seguridad** compatibles con Blazor; probar CSP antes de endurecerla.
3. **Restauración real del backup** y ensayo de rollback.
4. **Prueba Linux real** de SkiaSharp/comprobantes dentro del contenedor.
5. **Verificar permisos de bind mounts** con UID/GID del usuario `app` de la imagen.
6. **Validar red Docker elegida** y que la IP real de Caddy coincide con `ReverseProxy__KnownProxy`.
7. **Verificar desde Internet** que `8080` y SQLite no son accesibles.
8. **Retirar `UsuarioInicial__Correo` y `UsuarioInicial__Contrasena`** inmediatamente después del bootstrap y recrear el contenedor.
9. **Auditar paquetes NuGet directos/transitivos** y runtime antes del go-live; `net8.0` requiere plan de migración por fin de soporte el 10/11/2026.
10. **Reproducibilidad de imágenes**: registrar digest de las imágenes usadas y conservar la imagen desplegada/anterior para rollback.
11. **Data Protection en reposo**: las keys persistidas en filesystem no quedan cifradas automáticamente por esa configuración; proteger permisos, disco y backups y evaluar protección adicional si aumenta el riesgo.
12. **Transición del VPS actual**: el Compose versionado usa `/srv/resellmanager/...` y volúmenes nombrados para Caddy; antes de levantarlo reconciliarlo con los directorios/volúmenes ya creados en el VPS y detener el Caddy de prueba para evitar conflicto de puertos.

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

# Despliegue V1 — Runbook de preparación

## Estado y límites

Este documento prepara el go-live; no afirma que exista producción ni una release/tag 1.0.0. La referencia funcional es [Cierre V1, sección 19](18_Fase510_CierreV1.md#19-cierre-operativo-reservas-recepción-y-actividad-del-cliente-13092026). La verificación técnica allí registrada (405 tests .NET, 17 JS, build sin errores/warnings) no sustituye pruebas en el VPS ni QA visual real.

Objetivo: VPS con Ubuntu LTS soportado, Docker y Docker Compose, Caddy como reverse proxy y una instancia de ResellManager con SQLite persistente. No se implementan aquí Dockerfile, compose.yml, Caddyfile, scripts, migraciones ni ajustes de aplicación. La infraestructura y las configuraciones pendientes se prepararán y revisarán en una tarea posterior.

## Arquitectura objetivo

```text
Internet → HTTPS → Caddy → ResellManager ASP.NET Core / Blazor InteractiveServer
                                           └→ SQLite persistente
```

- Solo Caddy publica el servicio web hacia Internet; el puerto de la aplicación queda en una red privada de Compose, sin publicación directa en el host. Restringir aparte el acceso administrativo al VPS y verificar firewall efectivo, también para puertos publicados por Docker.
- Resolver dominio y DNS antes de emitir/verificar certificados. No hay dominio productivo confirmado en este runbook; no registrar aquí IP, credenciales ni datos reales de infraestructura.
- Configurar HTTPS, renovación y almacenamiento persistente de certificados/estado de Caddy. Validar redirección HTTP a HTTPS y el host permitido. Caddy admite proxy de WebSockets; comprobar la conexión interactiva de Blazor y su reconexión detrás del proxy. Ver [reverse proxy de Caddy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy).
- V1 no promete operación multiinstancia ni concurrencia fuerte multiusuario. No desplegar réplicas escribiendo sobre la misma SQLite como supuesto escalamiento automático.

## Diferencias entre código actual y preparación pendiente

El arranque actual aplica las migraciones existentes antes del bootstrap de usuario, usa redirección HTTPS y protege las rutas de negocio/comprobantes con autenticación. Eso no configura por sí solo la frontera HTTPS de un reverse proxy.

En la referencia c52ca6c, `Program.cs` no registra explícitamente Forwarded Headers ni persistencia/configuración de claves de Data Protection. **Ambos puntos deben resolverse y probarse antes del go-live**, en una tarea de preparación distinta a esta sincronización documental. No basta con crear volúmenes si la aplicación no usa sus rutas.

### Forwarded Headers y proxy confiable

Configurar el procesamiento de `X-Forwarded-Proto` y `X-Forwarded-For` antes de redirección HTTPS y autenticación. Definir explícitamente el proxy o red de confianza de Caddy según la topología real de Compose y un límite acorde a los saltos reales; restringir hosts aceptados. No aceptar cabeceras de cualquier origen ni vaciar listas de confianza como atajo.

Probar que ASP.NET Core reconoce el esquema HTTPS externo, que login/logout y antiforgery funcionan, que las cookies de sesión recibidas por HTTPS son seguras y que no hay bucles de redirección. El código usa actualmente `CookieSecurePolicy.SameAsRequest`, por lo que reconocer el esquema externo es importante. Incluir pruebas de cabeceras falsificadas desde fuentes no confiables. Consultar [configuración de proxies ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0) y [restricción de proxies desconocidos en .NET 8](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/8.0/forwarded-headers-unknown-proxies).

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

- [ ] Definir VPS Ubuntu LTS soportado, Docker/Compose e imágenes compatibles; preparar archivos de despliegue en otra tarea.
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

Ningún check se considera completado por crear este documento. [ROADMAP](../ROADMAP.md) resume versiones futuras; [V2](19_V2_Pendientes.md) y [V3](20_V3_Pendientes.md) no se implementan en este runbook.

# Quagga2 local

Paquete: **@ericblade/quagga2 1.11.0**, fijado en package.json/package-lock.json.
Origen: https://github.com/ericblade/quagga2
Bundle sin modificar: dist/quagga.min.js, distribuido como quagga.min.js.
SHA-256: 77b9806c3f6033810f237d82af70b75faefc426d3a65716805b8a81c6157df4d.

Copyright (c) 2014 Christoph Oberhofer, (c) 2019 Eric Blade y colaboradores.
Licencia MIT original en LICENSE.

Regenerar con npm ci y npm run scanner:vendor. css:build también lo sincroniza.
El asset está versionado para dotnet build/publish sin Node, y Docker copia la
versión generada en su etapa Node. No se usa ningún CDN en producción.

Flujos, orientación, privacidad, heurísticas, cleanup y validación:
[Documentación técnica](../../../../../docs/29_BarcodeScanner.md).

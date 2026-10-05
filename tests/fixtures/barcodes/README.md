# Fixtures ópticos de Quagga2

Imágenes del repositorio oficial https://github.com/ericblade/quagga2,
commit de la versión npm 1.11.0: ee7ec971850a669c85c70c29b967d7ec57b7bebf.
Se incluye la licencia MIT original en LICENSE.

| Archivo local | Archivo original bajo test/fixtures/ | Código esperado |
| --- | --- | --- |
| ean.jpg | ean/image-001.jpg | 3574660239843 |
| ean_8.jpg | ean_8/image-001.jpg | 42191605 |
| upc.jpg | upc/image-001.jpg | 882428015268 (o representación EAN-13 0882428015268) |
| upc_e.jpg | upc_e/image-002.jpg | 04965802 |
| code_128.jpg | code_128/image-001.jpg | 0001285112001000040801 |
| no_code.jpg | no_code/image-001.jpg | Sin código |

Valores contrastados con test/integration/decoders del mismo commit y con el
número impreso. upc_e/image-001.jpg figura como allowFail en el upstream; se
elige image-002.jpg, que su propia suite exige decodificar. Esto no garantiza
lectura de todos los UPC-E reales.

La foto EAN-8 vertical es una regresión específica: ean_reader antes de
ean_8_reader devuelve 54086883 con el conjunto de readers habilitado.
El test debe exigir 42191605, sin flexibilizar esa expectativa.

Son fotos de laboratorio upstream; no son las fotos ni el código real de
producción del usuario. La prueba live usa barras EAN-13 sintéticas en un
canvas y no valida una cámara física.

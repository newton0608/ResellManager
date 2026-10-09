const solicitudes = new Map();

async function consultar(ruta, clave, permitir404 = false) {
    solicitudes.get(clave)?.abort();
    const controlador = new AbortController();
    solicitudes.set(clave, controlador);
    try {
        const respuesta = await fetch(new URL(ruta, document.baseURI), {
            headers: { Accept: "application/json" },
            credentials: "omit",
            cache: "no-store",
            signal: controlador.signal
        });
        if (permitir404 && respuesta.status === 404) return null;
        if (!respuesta.ok) throw new Error("No fue posible consultar el catálogo.");
        return await respuesta.json();
    } finally {
        if (solicitudes.get(clave) === controlador) solicitudes.delete(clave);
    }
}

export async function listar(termino, categoriaId, marca) {
    const parametros = new URLSearchParams();
    if (termino?.trim()) parametros.set("termino", termino.trim());
    if (categoriaId != null) parametros.set("categoriaId", String(categoriaId));
    if (marca?.trim()) parametros.set("marca", marca.trim());
    const query = parametros.toString();
    const productos = await consultar(`api/catalogo/productos${query ? `?${query}` : ""}`, "listado");
    if (!Array.isArray(productos)) throw new Error("Respuesta de catálogo no válida.");
    return productos;
}

export function detalle(productoId) {
    if (!Number.isSafeInteger(productoId)) throw new Error("Producto no válido.");
    return consultar(`api/catalogo/productos/${productoId}`, "detalle", true);
}

export function actualizarUrl(termino, categoriaId, marca) {
    const url = new URL(window.location.href);
    for (const [nombre, valor] of [["termino", termino?.trim()], ["categoriaId", categoriaId], ["marca", marca?.trim()]]) {
        if (valor == null || valor === "") url.searchParams.delete(nombre);
        else url.searchParams.set(nombre, String(valor));
    }
    window.history.replaceState(window.history.state, "", url);
}

// Nuevas lecturas acotadas. La ruta anterior conserva su contrato para otros consumidores.
function query(filtros = {}) {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(filtros)) {
        if (value != null && String(value).trim() !== '') params.set(key, String(value).trim());
    }
    return params.toString() ? `?${params}` : '';
}
export function pagina(termino, categoriaId, marca, cursor) {
    return consultar(`api/catalogo/productos/pagina${query({ termino, categoriaId, marca, cursor, tamano: 16 })}`, 'pagina');
}
export function escaparate(cursor) {
    return consultar(`api/catalogo/productos/escaparate${query({ cursor, tamano: 3 })}`, 'escaparate');
}
export function raices(cursor) {
    return consultar(`api/catalogo/productos/raices${query({ cursor, tamano: 16 })}`, 'raices');
}
export function marcas(cursor) {
    return consultar(`api/catalogo/productos/marcas${query({ cursor, tamano: 16 })}`, 'marcas');
}
export function contexto(categoriaId) {
    return consultar(`api/catalogo/productos/categorias/${categoriaId}/contexto`, 'contexto', true);
}
export function hijas(raizId, cursor) {
    return consultar(`api/catalogo/productos/categorias/${raizId}/hijas${query({ cursor, tamano: 16 })}`, 'hijas');
}

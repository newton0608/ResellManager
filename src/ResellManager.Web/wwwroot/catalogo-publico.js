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

export async function listar(termino, categoriaId) {
    const parametros = new URLSearchParams();
    if (termino?.trim()) parametros.set("termino", termino.trim());
    if (categoriaId != null) parametros.set("categoriaId", String(categoriaId));
    const query = parametros.toString();
    const productos = await consultar(`api/catalogo/productos${query ? `?${query}` : ""}`, "listado");
    if (!Array.isArray(productos)) throw new Error("Respuesta de catálogo no válida.");
    return productos;
}

export function detalle(productoId) {
    if (!Number.isSafeInteger(productoId)) throw new Error("Producto no válido.");
    return consultar(`api/catalogo/productos/${productoId}`, "detalle", true);
}

const revisiones = new WeakMap();

export function abrir(dialogo, referencia) {
    if (revisiones.has(dialogo)) return;
    const anterior = document.activeElement;
    const cancelar = evento => {
        evento.preventDefault();
        if (dialogo.dataset.ocupado !== "true")
            referencia.invokeMethodAsync("CancelarDesdeTeclado");
    };
    revisiones.set(dialogo, { anterior, cancelar });
    dialogo.addEventListener("cancel", cancelar);
    dialogo.showModal(); // Modal nativo: fondo inerte y foco contenido en el diálogo.
    dialogo.querySelector("button")?.focus();
}

export function cerrar(dialogo) {
    const estado = revisiones.get(dialogo);
    if (!estado) return;
    dialogo.removeEventListener("cancel", estado.cancelar);
    if (dialogo.open) dialogo.close();
    if (estado.anterior?.isConnected) estado.anterior.focus();
    revisiones.delete(dialogo);
}

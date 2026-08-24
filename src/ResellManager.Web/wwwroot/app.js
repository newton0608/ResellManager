window.resellManager = window.resellManager || {};

window.resellManager.cerrarMenuMovil = () => {
    const drawer = document.getElementById("navegacion-movil");

    if (drawer?.matches(":popover-open")) {
        drawer.hidePopover();
    }
};

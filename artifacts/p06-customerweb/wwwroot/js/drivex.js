document.addEventListener("DOMContentLoaded", () => {
    const shell = document.querySelector(".dx-shell");
    const toggle = document.querySelector("[data-dx-toggle]");
    const close = () => shell?.classList.remove("dx-sidebar-open");
    toggle?.addEventListener("click", () => shell?.classList.toggle("dx-sidebar-open"));
    document.querySelector("[data-dx-backdrop]")?.addEventListener("click", close);
    document.querySelectorAll(".dx-sidebar .dx-nav-link").forEach((link) => {
        link.addEventListener("click", close);
    });
});

(() => {
    const root = document.querySelector("[data-pay-page]");
    if (!root) return;

    const show = (name) => {
        root.querySelectorAll("[data-pay-state]").forEach((el) => {
            el.classList.toggle("is-on", el.getAttribute("data-pay-state") === name);
        });
        root.querySelector("[data-pay-form]")?.classList.toggle("is-off", name !== "form");
        root.querySelector("[data-pay-quote-cta]")?.classList.toggle("is-off", name !== "form");
    };

    root.querySelectorAll("[data-pay-method]").forEach((btn) => {
        btn.addEventListener("click", () => {
            root.querySelectorAll("[data-pay-method]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            const key = btn.getAttribute("data-pay-method");
            root.querySelectorAll("[data-pay-pane]").forEach((pane) => {
                pane.classList.toggle("is-on", pane.getAttribute("data-pay-pane") === key);
            });
        });
    });

    root.querySelectorAll("[data-pay-wallet]").forEach((btn) => {
        btn.addEventListener("click", () => {
            root.querySelectorAll("[data-pay-wallet]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
        });
    });

    root.querySelector("[data-pay-submit]")?.addEventListener("click", () => {
        show("processing");
        window.setTimeout(() => show("success"), 1400);
    });

    root.querySelectorAll("[data-pay-retry]").forEach((btn) => {
        btn.addEventListener("click", () => show("form"));
    });

    show(root.getAttribute("data-pay-view") || "form");
})();

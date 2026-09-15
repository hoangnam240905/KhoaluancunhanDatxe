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

    const resolveMethod = () => {
        const selected = root.querySelector("[data-pay-method].is-on")?.getAttribute("data-pay-method");
        if (selected === "wallet") {
            return root.querySelector("[data-pay-wallet].is-on")?.getAttribute("data-pay-wallet") || "MoMo";
        }
        return "BankTransfer";
    };

    const form = root.querySelector("[data-pay-real-form]");
    form?.addEventListener("submit", (event) => {
        if (form.getAttribute("data-pay-busy") === "1") {
            event.preventDefault();
            return;
        }
        form.setAttribute("data-pay-busy", "1");
        const methodInput = form.querySelector("[data-pay-method-input]");
        if (methodInput) methodInput.value = resolveMethod();
        show("processing");
        form.querySelectorAll("[data-pay-submit]").forEach((btn) => {
            if (!btn.getAttribute("data-pay-idle-label"))
                btn.setAttribute("data-pay-idle-label", btn.textContent.trim());
            btn.disabled = true;
            btn.textContent = "Đang xử lý...";
        });
    });

    root.querySelectorAll("[data-pay-retry]").forEach((btn) => {
        btn.addEventListener("click", () => {
            form?.removeAttribute("data-pay-busy");
            form?.querySelectorAll("[data-pay-submit]").forEach((submit) => {
                submit.disabled = false;
                const idle = submit.getAttribute("data-pay-idle-label");
                if (idle) submit.textContent = idle;
            });
            show("form");
        });
    });

    show(root.getAttribute("data-pay-view") || "form");
})();

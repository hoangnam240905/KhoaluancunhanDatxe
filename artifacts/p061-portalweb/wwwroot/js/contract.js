(() => {
    const root = document.querySelector("[data-ct-page]");
    if (!root) return;

    const paper = root.querySelector("[data-ct-paper]");
    const full = root.querySelector("[data-ct-full]");
    const fullDoc = root.querySelector("[data-ct-full-doc]");
    const agree = root.querySelector("[data-ct-agree]");
    const check = root.querySelector("[data-ct-check]");

    const labels = { issued: "Chờ ký", signed: "Đã ký", voided: "Đã hủy" };

    const show = (name) => {
        root.setAttribute("data-ct-view", name);
        root.querySelectorAll("[data-ct-state]").forEach((el) => {
            el.classList.toggle("is-on", el.getAttribute("data-ct-state") === name);
        });
        root.querySelectorAll("[data-ct-doc-status]").forEach((el) => {
            el.textContent = labels[name] || labels.issued;
        });
    };

    const openFull = () => {
        if (!paper || !full || !fullDoc) return;
        fullDoc.innerHTML = paper.innerHTML;
        full.classList.add("is-on");
        document.body.style.overflow = "hidden";
    };

    const closeFull = () => {
        full?.classList.remove("is-on");
        document.body.style.overflow = "";
    };

    root.querySelectorAll("[data-ct-preview]").forEach((btn) => {
        btn.addEventListener("click", openFull);
    });
    root.querySelectorAll("[data-ct-full-close]").forEach((btn) => {
        btn.addEventListener("click", closeFull);
    });
    full?.addEventListener("click", (e) => {
        if (e.target === full) closeFull();
    });

    root.querySelectorAll("[data-ct-sign]").forEach((btn) => {
        btn.addEventListener("click", () => {
            if (root.getAttribute("data-ct-view") === "voided") return;
            if (check && !check.checked) {
                agree?.classList.add("is-need");
                agree?.scrollIntoView({ behavior: "smooth", block: "center" });
                return;
            }
            show("signed");
            window.scrollTo({ top: 0, behavior: "smooth" });
        });
    });

    check?.addEventListener("change", () => {
        if (check.checked) agree?.classList.remove("is-need");
        root.querySelectorAll("[data-ct-sign]").forEach((btn) => {
            btn.toggleAttribute("disabled", !check.checked);
        });
    });

    show(root.getAttribute("data-ct-view") || "issued");
    if (check) {
        root.querySelectorAll("[data-ct-sign]").forEach((btn) => {
            btn.toggleAttribute("disabled", !check.checked);
        });
    }
})();

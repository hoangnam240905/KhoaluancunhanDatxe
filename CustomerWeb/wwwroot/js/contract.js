(() => {
    const root = document.querySelector("[data-ct-page]");
    if (!root) return;

    const paper = root.querySelector("[data-ct-paper]");
    const full = root.querySelector("[data-ct-full]");
    const fullDoc = root.querySelector("[data-ct-full-doc]");
    const agree = root.querySelector("[data-ct-agree]");
    const check = root.querySelector("[data-ct-check]");
    const signForm = root.querySelector("[data-ct-sign-form]");
    const issueForm = root.querySelector("[data-ct-issue-form]");

    const labels = { issued: "Chờ ký", signed: "Đã ký", voided: "Đã hủy", missing: "Chưa có hợp đồng" };

    const show = (name) => {
        root.setAttribute("data-ct-view", name);
        root.querySelectorAll("[data-ct-state]").forEach((el) => {
            el.classList.toggle("is-on", el.getAttribute("data-ct-state") === name);
        });
        root.querySelectorAll("[data-ct-doc-status]").forEach((el) => {
            el.textContent = labels[name] || labels.missing;
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

    const busyButtons = (selector, label) => {
        root.querySelectorAll(selector).forEach((btn) => {
            btn.disabled = true;
            btn.setAttribute("aria-busy", "true");
            if (label) btn.textContent = label;
        });
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

    signForm?.addEventListener("submit", (e) => {
        if (root.getAttribute("data-ct-view") !== "issued") {
            e.preventDefault();
            return;
        }
        if (check && !check.checked) {
            e.preventDefault();
            agree?.classList.add("is-need");
            agree?.scrollIntoView({ behavior: "smooth", block: "center" });
            return;
        }
        if (signForm.dataset.ctBusy === "1") {
            e.preventDefault();
            return;
        }
        signForm.dataset.ctBusy = "1";
        busyButtons("[data-ct-sign]", "Đang ký...");
    });

    issueForm?.addEventListener("submit", (e) => {
        if (issueForm.dataset.ctBusy === "1") {
            e.preventDefault();
            return;
        }
        issueForm.dataset.ctBusy = "1";
        busyButtons("[data-ct-issue]", "Đang lập...");
    });

    check?.addEventListener("change", () => {
        if (check.checked) agree?.classList.remove("is-need");
        root.querySelectorAll("[data-ct-sign]").forEach((btn) => {
            btn.toggleAttribute("disabled", !check.checked);
        });
    });

    show(root.getAttribute("data-ct-view") || "missing");
    if (check) {
        root.querySelectorAll("[data-ct-sign]").forEach((btn) => {
            btn.toggleAttribute("disabled", !check.checked);
        });
    }
})();

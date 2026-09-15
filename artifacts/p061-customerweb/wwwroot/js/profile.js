(() => {
    const root = document.querySelector("[data-pf-page]");
    if (!root) return;

    const wide = () => window.matchMedia("(min-width: 1025px)").matches;

    const setTab = (name) => {
        root.setAttribute("data-pf-tab", name);
        root.querySelectorAll("[data-pf-tab-btn]").forEach((btn) => {
            btn.classList.toggle("is-on", btn.getAttribute("data-pf-tab-btn") === name);
        });
        root.querySelectorAll("[data-pf-panel]").forEach((panel) => {
            panel.classList.toggle("is-on", panel.getAttribute("data-pf-panel") === name);
        });
        if (wide()) {
            root.querySelector(`[data-pf-panel="${name}"]`)?.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    };

    root.querySelectorAll("[data-pf-tab-btn]").forEach((btn) => {
        btn.addEventListener("click", () => setTab(btn.getAttribute("data-pf-tab-btn") || "info"));
    });

    const info = root.querySelector("[data-pf-info]");
    const fields = () => [...(info?.querySelectorAll("[data-pf-field]") || [])];
    let snapshot = fields().map((el) => el.value);
    const setEdit = (on) => {
        info?.classList.toggle("is-edit", on);
        fields().forEach((el) => { el.readOnly = !on; });
    };
    root.querySelector("[data-pf-edit]")?.addEventListener("click", () => {
        snapshot = fields().map((el) => el.value);
        setEdit(true);
    });
    root.querySelector("[data-pf-cancel]")?.addEventListener("click", () => {
        fields().forEach((el, i) => { el.value = snapshot[i] ?? el.value; });
        setEdit(false);
    });
    root.querySelector("[data-pf-save]")?.addEventListener("click", () => setEdit(false));

    const passPanel = root.querySelector("[data-pf-pass-panel]");
    const oldPwd = root.querySelector("[data-pf-old]");
    const newPwd = root.querySelector("[data-pf-new]");
    const confirmPwd = root.querySelector("[data-pf-confirm]");
    const strength = root.querySelector("[data-pf-strength]");
    const strengthLabel = root.querySelector("[data-pf-strength-label]");
    const passError = root.querySelector("[data-pf-pass-error]");

    const clearPass = () => {
        if (oldPwd) oldPwd.value = "";
        if (newPwd) newPwd.value = "";
        if (confirmPwd) confirmPwd.value = "";
        syncStrength();
        if (passError) {
            passError.textContent = "";
            passError.classList.remove("is-on");
        }
    };

    root.querySelector("[data-pf-password]")?.addEventListener("click", () => {
        passPanel?.classList.add("is-on");
        setTab("security");
    });
    root.querySelector("[data-pf-pass-cancel]")?.addEventListener("click", () => {
        passPanel?.classList.remove("is-on");
        clearPass();
    });

    const strong = (value) => {
        const lengthOk = value.length >= 8;
        const upperOk = /[A-Z]/.test(value);
        const specialOk = /[^a-zA-Z0-9]/.test(value);
        return { lengthOk, upperOk, specialOk, score: [lengthOk, upperOk, specialOk].filter(Boolean).length };
    };

    const syncStrength = () => {
        const value = newPwd?.value || "";
        const { lengthOk, upperOk, specialOk, score } = strong(value);
        if (strength) strength.className = "pf-strength" + (score ? " is-" + score : "");
        if (strengthLabel) {
            strengthLabel.className = "pf-strength-meta" + (score ? " is-" + score : "");
            strengthLabel.textContent = score === 3 ? "Mạnh" : score === 2 ? "Trung bình" : score === 1 ? "Yếu" : "";
        }
        root.querySelector("[data-pf-rule='length']")?.classList.toggle("is-ok", lengthOk);
        root.querySelector("[data-pf-rule='upper']")?.classList.toggle("is-ok", upperOk);
        root.querySelector("[data-pf-rule='special']")?.classList.toggle("is-ok", specialOk);
    };

    newPwd?.addEventListener("input", syncStrength);

    const showPassError = (text) => {
        if (!passError) return;
        passError.textContent = text;
        passError.classList.toggle("is-on", Boolean(text));
    };

    root.querySelector("[data-pf-pass-save]")?.addEventListener("click", () => {
        const next = newPwd?.value || "";
        const check = strong(next);
        if (!(oldPwd?.value || "").trim()) {
            showPassError("Vui lòng nhập mật khẩu hiện tại.");
            return;
        }
        if (check.score < 3) {
            showPassError("Mật khẩu phải có ít nhất 8 ký tự, 1 chữ hoa và 1 ký tự đặc biệt.");
            return;
        }
        if (next !== (confirmPwd?.value || "")) {
            showPassError("Xác nhận mật khẩu không khớp.");
            return;
        }
        showPassError("");
        passPanel?.classList.remove("is-on");
        clearPass();
    });

    root.querySelectorAll(".pf-eye").forEach((btn) => {
        btn.addEventListener("click", () => {
            const wrap = btn.closest(".pf-wrap");
            const input = wrap?.querySelector("input");
            const icon = btn.querySelector("i");
            if (!input) return;
            const show = input.type === "password";
            input.type = show ? "text" : "password";
            btn.setAttribute("aria-label", show ? "Ẩn mật khẩu" : "Hiện mật khẩu");
            if (icon) icon.className = show ? "bi bi-eye-slash" : "bi bi-eye";
        });
    });

    root.querySelectorAll("[data-pf-switch]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const on = !btn.classList.contains("is-on");
            btn.classList.toggle("is-on", on);
            btn.setAttribute("aria-checked", on ? "true" : "false");
        });
    });
})();

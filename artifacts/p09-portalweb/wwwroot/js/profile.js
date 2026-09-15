(() => {
    const root = document.querySelector("[data-pf-page]");
    if (!root) return;

    const wide = () => window.matchMedia("(min-width: 1025px)").matches;

    const setTab = (name, scroll) => {
        root.setAttribute("data-pf-tab", name);
        root.querySelectorAll("[data-pf-tab-btn]").forEach((btn) => {
            btn.classList.toggle("is-on", btn.getAttribute("data-pf-tab-btn") === name);
        });
        root.querySelectorAll("[data-pf-panel]").forEach((panel) => {
            panel.classList.toggle("is-on", panel.getAttribute("data-pf-panel") === name);
        });
        if (scroll && wide()) {
            root.querySelector(`[data-pf-panel="${name}"]`)?.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    };

    root.querySelectorAll("[data-pf-tab-btn]").forEach((btn) => {
        btn.addEventListener("click", () => setTab(btn.getAttribute("data-pf-tab-btn") || "info", true));
    });

    const profileError = root.querySelector("[data-pf-profile-error]");
    root.querySelector("[data-pf-edit]")?.addEventListener("click", () => {
        if (!profileError) return;
        profileError.textContent = profileError.getAttribute("data-pf-unsupported") || "";
        profileError.classList.toggle("is-on", Boolean(profileError.textContent));
    });

    const passForm = root.querySelector("[data-pf-pass-form]");
    const passPanel = root.querySelector("[data-pf-pass-panel]");
    const oldPwd = root.querySelector("[data-pf-old]");
    const newPwd = root.querySelector("[data-pf-new]");
    const confirmPwd = root.querySelector("[data-pf-confirm]");
    const strength = root.querySelector("[data-pf-strength]");
    const strengthLabel = root.querySelector("[data-pf-strength-label]");
    const passError = root.querySelector("[data-pf-pass-error]");
    const saveBtn = root.querySelector("[data-pf-pass-save]");
    const idleLabel = saveBtn?.textContent || "Cập nhật mật khẩu";
    const busyLabel = saveBtn?.getAttribute("data-pf-busy-label") || "Đang cập nhật…";

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
        setTab("security", true);
    });
    root.querySelector("[data-pf-pass-cancel]")?.addEventListener("click", () => {
        passPanel?.classList.remove("is-on");
        clearPass();
        if (saveBtn) {
            saveBtn.disabled = false;
            saveBtn.textContent = idleLabel;
        }
        if (passForm) passForm.dataset.pfBusy = "0";
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

    passForm?.addEventListener("submit", (event) => {
        if (passForm.dataset.pfBusy === "1") {
            event.preventDefault();
            return;
        }

        const next = newPwd?.value || "";
        const check = strong(next);
        if (!(oldPwd?.value || "").trim()) {
            event.preventDefault();
            showPassError("Vui lòng nhập mật khẩu hiện tại.");
            return;
        }
        if (check.score < 3) {
            event.preventDefault();
            showPassError("Mật khẩu phải có ít nhất 8 ký tự, 1 chữ hoa và 1 ký tự đặc biệt.");
            return;
        }
        if (next !== (confirmPwd?.value || "")) {
            event.preventDefault();
            showPassError("Xác nhận mật khẩu không khớp.");
            return;
        }

        showPassError("");
        passForm.dataset.pfBusy = "1";
        if (saveBtn) {
            saveBtn.disabled = true;
            saveBtn.textContent = busyLabel;
        }
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

    setTab(root.getAttribute("data-pf-tab") || "info", false);
    syncStrength();
})();

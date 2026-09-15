document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".ax-toggle").forEach((btn) => {
        btn.addEventListener("click", () => {
            const wrap = btn.closest(".ax-wrap");
            const input = wrap?.querySelector("input");
            const icon = btn.querySelector("i");
            if (!input) return;
            const show = input.type === "password";
            input.type = show ? "text" : "password";
            btn.setAttribute("aria-label", show ? "Ẩn mật khẩu" : "Hiện mật khẩu");
            if (icon) icon.className = show ? "bi bi-eye-slash" : "bi bi-eye";
        });
    });

    const pwd = document.getElementById("register-password") || document.getElementById("reset-password");
    const strength = document.querySelector("[data-ax-strength]");
    const strengthLabel = document.querySelector("[data-ax-strength-label]");
    const sync = () => {
        if (!pwd) return;
        const v = pwd.value;
        const lengthOk = v.length >= 8;
        const caseOk = /[A-Z]/.test(v);
        const specialOk = /[^a-zA-Z0-9]/.test(v);
        const score = [lengthOk, caseOk, specialOk].filter(Boolean).length;
        if (strength) strength.className = "ax-strength" + (score ? " is-" + score : "");
        if (strengthLabel) {
            strengthLabel.className = "ax-strength-meta" + (score ? " is-" + score : "");
            strengthLabel.textContent = score === 3 ? "Mạnh" : score === 2 ? "Trung bình" : score === 1 ? "Yếu" : "";
        }
        document.getElementById("rule-length")?.classList.toggle("valid", lengthOk);
        document.getElementById("rule-upper")?.classList.toggle("valid", caseOk && /[a-z]/.test(v) && /\d/.test(v));
        document.getElementById("rule-special")?.classList.toggle("valid", specialOk);
    };
    pwd?.addEventListener("input", sync);
    sync();

    document.querySelectorAll("form[data-ax-form]").forEach((form) => {
        form.addEventListener("submit", () => {
            const btn = form.querySelector("[data-ax-submit]");
            if (btn) btn.classList.add("is-loading");
        });
    });

    document.querySelectorAll(".ax-terms input").forEach((box) => {
        const msg = "Vui lòng đồng ý với Điều khoản dịch vụ và Chính sách bảo mật.";
        box.addEventListener("invalid", () => box.setCustomValidity(msg));
        box.addEventListener("change", () => box.setCustomValidity(""));
        box.addEventListener("input", () => box.setCustomValidity(""));
    });

    document.querySelectorAll("[data-ax-otp]").forEach((wrap) => {
        const hidden = wrap.parentElement?.querySelector("[data-ax-otp-value]");
        const boxes = [...wrap.querySelectorAll("input")];
        if (!hidden || boxes.length !== 6) return;

        const write = () => {
            hidden.value = boxes.map((b) => b.value.replace(/\D/g, "").slice(0, 1)).join("");
            wrap.classList.toggle("is-error", hidden.classList.contains("input-validation-error") && hidden.value.length !== 6);
        };

        const fill = (digits) => {
            const chars = digits.replace(/\D/g, "").slice(0, 6).split("");
            boxes.forEach((box, i) => { box.value = chars[i] || ""; });
            write();
            const next = boxes[Math.min(chars.length, 5)];
            next?.focus();
        };

        fill(hidden.value || "");

        boxes.forEach((box, index) => {
            box.addEventListener("input", (e) => {
                const input = e.target;
                const raw = input.value.replace(/\D/g, "");
                if (raw.length > 1) {
                    fill((hidden.value.slice(0, index) + raw).slice(0, 6));
                    return;
                }
                input.value = raw.slice(0, 1);
                write();
                if (input.value && boxes[index + 1]) boxes[index + 1].focus();
            });
            box.addEventListener("keydown", (e) => {
                if (e.key === "Backspace" && !box.value && boxes[index - 1]) {
                    boxes[index - 1].focus();
                    boxes[index - 1].value = "";
                    write();
                    e.preventDefault();
                }
            });
            box.addEventListener("paste", (e) => {
                e.preventDefault();
                fill((e.clipboardData || window.clipboardData).getData("text") || "");
            });
        });

        wrap.closest("form")?.addEventListener("submit", write);
    });
});

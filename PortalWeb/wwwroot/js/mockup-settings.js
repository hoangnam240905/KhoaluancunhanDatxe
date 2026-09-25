(function () {
    "use strict";

    const boot = window.__SETTINGS_BOOTSTRAP__ || {};
    const handlers = window.__SETTINGS_HANDLERS__ || {};
    const caps = boot.capabilities || {};

    const state = {
        profile: {
            fullName: "",
            role: "",
            roleLabel: "",
            email: "",
            phone: "",
            code: "—",
            department: "—",
            joined: "—"
        },
        notifications: {
            bookingNew: true,
            vehicleStatus: true,
            driverStatus: true,
            scheduleConflict: true,
            maintenance: true,
            payment: false,
            inspection: true,
            browserPush: true,
            sound: false
        },
        appearance: {
            theme: "light",
            density: "standard",
            language: "Tiếng Việt"
        },
        security: {
            twoFactor: false
        },
        sessions: [
            {
                id: 1,
                device: "Chrome trên Windows",
                location: "TP. Hồ Chí Minh, Việt Nam",
                time: "Hiện tại",
                current: true,
                active: true
            },
            {
                id: 2,
                device: "Chrome trên Windows",
                location: "TP. Hồ Chí Minh, Việt Nam",
                time: "Hôm qua, 21:42",
                current: false,
                active: true
            }
        ]
    };

    if (boot.profile) {
        state.profile.fullName = boot.profile.fullName || "";
        state.profile.email = boot.profile.email || "";
        state.profile.phone = boot.profile.phone || "";
        state.profile.role = boot.profile.role || "";
        state.profile.roleLabel = boot.profile.roleLabel || boot.profile.role || "—";
        state.profile.code = "—";
        state.profile.department = "—";
        state.profile.joined = "—";
    }

    let profileSnapshot = clone(state.profile);
    let modalAction = null;

    const notifyDefs = [
        { key: "bookingNew", icon: "🔔", title: "Đơn thuê mới", desc: "Nhận thông báo khi có đơn thuê mới cần xử lý" },
        { key: "vehicleStatus", icon: "🚗", title: "Thay đổi trạng thái xe", desc: "Thông báo khi xe thay đổi trạng thái" },
        { key: "driverStatus", icon: "👤", title: "Thay đổi trạng thái tài xế", desc: "Thông báo khi tài xế nhận hoặc hoàn thành chuyến" },
        { key: "scheduleConflict", icon: "⚠️", title: "Cảnh báo xung đột lịch", desc: "Thông báo khi phát hiện xung đột lịch xe hoặc tài xế" },
        { key: "maintenance", icon: "🔧", title: "Cảnh báo bảo trì", desc: "Thông báo khi xe đến hạn kiểm tra hoặc bảo trì" },
        { key: "payment", icon: "💳", title: "Thanh toán", desc: "Thông báo về trạng thái thanh toán tiền cọc" },
        { key: "inspection", icon: "📋", title: "Kiểm xe", desc: "Thông báo khi có biên bản kiểm xe cần xem" }
    ];

    const extraDefs = [
        { key: "browserPush", icon: "🖥️", title: "Nhận thông báo trên trình duyệt", desc: "Hiển thị thông báo ngay trên trình duyệt khi đang làm việc" },
        { key: "sound", icon: "🔊", title: "Âm thanh thông báo", desc: "Phát âm thanh ngắn khi có thông báo mới" }
    ];

    const els = {
        nav: document.getElementById("stNav"),
        navSelect: document.getElementById("stNavSelect"),
        panels: document.querySelectorAll(".st-panel"),
        fullName: document.getElementById("stFullName"),
        phone: document.getElementById("stPhone"),
        email: document.getElementById("stEmail"),
        role: document.getElementById("stRole"),
        code: document.getElementById("stCode"),
        dept: document.getElementById("stDept"),
        joined: document.getElementById("stJoined"),
        displayName: document.getElementById("stDisplayName"),
        avatar: document.getElementById("stAvatar"),
        roleBadge: document.getElementById("stRoleBadge"),
        codeLabel: document.getElementById("stCodeLabel"),
        profileForm: document.getElementById("stProfileForm"),
        profileCancel: document.getElementById("stProfileCancel"),
        profileError: document.getElementById("stProfileError"),
        notifyList: document.getElementById("stNotifyList"),
        notifyExtra: document.getElementById("stNotifyExtra"),
        notifySave: document.getElementById("stNotifySave"),
        appearanceSave: document.getElementById("stAppearanceSave"),
        passwordForm: document.getElementById("stPasswordForm"),
        pwdCurrent: document.getElementById("stPwdCurrent"),
        pwdNew: document.getElementById("stPwdNew"),
        pwdConfirm: document.getElementById("stPwdConfirm"),
        pwdError: document.getElementById("stPwdError"),
        twoFaStatus: document.getElementById("stTwoFaStatus"),
        twoFaBtn: document.getElementById("stTwoFaBtn"),
        sessionList: document.getElementById("stSessionList"),
        logoutAll: document.getElementById("stLogoutAll"),
        modal: document.getElementById("stModal"),
        modalTitle: document.getElementById("stModalTitle"),
        modalBody: document.getElementById("stModalBody"),
        modalOk: document.getElementById("stModalOk"),
        modalCancel: document.getElementById("stModalCancel")
    };

    function clone(obj) {
        return JSON.parse(JSON.stringify(obj));
    }

    function toast(msg) {
        const el = document.getElementById("mkGlobalToast") || document.getElementById("mkToast");
        if (!el) return;
        el.textContent = msg || "";
        el.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => el.classList.remove("is-on"), 3200);
    }

    function antiforgery() {
        const input = document.querySelector("#stAntiForgery input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    function initials(name) {
        const parts = String(name || "").trim().split(/\s+/).filter(Boolean);
        if (!parts.length) return "—";
        if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
        return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }

    function showSection(key) {
        els.panels.forEach((p) => {
            const on = p.dataset.panel === key;
            p.classList.toggle("is-on", on);
            p.hidden = !on;
        });
        els.nav?.querySelectorAll(".st-nav-btn").forEach((btn) => {
            btn.classList.toggle("is-on", btn.dataset.section === key);
        });
        if (els.navSelect) els.navSelect.value = key;
    }

    function fillProfile() {
        const p = state.profile;
        if (els.fullName) els.fullName.value = p.fullName;
        if (els.phone) els.phone.value = p.phone;
        if (els.email) els.email.value = p.email;
        if (els.role) els.role.value = p.roleLabel || p.role || "—";
        if (els.code) els.code.value = p.code;
        if (els.dept) els.dept.value = p.department;
        if (els.joined) els.joined.value = p.joined;
        if (els.displayName) els.displayName.textContent = p.fullName || "—";
        if (els.codeLabel) els.codeLabel.textContent = p.code;
        if (els.avatar) els.avatar.textContent = initials(p.fullName);
        if (els.roleBadge) els.roleBadge.textContent = p.roleLabel || p.role || "—";
        if (els.profileError) {
            els.profileError.hidden = true;
            els.profileError.textContent = "";
        }
    }

    function toggleRow(def, checked) {
        return `<label class="st-toggle-row">
            <span>
                <strong>${def.icon} ${def.title}</strong>
                <p>${def.desc}</p>
            </span>
            <span class="st-switch">
                <input type="checkbox" data-pref="${def.key}" ${checked ? "checked" : ""} />
                <span></span>
            </span>
        </label>`;
    }

    function renderNotifications() {
        if (els.notifyList) {
            els.notifyList.innerHTML = notifyDefs
                .map((d) => toggleRow(d, !!state.notifications[d.key]))
                .join("");
        }
        if (els.notifyExtra) {
            els.notifyExtra.innerHTML = extraDefs
                .map((d) => toggleRow(d, !!state.notifications[d.key]))
                .join("");
        }
    }

    function fillAppearance() {
        document.querySelectorAll('input[name="stTheme"]').forEach((r) => {
            r.checked = r.value === state.appearance.theme;
        });
        document.querySelectorAll('input[name="stDensity"]').forEach((r) => {
            r.checked = r.value === state.appearance.density;
        });
    }

    function renderTwoFa() {
        const on = state.security.twoFactor;
        if (els.twoFaStatus) {
            els.twoFaStatus.textContent = on ? "Đang bật" : "Đang tắt";
            els.twoFaStatus.classList.toggle("is-on", on);
            els.twoFaStatus.classList.toggle("is-off", !on);
        }
        if (els.twoFaBtn) {
            els.twoFaBtn.textContent = on ? "Tắt xác thực hai bước" : "Bật xác thực hai bước";
        }
    }

    function renderSessions() {
        if (!els.sessionList) return;
        if (!state.sessions.length) {
            els.sessionList.innerHTML = `<div class="mk-empty">Không còn phiên đăng nhập nào.</div>`;
            return;
        }
        els.sessionList.innerHTML = state.sessions.map((s) => `
            <div class="st-session ${s.current ? "is-current" : ""}">
                <div class="st-session-meta">
                    <strong>${s.device}</strong>
                    <p class="mk-muted">Vị trí: ${s.location}</p>
                    <p class="mk-muted">Thời gian: ${s.time}</p>
                    <span class="mk-badge ${s.current ? "mk-badge-assigned" : "mk-badge-ok"}">
                        ${s.current ? "Phiên hiện tại" : "Đang hoạt động"}
                    </span>
                </div>
                ${s.current
                    ? ""
                    : `<button type="button" class="mk-btn mk-btn-ghost" data-logout-session="${s.id}">Đăng xuất phiên này</button>`}
            </div>`).join("");
    }

    function openModal(title, body, action) {
        modalAction = action;
        els.modalTitle.textContent = title;
        els.modalBody.textContent = body;
        els.modal.hidden = false;
    }

    function closeModal() {
        modalAction = null;
        els.modal.hidden = true;
    }

    function validateEmail(v) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
    }

    function validateStrongPassword(pwd) {
        if (!pwd || pwd.length < 8) return "Mật khẩu phải có ít nhất 8 ký tự.";
        if (!/[A-Z]/.test(pwd)) return "Mật khẩu phải có ít nhất 1 chữ hoa (A-Z).";
        if (!/[^a-zA-Z0-9]/.test(pwd)) return "Mật khẩu phải có ít nhất 1 ký tự đặc biệt (!@#$...).";
        return null;
    }

    async function postChangePassword(oldPassword, newPassword, confirmPassword) {
        const url = handlers.changePassword;
        if (!url) return { ok: false, error: "Thiếu handler đổi mật khẩu." };
        const body = new URLSearchParams();
        body.set("oldPassword", oldPassword);
        body.set("newPassword", newPassword);
        body.set("confirmPassword", confirmPassword);
        body.set("__RequestVerificationToken", antiforgery());
        const res = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                RequestVerificationToken: antiforgery(),
                "Content-Type": "application/x-www-form-urlencoded"
            },
            body: body.toString()
        });
        let data = null;
        try {
            data = await res.json();
        } catch {
            data = null;
        }
        if (!res.ok || !data || !data.ok) {
            return { ok: false, error: (data && data.error) || "Không thể đổi mật khẩu." };
        }
        return { ok: true, message: data.message || "Đã đổi mật khẩu." };
    }

    // ——— Events ———
    els.nav?.addEventListener("click", (e) => {
        const btn = e.target.closest("[data-section]");
        if (!btn) return;
        showSection(btn.dataset.section);
    });

    els.navSelect?.addEventListener("change", () => showSection(els.navSelect.value));

    els.profileForm?.addEventListener("submit", (e) => {
        e.preventDefault();
        if (!caps.profileUpdate) {
            els.profileError.hidden = false;
            els.profileError.textContent = "Không thể lưu — hệ thống chưa có API cập nhật hồ sơ.";
            toast("Không thể lưu — hệ thống chưa có API cập nhật hồ sơ.");
            return;
        }
        const fullName = els.fullName.value.trim();
        const phone = els.phone.value.trim();
        const email = els.email.value.trim();
        if (!fullName || !phone || !email) {
            els.profileError.hidden = false;
            els.profileError.textContent = "Vui lòng điền đầy đủ họ tên, số điện thoại và email.";
            return;
        }
        if (!validateEmail(email)) {
            els.profileError.hidden = false;
            els.profileError.textContent = "Email không hợp lệ.";
            return;
        }
        state.profile.fullName = fullName;
        state.profile.phone = phone;
        state.profile.email = email;
        profileSnapshot = clone(state.profile);
        fillProfile();
        toast("Đã lưu thay đổi thông tin cá nhân");
    });

    els.profileCancel?.addEventListener("click", () => {
        state.profile = clone(profileSnapshot);
        fillProfile();
        toast("Đã hủy thay đổi thông tin cá nhân");
    });

    els.notifyList?.addEventListener("change", (e) => {
        const input = e.target.closest("[data-pref]");
        if (!input) return;
        state.notifications[input.dataset.pref] = input.checked;
    });
    els.notifyExtra?.addEventListener("change", (e) => {
        const input = e.target.closest("[data-pref]");
        if (!input) return;
        state.notifications[input.dataset.pref] = input.checked;
    });

    els.notifySave?.addEventListener("click", () => {
        toast("Không thể lưu — hệ thống chưa có API tùy chọn thông báo.");
    });

    els.appearanceSave?.addEventListener("click", () => {
        const theme = document.querySelector('input[name="stTheme"]:checked')?.value || "light";
        const density = document.querySelector('input[name="stDensity"]:checked')?.value || "standard";
        state.appearance.theme = theme;
        state.appearance.density = density;
        toast("Đã ghi nhận trên giao diện (mô phỏng) — chưa có API lưu tùy chọn giao diện.");
    });

    els.passwordForm?.addEventListener("submit", async (e) => {
        e.preventDefault();
        const cur = els.pwdCurrent.value;
        const neu = els.pwdNew.value;
        const conf = els.pwdConfirm.value;
        if (!cur || !neu || !conf) {
            els.pwdError.hidden = false;
            els.pwdError.textContent = "Vui lòng điền đầy đủ các trường mật khẩu.";
            return;
        }
        const strongErr = validateStrongPassword(neu);
        if (strongErr) {
            els.pwdError.hidden = false;
            els.pwdError.textContent = strongErr;
            return;
        }
        if (neu !== conf) {
            els.pwdError.hidden = false;
            els.pwdError.textContent = "Xác nhận mật khẩu mới không khớp.";
            return;
        }
        els.pwdError.hidden = true;
        els.pwdError.textContent = "";

        const submitBtn = els.passwordForm.querySelector('button[type="submit"]');
        if (submitBtn) submitBtn.disabled = true;
        try {
            const result = await postChangePassword(cur, neu, conf);
            if (!result.ok) {
                els.pwdError.hidden = false;
                els.pwdError.textContent = result.error;
                return;
            }
            els.passwordForm.reset();
            toast(result.message);
        } catch {
            els.pwdError.hidden = false;
            els.pwdError.textContent = "Không thể đổi mật khẩu. Vui lòng thử lại.";
        } finally {
            if (submitBtn) submitBtn.disabled = false;
        }
    });

    els.twoFaBtn?.addEventListener("click", () => {
        if (!state.security.twoFactor) {
            openModal(
                "Bật xác thực hai bước",
                "Bạn sắp bật xác thực hai bước (mô phỏng). Không gửi OTP thật và không đổi bảo mật hệ thống.",
                "enable2fa"
            );
            return;
        }
        openModal(
            "Tắt xác thực hai bước",
            "Bạn sắp tắt xác thực hai bước (mô phỏng). Thao tác này không ảnh hưởng tài khoản thật.",
            "disable2fa"
        );
    });

    els.sessionList?.addEventListener("click", (e) => {
        const btn = e.target.closest("[data-logout-session]");
        if (!btn) return;
        const id = Number(btn.dataset.logoutSession);
        openModal(
            "Đăng xuất phiên",
            "Bạn có chắc muốn đăng xuất phiên này? (mô phỏng — không đăng xuất thật)",
            { type: "logoutOne", id }
        );
    });

    els.logoutAll?.addEventListener("click", () => {
        openModal(
            "Đăng xuất tất cả thiết bị",
            "Bạn có chắc muốn đăng xuất khỏi tất cả thiết bị? Phiên hiện tại cũng sẽ được đăng xuất (mô phỏng).",
            "logoutAll"
        );
    });

    els.modalOk?.addEventListener("click", () => {
        const action = modalAction;
        closeModal();
        if (!action) return;
        if (action === "enable2fa") {
            state.security.twoFactor = true;
            renderTwoFa();
            toast("Đã bật xác thực hai bước (mô phỏng)");
            return;
        }
        if (action === "disable2fa") {
            state.security.twoFactor = false;
            renderTwoFa();
            toast("Đã tắt xác thực hai bước (mô phỏng)");
            return;
        }
        if (action === "logoutAll") {
            state.sessions = state.sessions.filter((s) => s.current);
            renderSessions();
            toast("Đã đăng xuất tất cả phiên khác (mô phỏng)");
            return;
        }
        if (action.type === "logoutOne") {
            state.sessions = state.sessions.filter((s) => s.id !== action.id);
            renderSessions();
            toast("Đã đăng xuất phiên (mô phỏng)");
        }
    });

    els.modalCancel?.addEventListener("click", closeModal);
    els.modal?.addEventListener("click", (e) => {
        if (e.target === els.modal) closeModal();
    });

    fillProfile();
    renderNotifications();
    fillAppearance();
    renderTwoFa();
    renderSessions();
    showSection("profile");
})();

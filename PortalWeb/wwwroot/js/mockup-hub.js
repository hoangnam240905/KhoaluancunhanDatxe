(function () {
    const STATUS_LABEL = {
        Pending: "Chờ xác nhận",
        Confirmed: "Đã xác nhận",
        Assigned: "Đã phân công",
        InProgress: "Đang thực hiện",
        Completed: "Hoàn thành",
        Cancelled: "Đã hủy"
    };
    const MODE_LABEL = { SelfDrive: "Tự lái", WithDriver: "Có tài xế" };
    const TYPE_LABEL = { Sedan: "Sedan", SUV: "SUV", MPV: "MPV", Luxury: "Cao cấp" };
    const CONTRACT_LABEL = { none: "Chưa ký", issued: "Đã lập", signed: "Đã ký" };
    const DEPOSIT_LABEL = { none: "Chưa cọc", pending: "Chờ cọc", paid: "Đã cọc", failed: "Cọc lỗi" };

    const boot = window.__HUB_BOOTSTRAP__ || { bookings: [], handlers: {} };
    let bookings = Array.isArray(boot.bookings) ? boot.bookings.map((b) => ({ ...b })) : [];
    let selectedId = null;
    let busy = false;
    const filters = { q: "", status: "", mode: "", type: "", from: "", to: "", queue: "" };

    const els = {
        form: document.getElementById("mkFilterForm"),
        table: document.getElementById("mkTableBody"),
        cards: document.getElementById("mkCards"),
        empty: document.getElementById("mkEmpty"),
        pills: document.getElementById("mkPills"),
        drawer: document.getElementById("mkDrawer"),
        toast: document.getElementById("mkToast"),
        assignNote: document.getElementById("mkAssignNote"),
        reset: document.getElementById("mkReset"),
        metrics: document.getElementById("mkMetrics")
    };

    function antiforgery() {
        const input = document.querySelector("#mkHubAntiForgery input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    function badge(status) {
        return `<span class="mk-badge mk-badge-${String(status || "").toLowerCase()}">${STATUS_LABEL[status] || status}</span>`;
    }
    function contractBadge(v) {
        const cls = v === "signed" ? "ok" : v === "issued" ? "warn" : "gray";
        return `<span class="mk-badge mk-badge-${cls}">${CONTRACT_LABEL[v] || CONTRACT_LABEL.none}</span>`;
    }
    function depositBadge(v) {
        const cls = v === "paid" ? "ok" : v === "pending" || v === "failed" ? "warn" : "gray";
        return `<span class="mk-badge mk-badge-${cls}">${DEPOSIT_LABEL[v] || DEPOSIT_LABEL.none}</span>`;
    }
    function holdText(b) {
        if (b.status === "Assigned" || b.status === "InProgress") return `<div class="mk-hold held">Xe đã giữ · Đã phân công</div>`;
        if (b.status !== "Confirmed") return "";
        const veh = b.vehicleHeld ? "Xe đã giữ" : "Xe chưa giữ";
        const drv = b.mode === "WithDriver" && !b.driverAssigned ? " · Tài xế chưa phân công" : "";
        return `<div class="mk-hold ${b.vehicleHeld ? "held" : "free"}">${veh}${drv}</div>`;
    }

    function visible() {
        return bookings.filter((b) => {
            if (filters.queue === "assign" && b.status !== "Confirmed") return false;
            if (filters.status && b.status !== filters.status) return false;
            if (filters.mode && b.mode !== filters.mode) return false;
            if (filters.type) {
                const t = (b.type || "").toLowerCase();
                const want = filters.type.toLowerCase();
                if (!t.includes(want) && t !== want) return false;
            }
            if (filters.from && b.endIso < filters.from) return false;
            if (filters.to && b.startIso > filters.to) return false;
            if (filters.q) {
                const q = filters.q.toLowerCase();
                const blob = `${b.id} ${b.customer} ${b.phone} ${b.vehicle} ${b.type} ${b.plate}`.toLowerCase();
                if (!blob.includes(q) && !(`#${b.id}`.includes(q))) return false;
            }
            return true;
        });
    }

    function countStatus(status) {
        return bookings.filter((b) => b.status === status).length;
    }

    function updateMetrics() {
        if (!els.metrics) return;
        const map = {
            Pending: countStatus("Pending"),
            Confirmed: countStatus("Confirmed"),
            assign: bookings.filter((b) => b.status === "Confirmed").length,
            InProgress: countStatus("InProgress")
        };
        els.metrics.querySelectorAll(".mk-metric").forEach((btn) => {
            const key = btn.dataset.queue === "assign" ? "assign" : btn.dataset.filterStatus;
            const strong = btn.querySelector("strong");
            if (strong && key && map[key] !== undefined) strong.textContent = String(map[key]);
            btn.classList.toggle("is-on",
                (btn.dataset.queue && filters.queue === btn.dataset.queue) ||
                (btn.dataset.filterStatus && filters.queue !== "assign" && filters.status === btn.dataset.filterStatus)
            );
        });
    }

    function actions(b) {
        const bits = [`<button type="button" class="mk-btn mk-btn-ghost" data-act="detail" data-id="${b.id}">👁 Xem chi tiết</button>`];
        if (b.status === "Pending") {
            bits.push(`<button type="button" class="mk-btn mk-btn-success" data-act="confirm" data-id="${b.id}">✓ Xác nhận đơn</button>`);
            bits.push(`<button type="button" class="mk-btn mk-btn-danger" data-act="reject" data-id="${b.id}">✕ Không duyệt</button>`);
        }
        if (b.status === "Confirmed") {
            bits.push(`<button type="button" class="mk-btn mk-btn-primary" data-act="assign" data-id="${b.id}">Phân công xe & tài xế</button>`);
        }
        if (b.status === "Assigned") {
            bits.push(`<button type="button" class="mk-btn mk-btn-info" data-act="handover" data-id="${b.id}">Giao xe</button>`);
        }
        if (b.status === "InProgress") {
            bits.push(`<button type="button" class="mk-btn mk-btn-success" data-act="complete" data-id="${b.id}">Tiếp nhận trả xe</button>`);
        }
        return `<div class="mk-actions">${bits.join("")}</div>`;
    }

    function renderPills() {
        const items = [
            ["", "Tất cả"],
            ["Pending", "Chờ xác nhận"],
            ["Confirmed", "Đã xác nhận"],
            ["Assigned", "Đã phân công"],
            ["InProgress", "Đang thực hiện"],
            ["Completed", "Hoàn thành"],
            ["Cancelled", "Đã hủy"]
        ];
        els.pills.innerHTML = items.map(([v, label]) =>
            `<button type="button" class="mk-pill ${filters.queue !== "assign" && filters.status === v ? "is-on" : ""}" data-status="${v}">${label}</button>`
        ).join("") + `<button type="button" class="mk-pill ${filters.queue === "assign" ? "is-on" : ""}" data-queue="assign">🚦 Cần phân công</button>`;
    }

    function renderList() {
        const rows = visible();
        els.empty.hidden = rows.length > 0;
        els.assignNote.hidden = filters.queue !== "assign";
        updateMetrics();
        els.table.innerHTML = rows.map((b) => `
            <tr class="${selectedId === b.id ? "is-on" : ""}">
                <td><strong>#${b.id}</strong><div class="mk-muted">${b.created}</div></td>
                <td><strong>${escapeHtml(b.customer)}</strong><div class="mk-muted">${escapeHtml(b.phone)}</div></td>
                <td>${MODE_LABEL[b.mode] || b.mode}</td>
                <td>${escapeHtml(b.vehicle)}<div class="mk-muted">${TYPE_LABEL[b.type] || escapeHtml(b.type)} · ${escapeHtml(b.plate)}</div>${holdText(b)}</td>
                <td>${b.start}<div class="mk-muted">→ ${b.end}</div></td>
                <td>${contractBadge(b.contract)}</td>
                <td>${depositBadge(b.deposit)}</td>
                <td>${badge(b.status)}</td>
                <td>${actions(b)}</td>
            </tr>`).join("");
        els.cards.innerHTML = rows.map((b) => `
            <article class="mk-card mk-booking-card ${selectedId === b.id ? "is-on" : ""}">
                <div class="d-flex justify-content-between gap-2"><strong>#${b.id} · ${escapeHtml(b.customer)}</strong>${badge(b.status)}</div>
                <p class="mb-1">${MODE_LABEL[b.mode] || b.mode} · ${escapeHtml(b.vehicle)}</p>
                <p class="mk-muted mb-2">${b.start} → ${b.end}</p>
                ${holdText(b)}
                ${actions(b)}
            </article>`).join("");
        renderPills();
        if (selectedId) {
            const current = bookings.find((b) => b.id === selectedId);
            if (current) renderDrawer(current);
        }
    }

    function escapeHtml(s) {
        return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    }

    function timeline(b) {
        const steps = [
            ["Tạo yêu cầu", true, b.status === "Pending"],
            ["Xác nhận", ["Confirmed", "Assigned", "InProgress", "Completed"].includes(b.status), b.status === "Pending"],
            ["Ký hợp đồng", b.contract === "signed", b.status === "Confirmed" && b.contract !== "signed"],
            ["Thanh toán cọc", b.deposit === "paid", b.status === "Confirmed" && b.deposit !== "paid"],
            ["Phân công", ["Assigned", "InProgress", "Completed"].includes(b.status), b.status === "Confirmed"],
            ["Đang thực hiện", ["InProgress", "Completed"].includes(b.status), b.status === "Assigned"],
            ["Hoàn thành", b.status === "Completed", b.status === "InProgress"]
        ];
        if (b.status === "Cancelled") {
            return `<ol class="mk-timeline">
                <li class="is-done">① Tạo yêu cầu</li>
                <li>② Xác nhận</li>
                <li class="is-current">Đã hủy</li>
            </ol>`;
        }
        return `<ol class="mk-timeline">${steps.map((s, i) =>
            `<li class="${s[1] ? "is-done" : ""} ${s[2] ? "is-current" : ""}">${["①", "②", "③", "④", "⑤", "⑥", "⑦"][i]} ${s[0]}</li>`
        ).join("")}</ol>`;
    }

    function renderDrawer(b) {
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-2">Đơn #${b.id}</h2>
                    ${badge(b.status)}
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-act="close">Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <section class="mk-block">
                    <h3>Thông tin khách hàng</h3>
                    <dl class="mk-dl">
                        <div><dt>Họ tên</dt><dd>${escapeHtml(b.customer)}</dd></div>
                        <div><dt>Số điện thoại</dt><dd>${escapeHtml(b.phone)}</dd></div>
                        <div><dt>Email</dt><dd>${escapeHtml(b.email)}</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Thông tin đơn</h3>
                    <dl class="mk-dl">
                        <div><dt>Mã đơn</dt><dd>#${b.id}</dd></div>
                        <div><dt>Hình thức thuê</dt><dd>${MODE_LABEL[b.mode] || b.mode}</dd></div>
                        <div><dt>Xe</dt><dd>${escapeHtml(b.vehicle)} · ${escapeHtml(b.plate)}</dd></div>
                        <div><dt>Loại xe</dt><dd>${TYPE_LABEL[b.type] || escapeHtml(b.type)}</dd></div>
                        <div><dt>Ngày nhận</dt><dd>${b.start}</dd></div>
                        <div><dt>Ngày trả</dt><dd>${b.end}</dd></div>
                        <div><dt>Điểm nhận</dt><dd>${escapeHtml(b.pickup)}</dd></div>
                        <div><dt>Điểm trả</dt><dd>${escapeHtml(b.dropoff)}</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Tài chính</h3>
                    <dl class="mk-dl">
                        <div><dt>Giá thuê</dt><dd>${b.price} VNĐ</dd></div>
                        <div><dt>Tiền cọc</dt><dd>${b.depositAmt} VNĐ</dd></div>
                        <div><dt>Tổng tiền</dt><dd>${b.total} VNĐ</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Hợp đồng / Thanh toán</h3>
                    <dl class="mk-dl">
                        <div><dt>Hợp đồng</dt><dd>${CONTRACT_LABEL[b.contract] || CONTRACT_LABEL.none}</dd></div>
                        <div><dt>Trạng thái cọc</dt><dd>${DEPOSIT_LABEL[b.deposit] || DEPOSIT_LABEL.none}</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Kiểm tra khả năng phục vụ</h3>
                    <p class="mk-note">Xác nhận / phân công dùng API hệ thống; khối kiểm tra minh họa dưới đây chưa có endpoint riêng.</p>
                    <ul class="mk-checks">
                        <li>✅ Xe khả dụng</li>
                        <li>✅ Tài xế khả dụng</li>
                        <li>✅ Không có xung đột lịch</li>
                        <li>✅ Xe không đến hạn bảo trì</li>
                        <li>✅ Đủ điều kiện xác nhận</li>
                    </ul>
                </section>
                <section class="mk-block">
                    <h3>Tiến trình</h3>
                    ${timeline(b)}
                </section>
                ${actions(b)}
            </div>`;
    }

    function toast(msg) {
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 3200);
    }

    function upsertBooking(row) {
        const i = bookings.findIndex((x) => x.id === row.id);
        if (i >= 0) bookings[i] = { ...bookings[i], ...row };
        else bookings.unshift(row);
    }

    async function apiPost(handlerUrl, id) {
        const body = new URLSearchParams();
        body.set("id", String(id));
        body.set("__RequestVerificationToken", antiforgery());
        const res = await fetch(handlerUrl, {
            method: "POST",
            headers: {
                "RequestVerificationToken": antiforgery(),
                "Content-Type": "application/x-www-form-urlencoded"
            },
            body: body.toString(),
            credentials: "same-origin"
        });
        const data = await res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
        return data;
    }

    async function apiGetDetail(id) {
        const url = new URL(boot.handlers.detail, location.origin);
        url.searchParams.set("id", String(id));
        const res = await fetch(url.toString(), { credentials: "same-origin" });
        return res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
    }

    async function applyAction(act, id) {
        if (act === "close") {
            selectedId = null;
            els.drawer.classList.remove("is-open");
            els.drawer.setAttribute("aria-hidden", "true");
            renderList();
            return;
        }

        if (act === "detail") {
            selectedId = id;
            const local = bookings.find((x) => x.id === id);
            if (local) renderDrawer(local);
            renderList();
            if (boot.handlers.detail) {
                const data = await apiGetDetail(id);
                if (data.ok && data.booking) {
                    upsertBooking(data.booking);
                    selectedId = id;
                    renderList();
                    renderDrawer(data.booking);
                }
            }
            return;
        }

        const map = {
            confirm: boot.handlers.confirm,
            reject: boot.handlers.reject,
            assign: boot.handlers.assign,
            handover: boot.handlers.handover,
            complete: boot.handlers.complete
        };
        const url = map[act];
        if (!url) return;
        if (busy) return;
        busy = true;
        try {
            const data = await apiPost(url, id);
            if (!data.ok) {
                toast(data.error || "Thao tác thất bại.");
                return;
            }
            upsertBooking(data.booking);
            selectedId = id;
            const labels = {
                confirm: "Đã xác nhận đơn.",
                reject: "Đã hủy / không duyệt đơn.",
                assign: "Đã phân công xe" + (data.booking.mode === "WithDriver" ? " & tài xế." : "."),
                handover: "Đã giao xe.",
                complete: "Đã tiếp nhận trả xe."
            };
            toast(labels[act] || "Đã cập nhật.");
            renderList();
            renderDrawer(data.booking);
        } catch (e) {
            toast("Lỗi mạng khi gọi hệ thống.");
        } finally {
            busy = false;
        }
    }

    function readForm() {
        const data = new FormData(els.form);
        filters.q = (data.get("q") || "").toString().trim();
        filters.status = (data.get("status") || "").toString();
        filters.mode = (data.get("mode") || "").toString();
        filters.type = (data.get("type") || "").toString();
        filters.from = (data.get("from") || "").toString();
        filters.to = (data.get("to") || "").toString();
        if (filters.status) filters.queue = "";
    }

    els.form.addEventListener("submit", (e) => { e.preventDefault(); readForm(); renderList(); });
    els.reset.addEventListener("click", (e) => {
        e.preventDefault();
        els.form.reset();
        filters.q = filters.status = filters.mode = filters.type = filters.from = filters.to = filters.queue = "";
        renderList();
    });
    els.metrics.addEventListener("click", (e) => {
        const btn = e.target.closest(".mk-metric");
        if (!btn) return;
        if (btn.dataset.queue) {
            filters.queue = "assign";
            filters.status = "";
            els.form.status.value = "";
        } else {
            filters.queue = "";
            filters.status = btn.dataset.filterStatus || "";
            els.form.status.value = filters.status;
        }
        renderList();
    });
    els.pills.addEventListener("click", (e) => {
        const pill = e.target.closest(".mk-pill");
        if (!pill) return;
        if (pill.dataset.queue) {
            filters.queue = "assign";
            filters.status = "";
            els.form.status.value = "";
        } else {
            filters.queue = "";
            filters.status = pill.dataset.status || "";
            els.form.status.value = filters.status;
        }
        renderList();
    });
    document.body.addEventListener("click", (e) => {
        const btn = e.target.closest("[data-act]");
        if (!btn) return;
        applyAction(btn.dataset.act, Number(btn.dataset.id || selectedId));
    });

    const params = new URLSearchParams(location.search);
    if (params.get("queue") === "assign") filters.queue = "assign";
    renderList();
    const deepId = Number(params.get("id") || params.get("bookingId") || 0);
    if (deepId > 0) applyAction("detail", deepId);
})();

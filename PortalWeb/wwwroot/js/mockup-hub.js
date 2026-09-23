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
        metrics: document.getElementById("mkMetrics"),
        handoverModal: document.getElementById("mkHandoverModal"),
        handoverBody: document.getElementById("mkHandoverBody"),
        handoverConfirm: document.querySelector("[data-handover-confirm]"),
        returnModal: document.getElementById("mkReturnModal"),
        returnBody: document.getElementById("mkReturnBody"),
        returnConfirm: document.querySelector("[data-return-confirm]")
    };

    let handoverState = null;
    let returnState = null;

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
        if (b.status === "Assigned" && b.mode === "SelfDrive") {
            bits.push(`<button type="button" class="mk-btn mk-btn-info" data-act="handover" data-id="${b.id}">Giao xe</button>`);
        }
        if (b.status === "InProgress" && b.mode === "SelfDrive") {
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

    async function apiPost(handlerUrl, id, extra) {
        const body = new URLSearchParams();
        body.set("id", String(id));
        if (extra && typeof extra === "object") {
            Object.keys(extra).forEach((key) => {
                const val = extra[key];
                if (val !== null && val !== undefined && val !== "")
                    body.set(key, String(val));
            });
        }
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

    async function apiGetHandoverInfo(id) {
        const url = new URL(boot.handlers.handoverInfo, location.origin);
        url.searchParams.set("id", String(id));
        const res = await fetch(url.toString(), {
            headers: { Accept: "application/json" },
            credentials: "same-origin"
        });
        return res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
    }

    function closeHandoverModal() {
        handoverState = null;
        if (els.handoverModal) els.handoverModal.hidden = true;
        if (els.handoverConfirm) els.handoverConfirm.disabled = true;
    }

    function openHandoverModalLoading() {
        if (!els.handoverModal || !els.handoverBody) return;
        handoverState = null;
        els.handoverBody.innerHTML = `<p class="mk-note">Đang tải dữ liệu xe...</p>`;
        if (els.handoverConfirm) els.handoverConfirm.disabled = true;
        els.handoverModal.hidden = false;
    }

    /** Present numeric check — treats 0 as valid (do NOT use !value). */
    function isPresentNumber(v) {
        return v !== null && v !== undefined && v !== "" && Number.isFinite(Number(v));
    }
    function isValidKm(v) {
        return isPresentNumber(v) && Number(v) >= 0;
    }
    function isValidFuelPercent(v) {
        return isPresentNumber(v) && Number(v) >= 0 && Number(v) <= 100;
    }

    function parseFuelInput(raw) {
        if (raw === null || raw === undefined) return null;
        const v = String(raw).trim();
        if (v === "") return null;
        const n = Number(v);
        if (!Number.isFinite(n) || n < 0 || n > 100) return null;
        return n; // 0 is valid
    }

    function syncHandoverConfirmFromFuelInput() {
        if (!handoverState || !els.handoverConfirm) return;
        if (!handoverState.requiresFuelInput) {
            els.handoverConfirm.disabled = !isValidKm(handoverState.odometerKm) || !isValidFuelPercent(handoverState.fuelLevel);
            return;
        }
        const input = els.handoverBody?.querySelector("[data-handover-fuel-input]");
        const fuel = parseFuelInput(input?.value);
        handoverState.fuelLevel = fuel;
        els.handoverConfirm.disabled = !isValidKm(handoverState.odometerKm) || fuel === null;
    }

    function renderHandoverModal(info) {
        if (!els.handoverBody) return;
        const kmOk = isValidKm(info.currentKm) && !info.blockReason;
        const requiresFuelInput = !!info.requiresFuelInput;
        // fuelLevel === 0 is known valid fuel (must use != null, never !fuelLevel)
        const knownFuelOk = !requiresFuelInput && isValidFuelPercent(info.fuelLevel);

        handoverState = kmOk
            ? {
                id: info.bookingId,
                odometerKm: Number(info.currentKm),
                fuelLevel: knownFuelOk ? Number(info.fuelLevel) : null,
                requiresFuelInput
            }
            : null;

        const kmText = kmOk ? `${Number(info.currentKm).toLocaleString("vi-VN")} km` : "—";
        let fuelRow;
        if (!kmOk) {
            fuelRow = `<div><dt>Mức nhiên liệu</dt><dd>—</dd></div>`;
        } else if (requiresFuelInput) {
            fuelRow = `
                <div><dt>Mức nhiên liệu khi giao xe (%)</dt>
                <dd>
                    <input type="number" min="0" max="100" step="1" placeholder="Nhập %"
                           inputmode="numeric" data-handover-fuel-input class="mk-input" style="max-width:8rem" />
                </dd></div>`;
        } else {
            const fuelText = `${Number(info.fuelLevel).toLocaleString("vi-VN")}% 🔒`;
            fuelRow = `<div><dt>Mức nhiên liệu</dt><dd><strong>${escapeHtml(fuelText)}</strong></dd></div>`;
        }

        els.handoverBody.innerHTML = `
            <dl class="mk-dl">
                <div><dt>Xe</dt><dd>${escapeHtml(info.vehicle || "—")}</dd></div>
                <div><dt>Biển số</dt><dd>${escapeHtml(info.plate || "—")}</dd></div>
                <div><dt>KM hiện tại</dt><dd><strong>${escapeHtml(kmText)}</strong></dd></div>
                ${fuelRow}
            </dl>
            ${!kmOk ? `<p class="mk-note" style="color:#b91c1c">${escapeHtml(info.blockReason || "Xe chưa có số KM hiện tại trong hệ thống.")}</p>` : ""}
            ${kmOk && requiresFuelInput ? `<p class="mk-note">Xe chưa có dữ liệu nhiên liệu, vui lòng nhập mức nhiên liệu thực tế.</p>` : ""}
            ${kmOk && !requiresFuelInput ? `<p class="mk-note">KM và mức nhiên liệu lấy từ hệ thống — không thể chỉnh sửa tại đây.</p>` : ""}
            ${kmOk && requiresFuelInput ? `<p class="mk-note">Số km lấy từ hồ sơ xe trên hệ thống — không thể chỉnh sửa tại đây.</p>` : ""}`;

        if (els.handoverConfirm) {
            if (!kmOk) els.handoverConfirm.disabled = true;
            else if (requiresFuelInput) {
                els.handoverConfirm.disabled = true;
                const input = els.handoverBody.querySelector("[data-handover-fuel-input]");
                input?.addEventListener("input", syncHandoverConfirmFromFuelInput);
                input?.addEventListener("change", syncHandoverConfirmFromFuelInput);
            } else {
                els.handoverConfirm.disabled = false;
            }
        }
    }

    async function beginHandover(id) {
        const booking = bookings.find((b) => b.id === id);
        if (booking && booking.mode && booking.mode !== "SelfDrive") {
            toast("Chỉ đơn tự lái mới dùng giao xe tại điều phối.");
            return;
        }
        openHandoverModalLoading();
        try {
            const data = await apiGetHandoverInfo(id);
            if (!data.ok) {
                els.handoverBody.innerHTML = `<p class="mk-note" style="color:#b91c1c">${escapeHtml(data.error || "Không thể tải dữ liệu giao xe.")}</p>`;
                if (els.handoverConfirm) els.handoverConfirm.disabled = true;
                return;
            }
            renderHandoverModal(data);
        } catch (_) {
            els.handoverBody.innerHTML = `<p class="mk-note" style="color:#b91c1c">Lỗi mạng khi tải dữ liệu xe.</p>`;
            if (els.handoverConfirm) els.handoverConfirm.disabled = true;
        }
    }

    function closeReturnModal() {
        returnState = null;
        if (els.returnModal) els.returnModal.hidden = true;
        if (els.returnConfirm) els.returnConfirm.disabled = true;
    }

    function openReturnModalLoading() {
        if (!els.returnModal || !els.returnBody) return;
        returnState = null;
        els.returnBody.innerHTML = `<p class="mk-note">Đang tải dữ liệu xe...</p>`;
        if (els.returnConfirm) els.returnConfirm.disabled = true;
        els.returnModal.hidden = false;
    }

    function syncReturnConfirm() {
        if (!returnState || !els.returnConfirm) return;
        const kmInput = els.returnBody?.querySelector("[data-return-km]");
        const fuelInput = els.returnBody?.querySelector("[data-return-fuel]");
        const kmRaw = kmInput ? String(kmInput.value).trim() : "";
        const fuelRaw = fuelInput ? String(fuelInput.value).trim() : "";
        let message = "";
        let km = null;
        let fuel = null;
        if (kmRaw === "") message = "Vui lòng nhập số km khi trả xe.";
        else if (!Number.isFinite(Number(kmRaw))) message = "Số km khi trả xe không hợp lệ.";
        else if (Number(kmRaw) < 0) message = "Số km khi trả xe không được âm.";
        else km = Number(kmRaw);
        if (!message) {
            if (fuelRaw === "") message = "Vui lòng nhập mức nhiên liệu khi trả xe từ 0 đến 100.";
            else if (!Number.isFinite(Number(fuelRaw)) || Number(fuelRaw) < 0 || Number(fuelRaw) > 100)
                message = "Mức nhiên liệu phải từ 0 đến 100.";
            else fuel = Number(fuelRaw);
        }
        returnState.odometerKm = km;
        returnState.fuelLevel = fuel;
        const ready = km !== null && fuel !== null;
        return { ready, message };
    }

    function renderReturnModal(info) {
        if (!els.returnBody) return;
        returnState = { id: info.bookingId, odometerKm: null, fuelLevel: null };
        const handoverNote = info.handoverOdometerKm != null && Number(info.handoverOdometerKm) >= 0
            ? `<p class="mk-note">KM lúc giao: <strong>${Number(info.handoverOdometerKm).toLocaleString("vi-VN")} km</strong>. Số KM khi trả phải không nhỏ hơn mức này.</p>`
            : "";
        els.returnBody.innerHTML = `
            <dl class="mk-dl">
                <div><dt>Xe</dt><dd>${escapeHtml(info.vehicle || "—")}</dd></div>
                <div><dt>Biển số</dt><dd>${escapeHtml(info.plate || "—")}</dd></div>
            </dl>
            ${handoverNote}
            <label class="mk-field"><span>Số KM khi trả xe *</span>
                <input type="text" inputmode="decimal" placeholder="Nhập số km" data-return-km class="mk-input" autocomplete="off" />
            </label>
            <label class="mk-field"><span>Mức nhiên liệu khi trả xe (%)</span>
                <input type="text" inputmode="decimal" placeholder="Nhập %" data-return-fuel class="mk-input" autocomplete="off" />
            </label>
            <p class="mk-note" data-return-error style="color:#b91c1c"></p>`;
        const kmInput = els.returnBody.querySelector("[data-return-km]");
        const fuelInput = els.returnBody.querySelector("[data-return-fuel]");
        const onInput = () => {
            const check = syncReturnConfirm();
            const err = els.returnBody.querySelector("[data-return-error]");
            if (err) err.textContent = check.ready ? "" : (check.message || "");
        };
        kmInput?.addEventListener("input", onInput);
        fuelInput?.addEventListener("input", onInput);
        if (els.returnConfirm) els.returnConfirm.disabled = false;
    }

    async function beginReturn(id) {
        const booking = bookings.find((b) => b.id === id);
        if (booking && booking.mode && booking.mode !== "SelfDrive") {
            toast("Chỉ đơn tự lái mới tiếp nhận trả xe tại điều phối.");
            return;
        }
        openReturnModalLoading();
        try {
            const url = new URL(boot.handlers.returnInfo, location.origin);
            url.searchParams.set("id", String(id));
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" }, credentials: "same-origin" });
            const data = await res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
            if (!data.ok) {
                els.returnBody.innerHTML = `<p class="mk-note" style="color:#b91c1c">${escapeHtml(data.error || "Không thể tải dữ liệu trả xe.")}</p>`;
                if (els.returnConfirm) els.returnConfirm.disabled = true;
                return;
            }
            renderReturnModal(data);
        } catch (_) {
            els.returnBody.innerHTML = `<p class="mk-note" style="color:#b91c1c">Lỗi mạng khi tải dữ liệu xe.</p>`;
            if (els.returnConfirm) els.returnConfirm.disabled = true;
        }
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

        if (act === "handover") {
            await beginHandover(id);
            return;
        }

        if (act === "complete") {
            await beginReturn(id);
            return;
        }

        const map = {
            confirm: boot.handlers.confirm,
            reject: boot.handlers.reject,
            assign: boot.handlers.assign
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
                assign: "Đã phân công xe" + (data.booking.mode === "WithDriver" ? " & tài xế." : ".")
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

    document.querySelector("[data-handover-cancel]")?.addEventListener("click", () => closeHandoverModal());
    els.handoverModal?.addEventListener("click", (e) => {
        if (e.target === els.handoverModal) closeHandoverModal();
    });
    els.handoverConfirm?.addEventListener("click", async () => {
        if (!handoverState || busy) return;
        if (!boot.handlers.handover) return;
        syncHandoverConfirmFromFuelInput();
        if (handoverState.fuelLevel === null || handoverState.fuelLevel === undefined) {
            toast("Vui lòng nhập mức nhiên liệu từ 0 đến 100.");
            return;
        }
        busy = true;
        els.handoverConfirm.disabled = true;
        try {
            const data = await apiPost(boot.handlers.handover, handoverState.id, {
                odometerKm: handoverState.odometerKm,
                fuelLevel: handoverState.fuelLevel
            });
            if (!data.ok) {
                toast(data.error || "Giao xe thất bại.");
                els.handoverConfirm.disabled = false;
                return;
            }
            upsertBooking(data.booking);
            selectedId = handoverState.id;
            closeHandoverModal();
            toast("Đã giao xe.");
            renderList();
            renderDrawer(data.booking);
        } catch (_) {
            toast("Lỗi mạng khi gọi hệ thống.");
            els.handoverConfirm.disabled = false;
        } finally {
            busy = false;
        }
    });

    document.querySelector("[data-return-cancel]")?.addEventListener("click", () => closeReturnModal());
    els.returnModal?.addEventListener("click", (e) => {
        if (e.target === els.returnModal) closeReturnModal();
    });
    els.returnConfirm?.addEventListener("click", async () => {
        if (!returnState || busy || !boot.handlers.complete) return;
        const check = syncReturnConfirm();
        const err = els.returnBody?.querySelector("[data-return-error]");
        if (!check.ready) {
            if (err) err.textContent = check.message || "Vui lòng nhập số km khi trả xe.";
            return;
        }
        busy = true;
        els.returnConfirm.disabled = true;
        try {
            const data = await apiPost(boot.handlers.complete, returnState.id, {
                odometerKm: returnState.odometerKm,
                fuelLevel: returnState.fuelLevel
            });
            if (!data.ok) {
                if (err) err.textContent = data.error || "Tiếp nhận trả xe thất bại.";
                els.returnConfirm.disabled = false;
                return;
            }
            upsertBooking(data.booking);
            selectedId = returnState.id;
            closeReturnModal();
            toast("Đã tiếp nhận trả xe.");
            renderList();
            renderDrawer(data.booking);
        } catch (_) {
            if (err) err.textContent = "Lỗi mạng khi gọi hệ thống.";
            els.returnConfirm.disabled = false;
        } finally {
            busy = false;
        }
    });

    const params = new URLSearchParams(location.search);
    if (params.get("queue") === "assign") filters.queue = "assign";
    renderList();
    const deepId = Number(params.get("id") || params.get("bookingId") || 0);
    if (deepId > 0) applyAction("detail", deepId);
})();

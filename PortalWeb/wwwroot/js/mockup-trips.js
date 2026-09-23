(function () {
    const boot = window.__TRIPS_BOOTSTRAP__;
    const handlers = window.__TRIPS_HANDLERS__ || {};
    const ready = window.__TRIPS_READY__ === true;

    if (!boot || typeof boot !== "object" || !Array.isArray(boot.trips) || !ready) {
        const board = document.getElementById("opBoard");
        if (board) {
            board.innerHTML = `<p class="mk-empty">Trips chưa nhận bootstrap API. Rebuild/restart PortalWeb (tránh DLL cũ + wwwroot mới).</p>`;
        }
        console.error("[Trips] Missing __TRIPS_BOOTSTRAP__ — aborting.");
        return;
    }

    const STATUS = {
        waitingHandover: "Chờ giao xe",
        handedOver: "Đã giao xe",
        accepted: "Đã nhận chuyến",
        inProgress: "Đang thực hiện",
        returning: "Đang trả xe",
        completed: "Hoàn thành",
        incident: "Có sự cố"
    };
    const MODE_LABEL = { SelfDrive: "Tự lái", WithDriver: "Có tài xế" };
    const FLOW = ["Tạo đơn thuê", "Xác nhận", "Phân công", "Giao xe", "Nhận chuyến", "Đang thực hiện", "Trả xe", "Quyết toán", "Hoàn thành"];
    const FLOW_AT = {
        waitingHandover: 3,
        handedOver: 4,
        accepted: 5,
        inProgress: 5,
        returning: 6,
        completed: 8,
        incident: 5
    };
    const PAGE = 5;

    let trips = boot.trips.slice();
    let driverRows = Array.isArray(boot.drivers) ? boot.drivers.slice() : [];

    const state = { q: "", status: "", mode: "", dstatus: "", vehicle: "", selectedId: null, page: 1, modal: null };
    const els = {
        form: document.getElementById("opFilter"),
        vehicle: document.getElementById("opVehicleFilter"),
        board: document.getElementById("opBoard"),
        table: document.getElementById("opTableBody"),
        pager: document.getElementById("opPager"),
        meta: document.getElementById("opTableMeta"),
        alerts: document.getElementById("opAlerts"),
        driver: document.getElementById("opDriverPanel"),
        map: document.getElementById("opMap"),
        drawer: document.getElementById("opDrawer"),
        modal: document.getElementById("opModal"),
        modalBody: document.getElementById("opModalBody"),
        toast: document.getElementById("opToast"),
        metricToday: document.getElementById("opMetricToday"),
        metricWaiting: document.getElementById("opMetricWaiting"),
        metricInProgress: document.getElementById("opMetricInProgress"),
        metricIncidents: document.getElementById("opMetricIncidents"),
        metricSettlement: document.getElementById("opMetricSettlement")
    };

    const money = (n) => Number(n || 0).toLocaleString("vi-VN") + " ₫";
    const trip = (id) => trips.find((t) => t.id === Number(id));
    const badge = (st) => `<span class="mk-badge mk-badge-${String(st || "").toLowerCase()}">${STATUS[st] || st}</span>`;
    const settleBadge = (s) => s === "settled"
        ? `<span class="mk-badge mk-badge-settled">Đã quyết toán</span>`
        : s === "pending"
            ? `<span class="mk-badge mk-badge-pending">Chờ quyết toán</span>`
            : `<span class="mk-badge mk-badge-gray">—</span>`;

    function antiforgery() {
        const input = document.querySelector("#opAntiForgery input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    async function postForm(url, fields) {
        const body = new URLSearchParams();
        Object.keys(fields || {}).forEach((k) => {
            if (fields[k] != null && fields[k] !== "") body.set(k, String(fields[k]));
        });
        body.set("__RequestVerificationToken", antiforgery());
        const res = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                RequestVerificationToken: antiforgery()
            },
            credentials: "same-origin",
            body: body.toString()
        });
        let data = null;
        try { data = await res.json(); } catch { /* ignore */ }
        return { ok: res.ok && data && data.ok !== false, data };
    }

    async function getJson(url) {
        const res = await fetch(url, { credentials: "same-origin" });
        let data = null;
        try { data = await res.json(); } catch { /* ignore */ }
        return { ok: res.ok && data && data.ok !== false, data };
    }

    function applyMetrics(m) {
        if (!m) return;
        if (els.metricToday) els.metricToday.textContent = m.today ?? "—";
        if (els.metricWaiting) els.metricWaiting.textContent = m.waiting ?? "—";
        if (els.metricInProgress) els.metricInProgress.textContent = m.inProgress ?? "—";
        if (els.metricIncidents) els.metricIncidents.textContent = m.incidents ?? "—";
        if (els.metricSettlement) els.metricSettlement.textContent = m.settlement ?? "—";
    }
    applyMetrics(boot.metrics);

    function upsertTrip(row) {
        if (!row || !row.id) return;
        const i = trips.findIndex((t) => t.id === row.id);
        if (i >= 0) trips[i] = { ...trips[i], ...row };
        else trips.unshift(row);
    }

    async function reloadFromServer() {
        if (!handlers.reload) return;
        const { ok, data } = await getJson(handlers.reload);
        if (!ok || !data || !data.data) return;
        trips = Array.isArray(data.data.trips) ? data.data.trips : trips;
        driverRows = Array.isArray(data.data.drivers) ? data.data.drivers : driverRows;
        applyMetrics(data.data.metrics);
        fillVehicles();
    }

    function toast(msg) {
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2800);
    }

    function visible() {
        return trips.filter((t) => {
            if (state.status && t.status !== state.status) return false;
            if (state.mode && t.mode !== state.mode) return false;
            if (state.vehicle && t.vehicle !== state.vehicle) return false;
            if (state.dstatus === "Busy" && !t.driverBusy && t.mode === "WithDriver") return false;
            if (state.dstatus === "Available" && t.driverBusy) return false;
            if (state.dstatus === "Available" && t.mode === "SelfDrive") return false;
            if (state.q) {
                const q = state.q.toLowerCase();
                const blob = `#${t.id} ${t.customer} ${t.vehicle} ${t.plate} ${t.driver || ""} ${t.mode}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        });
    }

    function fillVehicles() {
        const names = [...new Set(trips.map((t) => t.vehicle).filter(Boolean))];
        els.vehicle.innerHTML = `<option value="">Tất cả</option>` + names.map((n) => `<option>${n}</option>`).join("");
    }

    function cardActions(t) {
        const btns = [];
        if (t.canHandoverApi || (t.mode === "SelfDrive" && t.status === "waitingHandover")) {
            btns.push(`<button type="button" class="mk-btn mk-btn-primary" data-handover="${t.id}">🔑 Giao xe</button>`);
        }
        if (t.canCompleteApi || (t.mode === "SelfDrive" && (t.status === "handedOver" || t.status === "inProgress" || t.status === "returning"))) {
            btns.push(`<button type="button" class="mk-btn mk-btn-primary" data-return="${t.id}">↩ Tiếp nhận trả xe</button>`);
        }
        if (t.canCompleteMockOnly) {
            btns.push(`<button type="button" class="mk-btn mk-btn-ghost" data-return="${t.id}">↩ Kiểm tra trả xe</button>`);
        }
        if (t.incident) {
            btns.push(`<button type="button" class="mk-btn mk-btn-danger" data-incident="${t.id}">Xem sự cố</button>`);
        }
        if (t.settlement === "pending") {
            btns.push(`<button type="button" class="mk-btn mk-btn-ghost" data-settle="${t.id}">💰 Quyết toán</button>`);
        }
        return btns.join("");
    }

    function renderBoard() {
        const list = visible();
        els.board.innerHTML = list.map((t) => `
            <article class="op-trip ${t.status === "incident" ? "is-alert" : ""}" data-open="${t.id}">
                <div class="op-trip-head">
                    <strong>Đơn #${t.id}</strong>
                    ${badge(t.status)}
                </div>
                <dl>
                    <div><dt>Khách hàng</dt><dd>${t.customer}</dd></div>
                    <div><dt>Xe</dt><dd>${t.vehicle}<br><span class="mk-muted">${t.plate}</span></dd></div>
                    <div><dt>Tài xế</dt><dd>${t.driver || "Tự lái — không gắn tài xế"}</dd></div>
                    <div><dt>Hình thức</dt><dd>${MODE_LABEL[t.mode] || t.mode}</dd></div>
                    <div><dt>Lộ trình</dt><dd>${t.route}</dd></div>
                    <div><dt>Bắt đầu</dt><dd>${t.start}</dd></div>
                    <div><dt>Dự kiến trả</dt><dd>${t.expectedReturn}</dd></div>
                    ${t.driver && t.status === "inProgress" ? `<div><dt>Trạng thái tài xế</dt><dd>🟢 Đang chạy</dd></div>` : ""}
                </dl>
                <div class="op-progress" aria-hidden="true"><i style="width:${t.progress || 0}%"></i></div>
                <div class="mk-muted">${t.progress || 0}% · ${t.location || ""}</div>
                ${t.incident ? `<p class="mk-note" style="margin-top:.55rem">⚠️ ${t.incident.summary}<br>${t.incident.time} · ${t.incident.status}</p>` : ""}
                <div class="op-trip-actions">${cardActions(t)}</div>
            </article>`).join("") || `<p class="mk-empty">Không có chuyến khớp bộ lọc.</p>`;
    }

    function renderTable() {
        const list = visible();
        const pages = Math.max(1, Math.ceil(list.length / PAGE));
        if (state.page > pages) state.page = pages;
        const slice = list.slice((state.page - 1) * PAGE, state.page * PAGE);
        els.meta.textContent = `${list.length} chuyến từ Backend · trang ${state.page}/${pages}`;
        els.table.innerHTML = slice.map((t) => `
            <tr data-open="${t.id}">
                <td><button type="button" class="mk-btn mk-btn-ghost" data-open="${t.id}">#${t.id}</button></td>
                <td>${t.customer}</td>
                <td>${t.vehicle}<br><span class="mk-muted">${t.plate}</span></td>
                <td>${t.driver || "—"}</td>
                <td>${MODE_LABEL[t.mode] || t.mode}</td>
                <td>${t.start}</td>
                <td>${t.expectedReturn}</td>
                <td>${badge(t.status)}</td>
                <td>${t.incident ? "⚠️ " + (t.incident.type || t.incident.summary) : "—"}</td>
                <td>${settleBadge(t.settlement)}</td>
            </tr>`).join("") || `<tr><td colspan="10">Không có chuyến khớp bộ lọc.</td></tr>`;
        els.pager.innerHTML = Array.from({ length: pages }, (_, i) =>
            `<button type="button" class="mk-btn mk-btn-ghost ${state.page === i + 1 ? "is-on" : ""}" data-page="${i + 1}">${i + 1}</button>`
        ).join("");
    }

    function renderAlerts() {
        const priority = visible().filter((t) =>
            t.incident || t.settlement === "pending" || t.status === "waitingHandover" || t.status === "incident"
        ).slice(0, 6);
        if (!priority.length) {
            els.alerts.innerHTML = `<p class="mk-muted">Không có cảnh báo từ dữ liệu hiện tại.</p>`;
            return;
        }
        els.alerts.innerHTML = priority.map((t) => {
            let tone = "🟢";
            let text = STATUS[t.status] || t.status;
            if (t.incident) { tone = "🔴"; text = "Có sự cố xe"; }
            else if (t.settlement === "pending") { tone = "🟡"; text = "Đang chờ quyết toán"; }
            else if (t.status === "waitingHandover") { tone = "🟡"; text = "Chờ giao xe"; }
            return `<button type="button" class="op-alert" data-open="${t.id}"><span><strong>${tone} Đơn #${t.id}</strong>${text}</span><span class="mk-muted">${STATUS[t.status] || t.status}</span></button>`;
        }).join("");
    }

    function renderDriver() {
        if (!driverRows.length) {
            els.driver.innerHTML = `<p class="mk-muted">Không có dữ liệu tài xế từ fleet-status / drivers.</p>`;
            return;
        }
        const d = driverRows.find((x) => String(x.status).toLowerCase() === "busy") || driverRows[0];
        const tripLink = d.bookingId
            ? `<button type="button" class="mk-btn mk-btn-primary" data-open="${d.bookingId}">Xem chuyến</button>`
            : "";
        const incTrip = trips.find((t) => t.incident);
        els.driver.innerHTML = `
            <div class="op-driver-block">
                <strong>${d.name}</strong>
                <p>${d.status === "Busy" || d.status === "busy" ? "🟡" : "🟢"} ${d.status}<br>
                ${d.bookingId ? `Chuyến: #${d.bookingId}` : "Không gắn chuyến"}<br>
                ${d.plate ? `Xe: ${d.plate}` : ""}</p>
                <div class="mk-actions">
                    ${tripLink}
                    <button type="button" class="mk-btn mk-btn-ghost" data-contact="${d.bookingId || ""}">Liên hệ tài xế</button>
                    ${incTrip ? `<button type="button" class="mk-btn mk-btn-danger" data-incident="${incTrip.id}">Xem sự cố</button>` : ""}
                </div>
            </div>`;
    }

    function renderMap() {
        els.map.querySelectorAll(".op-pin").forEach((n) => n.remove());
        // No GPS API — keep empty demo map (do not invent pins as real).
        const label = els.map.querySelector(".op-map-label");
        if (label) label.textContent = "Vị trí mô phỏng — không phải GPS thật (thiếu API)";
    }

    function condBlock(ret, handover, mode) {
        if (!ret) return "";
        const km = handover && mode === "SelfDrive" ? (ret.odo - handover.odo) : null;
        const warnEx = ret.exterior === "Cần kiểm tra";
        const warnTech = ret.tech === "Cần kiểm tra";
        return `<div class="op-cond">
            <div><span>🛞 Odometer</span><strong>${Number(ret.odo || 0).toLocaleString("vi-VN")} km</strong></div>
            <div><span>⛽ Fuel</span><strong>${ret.fuel}%</strong></div>
            <div class="${warnEx ? "warn" : ""}"><span>🚗 Exterior</span><strong>${ret.exterior}${warnEx ? " ⚠️" : ""}</strong></div>
            <div class="${warnTech ? "warn" : ""}"><span>🔧 Technical</span><strong>${ret.tech}${warnTech ? " ⚠️" : ""}</strong></div>
        </div>${km !== null ? `<p class="mk-note">Km thực tế: ${km.toLocaleString("vi-VN")} km = trả − giao (từ biên bản API).</p>` : ""}`;
    }

    function quickActions(t) {
        const a = [`<button type="button" class="mk-btn mk-btn-ghost" data-contact="${t.id}">📞 Liên hệ</button>`];
        if (t.status === "inProgress" || t.status === "accepted" || t.status === "handedOver") {
            a.push(`<button type="button" class="mk-btn mk-btn-danger" data-report="${t.id}">⚠️ Báo sự cố</button>`);
        }
        a.push(`<button type="button" class="mk-btn mk-btn-ghost" data-inspect="${t.id}">📋 Inspection</button>`);
        if (t.canHandoverApi || (t.mode === "SelfDrive" && t.status === "waitingHandover")) {
            a.push(`<button type="button" class="mk-btn mk-btn-primary" data-handover="${t.id}">🚗 Giao xe</button>`);
        }
        if (t.canCompleteApi || t.canCompleteMockOnly || (t.mode === "SelfDrive" && (t.status === "handedOver" || t.status === "inProgress"))) {
            a.push(`<button type="button" class="mk-btn mk-btn-primary" data-return="${t.id}">↩ Trả xe</button>`);
        }
        if (t.settlement === "pending" || t.status === "completed") {
            a.push(`<button type="button" class="mk-btn mk-btn-ghost" data-settle="${t.id}">💰 Quyết toán</button>`);
        }
        return a.join("");
    }

    function renderDrawer(t) {
        const step = FLOW_AT[t.status] ?? 0;
        const flow = FLOW.map((name, i) => {
            let cls = i < step ? "done" : i === step ? "now" : "";
            if (t.status === "incident" && name === "Đang thực hiện") cls = "warn";
            return `<li class="${cls}">${name}</li>`;
        }).join("");
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-2">Đơn #${t.id}</h2>
                    ${badge(t.status)} ${t.mode === "WithDriver" ? '<span class="mk-badge mk-badge-assigned">Có tài xế</span>' : '<span class="mk-badge mk-badge-gray">Tự lái</span>'}
                    <span class="mk-badge mk-badge-ok">API</span>
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <section class="mk-block">
                    <h3>Thao tác nhanh</h3>
                    <div class="mk-actions">${quickActions(t)}</div>
                </section>
                <section class="mk-block">
                    <h3>Chi tiết</h3>
                    <dl class="mk-dl">
                        <div><dt>Khách hàng</dt><dd>${t.customer}</dd></div>
                        <div><dt>Xe</dt><dd>${t.vehicle}<br>${t.plate}</dd></div>
                        <div><dt>Tài xế</dt><dd>${t.driver || "—"}</dd></div>
                        <div><dt>Điểm nhận</dt><dd>${t.pickup}</dd></div>
                        <div><dt>Điểm trả</dt><dd>${t.dropoff}</dd></div>
                        <div><dt>Bắt đầu</dt><dd>${t.start}</dd></div>
                        <div><dt>Dự kiến trả</dt><dd>${t.expectedReturn}</dd></div>
                        <div><dt>Trạng thái hiện tại</dt><dd>${STATUS[t.status] || t.status} <span class="mk-muted">(${t.bookingStatus || ""})</span></dd></div>
                        <div><dt>Tình trạng xe</dt><dd>${t.vehicleState}</dd></div>
                    </dl>
                    ${t.mode === "WithDriver" ? '<p class="mk-note">Có tài xế — hoàn thành trả xe qua API dispatcher chưa có (chỉ SelfDrive).</p>' : ""}
                </section>
                <section class="mk-block">
                    <h3>Luồng vận hành</h3>
                    <ul class="op-flow">${flow}</ul>
                </section>
                <section class="mk-block">
                    <h3>Nhật ký vận hành</h3>
                    <ul class="mk-timeline">${(t.ops || []).map((o) => `<li class="${o.cls || ""}"><strong>${o.t}</strong> ${o.text}</li>`).join("") || "<li class='mk-muted'>Chưa có sự kiện</li>"}</ul>
                </section>
                ${t.ret ? `<section class="mk-block"><h3>Tình trạng xe (trả)</h3>${condBlock(t.ret, t.handover, t.mode)}</section>` : ""}
                ${t.handover && !t.ret ? `<section class="mk-block"><h3>Biên bản giao xe</h3>${condBlock(t.handover, null, t.mode)}</section>` : ""}
                ${t.incident ? `<p class="mk-note">⚠️ ${t.incident.summary} · ${t.incident.time}</p>` : ""}
            </div>`;
    }

    function closeModal() {
        els.modal.hidden = true;
        els.modalBody.innerHTML = "";
        state.modal = null;
    }

    function openModal(html) {
        els.modal.hidden = false;
        els.modalBody.innerHTML = html;
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

    function parseHandoverFuel(raw) {
        if (raw === null || raw === undefined) return null;
        const v = String(raw).trim();
        if (v === "") return null;
        const n = Number(v);
        if (!Number.isFinite(n) || n < 0 || n > 100) return null;
        return n; // 0 is valid
    }

    function syncTripsHandoverConfirm() {
        const btn = document.querySelector("[data-handover-submit]");
        if (!btn || !state.handoverPayload) return;
        if (!state.handoverPayload.requiresFuelInput) {
            btn.disabled = !(isValidKm(state.handoverPayload.odometerKm) && isValidFuelPercent(state.handoverPayload.fuelLevel));
            return;
        }
        const input = document.querySelector("[data-handover-fuel-input]");
        const fuel = parseHandoverFuel(input?.value);
        state.handoverPayload.fuelLevel = fuel;
        btn.disabled = fuel === null || !isValidKm(state.handoverPayload.odometerKm);
    }

    async function handoverForm(t) {
        if (!t.canHandoverApi && t.mode !== "SelfDrive") {
            toast("Chỉ SelfDrive dùng API handover dispatcher.");
            return;
        }
        openModal(`
            <h2>Xác nhận giao xe</h2>
            <p class="mk-note">Đang tải dữ liệu xe...</p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                <button type="button" class="mk-btn mk-btn-success" data-handover-submit disabled>Xác nhận giao xe</button>
            </div>`);
        state.modal = "handover";
        state.handoverPayload = null;

        try {
            const url = new URL(handlers.handoverInfo, location.origin);
            url.searchParams.set("id", String(t.id));
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" }, credentials: "same-origin" });
            const data = await res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
            if (!data.ok) {
                openModal(`
                    <h2>Xác nhận giao xe</h2>
                    <p class="mk-note" style="color:#b91c1c">${data.error || "Không thể tải dữ liệu giao xe."}</p>
                    <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
                return;
            }
            const kmOk = isValidKm(data.currentKm) && !data.blockReason;
            const requiresFuelInput = !!data.requiresFuelInput;
            const knownFuelOk = !requiresFuelInput && isValidFuelPercent(data.fuelLevel);

            state.handoverPayload = kmOk
                ? {
                    id: t.id,
                    odometerKm: Number(data.currentKm),
                    fuelLevel: knownFuelOk ? Number(data.fuelLevel) : null,
                    requiresFuelInput
                }
                : null;

            const kmText = kmOk ? `${Number(data.currentKm).toLocaleString("vi-VN")} km` : "—";
            let fuelBlock;
            if (!kmOk) {
                fuelBlock = `<div><dt>Mức nhiên liệu</dt><dd>—</dd></div>`;
            } else if (requiresFuelInput) {
                fuelBlock = `<div><dt>Mức nhiên liệu khi giao xe (%)</dt><dd>
                    <input type="number" min="0" max="100" step="1" placeholder="Nhập %"
                           inputmode="numeric" data-handover-fuel-input class="mk-input" style="max-width:8rem" />
                </dd></div>`;
            } else {
                fuelBlock = `<div><dt>Mức nhiên liệu</dt><dd><strong>${Number(data.fuelLevel).toLocaleString("vi-VN")}% 🔒</strong></dd></div>`;
            }

            const canConfirmNow = kmOk && knownFuelOk;
            openModal(`
                <h2>Xác nhận giao xe</h2>
                <dl class="mk-dl">
                    <div><dt>Xe</dt><dd>${data.vehicle || t.vehicle || "—"}</dd></div>
                    <div><dt>Biển số</dt><dd>${data.plate || t.plate || "—"}</dd></div>
                    <div><dt>KM hiện tại</dt><dd><strong>${kmText}</strong></dd></div>
                    ${fuelBlock}
                </dl>
                ${!kmOk ? `<p class="mk-note" style="color:#b91c1c">${data.blockReason || "Xe chưa có số KM hiện tại trong hệ thống."}</p>` : ""}
                ${kmOk && requiresFuelInput ? `<p class="mk-note">Xe chưa có dữ liệu nhiên liệu, vui lòng nhập mức nhiên liệu thực tế.</p>` : ""}
                ${kmOk && !requiresFuelInput ? `<p class="mk-note">KM và mức nhiên liệu lấy từ hệ thống — không thể chỉnh sửa tại đây.</p>` : ""}
                ${kmOk && requiresFuelInput ? `<p class="mk-note">Số km lấy từ hồ sơ xe trên hệ thống — không thể chỉnh sửa tại đây.</p>` : ""}
                <div class="mk-actions">
                    <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                    <button type="button" class="mk-btn mk-btn-success" data-handover-submit ${canConfirmNow ? "" : "disabled"}>Xác nhận giao xe</button>
                </div>`);
            state.modal = "handover";
            if (kmOk && requiresFuelInput) {
                const input = document.querySelector("[data-handover-fuel-input]");
                input?.addEventListener("input", syncTripsHandoverConfirm);
                input?.addEventListener("change", syncTripsHandoverConfirm);
            }
        } catch (_) {
            openModal(`
                <h2>Xác nhận giao xe</h2>
                <p class="mk-note" style="color:#b91c1c">Lỗi mạng khi tải dữ liệu xe.</p>
                <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
        }
    }

    async function returnForm(t) {
        if (t.mode !== "SelfDrive") {
            toast("WithDriver complete: thiếu API dispatcher — không ghi Backend. (Mock UI only.)");
            return;
        }
        openModal(`
            <h2>Xác nhận tiếp nhận trả xe</h2>
            <p class="mk-note">Đang tải dữ liệu xe...</p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                <button type="button" class="mk-btn mk-btn-success" data-return-submit disabled>Xác nhận trả xe</button>
            </div>`);
        state.modal = "return";
        state.returnPayload = null;
        try {
            const url = new URL(handlers.returnInfo, location.origin);
            url.searchParams.set("id", String(t.id));
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" }, credentials: "same-origin" });
            const data = await res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
            if (!data.ok) {
                openModal(`
                    <h2>Xác nhận tiếp nhận trả xe</h2>
                    <p class="mk-note" style="color:#b91c1c">${data.error || "Không thể tải dữ liệu trả xe."}</p>
                    <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
                return;
            }
            const handoverNote = data.handoverOdometerKm != null
                ? `<p class="mk-note">KM lúc giao: <strong>${Number(data.handoverOdometerKm).toLocaleString("vi-VN")} km</strong>. Số KM khi trả phải không nhỏ hơn mức này.</p>`
                : "";
            openModal(`
                <h2>Xác nhận tiếp nhận trả xe</h2>
                <dl class="mk-dl">
                    <div><dt>Xe</dt><dd>${data.vehicle || t.vehicle || "—"}</dd></div>
                    <div><dt>Biển số</dt><dd>${data.plate || t.plate || "—"}</dd></div>
                </dl>
                ${handoverNote}
                <form id="opReturnForm" data-id="${t.id}" class="op-form-grid">
                    <label class="mk-field"><span>Số KM khi trả xe *</span>
                        <input name="odo" type="text" inputmode="decimal" placeholder="Nhập số km" autocomplete="off" />
                    </label>
                    <label class="mk-field"><span>Mức nhiên liệu khi trả xe (%)</span>
                        <input name="fuel" type="text" inputmode="decimal" placeholder="Nhập %" autocomplete="off" />
                    </label>
                    <label class="mk-field"><span>Ngoại thất</span>
                        <select name="exterior"><option value="">—</option><option>Tốt</option><option>Cần kiểm tra</option></select>
                    </label>
                    <label class="mk-field"><span>Tình trạng kỹ thuật</span>
                        <select name="tech"><option value="">—</option><option>Tốt</option><option>Cần kiểm tra</option></select>
                    </label>
                    <label class="mk-field" style="grid-column:1/-1"><span>Ghi chú</span><input name="note" value="" /></label>
                </form>
                <p class="mk-note" id="opReturnError" style="color:#b91c1c"></p>
                <div class="mk-actions">
                    <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                    <button type="submit" form="opReturnForm" class="mk-btn mk-btn-success" data-return-submit>Xác nhận trả xe</button>
                </div>`);
            state.modal = "return";
            state.returnPayload = { id: t.id };
            const form = document.getElementById("opReturnForm");
            form?.addEventListener("input", () => {
                const box = document.getElementById("opReturnError");
                if (box) box.textContent = "";
            });
        } catch (_) {
            openModal(`
                <h2>Xác nhận tiếp nhận trả xe</h2>
                <p class="mk-note" style="color:#b91c1c">Lỗi mạng khi tải dữ liệu xe.</p>
                <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
        }
    }

    function incidentForm(t) {
        const i = t.incident;
        if (!i) {
            toast("Chuyến này chưa có sự cố từ API. Dispatcher không có API tạo sự cố.");
            return;
        }
        openModal(`
            <h2>Báo cáo sự cố</h2>
            <p class="mk-note">Dữ liệu từ GET /api/dispatch/incidents. Không có API cập nhật trạng thái sự cố cho dispatcher.</p>
            <dl class="mk-dl">
                <div><dt>Mã đơn</dt><dd>#${t.id}</dd></div>
                <div><dt>Xe</dt><dd>${t.vehicle}<br>${t.plate}</dd></div>
                <div><dt>Người báo</dt><dd>${i.reporter}</dd></div>
                <div><dt>Thời gian</dt><dd>${i.time}</dd></div>
                <div><dt>Loại</dt><dd>${i.type}</dd></div>
                <div><dt>Trạng thái API</dt><dd id="opIncStatus">${i.status}</dd></div>
            </dl>
            <p>${i.desc || ""}</p>
            <div class="op-photo">[ Ảnh hiện trường — không có API media ]</div>
            <p class="mk-note" style="margin-top:.7rem">Nút trạng thái dưới đây chỉ đổi UI cục bộ — chưa gọi API.</p>
            <div class="mk-actions" style="margin-top:.7rem">
                <button type="button" class="mk-btn mk-btn-primary" data-inc-status="Tiếp nhận" data-for="${t.id}">Tiếp nhận</button>
                <button type="button" class="mk-btn mk-btn-ghost" data-inc-status="Đang xử lý" data-for="${t.id}">Đang xử lý</button>
                <button type="button" class="mk-btn mk-btn-success" data-inc-status="Đã xử lý" data-for="${t.id}">Đã xử lý</button>
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
            </div>`);
        state.modal = "incident";
    }

    function settlementForm(t) {
        const c = t.charges || { base: t.totalAmount || 0, deposit: t.depositAmount || 0, extraKm: 0, extraFuel: 0, damage: 0, other: 0 };
        const total = Number(c.base || 0) + Number(c.extraFuel || 0) + Number(c.damage || 0) + Number(c.other || 0);
        const remain = total - Number(c.deposit || 0);
        openModal(`
            <h2>Quyết toán chuyến thuê</h2>
            <p class="mk-note">Số tiền lấy từ Booking/Fees API khi có. <strong>Chốt quyết toán</strong> không có endpoint — chỉ presentation.</p>
            <ol class="mk-note" style="padding-left:1.1rem">
                <li>Đơn #${t.id} · ${MODE_LABEL[t.mode] || t.mode}</li>
                <li>Xe: ${t.vehicle} ${t.plate}</li>
                <li>Khách: ${t.customer}</li>
                <li>Tổng (API): ${t.totalAmountText || money(total)}</li>
                <li>Cọc: ${t.depositAmountText || money(c.deposit)}</li>
            </ol>
            <table class="op-settle">
                <tr><th>Giá thuê cơ bản</th><td>${money(c.base)}</td></tr>
                <tr><th>Tiền cọc</th><td>${money(c.deposit)}</td></tr>
                <tr><th>Phí nhiên liệu</th><td>${money(c.extraFuel)}</td></tr>
                <tr><th>Hư hỏng</th><td>${money(c.damage)}</td></tr>
                <tr><th>Khác</th><td>${money(c.other)}</td></tr>
                <tr class="total"><th>Tổng cộng</th><td>${money(total)}</td></tr>
                <tr><th>Còn lại</th><td>${money(remain)}</td></tr>
            </table>
            <p>${settleBadge(t.settlement)}</p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                <button type="button" class="mk-btn mk-btn-success" data-close-settle="${t.id}">✓ Chốt quyết toán (mock)</button>
            </div>`);
        state.modal = "settle";
    }

    function render() {
        renderAlerts();
        renderDriver();
        renderMap();
        renderBoard();
        renderTable();
        if (state.selectedId) {
            const t = trip(state.selectedId);
            if (t) renderDrawer(t);
        }
    }

    fillVehicles();

    els.form.addEventListener("submit", (e) => {
        e.preventDefault();
        const data = new FormData(els.form);
        state.q = (data.get("q") || "").toString().trim();
        state.status = (data.get("status") || "").toString();
        state.mode = (data.get("mode") || "").toString();
        state.dstatus = (data.get("dstatus") || "").toString();
        state.vehicle = (data.get("vehicle") || "").toString();
        state.page = 1;
        render();
    });
    document.getElementById("opReset").addEventListener("click", (e) => {
        e.preventDefault();
        els.form.reset();
        state.q = state.status = state.mode = state.dstatus = state.vehicle = "";
        state.page = 1;
        render();
    });

    document.body.addEventListener("click", async (e) => {
        if (e.target.closest("[data-close]")) {
            state.selectedId = null;
            els.drawer.classList.remove("is-open");
            return;
        }
        if (e.target.closest("[data-close-modal]") || e.target === els.modal) {
            closeModal();
            return;
        }
        const page = e.target.closest("[data-page]");
        if (page) { state.page = Number(page.dataset.page); render(); return; }

        const contact = e.target.closest("[data-contact]");
        if (contact) { toast("Liên hệ — không có API gọi/SMS. Presentation only."); return; }
        const inspect = e.target.closest("[data-inspect]");
        if (inspect) {
            const t = trip(inspect.dataset.inspect);
            if (t && (t.handover || t.ret)) {
                toast("Đã có biên bản từ API — xem trong drawer chi tiết.");
                state.selectedId = t.id;
                renderDrawer(t);
            } else toast("Chưa có biên bản inspection từ API cho chuyến này.");
            return;
        }
        const report = e.target.closest("[data-report]");
        if (report) {
            toast("Dispatcher không có API tạo sự cố (chỉ tài xế POST incidents). Không gắn cờ giả như dữ liệu thật.");
            return;
        }
        const incSt = e.target.closest("[data-inc-status]");
        if (incSt) {
            toast(`UI only: «${incSt.dataset.incStatus}» — thiếu API cập nhật trạng thái sự cố dispatcher.`);
            const box = document.getElementById("opIncStatus");
            if (box) box.textContent = incSt.dataset.incStatus + " (local)";
            return;
        }
        const settleDone = e.target.closest("[data-close-settle]");
        if (settleDone) {
            toast("Chốt quyết toán — thiếu API. Không đổi trạng thái Backend.");
            closeModal();
            return;
        }
        const ho = e.target.closest("[data-handover]");
        if (ho) { handoverForm(trip(ho.dataset.handover)); return; }
        const handoverSubmit = e.target.closest("[data-handover-submit]");
        if (handoverSubmit) {
            if (handoverSubmit.disabled || !state.handoverPayload) return;
            syncTripsHandoverConfirm();
            const payload = state.handoverPayload;
            if (payload.fuelLevel === null || payload.fuelLevel === undefined) {
                toast("Vui lòng nhập mức nhiên liệu từ 0 đến 100.");
                return;
            }
            const url = handlers.handover + (handlers.handover.includes("?") ? "&" : "?") + "id=" + payload.id;
            handoverSubmit.disabled = true;
            const { ok, data: res } = await postForm(url, {
                odometerKm: payload.odometerKm,
                fuelLevel: payload.fuelLevel
            });
            if (!ok) {
                toast((res && res.error) || "Giao xe thất bại.");
                handoverSubmit.disabled = false;
                return;
            }
            if (res.trip) upsertTrip(res.trip);
            closeModal();
            toast("Đã giao xe.");
            await reloadFromServer();
            render();
            const updated = trip(payload.id);
            if (updated) renderDrawer(updated);
            return;
        }
        const rt = e.target.closest("[data-return]");
        if (rt) { returnForm(trip(rt.dataset.return)); return; }
        const inc = e.target.closest("[data-incident]");
        if (inc) { incidentForm(trip(inc.dataset.incident)); return; }
        const se = e.target.closest("[data-settle]");
        if (se) { settlementForm(trip(se.dataset.settle)); return; }

        const open = e.target.closest("[data-open]");
        if (open && !e.target.closest("[data-handover],[data-return],[data-incident],[data-settle]")) {
            state.selectedId = Number(open.dataset.open);
            if (handlers.detail) {
                const url = handlers.detail + (handlers.detail.includes("?") ? "&" : "?") + "id=" + state.selectedId;
                const { ok, data } = await getJson(url);
                if (ok && data.trip) upsertTrip(data.trip);
            }
            const t = trip(state.selectedId);
            if (t) renderDrawer(t);
        }
    });

    document.body.addEventListener("submit", async (e) => {
        if (e.target.id === "opHandoverForm") {
            e.preventDefault();
            return;
        }
        if (e.target.id === "opReturnForm") {
            e.preventDefault();
            const t = trip(e.target.dataset.id);
            if (!t) return;
            const data = new FormData(e.target);
            const odoRaw = String(data.get("odo") || "").trim();
            const fuelRaw = String(data.get("fuel") || "").trim();
            const box = document.getElementById("opReturnError");
            if (odoRaw === "") {
                if (box) box.textContent = "Vui lòng nhập số km khi trả xe.";
                return;
            }
            if (!Number.isFinite(Number(odoRaw))) {
                if (box) box.textContent = "Số km khi trả xe không hợp lệ.";
                return;
            }
            if (Number(odoRaw) < 0) {
                if (box) box.textContent = "Số km khi trả xe không được âm.";
                return;
            }
            if (fuelRaw === "") {
                if (box) box.textContent = "Vui lòng nhập mức nhiên liệu khi trả xe từ 0 đến 100.";
                return;
            }
            if (!Number.isFinite(Number(fuelRaw)) || Number(fuelRaw) < 0 || Number(fuelRaw) > 100) {
                if (box) box.textContent = "Mức nhiên liệu phải từ 0 đến 100.";
                return;
            }
            if (t.mode !== "SelfDrive") {
                toast("WithDriver complete: thiếu API dispatcher — không ghi Backend. (Mock UI only.)");
                closeModal();
                return;
            }
            const url = handlers.complete + (handlers.complete.includes("?") ? "&" : "?") + "id=" + t.id;
            const { ok, data: res } = await postForm(url, {
                odometerKm: data.get("odo"),
                fuelLevel: data.get("fuel"),
                exteriorCondition: data.get("exterior"),
                technicalCondition: data.get("tech"),
                notes: data.get("note")
            });
            if (!ok) {
                const box = document.getElementById("opReturnError");
                if (box) box.textContent = (res && res.error) || "Trả xe / hoàn thành thất bại.";
                else toast((res && res.error) || "Trả xe / hoàn thành thất bại.");
                return;
            }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã hoàn thành trả xe qua API (SelfDrive).");
            closeModal();
            await reloadFromServer();
            render();
        }
    });

    render();
})();

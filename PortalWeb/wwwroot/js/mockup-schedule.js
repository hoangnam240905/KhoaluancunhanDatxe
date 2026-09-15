(function () {
    const boot = window.__SCHEDULE_BOOTSTRAP__;
    const handlers = window.__SCHEDULE_HANDLERS__ || {};
    const ready = window.__SCHEDULE_READY__ === true;

    // Guard: new JS must not run against an old page without API bootstrap (causes empty gantt + stale metrics).
    if (!boot || typeof boot !== "object" || !Array.isArray(boot.vehicles) || !ready) {
        const board = document.getElementById("schVehicleBoard");
        const driverBoard = document.getElementById("schDriverBoard");
        const msg = `<p class="mk-empty">Schedule chưa nhận bootstrap API. Hãy rebuild/restart PortalWeb để đồng bộ page + JS (tránh DLL cũ + wwwroot mới).</p>`;
        if (board) board.innerHTML = msg;
        if (driverBoard) driverBoard.innerHTML = msg;
        console.error("[Schedule] Missing __SCHEDULE_BOOTSTRAP__ — aborting mock-free render.");
        return;
    }

    const BUFFER_H = Number(boot.bufferHours) || 2;
    const WEEKDAYS = ["Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "CN"];

    let vehicles = boot.vehicles.slice();
    let drivers = Array.isArray(boot.drivers) ? boot.drivers.slice() : [];
    let bookings = Array.isArray(boot.bookings) ? boot.bookings.slice() : [];
    let fleetRows = Array.isArray(boot.fleet) ? boot.fleet.slice() : [];
    let ganttVehicleIds = Array.isArray(boot.ganttVehicleIds) && boot.ganttVehicleIds.length
        ? boot.ganttVehicleIds.slice()
        : vehicles.map((v) => v.id);
    let ganttDriverIds = Array.isArray(boot.ganttDriverIds) && boot.ganttDriverIds.length
        ? boot.ganttDriverIds.slice()
        : drivers.map((d) => d.id);

    const focusIso = boot.focusIso || null;
    const initialFocus = focusIso ? new Date(focusIso + "T00:00:00") : new Date();

    const state = {
        view: "week",
        focus: initialFocus,
        q: "",
        vstatus: "",
        mode: "",
        dstatus: "",
        conflict: "",
        selectedId: null,
        panel: "detail",
        pendingVehicleId: null,
        pendingDriverId: null,
        assignableCache: {}
    };

    const els = {
        range: document.getElementById("schRange"),
        date: document.getElementById("schDate"),
        vehicleBoard: document.getElementById("schVehicleBoard"),
        driverBoard: document.getElementById("schDriverBoard"),
        fleet: document.getElementById("schFleetBody"),
        conflictBox: document.getElementById("schConflictBox"),
        conflictList: document.getElementById("schConflictList"),
        drawer: document.getElementById("schDrawer"),
        toast: document.getElementById("schToast"),
        form: document.getElementById("schFilter"),
        metricTotal: document.getElementById("schMetricTotal"),
        metricAvail: document.getElementById("schMetricAvail"),
        metricBusy: document.getElementById("schMetricBusy"),
        metricDrivers: document.getElementById("schMetricDrivers")
    };

    const parse = (s) => new Date(s);
    const pad = (n) => String(n).padStart(2, "0");
    const isoDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const fmtTime = (d) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;
    const fmtWhen = (d) => `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${fmtTime(d)}`;
    const hoursBetween = (a, b) => (b - a) / 36e5;

    function antiforgery() {
        const form = document.getElementById("schAntiForgery");
        const input = form && form.querySelector('input[name="__RequestVerificationToken"]');
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
        return { ok: res.ok && data && data.ok !== false, status: res.status, data };
    }

    async function getJson(url) {
        const res = await fetch(url, { credentials: "same-origin" });
        let data = null;
        try { data = await res.json(); } catch { /* ignore */ }
        return { ok: res.ok && data && data.ok !== false, status: res.status, data };
    }

    function applyMetrics(m) {
        if (!m) return;
        if (els.metricTotal) els.metricTotal.textContent = m.totalVehicles ?? "—";
        if (els.metricAvail) els.metricAvail.textContent = m.availableVehicles ?? "—";
        if (els.metricBusy) els.metricBusy.textContent = m.busyVehicles ?? "—";
        if (els.metricDrivers) els.metricDrivers.textContent = m.activeDrivers ?? "—";
    }

    applyMetrics(boot.metrics);

    function startOfWeek(d) {
        const x = new Date(d.getFullYear(), d.getMonth(), d.getDate());
        const day = x.getDay();
        x.setDate(x.getDate() + (day === 0 ? -6 : 1 - day));
        x.setHours(0, 0, 0, 0);
        return x;
    }
    function addDays(d, n) { const x = new Date(d); x.setDate(x.getDate() + n); return x; }
    function weekDays(start) { return Array.from({ length: 7 }, (_, i) => addDays(start, i)); }
    function vehicle(id) { return vehicles.find((v) => v.id === id) || { id, name: "Xe chưa rõ", plate: "—", status: "Available" }; }
    function driver(id) { return drivers.find((d) => d.id === id) || { id, name: "Tài xế chưa rõ", status: "Available" }; }

    function vehicleConflicts() {
        const map = {};
        vehicles.forEach((v) => {
            const list = bookings.filter((b) => b.vehicleId === v.id).sort((a, b) => parse(a.start) - parse(b.start));
            for (let i = 0; i < list.length - 1; i++) {
                const a = list[i], b = list[i + 1];
                const gap = hoursBetween(parse(a.end), parse(b.start));
                if (gap < BUFFER_H) {
                    const item = { a, b, gap, vehicle: v, kind: "vehicle" };
                    map[a.id] = item; map[b.id] = item;
                }
            }
        });
        return map;
    }

    function driverConflicts() {
        const map = {};
        drivers.forEach((d) => {
            const list = bookings.filter((b) => b.driverId === d.id).sort((a, b) => parse(a.start) - parse(b.start));
            for (let i = 0; i < list.length - 1; i++) {
                const a = list[i], b = list[i + 1];
                const overlap = parse(b.start) < parse(a.end) || hoursBetween(parse(a.end), parse(b.start)) < BUFFER_H;
                if (overlap) {
                    const item = { a, b, gap: hoursBetween(parse(a.end), parse(b.start)), driver: d, kind: "driver" };
                    map[a.id] = item; map[b.id] = item;
                }
            }
        });
        return map;
    }

    function matchesBooking(b) {
        const v = b.vehicleId ? vehicle(b.vehicleId) : null;
        const d = b.driverId ? driver(b.driverId) : null;
        if (state.mode && b.mode !== state.mode) return false;
        if (state.vstatus && (!v || v.status !== state.vstatus)) return false;
        if (state.dstatus && (!d || d.status !== state.dstatus)) return false;
        const vConf = vehicleConflicts();
        const dConf = driverConflicts();
        const has = Boolean(vConf[b.id] || dConf[b.id]);
        if (state.conflict === "yes" && !has) return false;
        if (state.conflict === "no" && has) return false;
        if (state.q) {
            const q = state.q.toLowerCase();
            const blob = `#${b.id} ${b.customer} ${v ? v.name : ""} ${v ? v.plate : ""} ${d ? d.name : ""} ${b.mode}`.toLowerCase();
            if (!blob.includes(q)) return false;
        }
        return true;
    }

    function visibleBookings() { return bookings.filter(matchesBooking); }

    function inView(b) {
        if (state.view === "day") {
            const start = new Date(state.focus.getFullYear(), state.focus.getMonth(), state.focus.getDate());
            const end = addDays(start, 1);
            return parse(b.start) < end && parse(b.end) > start;
        }
        const ws = startOfWeek(state.focus);
        return parse(b.start) < addDays(ws, 7) && parse(b.end) > ws;
    }

    function rangeLabel() {
        if (state.view === "day") {
            const d = state.focus;
            return `${WEEKDAYS[(d.getDay() + 6) % 7]} ${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
        }
        if (state.view === "month") {
            return `Tháng ${pad(state.focus.getMonth() + 1)}, ${state.focus.getFullYear()}`;
        }
        const s = startOfWeek(state.focus);
        const e = addDays(s, 6);
        return `${s.getDate()} – ${e.getDate()} Tháng ${pad(s.getMonth() + 1)}, ${s.getFullYear()}`;
    }

    function pctInWeek(date, weekStart) {
        const end = addDays(weekStart, 7);
        const total = end - weekStart;
        return ((date - weekStart) / total) * 100;
    }

    function pctInDay(date, day) {
        const start = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 6, 0, 0);
        const end = new Date(day.getFullYear(), day.getMonth(), day.getDate(), 22, 0, 0);
        const t = Math.min(end, Math.max(start, date));
        return ((t - start) / (end - start)) * 100;
    }

    function blockClass(b) {
        const conf = vehicleConflicts()[b.id] || driverConflicts()[b.id];
        if (conf) return "conflict";
        if (b.vehicleId && vehicle(b.vehicleId).status === "Preparing") return "prep";
        return b.mode === "WithDriver" ? "with" : "self";
    }

    function renderBlocks(track, resourceBookings, weekStart, dayScaleDay) {
        resourceBookings.forEach((b) => {
            const start = parse(b.start);
            const end = parse(b.end);
            let left, width, bufLeft, bufWidth;
            if (dayScaleDay) {
                left = pctInDay(start, dayScaleDay);
                width = Math.max(4, pctInDay(end, dayScaleDay) - left);
                const bufEnd = new Date(end.getTime() + BUFFER_H * 36e5);
                bufLeft = pctInDay(end, dayScaleDay);
                bufWidth = Math.max(2.5, pctInDay(bufEnd, dayScaleDay) - bufLeft);
            } else {
                left = pctInWeek(start, weekStart);
                width = Math.max(3.2, pctInWeek(end, weekStart) - left);
                const bufEnd = new Date(end.getTime() + BUFFER_H * 36e5);
                bufLeft = pctInWeek(end, weekStart);
                bufWidth = Math.max(1.1, pctInWeek(bufEnd, weekStart) - bufLeft);
            }
            const conf = vehicleConflicts()[b.id];
            track.insertAdjacentHTML("beforeend", `
                <div class="sch-buffer" style="left:${bufLeft}%;width:${bufWidth}%" title="Khoảng đệm kỹ thuật — ${BUFFER_H} giờ">Đệm ${BUFFER_H} giờ</div>
                <button type="button" class="sch-block ${blockClass(b)}" style="left:${left}%;width:${width}%" data-open="${b.id}" data-panel="detail">
                    #${b.id} ${b.customer}
                    <small>${b.mode === "WithDriver" ? "Có tài xế" : "Tự lái"} · ${fmtTime(start)} – ${fmtTime(end)}${conf ? " · ⚠️" : ""}</small>
                </button>`);
        });
    }

    function dayHeaders(days) {
        return `<div class="sch-days">${days.map((d, i) =>
            `<span>${WEEKDAYS[i]} ${pad(d.getDate())}/${pad(d.getMonth() + 1)}</span>`).join("")}</div>`;
    }

    function renderVehicleGantt() {
        const weekStart = startOfWeek(state.focus);
        const days = weekDays(weekStart);
        const vConf = vehicleConflicts();
        const shown = ganttVehicleIds
            .map((id) => vehicle(id))
            .filter((v) => !state.vstatus || v.status === state.vstatus)
            .filter((v) => {
                if (!state.q) return true;
                const q = state.q.toLowerCase();
                return `${v.name} ${v.plate}`.toLowerCase().includes(q)
                    || visibleBookings().some((b) => b.vehicleId === v.id);
            });

        if (state.view === "month") {
            els.vehicleBoard.innerHTML = renderMonth(shown);
            return;
        }

        const headerDays = state.view === "day"
            ? `<div class="sch-days hours">${Array.from({ length: 16 }, (_, i) => `<span>${pad(i + 6)}:00</span>`).join("")}</div>`
            : dayHeaders(days);

        const rows = shown.map((v) => {
            const list = visibleBookings().filter((b) => b.vehicleId === v.id && inView(b));
            const hasConf = list.some((b) => vConf[b.id]);
            return `<div class="sch-row">
                <div class="sch-res">
                    <strong>${v.name}</strong>
                    <span class="mk-muted">${v.plate}</span>
                    <div>${statusBadge(v.status)}${hasConf ? ' <span class="mk-badge mk-badge-warn">⚠️ Xung đột</span>' : ""}</div>
                </div>
                <div class="sch-track ${state.view === "day" ? "day-scale" : ""}" data-track="${v.id}"></div>
            </div>`;
        }).join("");

        els.vehicleBoard.innerHTML = `<div class="sch-gantt">
            <div class="sch-gantt-head"><div class="sch-res">Xe</div>${headerDays}</div>
            ${rows || `<p class="mk-empty">Không có xe khớp bộ lọc.</p>`}
        </div>`;

        shown.forEach((v) => {
            const track = els.vehicleBoard.querySelector(`[data-track="${v.id}"]`);
            if (!track) return;
            const list = visibleBookings().filter((b) => b.vehicleId === v.id && inView(b));
            renderBlocks(track, list, weekStart, state.view === "day" ? state.focus : null);
        });
    }

    function renderMonth(shownVs) {
        const first = new Date(state.focus.getFullYear(), state.focus.getMonth(), 1);
        const start = startOfWeek(first);
        const cells = Array.from({ length: 42 }, (_, i) => addDays(start, i));
        const head = WEEKDAYS.map((w) => `<span>${w}</span>`).join("");
        const body = cells.map((d) => {
            const list = visibleBookings().filter((b) => {
                const s = parse(b.start), e = parse(b.end);
                const dayEnd = addDays(d, 1);
                return s < dayEnd && e > d && shownVs.some((v) => v.id === b.vehicleId);
            });
            const chips = list.slice(0, 3).map((b) => {
                const conf = vehicleConflicts()[b.id];
                return `<button type="button" class="sch-month-chip ${conf ? "conflict" : ""}" data-open="${b.id}" data-panel="detail">#${b.id} ${vehicle(b.vehicleId).plate}</button>`;
            }).join("");
            return `<div class="sch-month-cell"><strong>${d.getDate()}</strong>${chips}</div>`;
        }).join("");
        return `<div class="sch-month-grid">${head}${body}</div>`;
    }

    function renderDriverGantt() {
        const weekStart = startOfWeek(state.focus);
        const days = weekDays(weekStart);
        const dConf = driverConflicts();
        const shown = ganttDriverIds
            .map((id) => driver(id))
            .filter((d) => !state.dstatus || d.status === state.dstatus)
            .filter((d) => {
                if (!state.q) return true;
                const q = state.q.toLowerCase();
                return d.name.toLowerCase().includes(q)
                    || visibleBookings().some((b) => b.driverId === d.id);
            });

        if (state.view === "month") {
            els.driverBoard.innerHTML = `<p class="mk-muted" style="padding:.5rem">Chế độ tháng chỉ hiển thị lịch xe. Chuyển về Tuần để xem timeline tài xế.</p>`;
            return;
        }

        const headerDays = state.view === "day"
            ? `<div class="sch-days hours">${Array.from({ length: 16 }, (_, i) => `<span>${pad(i + 6)}:00</span>`).join("")}</div>`
            : dayHeaders(days);

        const rows = shown.map((d) => {
            const hasConf = visibleBookings().some((b) => b.driverId === d.id && dConf[b.id] && inView(b));
            const icon = d.status === "Available" ? "🟢" : d.status === "Busy" ? "🟡" : "⚫";
            return `<div class="sch-row">
                <div class="sch-res">
                    <strong>${icon} ${d.name}</strong>
                    <span class="mk-muted">${d.status}</span>
                    ${hasConf ? '<div class="mk-badge mk-badge-warn">⚠️ Xung đột tài xế</div>' : ""}
                </div>
                <div class="sch-track ${state.view === "day" ? "day-scale" : ""}" data-dtrack="${d.id}"></div>
            </div>`;
        }).join("");

        els.driverBoard.innerHTML = `<div class="sch-gantt">
            <div class="sch-gantt-head"><div class="sch-res">Tài xế</div>${headerDays}</div>
            ${rows || `<p class="mk-empty">Không có tài xế khớp bộ lọc.</p>`}
        </div>`;

        shown.forEach((d) => {
            const track = els.driverBoard.querySelector(`[data-dtrack="${d.id}"]`);
            if (!track) return;
            renderBlocks(track, visibleBookings().filter((b) => b.driverId === d.id && inView(b)), weekStart, state.view === "day" ? state.focus : null);
        });
    }

    function statusBadge(st) {
        if (st === "Available") return `<span class="mk-badge mk-badge-ok">🟢 Khả dụng</span>`;
        if (st === "Busy") return `<span class="mk-badge mk-badge-warn">🔴 Đang thuê</span>`;
        if (st === "Preparing") return `<span class="mk-badge mk-badge-pending">🟡 Đang chuẩn bị</span>`;
        return `<span class="mk-badge mk-badge-gray">⚫ Bảo trì</span>`;
    }

    function currentBookingForVehicle(v) {
        const now = new Date();
        return bookings.find((b) => b.vehicleId === v.id && parse(b.start) <= now && parse(b.end) >= now)
            || bookings.find((b) => b.vehicleId === v.id && parse(b.end) >= now)
            || null;
    }

    function renderFleet() {
        const source = fleetRows.length
            ? fleetRows
            : vehicles.map((v) => {
                const b = currentBookingForVehicle(v);
                return {
                    vehicleId: v.id,
                    name: v.name,
                    plate: v.plate,
                    status: v.status,
                    bookingId: b ? b.id : null,
                    start: b ? b.start : null,
                    end: b ? b.end : null
                };
            });

        const rows = source.filter((row) => {
            if (state.vstatus && row.status !== state.vstatus) return false;
            if (state.q) {
                const q = state.q.toLowerCase();
                const blob = `${row.name} ${row.plate} ${row.bookingId ? "#" + row.bookingId : ""}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        }).map((row) => `<tr>
                <td>${row.name}</td>
                <td>${row.plate}</td>
                <td>${statusBadge(row.status)}</td>
                <td>${row.bookingId ? "#" + row.bookingId : "—"}</td>
                <td>${row.start ? fmtWhen(parse(row.start)) : "—"}</td>
                <td>${row.end ? fmtWhen(parse(row.end)) : "—"}</td>
            </tr>`).join("");
        els.fleet.innerHTML = rows || `<tr><td colspan="6">Không có xe khớp bộ lọc.</td></tr>`;
    }

    function renderConflicts() {
        const items = [];
        const seen = new Set();
        Object.values(vehicleConflicts()).forEach((c) => {
            const key = `v-${c.a.id}-${c.b.id}`;
            if (seen.has(key)) return;
            seen.add(key);
            if (!matchesBooking(c.a) && !matchesBooking(c.b)) return;
            items.push({ ...c, kind: "vehicle" });
        });
        Object.values(driverConflicts()).forEach((c) => {
            const key = `d-${c.a.id}-${c.b.id}`;
            if (seen.has(key)) return;
            seen.add(key);
            if (!matchesBooking(c.a) && !matchesBooking(c.b)) return;
            items.push({ ...c, kind: "driver" });
        });
        if (!items.length) {
            els.conflictBox.hidden = true;
            return;
        }
        els.conflictBox.hidden = false;
        els.conflictList.innerHTML = items.map((c) => {
            if (c.kind === "driver") {
                return `<div class="sch-conflict-item">
                    <div>
                        <strong>⚠️ Xung đột tài xế</strong>
                        <div>#${c.a.id} và #${c.b.id} — ${c.driver.name} đang có lịch trong khoảng thời gian này.</div>
                        <div class="mk-muted">${fmtWhen(parse(c.a.start))} → ${fmtWhen(parse(c.a.end))} trùng ${fmtWhen(parse(c.b.start))} → ${fmtWhen(parse(c.b.end))}</div>
                    </div>
                    <button type="button" class="mk-btn mk-btn-danger" data-open="${c.b.id}" data-panel="driver">Tài xế thay thế</button>
                </div>`;
            }
            const gapTxt = c.gap < 0 ? "hai lịch chồng lên nhau" : `Khoảng cách giữa hai lịch chỉ ${c.gap.toFixed(0)} giờ.`;
            return `<div class="sch-conflict-item">
                <div>
                    <strong>Đơn #${c.a.id}</strong> ${c.vehicle.name}<br>
                    ${fmtWhen(parse(c.a.start))} → ${fmtWhen(parse(c.a.end))}<br>
                    Xung đột với <strong>đơn #${c.b.id}</strong> ${fmtWhen(parse(c.b.start))} → ${fmtWhen(parse(c.b.end))}<br>
                    <span class="mk-muted">${gapTxt} Yêu cầu tối thiểu: ${BUFFER_H} giờ.</span><br>
                    <span class="mk-badge mk-badge-warn">Không thể phân công</span>
                </div>
                <button type="button" class="mk-btn mk-btn-primary" data-open="${c.b.id}" data-panel="vehicle">Xe thay thế đề xuất</button>
            </div>`;
        }).join("");
    }

    function altVehiclesLocal(b) {
        return vehicles.filter((v) => v.id !== b.vehicleId && v.status === "Available").slice(0, 5);
    }
    function altDriversLocal(b) {
        return drivers.filter((d) => d.id !== b.driverId && d.status === "Available").slice(0, 5);
    }

    async function loadAssignable(bookingId) {
        if (!handlers.assignable) return null;
        if (state.assignableCache[bookingId]) return state.assignableCache[bookingId];
        const url = handlers.assignable + (handlers.assignable.includes("?") ? "&" : "?") + "id=" + encodeURIComponent(bookingId);
        const { ok, data } = await getJson(url);
        if (!ok || !data) return null;
        const pack = { vehicles: data.vehicles || [], drivers: data.drivers || [] };
        state.assignableCache[bookingId] = pack;
        return pack;
    }

    function renderAlts(list, kind, bookingId) {
        if (!list.length) return `<p class="mk-note">Không có phương án khả dụng từ API.</p>`;
        if (kind === "vehicle") {
            return list.map((x) => `<div class="sch-alt"><div><strong>${x.name}</strong><div class="mk-muted">${x.plate || ""}</div></div>
                <span class="mk-badge mk-badge-ok">🟢 Khả dụng</span>
                <button type="button" class="mk-btn mk-btn-primary" data-pick-vehicle="${x.id}" data-for="${bookingId}">Chọn xe</button></div>`).join("");
        }
        return list.map((x) => `<div class="sch-alt"><div><strong>${x.name}</strong></div>
            <span class="mk-badge mk-badge-ok">🟢 Khả dụng</span>
            <button type="button" class="mk-btn mk-btn-primary" data-pick-driver="${x.id}" data-for="${bookingId}">Chọn tài xế</button></div>`).join("");
    }

    async function renderDrawer(b, panel) {
        const v = b.vehicleId ? vehicle(b.vehicleId) : { name: "Chưa gán xe", plate: "—", status: "Available" };
        const d = b.driverId ? driver(b.driverId) : null;
        const vConf = vehicleConflicts()[b.id];
        const dConf = driverConflicts()[b.id];
        const end = parse(b.end);
        const buf = new Date(end.getTime() + BUFFER_H * 36e5);

        let extra = "";
        if (panel === "vehicle" || panel === "driver") {
            extra = `<section class="mk-block"><h3>${panel === "vehicle" ? "Xe thay thế đề xuất" : "Tài xế thay thế"}</h3>
                <p class="mk-note">Đang tải danh sách khả dụng…</p></section>`;
        }
        if (panel === "dispatch") {
            const pickV = state.pendingVehicleId ? vehicle(state.pendingVehicleId) : v;
            const pickD = state.pendingDriverId ? driver(state.pendingDriverId) : d;
            extra = `<section class="mk-block"><h3>⚡ Phân công</h3>
                <p class="mk-note">Đơn #${b.id}</p>
                <dl class="mk-dl">
                    <div><dt>Xe</dt><dd>${pickV.name}<br>${pickV.plate || ""}</dd></div>
                    <div><dt>Tài xế</dt><dd>${b.mode === "SelfDrive" ? "Tự lái — không gắn tài xế" : (pickD ? pickD.name : "Chưa chọn")}</dd></div>
                </dl>
                <ul class="sch-flow">
                    <li>${fmtTime(parse(b.start))} bắt đầu</li>
                    <li>${fmtTime(end)} kết thúc</li>
                    <li class="buf">${fmtTime(buf)} Đệm ${BUFFER_H} giờ</li>
                </ul>
                <div class="mk-actions" style="margin-top:.75rem">
                    <button type="button" class="mk-btn mk-btn-success" data-assign="${b.id}">✓ Xác nhận phân công</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="vehicle">↔ Chọn xe khác</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="driver">↔ Chọn tài xế khác</button>
                </div>
            </section>`;
        }

        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-2">Đơn #${b.id}</h2>
                    <span class="mk-badge mk-badge-${(b.status || "").toLowerCase()}">${({ Pending: "Chờ xác nhận", Confirmed: "Đã xác nhận", Assigned: "Đã phân công", InProgress: "Đang thực hiện", Completed: "Hoàn thành", Cancelled: "Đã hủy" })[b.status] || b.status}</span>
                    ${vConf ? '<span class="mk-badge mk-badge-warn">⚠️ Xung đột xe</span>' : ""}
                    ${dConf ? '<span class="mk-badge mk-badge-warn">⚠️ Xung đột tài xế</span>' : ""}
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <section class="mk-block">
                    <h3>Chi tiết</h3>
                    <dl class="mk-dl">
                        <div><dt>Khách</dt><dd>${b.customer}</dd></div>
                        <div><dt>Xe</dt><dd>${v.name} · ${v.plate || "—"}</dd></div>
                        <div><dt>Bắt đầu</dt><dd>${fmtWhen(parse(b.start))}</dd></div>
                        <div><dt>Kết thúc</dt><dd>${fmtWhen(end)}</dd></div>
                        <div><dt>Hình thức</dt><dd>${b.mode === "WithDriver" ? "Có tài xế" : "Tự lái"}</dd></div>
                        <div><dt>Tài xế</dt><dd>${d ? d.name : "—"}</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Khoảng đệm kỹ thuật — ${BUFFER_H} giờ</h3>
                    <p class="mk-note">Kết thúc ${fmtTime(end)} → đệm đến ${fmtTime(buf)}. Đệm hiển thị trên UI; kiểm tra xung đột client theo cùng ngưỡng ${BUFFER_H}h (khớp hằng số kỹ thuật Backend).</p>
                </section>
                ${vConf ? `<p class="mk-note">Khoảng cách với #${vConf.a.id === b.id ? vConf.b.id : vConf.a.id}: ${vConf.gap.toFixed(0)} giờ (yêu cầu ${BUFFER_H} giờ).</p>` : '<p class="mk-badge mk-badge-ok">✅ Không xung đột xe (theo lịch đơn hiện có)</p>'}
                <div class="mk-actions" style="margin:0.8rem 0">
                    <button type="button" class="mk-btn mk-btn-primary" data-open="${b.id}" data-panel="dispatch">⚡ Phân công</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="vehicle">Xe thay thế</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="driver">Tài xế thay thế</button>
                </div>
                ${extra}
            </div>`;

        if (panel === "vehicle" || panel === "driver") {
            const pack = await loadAssignable(b.id);
            const list = panel === "vehicle"
                ? (pack && pack.vehicles.length ? pack.vehicles : altVehiclesLocal(b))
                : (pack && pack.drivers.length ? pack.drivers : altDriversLocal(b));
            const note = pack
                ? "Từ API assignable (xe/tài xế khả dụng theo đơn)."
                : "Assignable API không trả dữ liệu — dùng danh sách Available cục bộ.";
            const block = els.drawer.querySelector(".mk-drawer-body .mk-block:last-child");
            if (block) {
                block.innerHTML = `<h3>${panel === "vehicle" ? "Xe thay thế đề xuất" : "Tài xế thay thế"}</h3>
                    <p class="mk-note">${note}</p>
                    ${renderAlts(list, panel, b.id)}`;
            }
        }
    }

    function toast(msg) {
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
    }

    function upsertBooking(row) {
        if (!row || !row.id) return;
        const i = bookings.findIndex((x) => x.id === row.id);
        if (i >= 0) bookings[i] = { ...bookings[i], ...row };
        else bookings.push(row);
        state.assignableCache = {};
    }

    async function reloadFromServer() {
        if (!handlers.reload) return;
        const { ok, data } = await getJson(handlers.reload);
        if (!ok || !data || !data.data) return;
        const d = data.data;
        vehicles = Array.isArray(d.vehicles) ? d.vehicles : vehicles;
        drivers = Array.isArray(d.drivers) ? d.drivers : drivers;
        bookings = Array.isArray(d.bookings) ? d.bookings : bookings;
        fleetRows = Array.isArray(d.fleet) ? d.fleet : fleetRows;
        ganttVehicleIds = Array.isArray(d.ganttVehicleIds) ? d.ganttVehicleIds : ganttVehicleIds;
        ganttDriverIds = Array.isArray(d.ganttDriverIds) ? d.ganttDriverIds : ganttDriverIds;
        applyMetrics(d.metrics);
        state.assignableCache = {};
    }

    function render() {
        els.range.textContent = rangeLabel();
        els.date.value = isoDate(state.focus);
        document.querySelectorAll("#schView .mk-pill").forEach((p) => p.classList.toggle("is-on", p.dataset.view === state.view));
        document.getElementById("schThis").textContent = state.view === "day" ? "Hôm nay" : state.view === "month" ? "Tháng này" : "Tuần này";
        renderConflicts();
        renderVehicleGantt();
        renderDriverGantt();
        renderFleet();
        if (state.selectedId) {
            const b = bookings.find((x) => x.id === state.selectedId);
            if (b) renderDrawer(b, state.panel);
        }
    }

    function shift(n) {
        if (state.view === "day") state.focus = addDays(state.focus, n);
        else if (state.view === "month") state.focus = new Date(state.focus.getFullYear(), state.focus.getMonth() + n, 1);
        else state.focus = addDays(state.focus, n * 7);
        render();
    }

    function goToday() {
        const t = new Date();
        state.focus = new Date(t.getFullYear(), t.getMonth(), t.getDate());
    }

    document.getElementById("schPrev").addEventListener("click", () => shift(-1));
    document.getElementById("schNext").addEventListener("click", () => shift(1));
    document.getElementById("schThis").addEventListener("click", () => { goToday(); render(); });
    document.getElementById("schToday").addEventListener("click", () => { goToday(); state.view = "day"; render(); });
    els.date.addEventListener("change", () => {
        if (els.date.value) state.focus = new Date(els.date.value + "T00:00:00");
        render();
    });
    document.getElementById("schView").addEventListener("click", (e) => {
        const btn = e.target.closest("[data-view]");
        if (!btn) return;
        state.view = btn.dataset.view;
        render();
    });
    els.form.addEventListener("submit", (e) => {
        e.preventDefault();
        const data = new FormData(els.form);
        state.q = (data.get("q") || "").toString().trim();
        state.vstatus = (data.get("vstatus") || "").toString();
        state.mode = (data.get("mode") || "").toString();
        state.dstatus = (data.get("dstatus") || "").toString();
        state.conflict = (data.get("conflict") || "").toString();
        render();
    });
    document.getElementById("schReset").addEventListener("click", (e) => {
        e.preventDefault();
        els.form.reset();
        state.q = state.vstatus = state.mode = state.dstatus = state.conflict = "";
        render();
    });
    document.body.addEventListener("click", async (e) => {
        if (e.target.closest("[data-close]")) {
            state.selectedId = null;
            els.drawer.classList.remove("is-open");
            return;
        }
        const pickV = e.target.closest("[data-pick-vehicle]");
        if (pickV) {
            const b = bookings.find((x) => x.id === Number(pickV.dataset.for));
            if (!b) return;
            state.pendingVehicleId = pickV.dataset.pickVehicle;
            toast(`Đã chọn ${vehicle(state.pendingVehicleId).name} — xác nhận phân công để gọi API.`);
            state.panel = "dispatch";
            await renderDrawer(b, "dispatch");
            return;
        }
        const pickD = e.target.closest("[data-pick-driver]");
        if (pickD) {
            const b = bookings.find((x) => x.id === Number(pickD.dataset.for));
            if (!b) return;
            state.pendingDriverId = pickD.dataset.pickDriver;
            toast(`Đã chọn ${driver(state.pendingDriverId).name} — xác nhận phân công để gọi API.`);
            state.panel = "dispatch";
            await renderDrawer(b, "dispatch");
            return;
        }
        const assign = e.target.closest("[data-assign]");
        if (assign) {
            const b = bookings.find((x) => x.id === Number(assign.dataset.assign));
            if (!b || !handlers.assign) return;
            const vehicleId = state.pendingVehicleId || b.vehicleId;
            const driverId = b.mode === "SelfDrive" ? null : (state.pendingDriverId || b.driverId);
            if (!vehicleId) {
                toast("Chưa có xe để phân công.");
                return;
            }
            if (b.mode === "WithDriver" && !driverId) {
                toast("Đơn có tài xế — cần chọn tài xế.");
                return;
            }
            const url = handlers.assign + (handlers.assign.includes("?") ? "&" : "?") + "id=" + encodeURIComponent(b.id);
            const { ok, data } = await postForm(url, { vehicleId, driverId });
            if (!ok) {
                const msg = (data && data.error) || "Phân công thất bại.";
                toast(msg);
                if (data && data.conflict) {
                    const altsV = data.conflict.vehicles || [];
                    const altsD = data.conflict.drivers || [];
                    if (altsV.length) state.assignableCache[b.id] = { vehicles: altsV, drivers: altsD };
                    state.panel = "vehicle";
                    await renderDrawer(b, "vehicle");
                }
                return;
            }
            if (data.booking) upsertBooking(data.booking);
            state.pendingVehicleId = null;
            state.pendingDriverId = null;
            toast("Phân công thành công.");
            await reloadFromServer();
            render();
            return;
        }
        const open = e.target.closest("[data-open]");
        if (open) {
            state.selectedId = Number(open.dataset.open);
            state.panel = open.dataset.panel || "detail";
            const b = bookings.find((x) => x.id === state.selectedId);
            if (b) await renderDrawer(b, state.panel);
        }
    });

    render();
})();

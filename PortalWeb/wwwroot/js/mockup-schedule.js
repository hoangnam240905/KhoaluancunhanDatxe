(function () {
    const boot = window.__SCHEDULE_BOOTSTRAP__;
    const handlers = window.__SCHEDULE_HANDLERS__ || {};
    const ready = window.__SCHEDULE_READY__ === true;

    // Guard: new JS must not run against an old page without API bootstrap (causes empty grid + stale metrics).
    if (!boot || typeof boot !== "object" || !Array.isArray(boot.vehicles) || !ready) {
        const grid = document.getElementById("schGrid");
        const msg = `<p class="mk-empty">Schedule chưa nhận bootstrap API. Hãy rebuild/restart PortalWeb để đồng bộ page + JS (tránh DLL cũ + wwwroot mới).</p>`;
        if (grid) grid.innerHTML = msg;
        console.error("[Schedule] Missing __SCHEDULE_BOOTSTRAP__ — aborting mock-free render.");
        return;
    }

    const BUFFER_H = Number(boot.bufferHours) || 2;
    const BUFFER_MS = BUFFER_H * 36e5;
    const WEEKDAYS = ["Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "CN"];
    // Bookings that occupy a vehicle/driver. Cancelled never blocks; Completed is shown muted only.
    const ACTIVE = new Set(["Pending", "Confirmed", "Assigned", "InProgress"]);
    const BOOKING_STATUS_VI = { Pending: "Chờ xác nhận", Confirmed: "Đã xác nhận", Assigned: "Đã phân công", InProgress: "Đang thực hiện", Completed: "Hoàn thành", Cancelled: "Đã hủy" };

    let vehicles = boot.vehicles.slice();
    let drivers = Array.isArray(boot.drivers) ? boot.drivers.slice() : [];
    let bookings = Array.isArray(boot.bookings) ? boot.bookings.slice() : [];

    const focusIso = boot.focusIso || null;
    const initialFocus = focusIso ? new Date(focusIso + "T00:00:00") : new Date();

    const state = {
        resource: "vehicle",
        view: window.matchMedia && window.matchMedia("(max-width: 640px)").matches ? "list" : "week",
        focus: initialFocus,
        q: "",
        seats: "",
        model: "",
        freeOnly: false,
        freeWindow: null,
        focusBookingId: null,
        collapsed: {},
        selectedId: null,
        panel: "detail",
        pendingVehicleId: null,
        pendingDriverId: null,
        assignableCache: {}
    };

    const els = {
        range: document.getElementById("schRange"),
        date: document.getElementById("schDate"),
        grid: document.getElementById("schGrid"),
        boardTitle: document.getElementById("schBoardTitle"),
        metrics: document.getElementById("schMetrics"),
        pending: document.getElementById("schPending"),
        pendingList: document.getElementById("schPendingList"),
        pendingCount: document.getElementById("schPendingCount"),
        focusNote: document.getElementById("schFocusNote"),
        conflictBox: document.getElementById("schConflictBox"),
        conflictList: document.getElementById("schConflictList"),
        drawer: document.getElementById("schDrawer"),
        toast: document.getElementById("schToast"),
        q: document.getElementById("fltQ"),
        seats: document.getElementById("fltSeats"),
        model: document.getElementById("fltModel"),
        freeOnly: document.getElementById("fltFreeOnly"),
        freeLabel: document.getElementById("fltFreeLabel"),
        thisBtn: document.getElementById("schThis")
    };

    const parse = (s) => new Date(s);
    const pad = (n) => String(n).padStart(2, "0");
    const isoDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const fmtTime = (d) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;
    const fmtWhen = (d) => `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${fmtTime(d)}`;
    const hoursBetween = (a, b) => (b - a) / 36e5;
    const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
    const isActive = (b) => ACTIVE.has(b.status);
    const overlaps = (aStart, aEnd, bStart, bEnd) => aStart < bEnd && aEnd > bStart;

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

    function dayStart(d) { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
    function startOfWeek(d) {
        const x = dayStart(d);
        const day = x.getDay();
        x.setDate(x.getDate() + (day === 0 ? -6 : 1 - day));
        return x;
    }
    function addDays(d, n) { const x = new Date(d); x.setDate(x.getDate() + n); return x; }
    function addHours(d, n) { const x = new Date(d); x.setHours(x.getHours() + n); return x; }
    function weekDays(start) { return Array.from({ length: 7 }, (_, i) => addDays(start, i)); }
    function vehicle(id) { return vehicles.find((v) => v.id === id) || { id, name: "Xe chưa rõ", plate: "—", status: "Available", seats: 0, model: "", typeName: "" }; }
    function driver(id) { return drivers.find((d) => d.id === id) || { id, name: "Tài xế chưa rõ", status: "Available" }; }

    // ---- Conflicts (client-side, same 2h threshold as Backend). Only active bookings count. ----
    let conflictCache = null;
    function invalidate() { conflictCache = null; }

    function computeVehicleConflicts() {
        const map = {};
        const active = bookings.filter(isActive);
        vehicles.forEach((v) => {
            const list = active.filter((b) => b.vehicleId === v.id).sort((a, b) => parse(a.start) - parse(b.start));
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

    function computeDriverConflicts() {
        const map = {};
        const active = bookings.filter(isActive);
        drivers.forEach((d) => {
            const list = active.filter((b) => b.driverId === d.id).sort((a, b) => parse(a.start) - parse(b.start));
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

    function conflicts() {
        if (!conflictCache) conflictCache = { v: computeVehicleConflicts(), d: computeDriverConflicts() };
        return conflictCache;
    }
    function vehicleConflicts() { return conflicts().v; }
    function driverConflicts() { return conflicts().d; }

    // ---- Range & slots ----
    function rangeBounds() {
        if (state.view === "day") {
            const s = dayStart(state.focus);
            return [s, addDays(s, 1)];
        }
        const ws = startOfWeek(state.focus);
        return [ws, addDays(ws, 7)];
    }

    function slots() {
        const today = isoDate(new Date());
        if (state.view === "day") {
            const d0 = dayStart(state.focus);
            const nowH = isoDate(d0) === today ? new Date().getHours() : -1;
            return Array.from({ length: 24 }, (_, h) => ({
                start: addHours(d0, h), end: addHours(d0, h + 1), label: `${pad(h)}h`, isNow: h === nowH
            }));
        }
        return weekDays(startOfWeek(state.focus)).map((d, i) => ({
            start: d, end: addDays(d, 1),
            label: `${WEEKDAYS[i]}<small>${pad(d.getDate())}/${pad(d.getMonth() + 1)}</small>`,
            isNow: isoDate(d) === today
        }));
    }

    function rangeLabel() {
        if (state.view === "day") {
            const d = state.focus;
            return `${WEEKDAYS[(d.getDay() + 6) % 7]} ${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
        }
        const s = startOfWeek(state.focus);
        const e = addDays(s, 6);
        return `${pad(s.getDate())}/${pad(s.getMonth() + 1)} – ${pad(e.getDate())}/${pad(e.getMonth() + 1)}/${e.getFullYear()}`;
    }

    // ---- Resource helpers ----
    function seatsForType(typeId, typeName) {
        const v = typeId ? vehicles.find((x) => x.typeId === typeId && x.seats > 0) : null;
        if (v) return v.seats;
        const m = /(\d+)\s*chỗ/i.exec(typeName || "");
        return m ? Number(m[1]) : 0;
    }

    function isVehicleOff(v) { return !["Available", "Busy", "Preparing"].includes(v.status); }
    function isDriverOff(d) { return d.status === "Offline"; }
    function isOff(r) { return state.resource === "vehicle" ? isVehicleOff(r) : isDriverOff(r); }
    // Live status "Busy" means the car/driver is out right now even if no booking covers now
    // (e.g. an overdue booking never closed). Only applies to the slot containing the current time.
    function busyNow(r, s, e) {
        const now = Date.now();
        return r.status === "Busy" && now >= +s && now < +e;
    }

    function indexBookings(key) {
        const map = {};
        bookings.forEach((b) => {
            const id = b[key];
            if (!id) return;
            (map[id] = map[id] || []).push(b);
        });
        return map;
    }

    function freeIn(list, off, wStart, wEnd, r) {
        if (off) return false;
        if (r && busyNow(r, wStart, wEnd)) return false;
        return !list.some((b) => isActive(b) && overlaps(parse(b.start), parse(b.end), wStart, wEnd));
    }

    function freeWindowBounds() {
        return state.freeWindow ? [state.freeWindow.start, state.freeWindow.end] : rangeBounds();
    }

    function textMatches(blob) {
        return !state.q || blob.toLowerCase().includes(state.q.toLowerCase());
    }

    function bookingsText(list) {
        return list.map((b) => `#${b.id} ${b.customer || ""}`).join(" ");
    }

    function filteredVehicles(byVehicle) {
        const [ws, we] = freeWindowBounds();
        const [rs, re] = rangeBounds();
        return vehicles.filter((v) => {
            if (state.seats !== "" && String(v.seats || 0) !== state.seats) return false;
            if (state.model && modelOf(v) !== state.model) return false;
            const list = byVehicle[v.id] || [];
            if (state.freeOnly && !freeIn(list, isVehicleOff(v), ws, we, v)) return false;
            const inRange = list.filter((b) => isActive(b) && overlaps(parse(b.start), parse(b.end), rs, re));
            return textMatches(`${v.name} ${v.plate} ${v.model || ""} ${v.typeName || ""} ${bookingsText(inRange)}`);
        });
    }

    function filteredDrivers(byDriver) {
        const [ws, we] = freeWindowBounds();
        const [rs, re] = rangeBounds();
        return drivers.filter((d) => {
            const list = byDriver[d.id] || [];
            if (state.freeOnly && !freeIn(list, isDriverOff(d), ws, we, d)) return false;
            const inRange = list.filter((b) => isActive(b) && overlaps(parse(b.start), parse(b.end), rs, re));
            return textMatches(`${d.name} ${bookingsText(inRange)}`);
        });
    }

    function modelOf(v) { return v.model || v.typeName || v.name || "Khác"; }
    const viCompare = (a, b) => String(a).localeCompare(String(b), "vi");

    function vehicleGroups(list) {
        const bySeats = new Map();
        list.forEach((v) => {
            const k = v.seats || 0;
            if (!bySeats.has(k)) bySeats.set(k, []);
            bySeats.get(k).push(v);
        });
        return [...bySeats.keys()]
            .sort((a, b) => (a === 0) - (b === 0) || a - b)
            .map((seats) => {
                const items = bySeats.get(seats);
                const byModel = new Map();
                items.forEach((v) => {
                    const m = modelOf(v);
                    if (!byModel.has(m)) byModel.set(m, []);
                    byModel.get(m).push(v);
                });
                const subgroups = [...byModel.keys()].sort(viCompare).map((m) => ({
                    title: m,
                    note: [...new Set(byModel.get(m).map((v) => v.typeName).filter(Boolean))].join(", "),
                    items: byModel.get(m).sort((a, b) => viCompare(a.plate, b.plate))
                }));
                return {
                    key: "s" + seats,
                    title: seats ? `🚗 ${seats} chỗ` : "❔ Khác (chưa rõ số chỗ)",
                    unit: "xe",
                    subgroups
                };
            });
    }

    function driverGroups(list) {
        const defs = [
            { key: "Available", title: "🟢 Khả dụng", test: (d) => d.status === "Available" },
            { key: "Busy", title: "🟡 Đang bận", test: (d) => d.status === "Busy" },
            { key: "Off", title: "⚫ Ngoại tuyến / khác", test: (d) => d.status !== "Available" && d.status !== "Busy" }
        ];
        return defs.map((g) => ({
            key: g.key,
            title: g.title,
            unit: "tài xế",
            subgroups: [{ title: null, items: list.filter(g.test).sort((a, b) => viCompare(a.name, b.name)) }]
        })).filter((g) => g.subgroups[0].items.length);
    }

    // ---- Cell colouring ("tô màu") ----
    // Returns how one resource looks in one time slot. Pure function — no DOM.
    function cellState(list, slot, off, confMap, liveBusy) {
        const s = +slot.start, e = +slot.end, len = e - s;
        const hits = [], done = [], parts = [];
        let buffer = null, conflict = false;
        list.forEach((b) => {
            const bs = +parse(b.start), be = +parse(b.end);
            if (b.status === "Completed") {
                if (overlaps(bs, be, s, e)) done.push(b);
                return;
            }
            if (!isActive(b)) return;
            if (overlaps(bs, be, s, e)) {
                hits.push(b);
                parts.push([Math.max(bs, s), Math.min(be, e)]);
                if (confMap[b.id]) conflict = true;
            } else if (overlaps(be, be + BUFFER_MS, s, e)) {
                buffer = b;
            }
        });
        parts.sort((a, b) => a[0] - b[0]);
        let covered = 0, cur = null;
        parts.forEach((p) => {
            if (!cur || p[0] > cur[1]) { if (cur) covered += cur[1] - cur[0]; cur = p.slice(); }
            else cur[1] = Math.max(cur[1], p[1]);
        });
        if (cur) covered += cur[1] - cur[0];
        const coverage = len ? covered / len : 0;

        let cls = "is-free";
        if (hits.length) cls = conflict ? "is-conflict" : coverage >= 0.9 ? "is-busy" : "is-partial";
        else if (off) cls = "is-off";
        else if (liveBusy) cls = "is-busy";
        else if (buffer) cls = "is-buffer";
        else if (done.length) cls = "is-done";
        return { cls, hits, done, buffer, conflict, coverage, liveBusy: Boolean(liveBusy), free: !hits.length && !off && !liveBusy };
    }

    function cellTitle(st, off) {
        const lines = st.hits.map((b) => `#${b.id} ${b.customer || ""} · ${fmtWhen(parse(b.start))} → ${fmtWhen(parse(b.end))}${st.conflict ? " ⚠️" : ""}`);
        if (!st.hits.length && off) lines.push(state.resource === "vehicle" ? "Bảo trì / ngưng hoạt động" : "Ngoại tuyến");
        if (!st.hits.length && st.liveBusy) lines.push(state.resource === "vehicle" ? "Đang thuê (trạng thái hiện tại, không có đơn đang chạy trong lịch)" : "Đang bận (trạng thái hiện tại)");
        if (st.buffer && !st.hits.length) lines.push(`Đệm ${BUFFER_H} giờ sau đơn #${st.buffer.id}`);
        st.done.forEach((b) => lines.push(`#${b.id} đã hoàn thành`));
        if (!lines.length) lines.push("Trống");
        return esc(lines.join("\n"));
    }

    function renderCell(st, slot, isFirst, off, hl) {
        const title = cellTitle(st, off);
        const cls = `dx-cell ${st.cls}${hl ? " is-highlight" : ""}`;
        const target = st.hits[0] || st.done[0];
        if (!target) return `<div class="${cls}" title="${title}"></div>`;
        // Label a booking only in the slot where it starts (or the first visible slot) to avoid repeating ids.
        const labelled = st.hits.filter((b) => isFirst || parse(b.start) >= slot.start);
        const chips = labelled.slice(0, 2).map((b) => `<span class="dx-chip" data-open="${b.id}" data-panel="detail">#${b.id}${st.conflict ? "⚠" : ""}</span>`).join("")
            + (labelled.length > 2 ? `<span class="dx-chip">+${labelled.length - 2}</span>` : "");
        return `<div class="${cls}" role="button" tabindex="0" title="${title}" data-open="${target.id}" data-panel="detail">${chips}</div>`;
    }

    function heatClass(free, total) {
        if (!total) return "";
        if (free === 0) return "dx-heat-none";
        if (free / total <= 0.25) return "dx-heat-low";
        return "dx-heat-ok";
    }

    function focusBooking() {
        return state.focusBookingId ? bookings.find((b) => b.id === state.focusBookingId) || null : null;
    }

    function renderGrid() {
        if (state.view === "list") {
            els.grid.innerHTML = renderList();
            return;
        }
        const isVehicle = state.resource === "vehicle";
        const sl = slots();
        const byRes = indexBookings(isVehicle ? "vehicleId" : "driverId");
        const confMap = isVehicle ? vehicleConflicts() : driverConflicts();
        const list = isVehicle ? filteredVehicles(byRes) : filteredDrivers(byRes);
        const groups = isVehicle ? vehicleGroups(list) : driverGroups(list);
        const fb = focusBooking();
        const hlSlot = sl.map((s) => Boolean(fb && overlaps(parse(fb.start), parse(fb.end), s.start, s.end)));
        const [ws, we] = freeWindowBounds();

        const pickAttr = (r) => {
            if (!fb) return "";
            const off = isOff(r);
            if (!freeIn(byRes[r.id] || [], off, ws, we, r)) return "";
            if (isVehicle && !fb.vehicleId) return `<button type="button" class="mk-btn mk-btn-primary dx-pick" data-pick-vehicle="${r.id}" data-for="${fb.id}">Chọn xe</button>`;
            if (!isVehicle && fb.mode === "WithDriver" && !fb.driverId) return `<button type="button" class="mk-btn mk-btn-primary dx-pick" data-pick-driver="${r.id}" data-for="${fb.id}">Chọn tài xế</button>`;
            return "";
        };

        const head = `<div class="dx-row dx-head">
            <div class="dx-name">${isVehicle ? "Xe" : "Tài xế"}${state.view === "day" ? ` · ${rangeLabel()}` : ""}</div>
            ${sl.map((s, i) => `<div class="dx-slot-head${s.isNow ? " is-now" : ""}${hlSlot[i] ? " is-highlight" : ""}">${s.label}</div>`).join("")}
        </div>`;

        const body = groups.map((g) => {
            const items = g.subgroups.flatMap((sg) => sg.items);
            const rows = new Map(items.map((r) => {
                const off = isOff(r);
                const own = byRes[r.id] || [];
                return [r.id, { off, cells: sl.map((s) => cellState(own, s, off, confMap, busyNow(r, s.start, s.end))) }];
            }));
            const ckey = `${state.resource}:${g.key}`;
            const collapsed = Boolean(state.collapsed[ckey]);
            const summary = sl.map((s, i) => {
                const free = items.filter((r) => rows.get(r.id).cells[i].free).length;
                return `<div class="dx-sum ${heatClass(free, items.length)}${hlSlot[i] ? " is-highlight" : ""}" title="${free}/${items.length} ${g.unit} ${isVehicle ? "trống" : "rảnh"}">${free}/${items.length}</div>`;
            }).join("");
            const groupHead = `<div class="dx-row dx-group-head">
                <button type="button" class="dx-name dx-group-toggle" data-toggle-group="${ckey}" aria-expanded="${!collapsed}">
                    <span class="dx-caret">${collapsed ? "▶" : "▼"}</span> <strong>${g.title}</strong> <span class="mk-muted">${items.length} ${g.unit}</span>
                </button>
                ${summary}
            </div>`;
            if (collapsed) return groupHead;

            const inner = g.subgroups.map((sg) => {
                // A model header only helps when it groups 2+ resources; single cars show the model on their own row.
                const sub = sg.title && sg.items.length > 1 ? `<div class="dx-model-row"><strong>${esc(sg.title)}</strong>${sg.note && sg.note !== sg.title ? ` <span class="mk-muted">· ${esc(sg.note)}</span>` : ""} <span class="mk-muted">· ${sg.items.length} ${g.unit}</span></div>` : "";
                const resRows = sg.items.map((r) => {
                    const row = rows.get(r.id);
                    const hasConf = row.cells.some((c) => c.conflict);
                    const label = isVehicle
                        ? `<strong>${esc(r.plate)}</strong>${statusBadge(r.status)}${hasConf ? ' <span class="mk-badge mk-badge-warn">⚠️</span>' : ""}<small>${esc(r.name)}</small>`
                        : `<strong>${esc(r.name)}</strong>${driverBadge(r.status)}${hasConf ? ' <span class="mk-badge mk-badge-warn">⚠️</span>' : ""}`;
                    return `<div class="dx-row">
                        <div class="dx-name dx-res">${label}${pickAttr(r)}</div>
                        ${row.cells.map((c, i) => renderCell(c, sl[i], i === 0, row.off, hlSlot[i])).join("")}
                    </div>`;
                }).join("");
                return sub + resRows;
            }).join("");
            return groupHead + inner;
        }).join("");

        const otherCount = isVehicle ? list.filter((v) => !v.seats).length : 0;
        const otherWarn = otherCount && otherCount / Math.max(1, list.length) > 0.2
            ? `<p class="mk-note dx-warn">⚠️ ${otherCount} xe chưa xác định số chỗ — kiểm tra dữ liệu loại xe.</p>` : "";

        els.grid.innerHTML = otherWarn + (groups.length
            ? `<div class="dx-grid ${state.view === "day" ? "is-day" : "is-week"}" style="--dx-cols:${sl.length}">${head}${body}</div>`
            : `<p class="mk-empty">Không có ${isVehicle ? "xe" : "tài xế"} khớp bộ lọc.</p>`);
    }

    // ---- List view ----
    function bookingSeats(b) {
        return b.vehicleId ? (vehicle(b.vehicleId).seats || 0) : seatsForType(b.requestedTypeId, b.requestedTypeName);
    }

    function matchesBooking(b) {
        const v = b.vehicleId ? vehicle(b.vehicleId) : null;
        const d = b.driverId ? driver(b.driverId) : null;
        if (state.resource === "vehicle") {
            if (state.seats !== "" && String(bookingSeats(b)) !== state.seats) return false;
            if (state.model && (!v || modelOf(v) !== state.model)) return false;
        }
        return textMatches(`#${b.id} ${b.customer || ""} ${v ? v.name + " " + v.plate : ""} ${d ? d.name : ""} ${b.requestedTypeName || ""}`);
    }

    function driverCell(b) {
        if (b.mode === "SelfDrive") return `<span class="mk-muted">Tự lái</span>`;
        return b.driverId ? esc(driver(b.driverId).name) : `<span class="mk-badge mk-badge-warn">Chưa gán</span>`;
    }

    function renderList() {
        const [rs, re] = rangeBounds();
        const rows = bookings
            .filter((b) => b.status !== "Cancelled")
            .filter((b) => overlaps(parse(b.start), parse(b.end), rs, re))
            .filter(matchesBooking)
            .sort((a, b) => parse(a.start) - parse(b.start));
        if (!rows.length) return `<p class="mk-empty">Không có đơn nào trong khoảng này khớp bộ lọc.</p>`;
        const conf = { ...driverConflicts(), ...vehicleConflicts() };
        return `<div class="mk-table-wrap dx-table-wrap"><table class="mk-table">
            <thead><tr><th>Đơn</th><th>Khách</th><th>Nhóm xe</th><th>Xe</th><th>Tài xế</th><th>Đến</th><th>Đi</th><th>Trạng thái</th><th></th></tr></thead>
            <tbody>${rows.map((b) => {
                const v = b.vehicleId ? vehicle(b.vehicleId) : null;
                const seats = bookingSeats(b);
                return `<tr${b.id === state.focusBookingId ? ' class="is-on"' : ""}>
                    <td><strong>#${b.id}</strong>${conf[b.id] ? ' <span title="Xung đột">⚠️</span>' : ""}</td>
                    <td>${esc(b.customer)}</td>
                    <td>${seats ? seats + " chỗ" : "—"}<div class="mk-muted">${esc(v ? v.typeName : b.requestedTypeName)}</div></td>
                    <td>${v ? `${esc(v.name)}<div class="mk-muted">${esc(v.plate)}</div>` : '<span class="mk-badge mk-badge-warn">Chưa gán</span>'}</td>
                    <td>${driverCell(b)}</td>
                    <td>${fmtWhen(parse(b.start))}</td>
                    <td>${fmtWhen(parse(b.end))}</td>
                    <td><span class="mk-badge mk-badge-${(b.status || "").toLowerCase()}">${BOOKING_STATUS_VI[b.status] || esc(b.status)}</span></td>
                    <td>${needsDispatch(b)
                        ? `<button type="button" class="mk-btn mk-btn-primary" data-open="${b.id}" data-panel="dispatch">⚡ Phân công</button>`
                        : `<button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="detail">Chi tiết</button>`}</td>
                </tr>`;
            }).join("")}</tbody>
        </table></div>`;
    }

    // ---- Pending dispatch ----
    function needsDispatch(b) {
        if (!(b.status === "Pending" || b.status === "Confirmed")) return false;
        return !b.vehicleId || (b.mode === "WithDriver" && !b.driverId);
    }

    function pendingBookings() {
        const now = new Date();
        return bookings
            .filter((b) => needsDispatch(b) && parse(b.end) >= now)
            .sort((a, b) => parse(a.start) - parse(b.start));
    }

    function renderPending() {
        const list = pendingBookings();
        els.pending.hidden = !list.length;
        els.pendingCount.textContent = list.length;
        els.pendingList.innerHTML = list.map((b) => {
            const seats = seatsForType(b.requestedTypeId, b.requestedTypeName);
            const needVehicle = !b.vehicleId;
            const needDriver = b.mode === "WithDriver" && !b.driverId;
            const what = [needVehicle ? "xe" : "", needDriver ? "tài xế" : ""].filter(Boolean).join(" + ");
            return `<div class="dx-pending-item${b.id === state.focusBookingId ? " is-on" : ""}">
                <div>
                    <strong>#${b.id}</strong> ${esc(b.customer)}
                    <span class="mk-badge mk-badge-${(b.status || "").toLowerCase()}">${BOOKING_STATUS_VI[b.status] || esc(b.status)}</span>
                    <span class="mk-badge ${b.mode === "SelfDrive" ? "mk-badge-gray" : "mk-badge-confirmed"}">${b.mode === "SelfDrive" ? "🚗 Tự lái" : "👨‍✈️ Có tài xế"}</span>
                    <div class="mk-muted">Cần ${seats ? seats + " chỗ" : esc(b.requestedTypeName || "xe")} · ${fmtWhen(parse(b.start))} → ${fmtWhen(parse(b.end))} · thiếu ${what}</div>
                </div>
                <div class="mk-actions">
                    ${needVehicle ? `<button type="button" class="mk-btn mk-btn-ghost" data-focus-booking="${b.id}" data-focus-res="vehicle">🔍 Xem xe trống</button>` : ""}
                    ${needDriver ? `<button type="button" class="mk-btn mk-btn-ghost" data-focus-booking="${b.id}" data-focus-res="driver">🔍 Xem tài xế rảnh</button>` : ""}
                    <button type="button" class="mk-btn mk-btn-primary" data-open="${b.id}" data-panel="dispatch">⚡ Phân công</button>
                </div>
            </div>`;
        }).join("");
    }

    function setFocusBooking(b, resource) {
        state.focusBookingId = b.id;
        state.resource = resource;
        if (state.view === "list") state.view = "week";
        state.focus = dayStart(parse(b.start));
        state.freeOnly = true;
        state.freeWindow = { start: new Date(+parse(b.start) - BUFFER_MS), end: new Date(+parse(b.end) + BUFFER_MS) };
        state.q = "";
        state.model = "";
        const seats = seatsForType(b.requestedTypeId, b.requestedTypeName);
        state.seats = resource === "vehicle" && seats ? String(seats) : "";
    }

    function clearFocus() {
        state.focusBookingId = null;
        state.freeWindow = null;
        state.freeOnly = false;
        state.seats = "";
        state.model = "";
    }

    function renderFocusNote() {
        const fb = focusBooking();
        if (!fb) { els.focusNote.hidden = true; return; }
        els.focusNote.hidden = false;
        const isVehicle = state.resource === "vehicle";
        els.focusNote.innerHTML = `<span>🎯 Đang tìm ${isVehicle ? "xe" : "tài xế"} cho <strong>đơn #${fb.id}</strong> (${esc(fb.customer)}) ·
            ${fmtWhen(parse(fb.start))} → ${fmtWhen(parse(fb.end))} · đã tính đệm ${BUFFER_H} giờ hai đầu. Cột tô viền là thời gian của đơn.</span>
            <button type="button" class="mk-btn mk-btn-ghost" id="schClearFocus">✕ Bỏ chọn</button>`;
    }

    // ---- Metrics (for the focused day) ----
    function renderMetrics() {
        const s = dayStart(state.focus), e = addDays(s, 1);
        const byV = indexBookings("vehicleId");
        const byD = indexBookings("driverId");
        const busyIn = (list) => list.some((b) => isActive(b) && overlaps(parse(b.start), parse(b.end), s, e));
        let vFree = 0, vBusy = 0, vOff = 0;
        vehicles.forEach((v) => {
            if (busyIn(byV[v.id] || []) || busyNow(v, s, e)) vBusy++;
            else if (isVehicleOff(v)) vOff++;
            else vFree++;
        });
        const dFree = drivers.filter((d) => !isDriverOff(d) && !busyIn(byD[d.id] || []) && !busyNow(d, s, e)).length;
        const pending = pendingBookings().length;
        const day = `${pad(s.getDate())}/${pad(s.getMonth() + 1)}`;
        const card = (cls, icon, label, n, total) => {
            const pct = total ? Math.round((n / total) * 100) : 0;
            return `<div class="dx-metric ${cls}">
                <span>${icon} ${label}</span>
                <strong>${n}${total != null ? `<small>/${total}</small>` : ""}</strong>
                ${total != null ? `<div class="dx-bar"><i style="width:${pct}%"></i></div><small class="mk-muted">${pct}%</small>` : ""}
            </div>`;
        };
        els.metrics.innerHTML =
            card("is-free", "🟢", `Xe trống ngày ${day}`, vFree, vehicles.length)
            + card("is-busy", "🔵", `Xe có lịch ngày ${day}`, vBusy, vehicles.length)
            + card("is-off", "⚫", "Bảo trì / ngưng", vOff, vehicles.length)
            + card("is-driver", "👨‍✈️", `Tài xế rảnh ngày ${day}`, dFree, drivers.length)
            + card("is-pending", "⏳", "Đơn chờ phân công", pending, null);
    }

    function statusBadge(st) {
        if (st === "Available") return `<span class="mk-badge mk-badge-ok">Khả dụng</span>`;
        if (st === "Busy") return `<span class="mk-badge mk-badge-confirmed">Đang thuê</span>`;
        if (st === "Preparing") return `<span class="mk-badge mk-badge-pending">Đang chuẩn bị</span>`;
        if (st === "Maintenance") return `<span class="mk-badge mk-badge-gray">Bảo trì</span>`;
        return `<span class="mk-badge mk-badge-gray">Ngưng hoạt động</span>`;
    }

    function driverBadge(st) {
        if (st === "Available") return `<span class="mk-badge mk-badge-ok">Khả dụng</span>`;
        if (st === "Busy") return `<span class="mk-badge mk-badge-confirmed">Đang bận</span>`;
        return `<span class="mk-badge mk-badge-gray">Ngoại tuyến</span>`;
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
                        <div>#${c.a.id} và #${c.b.id} — ${esc(c.driver.name)} đang có lịch trong khoảng thời gian này.</div>
                        <div class="mk-muted">${fmtWhen(parse(c.a.start))} → ${fmtWhen(parse(c.a.end))} trùng ${fmtWhen(parse(c.b.start))} → ${fmtWhen(parse(c.b.end))}</div>
                    </div>
                    <button type="button" class="mk-btn mk-btn-danger" data-open="${c.b.id}" data-panel="driver">Tài xế thay thế</button>
                </div>`;
            }
            const gapTxt = c.gap < 0 ? "hai lịch chồng lên nhau" : `Khoảng cách giữa hai lịch chỉ ${c.gap.toFixed(0)} giờ.`;
            return `<div class="sch-conflict-item">
                <div>
                    <strong>Đơn #${c.a.id}</strong> ${esc(c.vehicle.name)} · ${esc(c.vehicle.plate)}<br>
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
            return list.map((x) => `<div class="sch-alt"><div><strong>${x.name}</strong><div class="mk-muted">${x.plate || ""}${x.seats ? " · " + x.seats + " chỗ" : ""}</div></div>
                <span class="mk-badge mk-badge-ok">🟢 Khả dụng</span>
                <button type="button" class="mk-btn mk-btn-primary" data-pick-vehicle="${x.id}" data-for="${bookingId}">Chọn xe</button></div>`).join("");
        }
        return list.map((x) => `<div class="sch-alt"><div><strong>${x.name}</strong></div>
            <span class="mk-badge mk-badge-ok">🟢 Khả dụng</span>
            <button type="button" class="mk-btn mk-btn-primary" data-pick-driver="${x.id}" data-for="${bookingId}">Chọn tài xế</button></div>`).join("");
    }

    async function renderDrawer(b, panel) {
        const selfDrive = b.mode === "SelfDrive";
        // Self-drive bookings never carry a driver — there is nothing to swap.
        if (selfDrive && panel === "driver") panel = "detail";
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
                    <div><dt>Tài xế</dt><dd>${selfDrive ? "Tự lái — không gắn tài xế" : (pickD ? pickD.name : "Chưa chọn")}</dd></div>
                </dl>
                <ul class="sch-flow">
                    <li>${fmtTime(parse(b.start))} bắt đầu</li>
                    <li>${fmtTime(end)} kết thúc</li>
                    <li class="buf">${fmtTime(buf)} Đệm ${BUFFER_H} giờ</li>
                </ul>
                <div class="mk-actions" style="margin-top:.75rem">
                    <button type="button" class="mk-btn mk-btn-success" data-assign="${b.id}">✓ Xác nhận phân công</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="vehicle">↔ Chọn xe khác</button>
                    ${selfDrive ? "" : `<button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="driver">↔ Chọn tài xế khác</button>`}
                </div>
            </section>`;
        }

        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-2">Đơn #${b.id}</h2>
                    <span class="mk-badge mk-badge-${(b.status || "").toLowerCase()}">${BOOKING_STATUS_VI[b.status] || b.status}</span>
                    <span class="mk-badge ${selfDrive ? "mk-badge-gray" : "mk-badge-confirmed"}">${selfDrive ? "🚗 Tự lái" : "👨‍✈️ Có tài xế"}</span>
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
                        <div><dt>Hình thức</dt><dd>${selfDrive ? "Tự lái" : "Có tài xế"}</dd></div>
                        <div><dt>Tài xế</dt><dd>${selfDrive ? "Không áp dụng (tự lái)" : (d ? d.name : "Chưa gán")}</dd></div>
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
                    ${selfDrive ? "" : `<button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="driver">Tài xế thay thế</button>`}
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
        invalidate();
    }

    async function reloadFromServer() {
        if (!handlers.reload) return;
        const { ok, data } = await getJson(handlers.reload);
        if (!ok || !data || !data.data) return;
        const d = data.data;
        vehicles = Array.isArray(d.vehicles) ? d.vehicles : vehicles;
        drivers = Array.isArray(d.drivers) ? d.drivers : drivers;
        bookings = Array.isArray(d.bookings) ? d.bookings : bookings;
        state.assignableCache = {};
        invalidate();
    }

    // ---- Filter controls ----
    function fillSelect(select, options, value) {
        select.innerHTML = `<option value="">Tất cả</option>` + options.map((o) =>
            `<option value="${esc(o.value)}">${esc(o.label)}</option>`).join("");
        const has = options.some((o) => o.value === value);
        select.value = has ? value : "";
        return has ? value : "";
    }

    function syncFilterOptions() {
        const seatCounts = new Map();
        vehicles.forEach((v) => seatCounts.set(v.seats || 0, (seatCounts.get(v.seats || 0) || 0) + 1));
        const seatOpts = [...seatCounts.keys()]
            .sort((a, b) => (a === 0) - (b === 0) || a - b)
            .map((n) => ({ value: String(n), label: `${n ? n + " chỗ" : "Khác"} (${seatCounts.get(n)})` }));
        state.seats = fillSelect(els.seats, seatOpts, state.seats);

        const modelCounts = new Map();
        vehicles
            .filter((v) => state.seats === "" || String(v.seats || 0) === state.seats)
            .forEach((v) => modelCounts.set(modelOf(v), (modelCounts.get(modelOf(v)) || 0) + 1));
        const modelOpts = [...modelCounts.keys()].sort(viCompare).map((m) => ({ value: m, label: `${m} (${modelCounts.get(m)})` }));
        state.model = fillSelect(els.model, modelOpts, state.model);

        els.q.value = state.q;
        els.freeOnly.checked = state.freeOnly;
        const isVehicle = state.resource === "vehicle";
        els.freeLabel.textContent = state.freeWindow
            ? `Chỉ ${isVehicle ? "xe trống" : "tài xế rảnh"} trong thời gian của đơn`
            : `Chỉ ${isVehicle ? "xe trống" : "tài xế rảnh"} trong khoảng đang xem`;
        document.querySelectorAll("[data-only='vehicle']").forEach((el) => { el.hidden = !isVehicle; });
    }

    function renderBoardTitle() {
        const what = state.resource === "vehicle" ? "🚗 Lịch xe" : "👨‍✈️ Lịch tài xế";
        const when = state.view === "day" ? `Ngày ${rangeLabel()}` : `Tuần ${rangeLabel()}`;
        const unit = state.resource === "vehicle" ? "xe trống / tổng xe" : "tài xế rảnh / tổng tài xế";
        els.boardTitle.innerHTML = `<h2>${what} · ${when}</h2>`
            + (state.view === "list" ? "" : `<p class="mk-muted">Số trên dòng nhóm = ${unit} trong ${state.view === "day" ? "từng giờ" : "từng ngày"}. Bấm vào ô màu để xem đơn.</p>`);
    }

    function render() {
        invalidate();
        if (state.focusBookingId && !pendingBookings().some((b) => b.id === state.focusBookingId)) clearFocus();
        els.range.textContent = rangeLabel();
        els.date.value = isoDate(state.focus);
        document.querySelectorAll("#schView [data-view]").forEach((p) => p.classList.toggle("is-on", p.dataset.view === state.view));
        document.querySelectorAll("#schResource [data-res]").forEach((p) => p.classList.toggle("is-on", p.dataset.res === state.resource));
        els.thisBtn.textContent = state.view === "day" ? "Hôm nay" : "Tuần này";
        syncFilterOptions();
        renderMetrics();
        renderPending();
        renderFocusNote();
        renderConflicts();
        renderBoardTitle();
        renderGrid();
        if (state.selectedId) {
            const b = bookings.find((x) => x.id === state.selectedId);
            if (b) renderDrawer(b, state.panel);
        }
    }

    function shift(n) {
        state.focus = addDays(state.focus, state.view === "day" ? n : n * 7);
        render();
    }

    function goToday() {
        state.focus = dayStart(new Date());
    }

    document.getElementById("schPrev").addEventListener("click", () => shift(-1));
    document.getElementById("schNext").addEventListener("click", () => shift(1));
    els.thisBtn.addEventListener("click", () => { goToday(); render(); });
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
    document.getElementById("schResource").addEventListener("click", (e) => {
        const btn = e.target.closest("[data-res]");
        if (!btn || btn.dataset.res === state.resource) return;
        state.resource = btn.dataset.res;
        if (state.focusBookingId) clearFocus();
        render();
    });
    let qTimer = null;
    els.q.addEventListener("input", () => {
        clearTimeout(qTimer);
        qTimer = setTimeout(() => { state.q = els.q.value.trim(); render(); }, 250);
    });
    els.seats.addEventListener("change", () => { state.seats = els.seats.value; render(); });
    els.model.addEventListener("change", () => { state.model = els.model.value; render(); });
    els.freeOnly.addEventListener("change", () => {
        state.freeOnly = els.freeOnly.checked;
        if (!state.freeOnly) state.freeWindow = null;
        render();
    });
    document.getElementById("fltReset").addEventListener("click", () => {
        state.q = "";
        clearFocus();
        render();
    });

    document.body.addEventListener("keydown", (e) => {
        if ((e.key === "Enter" || e.key === " ") && e.target.matches(".dx-cell[data-open]")) {
            e.preventDefault();
            e.target.click();
        }
    });

    document.body.addEventListener("click", async (e) => {
        if (e.target.closest("[data-close]")) {
            state.selectedId = null;
            els.drawer.classList.remove("is-open");
            return;
        }
        const toggle = e.target.closest("[data-toggle-group]");
        if (toggle) {
            const k = toggle.dataset.toggleGroup;
            state.collapsed[k] = !state.collapsed[k];
            renderGrid();
            return;
        }
        if (e.target.closest("#schClearFocus")) {
            clearFocus();
            render();
            return;
        }
        const focusBtn = e.target.closest("[data-focus-booking]");
        if (focusBtn) {
            const b = bookings.find((x) => x.id === Number(focusBtn.dataset.focusBooking));
            if (!b) return;
            setFocusBooking(b, focusBtn.dataset.focusRes === "driver" ? "driver" : "vehicle");
            render();
            els.grid.scrollIntoView({ behavior: "smooth", block: "start" });
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

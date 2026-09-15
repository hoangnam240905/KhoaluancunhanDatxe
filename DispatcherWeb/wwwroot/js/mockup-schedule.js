(function () {
    const BUFFER_H = 2;
    const WEEKDAYS = ["Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "CN"];

    const vehicles = [
        { id: "v1", name: "Toyota Fortuner", plate: "51Z-12345", status: "Busy" },
        { id: "v2", name: "Mercedes E-Class", plate: "51Z-67890", status: "Busy" },
        { id: "v3", name: "Toyota Camry", plate: "51Z-24680", status: "Preparing" },
        { id: "v4", name: "Hyundai Santa Fe", plate: "51Z-13579", status: "Available" },
        { id: "v5", name: "Ford Everest", plate: "51Z-86420", status: "Available" },
        { id: "v6", name: "Kia Carnival", plate: "51G-45678", status: "Busy" },
        { id: "v7", name: "Mazda 3", plate: "51D-22211", status: "Maintenance" },
        { id: "v8", name: "VinFast VF8", plate: "51K-77777", status: "Busy" },
        { id: "v9", name: "Hyundai Accent", plate: "51Z-99901", status: "Busy" },
        { id: "v10", name: "Honda CR-V", plate: "51E-13579", status: "Available" },
        { id: "v11", name: "Toyota Vios", plate: "51A-10101", status: "Available" },
        { id: "v12", name: "Mitsubishi Xpander", plate: "51B-20202", status: "Available" },
        { id: "v13", name: "Ford Ranger", plate: "51C-30303", status: "Available" },
        { id: "v14", name: "Kia Sorento", plate: "51D-40404", status: "Available" },
        { id: "v15", name: "Peugeot 5008", plate: "51F-50505", status: "Available" },
        { id: "v16", name: "VinFast VF5", plate: "51G-60606", status: "Available" },
        { id: "v17", name: "Toyota Innova", plate: "51H-70707", status: "Busy" },
        { id: "v18", name: "Mercedes C-Class", plate: "51A-80808", status: "Available" },
        { id: "v19", name: "BMW 5 Series", plate: "51A-55555", status: "Available" },
        { id: "v20", name: "Hyundai Stargazer", plate: "51K-90909", status: "Available" },
        { id: "v21", name: "Toyota Raize", plate: "51L-11122", status: "Available" },
        { id: "v22", name: "Honda City", plate: "51M-33344", status: "Available" },
        { id: "v23", name: "Mazda CX-5", plate: "51N-55566", status: "Busy" },
        { id: "v24", name: "Ford Territory", plate: "51P-77788", status: "Available" }
    ];

    const drivers = [
        { id: "d1", name: "Nguyễn Văn A", status: "Busy" },
        { id: "d2", name: "Trần Văn B", status: "Available" },
        { id: "d3", name: "Lê Văn C", status: "Available" },
        { id: "d4", name: "Phạm Văn D", status: "Busy" },
        { id: "d5", name: "Hoàng Minh E", status: "Available" },
        { id: "d6", name: "Võ Quốc F", status: "Busy" },
        { id: "d7", name: "Đặng Thị G", status: "Available" },
        { id: "d8", name: "Bùi Văn H", status: "Busy" },
        { id: "d9", name: "Ngô Văn I", status: "Offline" },
        { id: "d10", name: "Lý Thị K", status: "Available" },
        { id: "d11", name: "Trịnh Văn L", status: "Busy" },
        { id: "d12", name: "Mai Văn M", status: "Available" }
    ];

    const bookings = [
        { id: 52, customer: "Nguyễn Minh Anh", mode: "SelfDrive", vehicleId: "v1", driverId: "d1", start: "2026-09-16T08:00:00", end: "2026-09-18T18:00:00", status: "Assigned" },
        { id: 48, customer: "Trần Gia Huy", mode: "SelfDrive", vehicleId: "v2", driverId: null, start: "2026-09-15T09:00:00", end: "2026-09-16T17:00:00", status: "InProgress" },
        { id: 53, customer: "Lê Hoàng Nam", mode: "WithDriver", vehicleId: "v1", driverId: "d8", start: "2026-09-20T08:00:00", end: "2026-09-20T12:00:00", status: "Pending" },
        { id: 54, customer: "Phạm Thảo My", mode: "SelfDrive", vehicleId: "v1", driverId: null, start: "2026-09-20T13:00:00", end: "2026-09-20T17:00:00", status: "Confirmed" },
        { id: 57, customer: "Võ Thanh Hà", mode: "SelfDrive", vehicleId: "v3", driverId: null, start: "2026-09-18T09:00:00", end: "2026-09-20T18:00:00", status: "Confirmed" },
        { id: 55, customer: "Đặng Gia Linh", mode: "WithDriver", vehicleId: "v6", driverId: "d4", start: "2026-09-17T08:00:00", end: "2026-09-17T20:00:00", status: "Assigned" },
        { id: 56, customer: "Bùi Minh Tuấn", mode: "SelfDrive", vehicleId: "v8", driverId: null, start: "2026-09-14T08:00:00", end: "2026-09-14T18:00:00", status: "Completed" },
        { id: 60, customer: "Ngô Phương Chi", mode: "WithDriver", vehicleId: "v9", driverId: "d1", start: "2026-09-20T11:00:00", end: "2026-09-20T13:00:00", status: "Assigned" },
        { id: 61, customer: "Hoàng Đức Anh", mode: "WithDriver", vehicleId: "v17", driverId: "d1", start: "2026-09-20T10:00:00", end: "2026-09-20T14:00:00", status: "Confirmed" },
        { id: 49, customer: "Mai Lan Phương", mode: "WithDriver", vehicleId: "v23", driverId: "d6", start: "2026-09-19T08:00:00", end: "2026-09-19T18:00:00", status: "Assigned" },
        { id: 62, customer: "Lý Ngọc Trâm", mode: "SelfDrive", vehicleId: "v11", driverId: null, start: "2026-09-19T18:00:00", end: "2026-09-19T22:00:00", status: "Confirmed" },
        { id: 63, customer: "Trịnh Văn Khoa", mode: "WithDriver", vehicleId: "v10", driverId: "d11", start: "2026-09-15T08:00:00", end: "2026-09-15T12:00:00", status: "Completed" },
        { id: 64, customer: "Trần Văn B khách", mode: "SelfDrive", vehicleId: "v3", driverId: null, start: "2026-09-16T08:00:00", end: "2026-09-16T16:00:00", status: "Completed" }
    ];

    const ganttVehicleIds = ["v1", "v2", "v3", "v4", "v5", "v6", "v7", "v8", "v9", "v17"];
    const ganttDriverIds = ["d1", "d2", "d3", "d4", "d5", "d6", "d8", "d11"];

    const state = {
        view: "week",
        focus: new Date(2026, 8, 14),
        q: "",
        vstatus: "",
        mode: "",
        dstatus: "",
        conflict: "",
        selectedId: null,
        panel: "detail"
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
        form: document.getElementById("schFilter")
    };

    const parse = (s) => new Date(s);
    const pad = (n) => String(n).padStart(2, "0");
    const isoDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const fmtTime = (d) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;
    const fmtWhen = (d) => `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${fmtTime(d)}`;
    const hoursBetween = (a, b) => (b - a) / 36e5;

    function startOfWeek(d) {
        const x = new Date(d.getFullYear(), d.getMonth(), d.getDate());
        const day = x.getDay();
        x.setDate(x.getDate() + (day === 0 ? -6 : 1 - day));
        x.setHours(0, 0, 0, 0);
        return x;
    }
    function addDays(d, n) { const x = new Date(d); x.setDate(x.getDate() + n); return x; }
    function weekDays(start) { return Array.from({ length: 7 }, (_, i) => addDays(start, i)); }
    function vehicle(id) { return vehicles.find((v) => v.id === id); }
    function driver(id) { return drivers.find((d) => d.id === id); }

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
        const v = vehicle(b.vehicleId);
        const d = b.driverId ? driver(b.driverId) : null;
        if (state.mode && b.mode !== state.mode) return false;
        if (state.vstatus && v.status !== state.vstatus) return false;
        if (state.dstatus && (!d || d.status !== state.dstatus)) return false;
        const vConf = vehicleConflicts();
        const dConf = driverConflicts();
        const has = Boolean(vConf[b.id] || dConf[b.id]);
        if (state.conflict === "yes" && !has) return false;
        if (state.conflict === "no" && has) return false;
        if (state.q) {
            const q = state.q.toLowerCase();
            const blob = `#${b.id} ${b.customer} ${v.name} ${v.plate} ${d ? d.name : ""} ${b.mode}`.toLowerCase();
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
        if (vehicle(b.vehicleId).status === "Preparing") return "prep";
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
                <div class="sch-buffer" style="left:${bufLeft}%;width:${bufWidth}%" title="Khoảng đệm kỹ thuật — 2 giờ">Đệm 2 giờ</div>
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
            ${rows || `<p class="mk-empty">Không có xe khớp bộ lọc mock.</p>`}
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
            ${rows}
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
        const weekStart = startOfWeek(state.focus);
        const weekEnd = addDays(weekStart, 7);
        return bookings.find((b) => b.vehicleId === v.id && parse(b.start) < weekEnd && parse(b.end) > weekStart) || null;
    }

    function renderFleet() {
        const rows = vehicles.filter((v) => !state.vstatus || v.status === state.vstatus).map((v) => {
            const b = currentBookingForVehicle(v);
            if (state.q) {
                const q = state.q.toLowerCase();
                const blob = `${v.name} ${v.plate} ${b ? "#" + b.id + " " + b.customer : ""}`.toLowerCase();
                if (!blob.includes(q)) return "";
            }
            return `<tr>
                <td>${v.name}</td>
                <td>${v.plate}</td>
                <td>${statusBadge(v.status)}</td>
                <td>${b ? "#" + b.id : "—"}</td>
                <td>${b ? fmtWhen(parse(b.start)) : "—"}</td>
                <td>${b ? fmtWhen(parse(b.end)) : "—"}</td>
            </tr>`;
        }).join("");
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
                    <span class="mk-muted">${gapTxt} Yêu cầu tối thiểu: 2 giờ.</span><br>
                    <span class="mk-badge mk-badge-warn">Không thể phân công</span>
                </div>
                <button type="button" class="mk-btn mk-btn-primary" data-open="${c.b.id}" data-panel="vehicle">Xe thay thế đề xuất</button>
            </div>`;
        }).join("");
    }

    function altVehicles(b) {
        return vehicles.filter((v) => v.id !== b.vehicleId && v.status === "Available").slice(0, 3);
    }
    function altDrivers(b) {
        return drivers.filter((d) => d.id !== b.driverId && d.status === "Available").slice(0, 3);
    }

    function renderDrawer(b, panel) {
        const v = vehicle(b.vehicleId);
        const d = b.driverId ? driver(b.driverId) : null;
        const vConf = vehicleConflicts()[b.id];
        const dConf = driverConflicts()[b.id];
        const end = parse(b.end);
        const buf = new Date(end.getTime() + BUFFER_H * 36e5);
        let extra = "";
        if (panel === "vehicle") {
            extra = `<section class="mk-block"><h3>Xe thay thế đề xuất</h3>
                <p class="mk-note">Mock UI — chọn xe chỉ đổi presentation, chưa gọi API.</p>
                ${altVehicles(b).map((x) => `<div class="sch-alt"><div><strong>${x.name}</strong><div class="mk-muted">${x.plate}</div></div>
                    <span class="mk-badge mk-badge-ok">🟢 Khả dụng</span>
                    <button type="button" class="mk-btn mk-btn-primary" data-pick-vehicle="${x.id}" data-for="${b.id}">Chọn xe</button></div>`).join("")}
            </section>`;
        }
        if (panel === "driver") {
            extra = `<section class="mk-block"><h3>Tài xế thay thế</h3>
                <p class="mk-note">Mock UI — chọn tài xế chỉ đổi presentation, chưa gọi API.</p>
                ${altDrivers(b).map((x) => `<div class="sch-alt"><div><strong>${x.name}</strong></div>
                    <span class="mk-badge mk-badge-ok">🟢 Khả dụng</span>
                    <button type="button" class="mk-btn mk-btn-primary" data-pick-driver="${x.id}" data-for="${b.id}">Chọn tài xế</button></div>`).join("")}
            </section>`;
        }
        if (panel === "dispatch") {
            extra = `<section class="mk-block"><h3>⚡ Phân công</h3>
                <p class="mk-note">Đơn #${b.id}</p>
                <dl class="mk-dl">
                    <div><dt>Xe</dt><dd>${v.name}<br>${v.plate}</dd></div>
                    <div><dt>Tài xế</dt><dd>${d ? d.name : "Tự lái — không gắn tài xế"}</dd></div>
                </dl>
                <ul class="sch-flow">
                    <li>${fmtTime(parse(b.start))} bắt đầu</li>
                    <li>${fmtTime(end)} kết thúc</li>
                    <li class="buf">${fmtTime(buf)} Đệm 2 giờ</li>
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
                    <span class="mk-badge mk-badge-${b.status.toLowerCase()}">${({ Pending: "Chờ xác nhận", Confirmed: "Đã xác nhận", Assigned: "Đã phân công", InProgress: "Đang thực hiện", Completed: "Hoàn thành", Cancelled: "Đã hủy" })[b.status] || b.status}</span>
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
                        <div><dt>Xe</dt><dd>${v.name} · ${v.plate}</dd></div>
                        <div><dt>Bắt đầu</dt><dd>${fmtWhen(parse(b.start))}</dd></div>
                        <div><dt>Kết thúc</dt><dd>${fmtWhen(end)}</dd></div>
                        <div><dt>Hình thức</dt><dd>${b.mode === "WithDriver" ? "Có tài xế" : "Tự lái"}</dd></div>
                        <div><dt>Tài xế</dt><dd>${d ? d.name : "—"}</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Khoảng đệm kỹ thuật — 2 giờ</h3>
                    <p class="mk-note">Kết thúc ${fmtTime(end)} → đệm đến ${fmtTime(buf)}. Hiển thị trên giao diện, chưa kiểm tra hệ thống thật.</p>
                </section>
                ${vConf ? `<p class="mk-note">Khoảng cách với #${vConf.a.id === b.id ? vConf.b.id : vConf.a.id}: ${vConf.gap.toFixed(0)} giờ (yêu cầu 2 giờ). Không thể phân công.</p>` : '<p class="mk-badge mk-badge-ok">✅ Không xung đột xe (mock)</p>'}
                <div class="mk-actions" style="margin:0.8rem 0">
                    <button type="button" class="mk-btn mk-btn-primary" data-open="${b.id}" data-panel="dispatch">⚡ Phân công</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="vehicle">Xe thay thế</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-open="${b.id}" data-panel="driver">Tài xế thay thế</button>
                </div>
                ${extra}
            </div>`;
    }

    function toast(msg) {
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
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

    document.getElementById("schPrev").addEventListener("click", () => shift(-1));
    document.getElementById("schNext").addEventListener("click", () => shift(1));
    document.getElementById("schThis").addEventListener("click", () => { state.focus = new Date(2026, 8, 14); render(); });
    document.getElementById("schToday").addEventListener("click", () => { state.focus = new Date(2026, 8, 14); state.view = "day"; render(); });
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
    document.body.addEventListener("click", (e) => {
        if (e.target.closest("[data-close]")) {
            state.selectedId = null;
            els.drawer.classList.remove("is-open");
            return;
        }
        const pickV = e.target.closest("[data-pick-vehicle]");
        if (pickV) {
            const b = bookings.find((x) => x.id === Number(pickV.dataset.for));
            b.vehicleId = pickV.dataset.pickVehicle;
            toast(`Demo UI: đã chọn ${vehicle(b.vehicleId).name} — chưa gọi API.`);
            renderDrawer(b, "detail");
            render();
            return;
        }
        const pickD = e.target.closest("[data-pick-driver]");
        if (pickD) {
            const b = bookings.find((x) => x.id === Number(pickD.dataset.for));
            b.driverId = pickD.dataset.pickDriver;
            toast(`Demo UI: đã chọn ${driver(b.driverId).name} — chưa gọi API.`);
            renderDrawer(b, "detail");
            render();
            return;
        }
        const assign = e.target.closest("[data-assign]");
        if (assign) {
            const b = bookings.find((x) => x.id === Number(assign.dataset.assign));
            b.status = "Assigned";
            toast("Demo UI: mô phỏng phân công thành công — chưa gọi API.");
            renderDrawer(b, "dispatch");
            render();
            return;
        }
        const open = e.target.closest("[data-open]");
        if (open) {
            state.selectedId = Number(open.dataset.open);
            state.panel = open.dataset.panel || "detail";
            const b = bookings.find((x) => x.id === state.selectedId);
            if (b) renderDrawer(b, state.panel);
        }
    });

    render();
})();

(function () {
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

    const trips = [
        {
            id: 61, customer: "Nguyễn Minh Anh", vehicle: "Toyota Fortuner", plate: "51Z-12345",
            driver: "Nguyễn Văn A", driverBusy: true, mode: "WithDriver", route: "TP.HCM → Đà Lạt",
            pickup: "Văn phòng DriveX Q.3", dropoff: "Đà Lạt", start: "14/09/2026 08:00", expectedReturn: "16/09/2026 18:00",
            status: "inProgress", progress: 70, location: "Đang trên tuyến", vehicleState: "Đang thuê",
            settlement: "none", pin: { x: 28, y: 42, show: true },
            handover: null,
            ret: null,
            incident: null,
            ops: [
                { t: "08:00", text: "✓ Nhận chuyến", cls: "done" },
                { t: "09:30", text: "✓ Bắt đầu chuyến", cls: "done" },
                { t: "12:00", text: "✓ Cập nhật vị trí", cls: "done" }
            ]
        },
        {
            id: 58, customer: "Trần Minh Anh", vehicle: "Mercedes E-Class", plate: "51Z-67890",
            driver: null, driverBusy: false, mode: "SelfDrive", route: "TP.HCM nội thành",
            pickup: "Văn phòng DriveX Q.3", dropoff: "Văn phòng DriveX Q.3", start: "14/09/2026 14:00", expectedReturn: "16/09/2026 18:00",
            status: "waitingHandover", progress: 5, location: "Chờ tại điểm giao", vehicleState: "Đang chuẩn bị",
            settlement: "none", pin: { x: 62, y: 30, show: true },
            handover: { odo: 52300, fuel: 80, exterior: "Tốt", tech: "Tốt", note: "Không có" },
            ret: null, incident: null,
            ops: [{ t: "13:40", text: "✓ Xe đã vào khu giao", cls: "done" }]
        },
        {
            id: 57, customer: "Võ Thanh Hà", vehicle: "Honda CR-V", plate: "51E-13579",
            driver: null, driverBusy: false, mode: "SelfDrive", route: "TP.HCM → Vũng Tàu",
            pickup: "Gò Vấp", dropoff: "Gò Vấp", start: "12/09/2026 08:00", expectedReturn: "14/09/2026 18:00",
            status: "returning", progress: 92, location: "Đang về điểm trả", vehicleState: "Đang thuê",
            settlement: "pending", pin: { x: 48, y: 68, show: false },
            handover: { odo: 52300, fuel: 80, exterior: "Tốt", tech: "Tốt", note: "Giao đầy đủ giấy tờ" },
            ret: { odo: 52680, fuel: 65, exterior: "Tốt", tech: "Tốt", note: "" },
            incident: null,
            charges: { base: 8000000, deposit: 4000000, extraKm: 380, extraFuel: 150000, damage: 0, other: 0 },
            ops: [
                { t: "12/09 08:00", text: "✓ Giao xe", cls: "done" },
                { t: "14/09 16:10", text: "✓ Khách báo đang trả xe", cls: "now" }
            ]
        },
        {
            id: 62, customer: "Hoàng Đức Anh", vehicle: "Toyota Camry", plate: "51Z-24680",
            driver: "Nguyễn Văn A", driverBusy: true, mode: "WithDriver", route: "Quận 1 → Thủ Đức",
            pickup: "Quận 1", dropoff: "Thủ Đức", start: "14/09/2026 09:00", expectedReturn: "14/09/2026 18:00",
            status: "incident", progress: 45, location: "Tạm dừng — kiểm tra xe", vehicleState: "Đang thuê",
            settlement: "none", pin: { x: 74, y: 58, show: true },
            handover: null,
            ret: null,
            incident: {
                reporter: "Nguyễn Văn A", time: "14/09/2026 15:30", type: "Ngoại thất",
                severity: "Trung bình", status: "Đang mở", summary: "Khách báo xước cản trước",
                desc: "Phát hiện vết xước nhỏ ở cản trước."
            },
            ops: [
                { t: "08:00", text: "✓ Nhận chuyến", cls: "done" },
                { t: "09:30", text: "✓ Bắt đầu chuyến", cls: "done" },
                { t: "12:00", text: "✓ Cập nhật vị trí", cls: "done" },
                { t: "15:30", text: "⚠️ Báo sự cố", cls: "warn" },
                { t: "16:00", text: "✓ Điều phối viên tiếp nhận", cls: "now" }
            ]
        },
        {
            id: 56, customer: "Bùi Minh Tuấn", vehicle: "VinFast VF8", plate: "51K-77777",
            driver: null, driverBusy: false, mode: "SelfDrive", route: "Thủ Đức nội thành",
            pickup: "Thủ Đức", dropoff: "Thủ Đức", start: "14/09/2026 08:00", expectedReturn: "15/09/2026 18:00",
            status: "handedOver", progress: 18, location: "Khách đã nhận xe", vehicleState: "Đang thuê",
            settlement: "none", pin: { x: 36, y: 72, show: false },
            handover: { odo: 18420, fuel: 90, exterior: "Tốt", tech: "Tốt", note: "Không có" },
            ret: null, incident: null, ops: [{ t: "08:12", text: "✓ Giao xe", cls: "done" }]
        },
        {
            id: 55, customer: "Đặng Gia Linh", vehicle: "Kia Carnival", plate: "51G-45678",
            driver: "Phạm Văn D", driverBusy: true, mode: "WithDriver", route: "TP.HCM → Cần Thơ",
            pickup: "Quận 5", dropoff: "Cần Thơ", start: "14/09/2026 06:00", expectedReturn: "15/09/2026 20:00",
            status: "inProgress", progress: 55, location: "Cao tốc Trung Lương", vehicleState: "Đang thuê",
            settlement: "none", pin: { x: 18, y: 60, show: false },
            handover: null, ret: null, incident: null,
            ops: [{ t: "06:00", text: "✓ Nhận chuyến", cls: "done" }, { t: "06:20", text: "✓ Bắt đầu chuyến", cls: "done" }]
        },
        {
            id: 54, customer: "Phạm Thảo My", vehicle: "Hyundai Santa Fe", plate: "51Z-13579",
            driver: null, driverBusy: false, mode: "SelfDrive", route: "TP.HCM → Phan Thiết",
            pickup: "Tân Bình", dropoff: "Tân Bình", start: "10/09/2026 08:00", expectedReturn: "12/09/2026 18:00",
            status: "completed", progress: 100, location: "Đã hoàn tất", vehicleState: "Khả dụng",
            settlement: "settled", pin: { x: 0, y: 0, show: false },
            handover: { odo: 40110, fuel: 75, exterior: "Tốt", tech: "Tốt", note: "" },
            ret: { odo: 40900, fuel: 70, exterior: "Tốt", tech: "Tốt", note: "" },
            incident: null,
            charges: { base: 5200000, deposit: 2000000, extraKm: 790, extraFuel: 0, damage: 0, other: 0 },
            ops: [{ t: "12/09 18:40", text: "✓ Đã quyết toán", cls: "done" }]
        },
        {
            id: 53, customer: "Lê Hoàng Nam", vehicle: "Ford Everest", plate: "51Z-86420",
            driver: "Trần Văn B", driverBusy: false, mode: "WithDriver", route: "Sân bay → Quận 1",
            pickup: "Sân bay Tân Sơn Nhất", dropoff: "Quận 1", start: "13/09/2026 09:00", expectedReturn: "13/09/2026 17:00",
            status: "completed", progress: 100, location: "Chờ quyết toán", vehicleState: "Khả dụng",
            settlement: "pending", pin: { x: 0, y: 0, show: false },
            handover: null,
            ret: { odo: 61240, fuel: 55, exterior: "Tốt", tech: "Tốt", note: "Trả đúng giờ" },
            incident: null,
            charges: { base: 1800000, deposit: 800000, extraKm: 42, extraFuel: 0, damage: 0, other: 0 },
            ops: [{ t: "13/09 17:10", text: "✓ Trả xe / Kiểm tra trả xe", cls: "done" }]
        },
        {
            id: 60, customer: "Ngô Phương Chi", vehicle: "Hyundai Accent", plate: "51Z-99901",
            driver: "Lê Văn C", driverBusy: true, mode: "WithDriver", route: "Quận 7 → Quận 1",
            pickup: "Quận 7", dropoff: "Quận 1", start: "14/09/2026 10:00", expectedReturn: "14/09/2026 16:00",
            status: "accepted", progress: 12, location: "Tài xế đã nhận chuyến", vehicleState: "Đang thuê",
            settlement: "none", pin: { x: 55, y: 22, show: false },
            handover: null, ret: null, incident: null,
            ops: [{ t: "10:05", text: "✓ Nhận chuyến", cls: "done" }]
        },
        {
            id: 48, customer: "Trần Gia Huy", vehicle: "Mazda CX-5", plate: "51N-55566",
            driver: null, driverBusy: false, mode: "SelfDrive", route: "Bình Dương nội thành",
            pickup: "Bình Dương", dropoff: "Bình Dương", start: "14/09/2026 07:30", expectedReturn: "15/09/2026 18:00",
            status: "inProgress", progress: 40, location: "Khách đang sử dụng", vehicleState: "Đang thuê",
            settlement: "none", pin: { x: 22, y: 28, show: false },
            handover: { odo: 33100, fuel: 70, exterior: "Tốt", tech: "Tốt", note: "" },
            ret: null, incident: null, ops: [{ t: "07:40", text: "✓ Giao xe", cls: "done" }]
        },
        {
            id: 49, customer: "Mai Lan Phương", vehicle: "Toyota Innova", plate: "51H-70707",
            driver: "Võ Quốc F", driverBusy: true, mode: "WithDriver", route: "TP.HCM → Long Hải",
            pickup: "Quận 3", dropoff: "Long Hải", start: "13/09/2026 08:00", expectedReturn: "14/09/2026 19:00",
            status: "returning", progress: 88, location: "Đang về điểm trả", vehicleState: "Đang thuê",
            settlement: "pending", pin: { x: 80, y: 36, show: false },
            handover: null,
            ret: { odo: 90210, fuel: 40, exterior: "Cần kiểm tra", tech: "Tốt", note: "Bụi đường" },
            incident: null,
            charges: { base: 3600000, deposit: 1500000, extraKm: 210, extraFuel: 80000, damage: 0, other: 0 },
            ops: [{ t: "18:20", text: "✓ Bắt đầu trả xe", cls: "now" }]
        },
        {
            id: 50, customer: "Lý Ngọc Trâm", vehicle: "Ford Ranger", plate: "51C-30303",
            driver: "Bùi Văn H", driverBusy: true, mode: "WithDriver", route: "Q.2 → Đồng Nai",
            pickup: "Thủ Đức", dropoff: "Biên Hòa", start: "14/09/2026 11:00", expectedReturn: "14/09/2026 20:00",
            status: "incident", progress: 33, location: "Dừng kiểm tra kỹ thuật", vehicleState: "Đang thuê",
            settlement: "none", pin: { x: 40, y: 50, show: false },
            handover: null, ret: null,
            incident: {
                reporter: "Bùi Văn H", time: "14/09/2026 13:10", type: "Kỹ thuật",
                severity: "Thấp", status: "Đang mở", summary: "Đèn báo động cơ nhấp nháy",
                desc: "Tài xế báo đèn check-engine sáng khi lên cao tốc."
            },
            ops: [{ t: "13:10", text: "⚠️ Báo sự cố kỹ thuật", cls: "warn" }]
        }
    ];

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
        toast: document.getElementById("opToast")
    };

    const money = (n) => n.toLocaleString("vi-VN") + " ₫";
    const trip = (id) => trips.find((t) => t.id === Number(id));
    const badge = (st) => `<span class="mk-badge mk-badge-${st.toLowerCase()}">${STATUS[st]}</span>`;
    const settleBadge = (s) => s === "settled"
        ? `<span class="mk-badge mk-badge-settled">Đã quyết toán</span>`
        : s === "pending"
            ? `<span class="mk-badge mk-badge-pending">Chờ quyết toán</span>`
            : `<span class="mk-badge mk-badge-gray">—</span>`;

    function toast(msg) {
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
    }

    function visible() {
        return trips.filter((t) => {
            if (state.status && t.status !== state.status) return false;
            if (state.mode && t.mode !== state.mode) return false;
            if (state.vehicle && t.vehicle !== state.vehicle) return false;
            if (state.dstatus === "Busy" && !t.driverBusy) return false;
            if (state.dstatus === "Available" && t.driverBusy) return false;
            if (state.q) {
                const q = state.q.toLowerCase();
                const blob = `#${t.id} ${t.customer} ${t.vehicle} ${t.plate} ${t.driver || ""} ${t.mode}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        });
    }

    function fillVehicles() {
        const names = [...new Set(trips.map((t) => t.vehicle))];
        els.vehicle.innerHTML = `<option value="">Tất cả</option>` + names.map((n) => `<option>${n}</option>`).join("");
    }

    function cardActions(t) {
        const btns = [];
        if (t.mode === "SelfDrive" && t.status === "waitingHandover") {
            btns.push(`<button type="button" class="mk-btn mk-btn-primary" data-handover="${t.id}">🔑 Giao xe</button>`);
        }
        if (t.status === "returning") {
            btns.push(`<button type="button" class="mk-btn mk-btn-primary" data-return="${t.id}">↩ Tiếp nhận trả xe</button>`);
        }
        if (t.incident) {
            btns.push(`<button type="button" class="mk-btn mk-btn-danger" data-incident="${t.id}">Xem sự cố</button>`);
        }
        if (t.settlement === "pending") {
            btns.push(`<button type="button" class="mk-btn mk-btn-ghost" data-settle="${t.id}">💰 Quyết toán</button>`);
        }
        if (t.mode === "WithDriver" && (t.status === "inProgress" || t.status === "accepted")) {
            btns.push(`<button type="button" class="mk-btn mk-btn-ghost" data-return="${t.id}">↩ Kiểm tra trả xe</button>`);
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
                <div class="op-progress" aria-hidden="true"><i style="width:${t.progress}%"></i></div>
                <div class="mk-muted">${t.progress}% · ${t.location}</div>
                ${t.incident ? `<p class="mk-note" style="margin-top:.55rem">⚠️ ${t.incident.summary}<br>${t.incident.time} · 🟡 ${t.incident.severity} · ${t.incident.status}</p>` : ""}
                <div class="op-trip-actions">${cardActions(t)}</div>
            </article>`).join("") || `<p class="mk-empty">Không có chuyến khớp bộ lọc mô phỏng.</p>`;
    }

    function renderTable() {
        const list = visible();
        const pages = Math.max(1, Math.ceil(list.length / PAGE));
        if (state.page > pages) state.page = pages;
        const slice = list.slice((state.page - 1) * PAGE, state.page * PAGE);
        els.meta.textContent = `${list.length} chuyến mô phỏng · trang ${state.page}/${pages}`;
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
                <td>${t.incident ? "⚠️ " + t.incident.severity : "—"}</td>
                <td>${settleBadge(t.settlement)}</td>
            </tr>`).join("") || `<tr><td colspan="10">Không có chuyến khớp bộ lọc.</td></tr>`;
        els.pager.innerHTML = Array.from({ length: pages }, (_, i) =>
            `<button type="button" class="mk-btn mk-btn-ghost ${state.page === i + 1 ? "is-on" : ""}" data-page="${i + 1}">${i + 1}</button>`
        ).join("");
    }

    function renderAlerts() {
        const items = [62, 57, 58, 61].map((id) => {
            const t = trip(id);
            if (!t) return "";
            let tone = "🟢";
            let text = STATUS[t.status];
            if (t.incident && t.incident.status !== "Đã xử lý") { tone = "🔴"; text = "Có sự cố xe"; }
            else if (t.settlement === "pending") { tone = "🟡"; text = "Đang chờ quyết toán"; }
            else if (t.status === "waitingHandover") { tone = "🟡"; text = "Sắp đến giờ giao xe"; }
            else if (t.status === "inProgress") { tone = "🟢"; text = "Đang thực hiện bình thường"; }
            else if (t.settlement === "settled") { tone = "🟢"; text = "Đã quyết toán"; }
            return `<button type="button" class="op-alert" data-open="${id}"><span><strong>${tone} Đơn #${id}</strong>${text}</span><span class="mk-muted">${STATUS[t.status]}</span></button>`;
        }).join("");
        els.alerts.innerHTML = items;
    }

    function renderDriver() {
        const t = trip(61);
        els.driver.innerHTML = `
            <div class="op-driver-block">
                <strong>${t.driver}</strong>
                <p>🟢 Đang thực hiện chuyến<br>Chuyến: #${t.id}<br>Hiện tại: ${t.location}</p>
                <div class="mk-actions">
                    <button type="button" class="mk-btn mk-btn-primary" data-open="61">Xem chuyến</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-contact="61">Liên hệ tài xế</button>
                    <button type="button" class="mk-btn mk-btn-danger" data-incident="62">Xem sự cố</button>
                </div>
            </div>`;
    }

    function renderMap() {
        els.map.querySelectorAll(".op-pin").forEach((n) => n.remove());
        trips.filter((t) => t.pin && t.pin.show).forEach((t) => {
            els.map.insertAdjacentHTML("beforeend",
                `<button type="button" class="op-pin" style="left:${t.pin.x}%;top:${t.pin.y}%" data-open="${t.id}"><span>📍</span>${t.vehicle}</button>`);
        });
    }

    function condBlock(ret, handover, mode) {
        if (!ret) return "";
        const km = handover && mode === "SelfDrive" ? (ret.odo - handover.odo) : null;
        const warnEx = ret.exterior === "Cần kiểm tra";
        const warnTech = ret.tech === "Cần kiểm tra";
        return `<div class="op-cond">
            <div><span>🛞 Odometer</span><strong>${ret.odo.toLocaleString("vi-VN")} km</strong></div>
            <div><span>⛽ Fuel</span><strong>${ret.fuel}%</strong></div>
            <div class="${warnEx ? "warn" : ""}"><span>🚗 Exterior</span><strong>${ret.exterior}${warnEx ? " ⚠️" : ""}</strong></div>
            <div class="${warnTech ? "warn" : ""}"><span>🔧 Technical</span><strong>${ret.tech}${warnTech ? " ⚠️" : ""}</strong></div>
        </div>${km !== null ? `<p class="mk-note">Km thực tế (mock): ${km.toLocaleString("vi-VN")} km = trả − giao.</p>` : ""}`;
    }

    function quickActions(t) {
        const a = [`<button type="button" class="mk-btn mk-btn-ghost" data-contact="${t.id}">📞 Liên hệ</button>`];
        if (t.status === "inProgress" || t.status === "accepted" || t.status === "handedOver") {
            a.push(`<button type="button" class="mk-btn mk-btn-danger" data-report="${t.id}">⚠️ Báo sự cố</button>`);
        }
        a.push(`<button type="button" class="mk-btn mk-btn-ghost" data-inspect="${t.id}">📋 Inspection</button>`);
        if (t.mode === "SelfDrive" && t.status === "waitingHandover") {
            a.push(`<button type="button" class="mk-btn mk-btn-primary" data-handover="${t.id}">🚗 Giao xe</button>`);
        }
        if (t.status === "returning" || (t.mode === "WithDriver" && (t.status === "inProgress" || t.status === "accepted"))) {
            a.push(`<button type="button" class="mk-btn mk-btn-primary" data-return="${t.id}">↩ Trả xe</button>`);
        }
        if (t.settlement === "pending" || t.status === "completed" || t.status === "returning") {
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
                        <div><dt>Trạng thái hiện tại</dt><dd>${STATUS[t.status]}</dd></div>
                        <div><dt>Tình trạng xe</dt><dd>${t.vehicleState}</dd></div>
                    </dl>
                    ${t.mode === "WithDriver" ? '<p class="mk-note">Có tài xế — không hiển thị số km giao xe. Hoàn thành chuyến chỉ qua kiểm tra trả xe.</p>' : ""}
                </section>
                <section class="mk-block">
                    <h3>Luồng vận hành</h3>
                    <ul class="op-flow">${flow}</ul>
                </section>
                <section class="mk-block">
                    <h3>Nhật ký vận hành</h3>
                    <ul class="mk-timeline">${(t.ops || []).map((o) => `<li class="${o.cls || ""}"><strong>${o.t}</strong> ${o.text}</li>`).join("")}</ul>
                </section>
                ${t.ret ? `<section class="mk-block"><h3>Tình trạng xe</h3>${condBlock(t.ret, t.handover, t.mode)}</section>` : ""}
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

    function handoverForm(t) {
        const h = t.handover || { odo: 0, fuel: 80, exterior: "Tốt", tech: "Tốt", note: "" };
        openModal(`
            <h2>Biên bản giao xe</h2>
            <p class="mk-note">Đơn #${t.id} · ${t.customer}<br>Xe ${t.vehicle} ${t.plate}</p>
            <form id="opHandoverForm" data-id="${t.id}" class="op-form-grid">
                <label class="mk-field"><span>Số km hiện tại</span><input name="odo" type="number" value="${h.odo}" required /></label>
                <label class="mk-field"><span>Nhiên liệu (%)</span><input name="fuel" type="number" min="0" max="100" value="${h.fuel}" required /></label>
                <label class="mk-field"><span>Ngoại thất</span>
                    <select name="exterior"><option>Tốt</option><option>Cần kiểm tra</option></select>
                </label>
                <label class="mk-field"><span>Tình trạng kỹ thuật</span>
                    <select name="tech"><option>Tốt</option><option>Cần kiểm tra</option></select>
                </label>
                <label class="mk-field" style="grid-column:1/-1"><span>Ghi chú</span><input name="note" value="${h.note || "Không có"}" /></label>
            </form>
            <div class="op-check">
                <label><input type="checkbox" checked /> Giấy tờ xe</label>
                <label><input type="checkbox" checked /> Chìa khóa</label>
                <label><input type="checkbox" checked /> Ngoại thất</label>
                <label><input type="checkbox" checked /> Lốp xe</label>
                <label><input type="checkbox" checked /> Nội thất</label>
                <label><input type="checkbox" checked /> Thiết bị an toàn</label>
            </div>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                <button type="submit" form="opHandoverForm" class="mk-btn mk-btn-success">✓ Xác nhận giao xe</button>
            </div>`);
        state.modal = "handover";
    }

    function returnForm(t) {
        const r = t.ret || { odo: (t.handover && t.handover.odo ? t.handover.odo + 50 : 10000), fuel: 65, exterior: "Tốt", tech: "Tốt", note: "" };
        const self = t.mode === "SelfDrive" && t.handover;
        const km = self ? r.odo - t.handover.odo : null;
        openModal(`
            <h2>Tiếp nhận trả xe</h2>
            <p class="mk-note">Đơn #${t.id} · ${t.customer}<br>${t.vehicle} ${t.plate}${t.mode === "WithDriver" ? "<br>Có tài xế — không dùng số km giao xe." : ""}</p>
            ${self ? `<p class="mk-note">Km giao: ${t.handover.odo.toLocaleString("vi-VN")} km</p>` : ""}
            <form id="opReturnForm" data-id="${t.id}" class="op-form-grid">
                <label class="mk-field"><span>Số km trả xe</span><input name="odo" type="number" value="${r.odo}" required /></label>
                <label class="mk-field"><span>Nhiên liệu (%)</span><input name="fuel" type="number" min="0" max="100" value="${r.fuel}" required /></label>
                <label class="mk-field"><span>Ngoại thất</span>
                    <select name="exterior"><option${r.exterior === "Tốt" ? " selected" : ""}>Tốt</option><option${r.exterior === "Cần kiểm tra" ? " selected" : ""}>Cần kiểm tra</option></select>
                </label>
                <label class="mk-field"><span>Tình trạng kỹ thuật</span>
                    <select name="tech"><option${r.tech === "Tốt" ? " selected" : ""}>Tốt</option><option${r.tech === "Cần kiểm tra" ? " selected" : ""}>Cần kiểm tra</option></select>
                </label>
                <label class="mk-field" style="grid-column:1/-1"><span>Ghi chú</span><input name="note" value="${r.note || ""}" /></label>
            </form>
            <p class="mk-note" id="opKmOut">${self ? `Km thực tế: <strong>${km.toLocaleString("vi-VN")} km</strong> (số km trả − số km giao). Tính trên giao diện.` : "Kiểm tra trả xe mô phỏng — chưa ghi nhận hệ thống thật."}</p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                <button type="submit" form="opReturnForm" class="mk-btn mk-btn-success">✓ Xác nhận trả xe</button>
            </div>`);
        state.modal = "return";
        const form = document.getElementById("opReturnForm");
        form?.querySelector('[name="odo"]')?.addEventListener("input", () => {
            if (!self) return;
            const v = Number(form.odo.value || 0);
            const out = document.getElementById("opKmOut");
            if (out) out.innerHTML = `Km thực tế: <strong>${(v - t.handover.odo).toLocaleString("vi-VN")} km</strong> (số km trả − số km giao). Tính trên giao diện.`;
        });
    }

    function incidentForm(t) {
        const i = t.incident;
        if (!i) { toast("Giao diện mô phỏng: chuyến này chưa có sự cố mẫu."); return; }
        openModal(`
            <h2>Báo cáo sự cố</h2>
            <dl class="mk-dl">
                <div><dt>Mã đơn</dt><dd>#${t.id}</dd></div>
                <div><dt>Xe</dt><dd>${t.vehicle}<br>${t.plate}</dd></div>
                <div><dt>Người báo</dt><dd>${i.reporter}</dd></div>
                <div><dt>Thời gian</dt><dd>${i.time}</dd></div>
                <div><dt>Loại</dt><dd>${i.type}</dd></div>
                <div><dt>Mức độ</dt><dd>🟡 ${i.severity}</dd></div>
                <div><dt>Trạng thái</dt><dd id="opIncStatus">${i.status}</dd></div>
            </dl>
            <p>${i.desc}</p>
            <div class="op-photo">[ Ảnh hiện trường ]</div>
            <p class="mk-note" style="margin-top:.7rem">Thao tác chỉ đổi giao diện mô phỏng.</p>
            <div class="mk-actions" style="margin-top:.7rem">
                <button type="button" class="mk-btn mk-btn-primary" data-inc-status="Tiếp nhận" data-for="${t.id}">Tiếp nhận</button>
                <button type="button" class="mk-btn mk-btn-ghost" data-inc-status="Đang xử lý" data-for="${t.id}">Đang xử lý</button>
                <button type="button" class="mk-btn mk-btn-success" data-inc-status="Đã xử lý" data-for="${t.id}">Đã xử lý</button>
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
            </div>`);
        state.modal = "incident";
    }

    function settlementForm(t) {
        const c = t.charges || { base: 0, deposit: 0, extraKm: 0, extraFuel: 0, damage: 0, other: 0 };
        const total = c.base + c.extraFuel + c.damage + c.other;
        const remain = total - c.deposit;
        openModal(`
            <h2>Quyết toán chuyến thuê</h2>
            <p class="mk-note">Quyết toán &amp; phụ phí cuối — tính trên giao diện mô phỏng, chưa gọi hệ thống.</p>
            <ol class="mk-note" style="padding-left:1.1rem">
                <li>Thông tin chuyến: Đơn #${t.id} · ${MODE_LABEL[t.mode] || t.mode}</li>
                <li>Thông tin xe: ${t.vehicle} ${t.plate}</li>
                <li>Thông tin khách: ${t.customer}</li>
                <li>Km thực tế: ${c.extraKm.toLocaleString("vi-VN")} km</li>
                <li>Nhiên liệu: phí phát sinh ${money(c.extraFuel)}</li>
                <li>Phí phát sinh / hư hỏng: ${money(c.damage + c.other)}</li>
                <li>Tiền cọc: ${money(c.deposit)}</li>
                <li>Tổng tiền: ${money(total)}</li>
                <li>Số tiền còn lại: ${money(remain)}</li>
            </ol>
            <table class="op-settle">
                <tr><th>Giá thuê cơ bản</th><td>${money(c.base)}</td></tr>
                <tr><th>Tiền cọc</th><td>${money(c.deposit)}</td></tr>
                <tr><th>Quãng đường phát sinh</th><td>${c.extraKm.toLocaleString("vi-VN")} km</td></tr>
                <tr><th>Phí nhiên liệu</th><td>${money(c.extraFuel)}</td></tr>
                <tr><th>Hư hỏng</th><td>${money(c.damage)}</td></tr>
                <tr><th>Khác</th><td>${money(c.other)}</td></tr>
                <tr class="total"><th>Tổng cộng</th><td>${money(total)}</td></tr>
                <tr><th>Còn lại</th><td>${money(remain)}</td></tr>
            </table>
            <p>${settleBadge(t.settlement)}</p>
            <div class="op-check">
                <label><input type="checkbox" /> Đã kiểm tra biên bản giao xe</label>
                <label><input type="checkbox" /> Đã kiểm tra biên bản trả xe</label>
                <label><input type="checkbox" /> Đã xử lý sự cố</label>
                <label><input type="checkbox" /> Đã đối soát thanh toán</label>
            </div>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                <button type="button" class="mk-btn mk-btn-success" data-close-settle="${t.id}">✓ Chốt quyết toán</button>
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

    document.body.addEventListener("click", (e) => {
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
        if (contact) { toast("Demo UI: liên hệ mock — chưa gọi API / chưa mở điện thoại."); return; }
        const inspect = e.target.closest("[data-inspect]");
        if (inspect) { toast("Demo UI: Inspection presentation-only — chưa mở biên bản backend."); return; }
        const report = e.target.closest("[data-report]");
        if (report) {
            const t = trip(report.dataset.report);
            t.status = "incident";
            t.incident = t.incident || {
                reporter: t.driver || t.customer, time: "14/09/2026 16:45", type: "Khác",
                severity: "Thấp", status: "Đang mở", summary: "Điều phối viên tạo sự cố mô phỏng",
                desc: "Sự cố được ghi nhận trên giao diện presentation."
            };
            toast("Demo UI: đã gắn cờ sự cố mock — chưa gọi API.");
            render();
            incidentForm(t);
            return;
        }
        const incSt = e.target.closest("[data-inc-status]");
        if (incSt) {
            const t = trip(incSt.dataset.for);
            t.incident.status = incSt.dataset.incStatus;
            const box = document.getElementById("opIncStatus");
            if (box) box.textContent = t.incident.status;
            toast(`Demo UI: sự cố → ${t.incident.status} — chưa gọi API.`);
            render();
            return;
        }
        const settleDone = e.target.closest("[data-close-settle]");
        if (settleDone) {
            const t = trip(settleDone.dataset.closeSettle);
            t.settlement = "settled";
            t.status = "completed";
            t.progress = 100;
            t.vehicleState = "Khả dụng";
            t.location = "Đã quyết toán";
            toast("Demo UI: đã chốt quyết toán — chưa gọi API.");
            closeModal();
            render();
            return;
        }
        const ho = e.target.closest("[data-handover]");
        if (ho) { handoverForm(trip(ho.dataset.handover)); return; }
        const rt = e.target.closest("[data-return]");
        if (rt) { returnForm(trip(rt.dataset.return)); return; }
        const inc = e.target.closest("[data-incident]");
        if (inc) { incidentForm(trip(inc.dataset.incident)); return; }
        const se = e.target.closest("[data-settle]");
        if (se) { settlementForm(trip(se.dataset.settle)); return; }

        const open = e.target.closest("[data-open]");
        if (open && !e.target.closest("[data-handover],[data-return],[data-incident],[data-settle]")) {
            state.selectedId = Number(open.dataset.open);
            renderDrawer(trip(state.selectedId));
        }
    });

    document.body.addEventListener("submit", (e) => {
        if (e.target.id === "opHandoverForm") {
            e.preventDefault();
            const t = trip(e.target.dataset.id);
            const data = new FormData(e.target);
            t.handover = {
                odo: Number(data.get("odo")),
                fuel: Number(data.get("fuel")),
                exterior: data.get("exterior").toString(),
                tech: data.get("tech").toString(),
                note: data.get("note").toString()
            };
            t.status = "handedOver";
            t.vehicleState = "Đang thuê";
            t.progress = 15;
            t.location = "Khách đã nhận xe";
            toast("Demo UI: đã xác nhận giao xe — chưa gọi API.");
            closeModal();
            render();
        }
        if (e.target.id === "opReturnForm") {
            e.preventDefault();
            const t = trip(e.target.dataset.id);
            const data = new FormData(e.target);
            t.ret = {
                odo: Number(data.get("odo")),
                fuel: Number(data.get("fuel")),
                exterior: data.get("exterior").toString(),
                tech: data.get("tech").toString(),
                note: data.get("note").toString()
            };
            if (t.mode === "SelfDrive" && t.handover) {
                const km = t.ret.odo - t.handover.odo;
                t.charges = t.charges || { base: 8000000, deposit: 4000000, extraKm: km, extraFuel: 150000, damage: 0, other: 0 };
                t.charges.extraKm = km;
            }
            t.status = "completed";
            t.settlement = t.settlement === "settled" ? "settled" : "pending";
            t.progress = 100;
            t.location = "Chờ quyết toán";
            toast("Demo UI: đã xác nhận trả xe — chưa gọi API.");
            closeModal();
            render();
        }
    });

    render();
})();

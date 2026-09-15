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

    const INITIAL = [
        { id: 53, created: "14/09/2026 11:48", customer: "Nguyễn Văn Nam", phone: "0901 234 567", email: "nam.nguyen@email.vn", mode: "WithDriver", vehicle: "Toyota Fortuner", type: "SUV", plate: "51H-123.45", start: "20/09/2026 08:00", end: "22/09/2026 18:00", startIso: "2026-09-20", endIso: "2026-09-22", pickup: "Sân bay Tân Sơn Nhất", dropoff: "Quận 1, TP.HCM", contract: "none", deposit: "none", status: "Pending", price: "4.800.000", depositAmt: "1.500.000", total: "6.300.000", vehicleHeld: false, driverAssigned: false },
        { id: 52, created: "12/09/2026 09:10", customer: "Trần Minh Anh", phone: "0902 888 111", email: "minhanh.tran@email.vn", mode: "SelfDrive", vehicle: "Mercedes E-Class", type: "Luxury", plate: "51A-888.88", start: "18/09/2026 09:00", end: "20/09/2026 18:00", startIso: "2026-09-18", endIso: "2026-09-20", pickup: "Văn phòng DriveX Q.3", dropoff: "Văn phòng DriveX Q.3", contract: "signed", deposit: "paid", status: "Confirmed", price: "8.400.000", depositAmt: "3.000.000", total: "11.400.000", vehicleHeld: true, driverAssigned: false },
        { id: 51, created: "12/09/2026 08:22", customer: "Lê Thị Hương", phone: "0913 456 789", email: "huong.le@email.vn", mode: "WithDriver", vehicle: "Kia Carnival", type: "MPV", plate: "51G-456.78", start: "21/09/2026 07:00", end: "23/09/2026 20:00", startIso: "2026-09-21", endIso: "2026-09-23", pickup: "Khách sạn Rex", dropoff: "Sân bay Tân Sơn Nhất", contract: "issued", deposit: "pending", status: "Confirmed", price: "6.200.000", depositAmt: "2.000.000", total: "8.200.000", vehicleHeld: false, driverAssigned: false },
        { id: 50, created: "11/09/2026 16:40", customer: "Phạm Quốc Bảo", phone: "0987 111 222", email: "bao.pham@email.vn", mode: "SelfDrive", vehicle: "Mazda 3", type: "Sedan", plate: "51D-222.11", start: "19/09/2026 08:00", end: "19/09/2026 22:00", startIso: "2026-09-19", endIso: "2026-09-19", pickup: "Quận 7", dropoff: "Quận 7", contract: "none", deposit: "none", status: "Pending", price: "1.600.000", depositAmt: "800.000", total: "2.400.000", vehicleHeld: false, driverAssigned: false },
        { id: 49, created: "10/09/2026 14:05", customer: "Võ Thanh Hà", phone: "0908 333 444", email: "ha.vo@email.vn", mode: "WithDriver", vehicle: "Hyundai Santa Fe", type: "SUV", plate: "51F-999.00", start: "25/09/2026 06:00", end: "28/09/2026 18:00", startIso: "2026-09-25", endIso: "2026-09-28", pickup: "Bình Dương", dropoff: "Vũng Tàu", contract: "signed", deposit: "paid", status: "Assigned", price: "9.600.000", depositAmt: "2.500.000", total: "12.100.000", vehicleHeld: true, driverAssigned: true },
        { id: 48, created: "10/09/2026 11:18", customer: "Đặng Gia Linh", phone: "0932 555 666", email: "linh.dang@email.vn", mode: "SelfDrive", vehicle: "VinFast VF8", type: "SUV", plate: "51K-777.77", start: "15/09/2026 08:00", end: "17/09/2026 18:00", startIso: "2026-09-15", endIso: "2026-09-17", pickup: "Thủ Đức", dropoff: "Thủ Đức", contract: "signed", deposit: "paid", status: "InProgress", price: "5.400.000", depositAmt: "1.800.000", total: "7.200.000", vehicleHeld: true, driverAssigned: false },
        { id: 47, created: "08/09/2026 19:30", customer: "Bùi Minh Tuấn", phone: "0971 222 333", email: "tuan.bui@email.vn", mode: "WithDriver", vehicle: "Toyota Camry", type: "Sedan", plate: "51C-321.00", start: "12/09/2026 08:00", end: "13/09/2026 18:00", startIso: "2026-09-12", endIso: "2026-09-13", pickup: "Quận 5", dropoff: "Quận 1", contract: "signed", deposit: "paid", status: "Completed", price: "2.800.000", depositAmt: "1.000.000", total: "3.800.000", vehicleHeld: false, driverAssigned: true },
        { id: 46, created: "08/09/2026 10:02", customer: "Ngô Phương Chi", phone: "0944 121 212", email: "chi.ngo@email.vn", mode: "SelfDrive", vehicle: "BMW 5 Series", type: "Luxury", plate: "51A-555.55", start: "09/09/2026 09:00", end: "11/09/2026 18:00", startIso: "2026-09-09", endIso: "2026-09-11", pickup: "Quận 2", dropoff: "Quận 2", contract: "none", deposit: "none", status: "Cancelled", price: "7.200.000", depositAmt: "2.400.000", total: "9.600.000", vehicleHeld: false, driverAssigned: false },
        { id: 45, created: "07/09/2026 15:44", customer: "Hoàng Đức Anh", phone: "0968 444 555", email: "anh.hoang@email.vn", mode: "WithDriver", vehicle: "Ford Transit", type: "MPV", plate: "51H-246.80", start: "30/09/2026 05:00", end: "02/10/2026 21:00", startIso: "2026-09-30", endIso: "2026-10-02", pickup: "Bến xe Miền Đông", dropoff: "Đà Lạt", contract: "none", deposit: "none", status: "Pending", price: "11.000.000", depositAmt: "3.000.000", total: "14.000.000", vehicleHeld: false, driverAssigned: false },
        { id: 44, created: "06/09/2026 13:20", customer: "Mai Lan Phương", phone: "0916 777 888", email: "phuong.mai@email.vn", mode: "SelfDrive", vehicle: "Honda CR-V", type: "SUV", plate: "51E-135.79", start: "24/09/2026 08:00", end: "26/09/2026 18:00", startIso: "2026-09-24", endIso: "2026-09-26", pickup: "Gò Vấp", dropoff: "Gò Vấp", contract: "signed", deposit: "paid", status: "Confirmed", price: "4.200.000", depositAmt: "1.400.000", total: "5.600.000", vehicleHeld: false, driverAssigned: false },
        { id: 43, created: "05/09/2026 09:55", customer: "Trịnh Văn Khoa", phone: "0903 999 000", email: "khoa.trinh@email.vn", mode: "WithDriver", vehicle: "Mercedes V-Class", type: "Luxury", plate: "51A-111.11", start: "27/09/2026 07:30", end: "27/09/2026 22:00", startIso: "2026-09-27", endIso: "2026-09-27", pickup: "Nhà hát Thành phố", dropoff: "Sân bay Tân Sơn Nhất", contract: "issued", deposit: "failed", status: "Confirmed", price: "3.900.000", depositAmt: "1.500.000", total: "5.400.000", vehicleHeld: false, driverAssigned: false },
        { id: 42, created: "04/09/2026 17:12", customer: "Lý Ngọc Trâm", phone: "0938 654 321", email: "tram.ly@email.vn", mode: "SelfDrive", vehicle: "Hyundai Accent", type: "Sedan", plate: "51D-888.12", start: "16/09/2026 08:00", end: "18/09/2026 18:00", startIso: "2026-09-16", endIso: "2026-09-18", pickup: "Tân Bình", dropoff: "Tân Bình", contract: "signed", deposit: "paid", status: "Assigned", price: "2.400.000", depositAmt: "900.000", total: "3.300.000", vehicleHeld: true, driverAssigned: false }
    ];

    let bookings = INITIAL.map((b) => ({ ...b }));
    let selectedId = null;
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
        reset: document.getElementById("mkReset")
    };

    function badge(status) {
        return `<span class="mk-badge mk-badge-${status.toLowerCase()}">${STATUS_LABEL[status]}</span>`;
    }
    function contractBadge(v) {
        const cls = v === "signed" ? "ok" : v === "issued" ? "warn" : "gray";
        return `<span class="mk-badge mk-badge-${cls}">${CONTRACT_LABEL[v]}</span>`;
    }
    function depositBadge(v) {
        const cls = v === "paid" ? "ok" : v === "pending" || v === "failed" ? "warn" : "gray";
        return `<span class="mk-badge mk-badge-${cls}">${DEPOSIT_LABEL[v]}</span>`;
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
            if (filters.type && b.type !== filters.type) return false;
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
        document.querySelectorAll(".mk-metric").forEach((btn) => {
            btn.classList.toggle("is-on",
                (btn.dataset.queue && filters.queue === btn.dataset.queue) ||
                (btn.dataset.filterStatus && filters.queue !== "assign" && filters.status === btn.dataset.filterStatus)
            );
        });
        els.table.innerHTML = rows.map((b) => `
            <tr class="${selectedId === b.id ? "is-on" : ""}">
                <td><strong>#${b.id}</strong><div class="mk-muted">${b.created}</div></td>
                <td><strong>${b.customer}</strong><div class="mk-muted">${b.phone}</div></td>
                <td>${MODE_LABEL[b.mode] || b.mode}</td>
                <td>${b.vehicle}<div class="mk-muted">${TYPE_LABEL[b.type] || b.type} · ${b.plate}</div>${holdText(b)}</td>
                <td>${b.start}<div class="mk-muted">→ ${b.end}</div></td>
                <td>${contractBadge(b.contract)}</td>
                <td>${depositBadge(b.deposit)}</td>
                <td>${badge(b.status)}</td>
                <td>${actions(b)}</td>
            </tr>`).join("");
        els.cards.innerHTML = rows.map((b) => `
            <article class="mk-card mk-booking-card ${selectedId === b.id ? "is-on" : ""}">
                <div class="d-flex justify-content-between gap-2"><strong>#${b.id} · ${b.customer}</strong>${badge(b.status)}</div>
                <p class="mb-1">${MODE_LABEL[b.mode] || b.mode} · ${b.vehicle}</p>
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
            `<li class="${s[1] ? "is-done" : ""} ${s[2] ? "is-current" : ""}">${["①","②","③","④","⑤","⑥","⑦"][i]} ${s[0]}</li>`
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
                        <div><dt>Họ tên</dt><dd>${b.customer}</dd></div>
                        <div><dt>Số điện thoại</dt><dd>${b.phone}</dd></div>
                        <div><dt>Email</dt><dd>${b.email}</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Thông tin đơn</h3>
                    <dl class="mk-dl">
                        <div><dt>Mã đơn</dt><dd>#${b.id}</dd></div>
                        <div><dt>Hình thức thuê</dt><dd>${MODE_LABEL[b.mode] || b.mode}</dd></div>
                        <div><dt>Xe</dt><dd>${b.vehicle} · ${b.plate}</dd></div>
                        <div><dt>Loại xe</dt><dd>${TYPE_LABEL[b.type] || b.type}</dd></div>
                        <div><dt>Ngày nhận</dt><dd>${b.start}</dd></div>
                        <div><dt>Ngày trả</dt><dd>${b.end}</dd></div>
                        <div><dt>Điểm nhận</dt><dd>${b.pickup}</dd></div>
                        <div><dt>Điểm trả</dt><dd>${b.dropoff}</dd></div>
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
                        <div><dt>Hợp đồng</dt><dd>${CONTRACT_LABEL[b.contract]}</dd></div>
                        <div><dt>Trạng thái cọc</dt><dd>${DEPOSIT_LABEL[b.deposit]}</dd></div>
                    </dl>
                </section>
                <section class="mk-block">
                    <h3>Kiểm tra khả năng phục vụ</h3>
                    <p class="mk-note">Giao diện minh họa — chưa gọi backend kiểm tra xe, tài xế, lịch hay bảo trì.</p>
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
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
    }

    function applyAction(act, id) {
        const b = bookings.find((x) => x.id === id);
        if (!b) return;
        if (act === "detail") { selectedId = id; renderDrawer(b); renderList(); return; }
        if (act === "close") {
            selectedId = null;
            els.drawer.classList.remove("is-open");
            els.drawer.setAttribute("aria-hidden", "true");
            renderList();
            return;
        }
        if (act === "confirm" && b.status === "Pending") {
            b.status = "Confirmed";
            toast("Giao diện mô phỏng: đơn đã chuyển sang Đã xác nhận — chưa gọi hệ thống.");
        } else if (act === "reject" && (b.status === "Pending" || b.status === "Confirmed")) {
            b.status = "Cancelled";
            toast("Giao diện mô phỏng: đơn đã chuyển sang Đã hủy — chưa gọi hệ thống.");
        } else if (act === "assign" && b.status === "Confirmed") {
            b.status = "Assigned";
            b.vehicleHeld = true;
            b.driverAssigned = b.mode === "WithDriver";
            toast("Giao diện mô phỏng: đã phân công — chưa gọi hệ thống.");
        } else if (act === "handover" && b.status === "Assigned") {
            b.status = "InProgress";
            toast("Giao diện mô phỏng: đã giao xe — chưa gọi hệ thống.");
        } else if (act === "complete" && b.status === "InProgress") {
            b.status = "Completed";
            toast("Giao diện mô phỏng: đã tiếp nhận trả xe — chưa gọi hệ thống.");
        }
        selectedId = id;
        renderList();
        renderDrawer(b);
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
    document.getElementById("mkMetrics").addEventListener("click", (e) => {
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
})();

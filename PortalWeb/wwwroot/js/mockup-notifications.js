(function () {
    const DEMO = "Đây là chế độ mô phỏng — chưa kết nối hệ thống.";
    const PAGE = 10;
    const TYPE = {
        booking: { label: "Đơn thuê", icon: "📋" },
        vehicle: { label: "Xe", icon: "🚗" },
        driver: { label: "Tài xế", icon: "👨‍✈️" },
        alert: { label: "Cảnh báo", icon: "⚠️" },
        payment: { label: "Thanh toán", icon: "💰" },
        schedule: { label: "Lịch", icon: "📅" },
        maintenance: { label: "Bảo trì", icon: "🔧" },
        inspection: { label: "Kiểm xe", icon: "📋" }
    };
    const PRI = {
        high: { label: "Cao", cls: "nt-pri-high", mark: "🔴" },
        medium: { label: "Trung bình", cls: "nt-pri-medium", mark: "🟡" },
        normal: { label: "Thông thường", cls: "nt-pri-normal", mark: "🔵" }
    };
    const GROUP = { today: "Hôm nay", yesterday: "Hôm qua", earlier: "Trước đó" };

    let items = [
        { id: 1, unread: true, type: "booking", pri: "high", group: "today", time: "5 phút trước", title: "Đơn thuê cần xác nhận", body: "Đơn thuê #BK2026018 đang chờ xác nhận.", booking: "BK2026018", vehicle: "Toyota Vios", plate: "51K-888.12", when: "15/09/2026 13:00 – 17/09/2026 18:00", extra: "Khách hàng Lê Minh Đức đang chờ điều phối viên xác nhận.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 2, unread: true, type: "alert", pri: "high", group: "today", time: "15 phút trước", title: "Cảnh báo xung đột lịch", body: "Xe Toyota Fortuner biển số 51K-123.45 có khả năng xung đột lịch.", booking: "BK2026021", vehicle: "Toyota Fortuner", plate: "51K-123.45", when: "20/09/2026 08:00 – 12:00", extra: "Khoảng thời gian thuê có khả năng xung đột với lịch hiện tại của xe.", href: "/Mockup/Schedule", action: "Xem lịch xe" },
        { id: 3, unread: true, type: "booking", pri: "normal", group: "today", time: "30 phút trước", title: "Đã có đơn thuê mới", body: "Khách hàng Nguyễn Minh Anh vừa tạo đơn thuê #BK2026019.", booking: "BK2026019", vehicle: "Honda CR-V", plate: "51H-246.80", when: "16/09/2026 08:00 – 18/09/2026 18:00", extra: "Đơn mới cần được xem và xác nhận trong ca hôm nay.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 4, unread: true, type: "maintenance", pri: "medium", group: "today", time: "1 giờ trước", title: "Xe sắp đến hạn bảo trì", body: "Xe Mercedes E-Class 51H-678.90 cần kiểm tra bảo trì.", booking: "—", vehicle: "Mercedes E-Class", plate: "51H-678.90", when: "Dự kiến 17/09/2026", extra: "Nên sắp xếp kiểm tra trước khi nhận đơn tiếp theo.", href: "/Mockup/Inspections", action: "Xem biên bản" },
        { id: 5, unread: true, type: "alert", pri: "medium", group: "today", time: "1 giờ trước", title: "Xe sắp đến giờ trả", body: "Chuyến #BK2026014 sẽ trả xe Toyota Camry trong 45 phút.", booking: "BK2026014", vehicle: "Toyota Camry", plate: "51C-321.00", when: "15/09/2026 15:00", extra: "Chuẩn bị nhân sự nhận xe và kiểm tra tình trạng.", href: "/Mockup/Trips", action: "Xem chuyến đang chạy" },
        { id: 6, unread: true, type: "alert", pri: "high", group: "today", time: "2 giờ trước", title: "Xung đột lịch xe", body: "Kia Carnival 51G-456.78 bị trùng hai đơn trong cùng khung giờ.", booking: "BK2026022", vehicle: "Kia Carnival", plate: "51G-456.78", when: "21/09/2026 07:00 – 23/09/2026 20:00", extra: "Cần đổi xe hoặc dời một trong hai đơn thuê.", href: "/Mockup/Schedule", action: "Xem lịch xe" },
        { id: 7, unread: true, type: "booking", pri: "medium", group: "today", time: "2 giờ trước", title: "Đơn thuê chờ phân công", body: "Đơn #BK2026012 đã xác nhận nhưng chưa có tài xế.", booking: "BK2026012", vehicle: "Hyundai Santa Fe", plate: "51F-999.00", when: "18/09/2026 06:00 – 20/09/2026 18:00", extra: "Hình thức có tài xế — cần phân công trước 17/09.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 8, unread: true, type: "vehicle", pri: "medium", group: "today", time: "3 giờ trước", title: "Xe cần chú ý sau chuyến", body: "VinFast VF8 51K-777.77 được báo có cảnh báo kỹ thuật nhẹ.", booking: "BK2026009", vehicle: "VinFast VF8", plate: "51K-777.77", when: "15/09/2026 11:20", extra: "Nên mở biên bản kiểm xe trước khi cho thuê lại.", href: "/Mockup/Inspections", action: "Xem biên bản" },
        { id: 9, unread: false, type: "driver", pri: "normal", group: "today", time: "2 giờ trước", title: "Tài xế đã nhận chuyến", body: "Tài xế Nguyễn Văn Hùng đã nhận chuyến #BK2026015.", booking: "BK2026015", vehicle: "Mazda CX-5", plate: "51A-456.78", when: "15/09/2026 08:00", extra: "Tài xế đang trên đường đến điểm giao xe.", href: "/Mockup/Trips", action: "Xem chuyến đang chạy" },
        { id: 10, unread: false, type: "vehicle", pri: "normal", group: "today", time: "3 giờ trước", title: "Xe đã được bàn giao", body: "Xe Toyota Vios đã được bàn giao cho khách hàng.", booking: "BK2026008", vehicle: "Toyota Vios", plate: "51D-888.12", when: "15/09/2026 08:10", extra: "Khách Hoàng Gia Bảo đã nhận xe tại văn phòng.", href: "/Mockup/Trips", action: "Xem chuyến đang chạy" },
        { id: 11, unread: false, type: "payment", pri: "normal", group: "yesterday", time: "Hôm qua 16:20", title: "Khách đã thanh toán cọc", body: "Đơn #BK2026007 đã nhận tiền cọc 1.500.000 ₫.", booking: "BK2026007", vehicle: "Kia Seltos", plate: "51F-987.65", when: "14/09/2026 16:20", extra: "Có thể tiếp tục bước xác nhận và giữ xe.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 12, unread: false, type: "inspection", pri: "medium", group: "yesterday", time: "Hôm qua 17:35", title: "Biên bản nhận xe đã lập", body: "Biên bản nhận xe Honda CR-V #BK202602 đã được ghi nhận.", booking: "BK202602", vehicle: "Honda CR-V", plate: "51H-678.90", when: "14/09/2026 17:30", extra: "Có vấn đề nhỏ ở cản trước — xem chi tiết biên bản.", href: "/Mockup/Inspections", action: "Xem biên bản" },
        { id: 13, unread: false, type: "schedule", pri: "normal", group: "yesterday", time: "Hôm qua 09:00", title: "Lịch xe đã được cập nhật", body: "Ca 15/09 của đội xe khu vực quận 1 đã chốt.", booking: "—", vehicle: "Nhiều xe", plate: "—", when: "14/09/2026 09:00", extra: "Không phát hiện trùng lịch trong ca sáng.", href: "/Mockup/Schedule", action: "Xem lịch xe" },
        { id: 14, unread: false, type: "driver", pri: "medium", group: "yesterday", time: "Hôm qua 07:40", title: "Tài xế chưa nhận chuyến", body: "Tài xế Trần Minh Nam chưa xác nhận chuyến #BK2026011.", booking: "BK2026011", vehicle: "Ford Transit", plate: "51H-246.80", when: "14/09/2026 05:10", extra: "Đã nhắc trên ứng dụng tài xế. Có thể đổi tài xế nếu quá 30 phút.", href: "/Mockup/Trips", action: "Xem chuyến đang chạy" },
        { id: 15, unread: false, type: "booking", pri: "normal", group: "yesterday", time: "Hôm qua 11:15", title: "Đơn thuê đã được xác nhận", body: "Đơn #BK2026005 của Phạm Thị Hà đã xác nhận.", booking: "BK2026005", vehicle: "Mazda 3", plate: "51D-222.11", when: "14/09/2026 11:15", extra: "Khách tự lái — chờ bước giao xe vào ngày nhận.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 16, unread: false, type: "booking", pri: "normal", group: "yesterday", time: "Hôm qua 19:40", title: "Khách hủy đơn thuê", body: "Đơn #BK2026003 đã được khách hủy trước giờ giao.", booking: "BK2026003", vehicle: "BMW 5 Series", plate: "51A-555.55", when: "14/09/2026 19:40", extra: "Xe đã được trả về trạng thái khả dụng trên lịch mô phỏng.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 17, unread: false, type: "booking", pri: "normal", group: "earlier", time: "13/09/2026", title: "Đơn thuê hoàn thành", body: "Đơn #BK2026001 đã hoàn tất và quyết toán.", booking: "BK2026001", vehicle: "Toyota Vios", plate: "51K-123.45", when: "13/09/2026 18:00", extra: "Không phát sinh phụ phí.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 18, unread: false, type: "booking", pri: "normal", group: "earlier", time: "13/09/2026", title: "Yêu cầu đổi điểm nhận xe", body: "Khách Vũ Thị Mai muốn đổi điểm nhận cho #BK2026013.", booking: "BK2026013", vehicle: "Kia Carnival", plate: "51G-456.78", when: "13/09/2026 14:00", extra: "Điểm mới: sân bay Tân Sơn Nhất.", href: "/Mockup/Hub", action: "Xem đơn thuê" },
        { id: 19, unread: false, type: "alert", pri: "high", group: "earlier", time: "12/09/2026", title: "Cảnh báo trễ giờ giao xe", body: "Chuyến #BK2025998 giao muộn 25 phút so với kế hoạch.", booking: "BK2025998", vehicle: "Hyundai Santa Fe", plate: "51F-999.00", when: "12/09/2026 08:25", extra: "Đã liên hệ khách. Không phát sinh khiếu nại.", href: "/Mockup/Trips", action: "Xem chuyến đang chạy" },
        { id: 20, unread: false, type: "alert", pri: "medium", group: "earlier", time: "12/09/2026", title: "Cảnh báo nhiên liệu thấp khi nhận", body: "Mazda CX-5 trả về với mức nhiên liệu 20%.", booking: "BK2025995", vehicle: "Mazda CX-5", plate: "51A-456.78", when: "12/09/2026 17:10", extra: "Cần đối chiếu phụ phí nhiên liệu trên biên bản.", href: "/Mockup/Inspections", action: "Xem biên bản" },
        { id: 21, unread: false, type: "vehicle", pri: "normal", group: "earlier", time: "11/09/2026", title: "Xe đã về bãi", body: "Mercedes V-Class 51A-111.11 đã về bãi quận 3.", booking: "BK2025990", vehicle: "Mercedes V-Class", plate: "51A-111.11", when: "11/09/2026 21:00", extra: "Sẵn sàng cho ca sáng hôm sau.", href: "/Mockup/Schedule", action: "Xem lịch xe" },
        { id: 22, unread: false, type: "vehicle", pri: "medium", group: "earlier", time: "11/09/2026", title: "Xe tạm ngưng khai thác", body: "Ford Transit 51H-246.80 tạm ngưng để thay lốp.", booking: "—", vehicle: "Ford Transit", plate: "51H-246.80", when: "11/09/2026 10:00", extra: "Dự kiến trở lại sau 1 ngày làm việc.", href: "/Mockup/Schedule", action: "Xem lịch xe" },
        { id: 23, unread: false, type: "vehicle", pri: "normal", group: "earlier", time: "10/09/2026", title: "Xe đã vệ sinh xong", body: "Honda CR-V 51E-135.79 đã vệ sinh nội thất và sẵn sàng giao.", booking: "—", vehicle: "Honda CR-V", plate: "51E-135.79", when: "10/09/2026 16:45", extra: "Có thể xếp vào đơn ngày 16/09.", href: "/Mockup/Schedule", action: "Xem lịch xe" },
        { id: 24, unread: false, type: "driver", pri: "normal", group: "earlier", time: "10/09/2026", title: "Tài xế bắt đầu ca", body: "Tài xế Lê Quang Duy đã điểm danh ca sáng.", booking: "—", vehicle: "—", plate: "—", when: "10/09/2026 06:00", extra: "Trạng thái: khả dụng.", href: "/Mockup/Schedule", action: "Xem lịch xe" },
        { id: 25, unread: false, type: "driver", pri: "normal", group: "earlier", time: "09/09/2026", title: "Tài xế kết thúc ca", body: "Tài xế Phạm Tiến Đạt đã kết thúc ca và nộp biên bản.", booking: "BK2025988", vehicle: "Toyota Camry", plate: "51C-321.00", when: "09/09/2026 21:10", extra: "Không ghi nhận sự cố trong ca.", href: "/Mockup/Trips", action: "Xem chuyến đang chạy" }
    ];

    let tab = "all";
    let query = "";
    let shown = PAGE;
    let menuId = null;
    let selectedId = null;

    const els = {
        btn: document.getElementById("mkNotifyBtn"),
        drop: document.getElementById("mkNotifyDrop"),
        dropList: document.getElementById("mkNotifyDropList"),
        dropMeta: document.getElementById("mkNotifyDropMeta"),
        dot: document.getElementById("mkNotifyDot"),
        toast: document.getElementById("mkGlobalToast") || document.getElementById("mkToast"),
        kpis: document.getElementById("ntKpis"),
        tabs: document.getElementById("ntTabs"),
        search: document.getElementById("ntSearch"),
        list: document.getElementById("ntList"),
        empty: document.getElementById("ntEmpty"),
        meta: document.getElementById("ntMeta"),
        more: document.getElementById("ntMore"),
        drawer: document.getElementById("ntDrawer"),
        markAll: document.getElementById("ntMarkAll")
    };

    function toast(msg) {
        if (!els.toast) return;
        els.toast.textContent = msg || DEMO;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
    }

    function unreadCount() { return items.filter((x) => x.unread).length; }
    function isVehicleType(t) { return t === "vehicle" || t === "maintenance"; }

    function visible() {
        return items.filter((n) => {
            if (tab === "unread" && !n.unread) return false;
            if (tab === "read" && n.unread) return false;
            if (tab === "alert" && n.type !== "alert") return false;
            if (tab === "booking" && n.type !== "booking") return false;
            if (tab === "vehicle" && !isVehicleType(n.type)) return false;
            if (tab === "driver" && n.type !== "driver") return false;
            if (query) {
                const q = query.toLowerCase();
                const blob = `${n.title} ${n.body} ${n.booking} ${n.plate} ${n.vehicle} ${n.extra}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        });
    }

    function badgePri(p) { return `<span class="mk-badge ${PRI[p].cls}">${PRI[p].label}</span>`; }
    function badgeType(t) { return `<span class="mk-badge mk-badge-gray">${TYPE[t].label}</span>`; }

    /** Parse mock codes like BK2026018 → 18; also accept plain #52 / 52. */
    function parseBookingId(code) {
        if (code == null || code === "" || code === "—") return null;
        const s = String(code).trim();
        const bk = s.match(/^BK\d{4}(\d+)$/i);
        if (bk) return Number(bk[1]);
        const num = s.match(/^#?(\d+)$/);
        return num ? Number(num[1]) : null;
    }

    /** Hub list links with a booking code → open that booking's Hub detail. */
    function actionHref(n) {
        const id = n.bookingId != null ? Number(n.bookingId) : parseBookingId(n.booking);
        if (id > 0 && (n.href === "/Mockup/Hub" || n.action === "Xem đơn thuê"))
            return "/Mockup/Hub?id=" + id;
        return n.href;
    }

    function syncBell() {
        const unread = unreadCount();
        if (els.dot) {
            els.dot.textContent = String(unread);
            els.dot.hidden = unread === 0;
        }
        if (els.dropMeta) els.dropMeta.textContent = unread ? `${unread} thông báo chưa đọc` : "Không có thông báo chưa đọc";
        if (els.dropList) {
            const newest = items.filter((x) => x.unread).slice(0, 4);
            els.dropList.innerHTML = newest.length
                ? newest.map((n) => `
                    <button type="button" class="nt-drop-item is-unread" data-drop-open="${n.id}">
                        <span>${TYPE[n.type].icon}</span>
                        <span><strong>${n.title}</strong><small>${n.body}<br>${n.time}</small></span>
                    </button>`).join("")
                : `<p class="nt-drop-empty">Không có thông báo chưa đọc.</p>`;
        }
    }

    function kpis() {
        if (!els.kpis) return;
        const unread = unreadCount();
        const booking = items.filter((x) => x.unread && x.type === "booking").length;
        const alert = items.filter((x) => x.unread && x.type === "alert").length;
        const vehicle = items.filter((x) => x.unread && isVehicleType(x.type)).length;
        const cards = [
            { key: "unread", icon: "🔵", label: "Thông báo mới", value: unread, hint: "Chưa xử lý" },
            { key: "booking", icon: "📋", label: "Đơn thuê", value: booking, hint: "Cần xem đơn" },
            { key: "alert", icon: "⚠️", label: "Cảnh báo", value: alert, hint: "Ưu tiên xử lý" },
            { key: "vehicle", icon: "🔧", label: "Xe cần chú ý", value: vehicle, hint: "Bảo trì / sự cố" }
        ];
        els.kpis.innerHTML = cards.map((c) => `
            <button type="button" class="mk-metric nt-kpi ${tab === c.key ? "is-on" : ""}" data-tab="${c.key}">
                <span>${c.icon} ${c.label}</span>
                <strong>${c.value}</strong>
                <em>${c.hint}</em>
            </button>`).join("");
    }

    function tabs() {
        if (!els.tabs) return;
        const all = items.length;
        const unread = unreadCount();
        const defs = [
            { key: "all", label: `Tất cả (${all})` },
            { key: "unread", label: `Chưa đọc (${unread})` },
            { key: "read", label: `Đã đọc (${all - unread})` },
            { key: "alert", label: `Cảnh báo (${items.filter((x) => x.type === "alert").length})` },
            { key: "booking", label: `Đơn thuê (${items.filter((x) => x.type === "booking").length})` },
            { key: "vehicle", label: `Xe (${items.filter((x) => isVehicleType(x.type)).length})` },
            { key: "driver", label: `Tài xế (${items.filter((x) => x.type === "driver").length})` }
        ];
        els.tabs.innerHTML = defs.map((d) =>
            `<button type="button" class="mk-pill ${tab === d.key ? "is-on" : ""}" data-tab="${d.key}">${d.label}</button>`
        ).join("");
    }

    function itemHtml(n) {
        return `<div class="nt-item ${n.unread ? "is-unread" : ""} ${selectedId === n.id ? "is-on" : ""}" data-open="${n.id}">
            <span class="nt-ico">${TYPE[n.type].icon}</span>
            <span>
                <span class="nt-title">${PRI[n.pri].mark} ${n.title}</span>
                <span class="nt-body">${n.body}</span>
                <span class="nt-meta">${badgeType(n.type)} ${badgePri(n.pri)} <span class="mk-badge ${n.unread ? "mk-badge-confirmed" : "mk-badge-gray"}">${n.unread ? "Chưa đọc" : "Đã đọc"}</span> <span class="nt-time">${n.time}</span></span>
            </span>
            <span class="nt-dot" title="Chưa đọc"></span>
            <span class="nt-menu-wrap">
                <button type="button" class="nt-menu-btn" data-menu="${n.id}" aria-label="Thao tác">⋯</button>
                ${menuId === n.id ? `<div class="nt-menu">
                    ${n.unread ? `<button type="button" data-read="${n.id}">Đánh dấu đã đọc</button>` : ""}
                    <button type="button" data-del="${n.id}">Xóa khỏi danh sách</button>
                </div>` : ""}
            </span>
        </div>`;
    }

    function renderPage() {
        if (!els.list) { syncBell(); return; }
        kpis();
        tabs();
        const all = visible();
        if (shown > all.length) shown = Math.max(PAGE, all.length);
        const slice = all.slice(0, shown);
        const groups = ["today", "yesterday", "earlier"];
        let html = "";
        groups.forEach((g) => {
            const rows = slice.filter((x) => x.group === g);
            if (!rows.length) return;
            html += `<div class="nt-group">${GROUP[g]}</div>` + rows.map(itemHtml).join("");
        });
        els.list.innerHTML = html;
        els.empty.hidden = slice.length > 0;
        els.meta.textContent = all.length
            ? `Hiển thị 1–${slice.length} trong ${all.length} thông báo`
            : "Không có thông báo phù hợp";
        if (els.more) els.more.hidden = !all.length || slice.length >= all.length;
        syncBell();
    }

    function openDrawer(id) {
        const n = items.find((x) => x.id === Number(id));
        if (!n || !els.drawer) return;
        selectedId = n.id;
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        const readBtn = n.unread
            ? `<button type="button" class="mk-btn mk-btn-ghost" data-read="${n.id}">Đánh dấu đã đọc</button>`
            : "";
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-0">Chi tiết thông báo</h2>
                    <p class="mk-muted mb-0">${n.time}</p>
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <h3 class="h6">${PRI[n.pri].mark} ${n.title}</h3>
                <dl class="mk-dl mk-block">
                    <div><dt>Mã liên quan</dt><dd>${n.booking}</dd></div>
                    <div><dt>Xe</dt><dd>${n.vehicle}</dd></div>
                    <div><dt>Biển số</dt><dd>${n.plate}</dd></div>
                    <div><dt>Thời gian</dt><dd>${n.when}</dd></div>
                    <div><dt>Mức độ</dt><dd>${badgePri(n.pri)}</dd></div>
                    <div><dt>Trạng thái</dt><dd><span class="mk-badge ${n.unread ? "mk-badge-confirmed" : "mk-badge-gray"}">${n.unread ? "Chưa đọc" : "Đã đọc"}</span></dd></div>
                    <div><dt>Loại</dt><dd>${badgeType(n.type)}</dd></div>
                </dl>
                <div class="mk-block">
                    <h3>Nội dung</h3>
                    <p>${n.body}</p>
                    <p class="mk-muted">${n.extra}</p>
                </div>
                <div class="nt-actions">
                    <a class="mk-btn mk-btn-primary" href="${actionHref(n)}">${n.action}</a>
                    ${readBtn}
                    <button type="button" class="mk-btn mk-btn-ghost" data-del="${n.id}">Xóa khỏi danh sách</button>
                </div>
                <p class="mk-note">${DEMO}</p>
            </div>`;
        renderPage();
    }

    function markRead(id) {
        const n = items.find((x) => x.id === Number(id));
        if (!n || !n.unread) return;
        n.unread = false;
        toast("Đã đánh dấu thông báo là đã đọc.");
        renderPage();
        if (selectedId === n.id) openDrawer(n.id);
    }

    function removeItem(id) {
        items = items.filter((x) => x.id !== Number(id));
        if (selectedId === Number(id) && els.drawer) {
            els.drawer.classList.remove("is-open");
            selectedId = null;
        }
        toast("Đã xóa thông báo khỏi danh sách.");
        renderPage();
    }

    function goDetail(id) {
        if (els.list) openDrawer(id);
        else window.location.href = "/Mockup/Notifications?mo=" + id;
    }

    els.btn?.addEventListener("click", (e) => {
        e.stopPropagation();
        const open = els.drop.hidden;
        els.drop.hidden = !open;
        els.btn.setAttribute("aria-expanded", open ? "true" : "false");
        if (open) syncBell();
    });

    els.search?.addEventListener("input", () => {
        query = els.search.value.trim();
        shown = PAGE;
        renderPage();
    });

    els.more?.addEventListener("click", () => {
        shown += PAGE;
        renderPage();
        toast(DEMO);
    });

    els.markAll?.addEventListener("click", () => {
        items.forEach((x) => { x.unread = false; });
        toast("Đã đánh dấu tất cả thông báo là đã đọc.");
        renderPage();
    });

    document.addEventListener("click", (e) => {
        if (els.drop && !els.drop.hidden && !e.target.closest(".nt-bell-wrap")) {
            els.drop.hidden = true;
            els.btn?.setAttribute("aria-expanded", "false");
        }
        if (menuId && !e.target.closest(".nt-menu-wrap")) {
            menuId = null;
            renderPage();
        }
        const dropOpen = e.target.closest("[data-drop-open]");
        if (dropOpen) {
            els.drop.hidden = true;
            goDetail(dropOpen.dataset.dropOpen);
            return;
        }
        if (e.target.closest("[data-close]")) {
            els.drawer?.classList.remove("is-open");
            selectedId = null;
            renderPage();
            return;
        }
        const t = e.target.closest("[data-tab]");
        if (t && (els.tabs?.contains(t) || els.kpis?.contains(t))) {
            tab = t.dataset.tab;
            shown = PAGE;
            renderPage();
            return;
        }
        const menu = e.target.closest("[data-menu]");
        if (menu) {
            e.stopPropagation();
            menuId = menuId === Number(menu.dataset.menu) ? null : Number(menu.dataset.menu);
            renderPage();
            return;
        }
        const read = e.target.closest("[data-read]");
        if (read) {
            e.stopPropagation();
            menuId = null;
            markRead(read.dataset.read);
            return;
        }
        const del = e.target.closest("[data-del]");
        if (del) {
            e.stopPropagation();
            menuId = null;
            removeItem(del.dataset.del);
            return;
        }
        const open = e.target.closest("[data-open]");
        if (open && !e.target.closest(".nt-menu-wrap")) openDrawer(open.dataset.open);
    });

    syncBell();
    renderPage();
    const mo = new URLSearchParams(location.search).get("mo");
    if (mo && els.list) openDrawer(mo);
})();

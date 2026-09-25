(function () {
  const M = window.DriveRentMock || {};

  const statusTone = {
    Pending: "amber", Confirmed: "green", "In Progress": "blue", Completed: "green", Cancelled: "red",
    Available: "green", "In Use": "blue", Maintenance: "amber", "Out of Service": "red",
    Active: "green", Locked: "red",
    Busy: "amber", Offline: "slate",
    Paid: "green", Failed: "red",
    Signed: "green", Issued: "blue", Voided: "red",
    Healthy: "green", "Due Soon": "amber", Overdue: "red", "In Service": "blue",
    Investigating: "amber", Resolved: "green", Closed: "slate",
    Critical: "red", High: "amber", Medium: "blue", Low: "slate",
    Premium: "purple", Standard: "blue", Basic: "slate"
  };

  function esc(s) {
    return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }

  function initials(name) {
    return String(name || "?")
      .split(/\s+/).filter(Boolean).slice(-2).map((p) => p[0]).join("").toUpperCase();
  }

  const vi = {
    Pending: "Chờ xác nhận", Confirmed: "Đã xác nhận", Assigned: "Đã phân công",
    "In Progress": "Đang thực hiện", Completed: "Hoàn thành", Cancelled: "Đã hủy",
    Available: "Có sẵn", "In Use": "Đang sử dụng", Maintenance: "Đang bảo trì",
    "Out of Service": "Ngừng hoạt động", Active: "Đang hoạt động", Locked: "Đã khóa",
    Busy: "Đang bận", Offline: "Ngoại tuyến", Paid: "Đã thanh toán", Failed: "Thất bại",
    Signed: "Đã ký", Issued: "Đã phát hành", Voided: "Đã vô hiệu",
    Healthy: "Tốt", "Due Soon": "Sắp đến hạn", Overdue: "Quá hạn", "In Service": "Đang bảo dưỡng",
    Investigating: "Đang điều tra", Resolved: "Đã xử lý", Closed: "Đã đóng",
    Critical: "Nghiêm trọng", High: "Cao", Medium: "Trung bình", Low: "Thấp",
    Premium: "Cao cấp", Standard: "Tiêu chuẩn", Basic: "Cơ bản",
    "Self Drive": "Tự lái", "With Driver": "Có tài xế",
    "Credit Card": "Thẻ tín dụng", "Bank Transfer": "Chuyển khoản ngân hàng",
    Cash: "Tiền mặt", Momo: "Momo", VNPAY: "VNPAY",
    Accident: "Tai nạn", "Mechanical Issue": "Sự cố cơ khí", "Flat Tire": "Lốp xẹp",
    "Customer Damage": "Hư hỏng do khách", "Engine Overheat": "Động cơ quá nhiệt",
    "Minor Scratch": "Trầy xước nhẹ", "Electrical Issue": "Sự cố điện",
    "Brake Problem": "Lỗi phanh", "Battery Issue": "Lỗi ắc quy"
  };
  function t(key) { return vi[key] || key; }
  function badge(status) {
    const tone = statusTone[status] || "slate";
    return `<span class="aui-badge ${tone}">${esc(t(status))}</span>`;
  }

  function toast(msg) {
    const el = document.getElementById("auiToast");
    if (!el) return;
    el.textContent = msg;
    el.hidden = false;
    clearTimeout(toast._t);
    toast._t = setTimeout(() => { el.hidden = true; }, 2600);
  }

  /* Sidebar / shell */
  const shell = document.querySelector(".aui-shell");
  const sidebarToggle = document.querySelector("[data-aui-toggle]");
  const backdrop = document.querySelector("[data-aui-backdrop]");
  sidebarToggle?.addEventListener("click", () => shell?.classList.toggle("is-sidebar-open"));
  backdrop?.addEventListener("click", () => shell?.classList.remove("is-sidebar-open"));

  document.querySelectorAll("[data-aui-toast]").forEach((el) => {
    el.addEventListener("click", (e) => {
      e.preventDefault();
      toast(el.getAttribute("data-aui-toast"));
    });
  });

  /* Drawer */
  const drawer = document.getElementById("auiDrawer");
  const drawerBackdrop = document.querySelector("[data-aui-drawer-backdrop]");
  const drawerTitle = document.getElementById("auiDrawerTitle");
  const drawerSub = document.getElementById("auiDrawerSub");
  const drawerTabs = document.getElementById("auiDrawerTabs");
  const drawerBody = document.getElementById("auiDrawerBody");
  const drawerFoot = document.getElementById("auiDrawerFoot");

  function closeDrawer() {
    drawer?.classList.remove("is-open");
    drawer?.setAttribute("aria-hidden", "true");
    drawerBackdrop?.classList.remove("is-on");
  }

  function openDrawer({ title, subtitle, tabs, renderTab, footerHtml }) {
    if (!drawer) return;
    drawerTitle.textContent = title || "Details";
    drawerSub.textContent = subtitle || "";
    drawerTabs.innerHTML = "";
    drawerTabs.hidden = !tabs?.length;

    let active = tabs?.[0]?.key;
    function paint() {
      if (tabs?.length) {
        drawerTabs.innerHTML = tabs.map((t) =>
          `<button type="button" class="${t.key === active ? "is-on" : ""}" data-tab="${esc(t.key)}">${esc(t.label)}</button>`
        ).join("");
        drawerTabs.querySelectorAll("[data-tab]").forEach((btn) => {
          btn.addEventListener("click", () => { active = btn.getAttribute("data-tab"); paint(); });
        });
      }
      drawerBody.innerHTML = renderTab ? renderTab(active) : "";
      drawerFoot.innerHTML = footerHtml || "";
      drawerFoot.querySelectorAll("[data-aui-toast]").forEach((el) => {
        el.addEventListener("click", () => toast(el.getAttribute("data-aui-toast")));
      });
      drawerFoot.querySelectorAll("[data-aui-drawer-close]").forEach((el) => {
        el.addEventListener("click", closeDrawer);
      });
    }
    paint();
    drawer.classList.add("is-open");
    drawer.setAttribute("aria-hidden", "false");
    drawerBackdrop?.classList.add("is-on");
  }

  document.querySelectorAll("[data-aui-drawer-close]").forEach((el) => el.addEventListener("click", closeDrawer));
  drawerBackdrop?.addEventListener("click", closeDrawer);

  /* Modal */
  const modal = document.getElementById("auiModal");
  const modalTitle = document.getElementById("auiModalTitle");
  const modalBody = document.getElementById("auiModalBody");
  const modalOk = document.getElementById("auiModalOk");

  function openModal({ title, body, okLabel, onOk }) {
    modalTitle.textContent = title || "Confirm";
    modalBody.textContent = body || "";
    modalOk.textContent = okLabel || "Confirm";
    modal.hidden = false;
    modalOk.onclick = () => {
      modal.hidden = true;
      onOk?.();
    };
  }
  document.querySelectorAll("[data-aui-modal-close]").forEach((el) => {
    el.addEventListener("click", () => { modal.hidden = true; });
  });

  /* Charts */
  const chartDefaults = {
    font: { family: "'Inter', system-ui, sans-serif" },
    color: "#64748B"
  };
  if (window.Chart) {
    Chart.defaults.font.family = chartDefaults.font.family;
    Chart.defaults.color = chartDefaults.color;
  }

  function lineChart(canvas, labels, values, label) {
    if (!window.Chart || !canvas) return;
    return new Chart(canvas, {
      type: "line",
      data: {
        labels,
        datasets: [{
          label: label || "Doanh thu",
          data: values,
          borderColor: "#2563EB",
          backgroundColor: "rgba(37,99,235,.12)",
          fill: true,
          tension: 0.35,
          pointRadius: 3,
          pointBackgroundColor: "#2563EB",
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false } },
          y: { grid: { color: "#E2E8F0" }, ticks: { callback: (v) => "$" + (v / 1000) + "k" } }
        }
      }
    });
  }

  function donutChart(canvas, items) {
    if (!window.Chart || !canvas) return;
    return new Chart(canvas, {
      type: "doughnut",
      data: {
        labels: items.map((i) => i.label),
        datasets: [{
          data: items.map((i) => i.value),
          backgroundColor: items.map((i) => i.color),
          borderWidth: 0,
          hoverOffset: 4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: "72%",
        plugins: { legend: { display: false } }
      }
    });
  }

  /* Pagination helper */
  function paginate(items, page, pageSize) {
    const total = items.length;
    const pages = Math.max(1, Math.ceil(total / pageSize));
    const p = Math.min(Math.max(1, page), pages);
    return { page: p, pages, total, slice: items.slice((p - 1) * pageSize, p * pageSize) };
  }

  function renderPager(el, state, onChange) {
    if (!el) return;
    const buttons = [];
    buttons.push(`<button type="button" data-p="${state.page - 1}" ${state.page <= 1 ? "disabled" : ""}>‹</button>`);
    for (let i = 1; i <= state.pages; i++) {
      if (state.pages > 7 && Math.abs(i - state.page) > 2 && i !== 1 && i !== state.pages) {
        if (i === 2 || i === state.pages - 1) buttons.push(`<button type="button" disabled>…</button>`);
        continue;
      }
      buttons.push(`<button type="button" class="${i === state.page ? "is-on" : ""}" data-p="${i}">${i}</button>`);
    }
    buttons.push(`<button type="button" data-p="${state.page + 1}" ${state.page >= state.pages ? "disabled" : ""}>›</button>`);
    el.innerHTML = `
      <span class="aui-muted">Hiển thị ${(state.page - 1) * state.pageSize + (state.total ? 1 : 0)}–${Math.min(state.page * state.pageSize, state.total)} / ${state.total}</span>
      <div class="aui-pager-btns">${buttons.join("")}</div>`;
    el.querySelectorAll("[data-p]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const n = Number(btn.getAttribute("data-p"));
        if (!Number.isNaN(n) && !btn.disabled) onChange(n);
      });
    });
  }

  /* Table selection */
  function bindSelectAll(root) {
    const all = root.querySelector("[data-select-all]");
    all?.addEventListener("change", () => {
      root.querySelectorAll("tbody [data-row-check]").forEach((cb) => { cb.checked = all.checked; });
    });
  }

  /* Page: Dashboard */
  function initDashboard() {
    const root = document.getElementById("auiDashboard");
    if (!root) return;
    const d = M.mockDashboardData;
    lineChart(document.getElementById("chartRevenue"), d.revenue.months, d.revenue.values, "Doanh thu");
    const totalStatus = d.bookingStatus.reduce((s, x) => s + x.value, 0);
    donutChart(document.getElementById("chartBookingStatus"), d.bookingStatus.map((x) => ({ ...x, label: t(x.label) })));
    const center = document.getElementById("donutCenter");
    if (center) center.innerHTML = `<strong>${totalStatus.toLocaleString()}</strong><span>Tổng đơn thuê</span>`;

    const top = document.getElementById("topVehicles");
    if (top) {
      top.innerHTML = d.topVehicles.map((v) => `
        <div class="aui-vehicle-row">
          <div class="aui-vehicle-thumb"><i class="bi bi-car-front"></i></div>
          <div class="aui-vehicle-meta">
            <strong>${esc(v.name)}</strong>
            <small>${v.bookings} đơn thuê</small>
            <div class="aui-progress" style="margin-top:.35rem"><span style="width:${v.pct}%"></span></div>
          </div>
        </div>`).join("");
    }
  }

  /* Page: Bookings */
  function initBookings() {
    const root = document.getElementById("auiBookings");
    if (!root) return;
    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const rental = root.querySelector("[data-filter-rental]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return M.mockBookings.filter((b) => {
        if (status?.value && b.status !== status.value) return false;
        if (rental?.value && b.rentalType !== rental.value) return false;
        if (!qq) return true;
        return [b.id, b.customer, b.vehicle].join(" ").toLowerCase().includes(qq);
      });
    }

    function renderBookingTab(b, tab) {
      if (tab === "customer") {
        return `
          <div class="aui-section">
            <div class="aui-person" style="margin-bottom:1rem">
              <span class="aui-avatar">${esc(initials(b.customer))}</span>
              <div><strong>${esc(b.customer)}</strong><small>${esc(b.email)}</small></div>
            </div>
            <dl class="aui-kv">
              <div><dt>Số điện thoại</dt><dd>${esc(b.phone)}</dd></div>
              <div><dt>Hạng thành viên</dt><dd>${badge(b.membership)}</dd></div>
              <div><dt>Email</dt><dd>${esc(b.email)}</dd></div>
            </dl>
          </div>`;
      }
      if (tab === "vehicle") {
        return `
          <div class="aui-section">
            <div class="aui-vehicle-row" style="border:0;padding:0;margin-bottom:1rem">
              <div class="aui-vehicle-thumb" style="width:64px;height:64px"><i class="bi bi-car-front-fill"></i></div>
              <div class="aui-vehicle-meta"><strong>${esc(b.vehicle)}</strong><small>${esc(b.plate)}</small></div>
            </div>
            <dl class="aui-kv">
              <div><dt>Loại xe</dt><dd>${esc(b.type)}</dd></div>
              <div><dt>Hộp số</dt><dd>${esc(b.transmission === "Automatic" ? "Tự động" : b.transmission === "Manual" ? "Số sàn" : b.transmission)}</dd></div>
              <div><dt>Số chỗ</dt><dd>${esc(b.seats)}</dd></div>
              <div><dt>Biển số xe</dt><dd>${esc(b.plate)}</dd></div>
            </dl>
          </div>`;
      }
      if (tab === "payments") {
        return `
          <div class="aui-section">
            <h4>Chi tiết giá</h4>
            <dl class="aui-kv">
              <div><dt>Đơn giá theo ngày</dt><dd>${esc(b.dailyRate)}</dd></div>
              <div><dt>Bảo hiểm</dt><dd>${esc(b.insurance)}</dd></div>
              <div><dt>Phí sân bay</dt><dd>${esc(b.airportFee)}</dd></div>
              <div><dt>Giảm giá</dt><dd>${esc(b.discount)}</dd></div>
              <div><dt>Tổng tiền</dt><dd>${esc(b.amount)}</dd></div>
            </dl>
          </div>`;
      }
      if (tab === "history") {
        return `
          <ul class="aui-timeline">
            <li><strong>Đơn thuê được tạo</strong><small>${esc(b.created)}</small></li>
            <li><strong>Trạng thái: ${esc(t(b.status))}</strong><small>Sự kiện hệ thống (mô phỏng)</small></li>
            <li><strong>Đã thông báo khách hàng</strong><small>Email mô phỏng</small></li>
          </ul>`;
      }
      return `
        <div class="aui-section">
          <h4>Thông tin đơn thuê</h4>
          <dl class="aui-kv">
            <div><dt>Hình thức thuê</dt><dd>${esc(t(b.rentalType))}</dd></div>
            <div><dt>Thời lượng</dt><dd>${esc(b.duration)}</dd></div>
            <div><dt>Điểm nhận xe</dt><dd>${esc(b.pickup)}</dd></div>
            <div><dt>Điểm trả xe</dt><dd>${esc(b.dropoff)}</dd></div>
            <div class="full"><dt>Thời gian thuê</dt><dd>${esc(b.dateRange)}</dd></div>
          </dl>
        </div>
        <div class="aui-section">
          <h4>Thông tin khách hàng</h4>
          <div class="aui-person" style="margin-bottom:.75rem">
            <span class="aui-avatar">${esc(initials(b.customer))}</span>
            <div>
              <strong>${esc(b.customer)}</strong>
              <small>${esc(b.phone)} · ${esc(b.email)}</small>
            </div>
            ${badge(b.membership)}
          </div>
        </div>
        <div class="aui-section">
          <h4>Thông tin xe</h4>
          <dl class="aui-kv">
            <div><dt>Mẫu xe</dt><dd>${esc(b.vehicle)}</dd></div>
            <div><dt>Biển số xe</dt><dd>${esc(b.plate)}</dd></div>
            <div><dt>Loại xe</dt><dd>${esc(b.type)}</dd></div>
            <div><dt>Hộp số</dt><dd>${esc(b.transmission === "Automatic" ? "Tự động" : b.transmission === "Manual" ? "Số sàn" : b.transmission)}</dd></div>
            <div><dt>Số chỗ</dt><dd>${esc(b.seats)}</dd></div>
          </dl>
        </div>
        <div class="aui-section">
          <h4>Chi tiết giá</h4>
          <dl class="aui-kv">
            <div><dt>Đơn giá theo ngày</dt><dd>${esc(b.dailyRate)}</dd></div>
            <div><dt>Bảo hiểm</dt><dd>${esc(b.insurance)}</dd></div>
            <div><dt>Phí sân bay</dt><dd>${esc(b.airportFee)}</dd></div>
            <div><dt>Giảm giá</dt><dd>${esc(b.discount)}</dd></div>
            <div><dt>Tổng tiền</dt><dd>${esc(b.amount)}</dd></div>
          </dl>
        </div>`;
    }

    function openBooking(b) {
      openDrawer({
        title: "Chi tiết đơn thuê",
        subtitle: `${b.id} · Tạo lúc ${b.created}`,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "customer", label: "Khách hàng" },
          { key: "vehicle", label: "Xe" },
          { key: "payments", label: "Thanh toán" },
          { key: "history", label: "Lịch sử" }
        ],
        renderTab: (tab) => `
          <div style="margin-bottom:1rem;display:flex;gap:.5rem;align-items:center;flex-wrap:wrap">
            ${badge(b.status)}
            <span class="aui-muted" style="font-size:.82rem">${esc(b.id)}</span>
          </div>
          ${renderBookingTab(b, tab)}`,
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-outline-danger" data-cancel="${esc(b.id)}">Hủy đơn</button>
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-toast="Đã mở soạn tin nhắn (mô phỏng).">Gửi tin nhắn</button>`
      });
      drawerFoot.querySelector("[data-cancel]")?.addEventListener("click", () => {
        openModal({
          title: "Hủy đơn",
          body: `Hủy đơn ${b.id}? Đây chỉ là giao diện mô phỏng — đơn thuê sẽ không bị thay đổi.`,
          okLabel: "Hủy đơn",
          onOk: () => toast(`Đơn ${b.id} đã được đánh dấu hủy (mô phỏng).`)
        });
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((b, i) => `
        <tr data-id="${esc(b.id)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>${esc(b.id)}</strong></td>
          <td>${esc(b.customer)}</td>
          <td>${esc(b.vehicle)}</td>
          <td>${esc(t(b.rentalType))}</td>
          <td>${esc(b.dateRange)}</td>
          <td>${esc(b.amount)}</td>
          <td>${badge(b.status)}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-view="${esc(b.id)}" title="Xem"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="10"><div class="aui-empty"><i class="bi bi-inbox"></i>Không có đơn thuê phù hợp bộ lọc.</div></td></tr>`;

      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const b = M.mockBookings.find((x) => x.id === tr.getAttribute("data-id"));
          if (b) openBooking(b);
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const b = M.mockBookings.find((x) => x.id === btn.getAttribute("data-view"));
          if (b) openBooking(b);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    [q, status, rental].forEach((el) => el?.addEventListener("input", () => { page = 1; paint(); }));
    [q, status, rental].forEach((el) => el?.addEventListener("change", () => { page = 1; paint(); }));
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Đã bắt đầu xuất dữ liệu (mô phỏng)."));
    root.querySelector("[data-new-booking]")?.addEventListener("click", () => toast("Đã mở form tạo đơn thuê (mô phỏng)."));
    paint();
  }

  /* Vehicles */
  function initVehicles() {
    const root = document.getElementById("auiVehicles");
    if (!root) return;
    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const type = root.querySelector("[data-filter-type]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return M.mockVehicles.filter((v) => {
        if (status?.value && v.status !== status.value) return false;
        if (type?.value && v.type !== type.value) return false;
        if (!qq) return true;
        return [v.plate, v.model, v.vin].join(" ").toLowerCase().includes(qq);
      });
    }

    function openVehicleForm(v) {
      const isEdit = !!v;
      openDrawer({
        title: isEdit ? "Chỉnh sửa xe" : "Thêm xe mới",
        subtitle: isEdit ? v.plate : "Tạo xe mới cho đội xe (mô phỏng)",
        renderTab: () => `
          <div class="aui-form-grid">
            <div class="aui-field"><label>Biển số xe *</label><input class="aui-input" value="${esc(v?.plate || "")}" /></div>
            <div class="aui-field"><label>Số đăng ký *</label><input class="aui-input" value="${esc(v?.regNo || "")}" /></div>
            <div class="aui-field"><label>Mẫu xe *</label><input class="aui-input" value="${esc(v?.model || "")}" /></div>
            <div class="aui-field"><label>Năm sản xuất *</label><input class="aui-input" value="${esc(v?.year || "2024")}" /></div>
            <div class="aui-field"><label>Loại xe *</label>
              <select class="aui-select">
                ${["Sedan","SUV","MPV","EV SUV"].map((tpe) => `<option ${v?.type === tpe ? "selected" : ""}>${tpe}</option>`).join("")}
              </select>
            </div>
            <div class="aui-field"><label>Trạng thái *</label>
              <select class="aui-select">
                <option value="Available" ${v?.status === "Available" ? "selected" : ""}>Có sẵn</option>
                <option value="In Use" ${v?.status === "In Use" ? "selected" : ""}>Đang sử dụng</option>
                <option value="Maintenance" ${v?.status === "Maintenance" ? "selected" : ""}>Đang bảo trì</option>
                <option value="Out of Service" ${v?.status === "Out of Service" ? "selected" : ""}>Ngừng hoạt động</option>
              </select>
            </div>
            <div class="aui-field"><label>Ngày đăng ký *</label><input class="aui-input" value="${esc(v?.regDate || "")}" /></div>
            <div class="aui-field"><label>Ngày hết hạn đăng ký *</label><input class="aui-input" value="${esc(v?.expiry || "")}" /></div>
            <div class="aui-field"><label>Màu xe</label><input class="aui-input" value="${esc(v?.color || "")}" /></div>
            <div class="aui-field full"><label>Ghi chú</label><textarea class="aui-textarea">${esc(v?.notes || "")}</textarea></div>
          </div>`,
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Hủy</button>
          <button type="button" class="aui-btn aui-btn-primary" data-save>${isEdit ? "Lưu thay đổi" : "Thêm xe"}</button>`
      });
      drawerFoot.querySelector("[data-save]")?.addEventListener("click", () => {
        toast(isEdit ? `Đã cập nhật xe ${v.plate} (mô phỏng).` : "Đã thêm xe (mô phỏng).");
        closeDrawer();
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((v, i) => `
        <tr data-plate="${esc(v.plate)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>${esc(v.plate)}</strong></td>
          <td>${esc(v.model)}</td>
          <td>${esc(v.type)}</td>
          <td>${badge(v.status)}</td>
          <td>${esc(v.expiry)}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-edit="${esc(v.plate)}" title="Chỉnh sửa"><i class="bi bi-pencil"></i></button>
            <button type="button" class="aui-icon-btn" data-view="${esc(v.plate)}" title="Xem"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="8"><div class="aui-empty"><i class="bi bi-inbox"></i>Không tìm thấy xe.</div></td></tr>`;

      tbody.querySelectorAll("tr[data-plate]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const v = M.mockVehicles.find((x) => x.plate === tr.getAttribute("data-plate"));
          if (v) openVehicleForm(v);
        });
      });
      tbody.querySelectorAll("[data-edit],[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const v = M.mockVehicles.find((x) => x.plate === (btn.getAttribute("data-edit") || btn.getAttribute("data-view")));
          if (v) openVehicleForm(v);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    [q, status, type].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    root.querySelector("[data-add-vehicle]")?.addEventListener("click", () => openVehicleForm(null));
    paint();
  }

  /* Customers */
  function initCustomers() {
    const root = document.getElementById("auiCustomers");
    if (!root) return;
    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const membership = root.querySelector("[data-filter-membership]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return M.mockCustomers.filter((c) => {
        if (status?.value && c.status !== status.value) return false;
        if (membership?.value && c.membership !== membership.value) return false;
        if (!qq) return true;
        return [c.name, c.email, c.phone].join(" ").toLowerCase().includes(qq);
      });
    }

    function openCustomer(c) {
      openDrawer({
        title: "Chi tiết khách hàng",
        subtitle: c.id,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "bookings", label: "Đơn thuê" },
          { key: "payments", label: "Thanh toán" },
          { key: "notes", label: "Ghi chú" }
        ],
        renderTab: (tab) => {
          if (tab === "bookings") return `<div class="aui-empty"><i class="bi bi-calendar2"></i>${c.bookings} đơn thuê (danh sách mô phỏng).</div>`;
          if (tab === "payments") return `<div class="aui-empty"><i class="bi bi-credit-card"></i>Tổng chi tiêu ${esc(c.spent)} (mô phỏng).</div>`;
          if (tab === "notes") return `<div class="aui-empty"><i class="bi bi-journal-text"></i>Chưa có ghi chú.</div>`;
          return `
            <div class="aui-person" style="margin-bottom:1rem">
              <span class="aui-avatar" style="width:52px;height:52px;font-size:1rem">${esc(initials(c.name))}</span>
              <div>
                <strong style="font-size:1.05rem">${esc(c.name)}</strong>
                <small>${esc(c.location)}</small>
                <div style="margin-top:.35rem">${badge(c.membership)} ${badge(c.status)}</div>
              </div>
            </div>
            <dl class="aui-kv">
              <div><dt>Mã khách hàng</dt><dd>${esc(c.id)}</dd></div>
              <div><dt>Họ và tên</dt><dd>${esc(c.name)}</dd></div>
              <div><dt>Ngày sinh</dt><dd>${esc(c.dob)}</dd></div>
              <div><dt>Ngày tham gia</dt><dd>${esc(c.joinDate)}</dd></div>
              <div><dt>Email</dt><dd>${esc(c.email)}</dd></div>
              <div><dt>Số điện thoại</dt><dd>${esc(c.phone)}</dd></div>
              <div class="full"><dt>Địa chỉ</dt><dd>${esc(c.address)}</dd></div>
              <div><dt>Hạng thành viên</dt><dd>${esc(t(c.membership))}</dd></div>
              <div><dt>Trạng thái tài khoản</dt><dd>${esc(t(c.status))}</dd></div>
              <div><dt>Tổng đơn thuê</dt><dd>${esc(c.bookings)}</dd></div>
              <div><dt>Tổng chi tiêu</dt><dd>${esc(c.spent)}</dd></div>
            </dl>`;
        },
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-toast="Chỉnh sửa khách hàng (mô phỏng).">Chỉnh sửa</button>
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-toast="Đã mở soạn email (mô phỏng).">Gửi email</button>
          <button type="button" class="aui-btn aui-btn-outline-danger" data-lock>Khóa tài khoản</button>`
      });
      drawerFoot.querySelector("[data-lock]")?.addEventListener("click", () => {
        openModal({
          title: "Khóa tài khoản",
          body: `Khóa tài khoản ${c.name}? Chỉ là giao diện mô phỏng — tài khoản sẽ không bị thay đổi.`,
          okLabel: "Khóa tài khoản",
          onOk: () => toast(`Tài khoản ${c.id} đã khóa (mô phỏng).`)
        });
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((c, i) => `
        <tr data-id="${esc(c.id)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><div class="aui-person"><span class="aui-avatar">${esc(initials(c.name))}</span><div><strong>${esc(c.name)}</strong><small>${esc(c.id)}</small></div></div></td>
          <td>${esc(c.email)}</td>
          <td>${esc(c.phone)}</td>
          <td>${badge(c.membership)}</td>
          <td>${esc(c.joinDate)}</td>
          <td>${badge(c.status)}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-view="${esc(c.id)}"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="9"><div class="aui-empty"><i class="bi bi-inbox"></i>Không tìm thấy khách hàng.</div></td></tr>`;

      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const c = M.mockCustomers.find((x) => x.id === tr.getAttribute("data-id"));
          if (c) openCustomer(c);
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const c = M.mockCustomers.find((x) => x.id === btn.getAttribute("data-view"));
          if (c) openCustomer(c);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    [q, status, membership].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Đã bắt đầu xuất khách hàng (mô phỏng)."));
    root.querySelector("[data-add-customer]")?.addEventListener("click", () => toast("Đã mở form thêm khách hàng (mô phỏng)."));
    paint();
  }

  /* Drivers */
  function initDrivers() {
    const root = document.getElementById("auiDrivers");
    if (!root) return;
    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const city = root.querySelector("[data-filter-city]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return M.mockDrivers.filter((d) => {
        if (status?.value && d.status !== status.value) return false;
        if (city?.value && d.city !== city.value) return false;
        if (!qq) return true;
        return [d.name, d.phone, d.id].join(" ").toLowerCase().includes(qq);
      });
    }

    function openDriver(d) {
      openDrawer({
        title: "Hồ sơ tài xế",
        subtitle: d.id,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "performance", label: "Hiệu suất" },
          { key: "incidents", label: "Sự cố" },
          { key: "documents", label: "Giấy tờ" }
        ],
        renderTab: (tab) => {
          if (tab === "performance") return `<div class="aui-stat-row" style="grid-template-columns:1fr 1fr"><div class="aui-stat-mini"><span>Tổng chuyến</span><strong>${d.trips}</strong></div><div class="aui-stat-mini"><span>Tỷ lệ hoàn thành</span><strong>${esc(d.completion)}</strong></div></div>`;
          if (tab === "incidents") return `<div class="aui-empty"><i class="bi bi-exclamation-triangle"></i>Không có sự cố đang mở.</div>`;
          if (tab === "documents") return `<div class="aui-empty"><i class="bi bi-file-earmark"></i>Giấy phép ${esc(d.license)}</div>`;
          return `
            <div class="aui-person" style="margin-bottom:1rem">
              <span class="aui-avatar" style="width:52px;height:52px">${esc(initials(d.name))}</span>
              <div>
                <strong style="font-size:1.05rem">${esc(d.name)}</strong>
                <small>${esc(d.email)}</small>
                <div style="margin-top:.35rem">${badge(d.status)} <span class="aui-badge amber">★ ${d.rating}</span></div>
              </div>
            </div>
            <dl class="aui-kv">
              <div><dt>Mã tài xế</dt><dd>${esc(d.id)}</dd></div>
              <div><dt>Số điện thoại</dt><dd>${esc(d.phone)}</dd></div>
              <div><dt>Khu vực</dt><dd>${esc(d.location)}, ${esc(d.city)}</dd></div>
              <div><dt>Giấy phép</dt><dd>${esc(d.license)}</dd></div>
              <div><dt>Ngày tham gia</dt><dd>${esc(d.joined)}</dd></div>
              <div><dt>Xe hiện tại</dt><dd>${esc(d.vehicle)}</dd></div>
            </dl>
            <div class="aui-stat-row" style="margin-top:1rem;grid-template-columns:1fr 1fr">
              <div class="aui-stat-mini"><span>Tổng chuyến</span><strong>${d.trips}</strong></div>
              <div class="aui-stat-mini"><span>Tổng quãng đường</span><strong>${esc(d.distance)}</strong></div>
              <div class="aui-stat-mini"><span>Tỷ lệ hoàn thành</span><strong>${esc(d.completion)}</strong></div>
              <div class="aui-stat-mini"><span>Đánh giá khách hàng</span><strong>${d.rating}</strong></div>
            </div>
            <div class="aui-section" style="margin-top:1.25rem">
              <h4>Hoạt động gần đây</h4>
              <ul class="aui-timeline">
                ${d.activity.map((a) => `<li><strong>${esc(a.title)}</strong><small>${esc(a.time)}</small></li>`).join("")}
              </ul>
            </div>`;
        },
        footerHtml: `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>
          <button type="button" class="aui-btn aui-btn-primary" data-aui-toast="Phân công chuyến (mô phỏng).">Phân công chuyến</button>`
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((d, i) => `
        <tr data-id="${esc(d.id)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><div class="aui-person"><span class="aui-avatar">${esc(initials(d.name))}</span><div><strong>${esc(d.name)}</strong><small>${esc(d.id)}</small></div></div></td>
          <td>${esc(d.phone)}</td>
          <td>${badge(d.status)}</td>
          <td>★ ${d.rating}</td>
          <td>${d.trips}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-view="${esc(d.id)}"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="8"><div class="aui-empty"><i class="bi bi-inbox"></i>Không tìm thấy tài xế.</div></td></tr>`;

      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const d = M.mockDrivers.find((x) => x.id === tr.getAttribute("data-id"));
          if (d) openDriver(d);
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const d = M.mockDrivers.find((x) => x.id === btn.getAttribute("data-view"));
          if (d) openDriver(d);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    [q, status, city].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Đã bắt đầu xuất tài xế (mô phỏng)."));
    root.querySelector("[data-add-driver]")?.addEventListener("click", () => toast("Đã mở form thêm tài xế (mô phỏng)."));
    paint();
  }

  /* Payments */
  function initPayments() {
    const root = document.getElementById("auiPayments");
    if (!root) return;
    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const payStatus = root.querySelector("[data-filter-pay]");
    const contract = root.querySelector("[data-filter-contract]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");

    lineChart(document.getElementById("chartPayTrend"), M.months, M.revenueByMonth, "Doanh thu");
    donutChart(document.getElementById("chartPayMethods"), M.paymentMethodBreakdown.map((x) => ({ ...x, label: t(x.label) })));

    const act = document.getElementById("contractActivities");
    if (act) {
      act.innerHTML = M.contractActivities.map((a) => `
        <li>
          <span class="dot"><i class="bi ${esc(a.icon)}"></i></span>
          <div><strong>${esc(a.title)}</strong><small class="aui-muted" style="display:block">${esc(a.detail)} · ${esc(a.time)}</small></div>
        </li>`).join("");
    }

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return M.mockPayments.filter((p) => {
        if (payStatus?.value && p.paymentStatus !== payStatus.value) return false;
        if (contract?.value && p.contractStatus !== contract.value) return false;
        if (!qq) return true;
        return [p.id, p.customer, p.bookingId].join(" ").toLowerCase().includes(qq);
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((p, i) => `
        <tr>
          <td><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>${esc(p.id)}</strong></td>
          <td>${esc(p.customer)}</td>
          <td>${esc(p.bookingId)}</td>
          <td>${esc(p.deposit)}</td>
          <td>${esc(t(p.method))}</td>
          <td>${badge(p.paymentStatus)}</td>
          <td>${badge(p.contractStatus)}</td>
          <td>${esc(p.datetime)}</td>
          <td class="aui-actions">
            <button type="button" class="aui-icon-btn" data-aui-toast="Chi tiết giao dịch (mô phỏng)."><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("");
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
      tbody.querySelectorAll("[data-aui-toast]").forEach((el) => {
        el.addEventListener("click", () => toast(el.getAttribute("data-aui-toast")));
      });
    }

    [q, payStatus, contract].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Đã xuất báo cáo thanh toán (mô phỏng)."));
    paint();
  }

  /* Maintenance + Incidents */
  function initMaintenance() {
    const root = document.getElementById("auiMaintenance");
    if (!root) return;
    const params = new URLSearchParams(location.search);
    let mainTab = params.get("tab") === "incidents" ? "incidents" : "maintenance";

    const seg = root.querySelector("[data-main-seg]");
    const panelMaint = root.querySelector("[data-panel-maintenance]");
    const panelInc = root.querySelector("[data-panel-incidents]");

    function switchMain(tab) {
      mainTab = tab;
      seg?.querySelectorAll("button").forEach((b) => b.classList.toggle("is-on", b.getAttribute("data-tab") === tab));
      if (panelMaint) panelMaint.hidden = tab !== "maintenance";
      if (panelInc) panelInc.hidden = tab !== "incidents";
    }
    seg?.querySelectorAll("button").forEach((b) => {
      b.addEventListener("click", () => switchMain(b.getAttribute("data-tab")));
    });
    switchMain(mainTab);

    /* maintenance sub tabs */
    const subSeg = root.querySelector("[data-sub-seg]");
    subSeg?.querySelectorAll("button").forEach((b) => {
      b.addEventListener("click", () => {
        subSeg.querySelectorAll("button").forEach((x) => x.classList.remove("is-on"));
        b.classList.add("is-on");
        toast(`Xem ${b.textContent.trim()} (mô phỏng).`);
      });
    });

    const maintStatus = [
      { label: "Tốt", value: M.mockMaintenance.filter((x) => x.status === "Healthy").length, color: "#16A34A" },
      { label: "Sắp đến hạn", value: M.mockMaintenance.filter((x) => x.status === "Due Soon").length, color: "#D97706" },
      { label: "Quá hạn", value: M.mockMaintenance.filter((x) => x.status === "Overdue").length, color: "#DC2626" },
      { label: "Đang bảo dưỡng", value: M.mockMaintenance.filter((x) => x.status === "In Service").length, color: "#2563EB" }
    ];
    donutChart(document.getElementById("chartMaintStatus"), maintStatus);

    const upcoming = document.getElementById("upcomingServices");
    if (upcoming) {
      upcoming.innerHTML = M.upcomingServices.map((s) => `
        <div class="aui-vehicle-row">
          <div class="aui-vehicle-thumb"><i class="bi bi-wrench"></i></div>
          <div class="aui-vehicle-meta">
            <strong>${esc(s.model)} · ${esc(s.plate)}</strong>
            <small>${esc(s.date)} · ${esc(s.type)}</small>
          </div>
        </div>`).join("");
    }

    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-maint-q]");
    const status = root.querySelector("[data-maint-status]");
    const type = root.querySelector("[data-maint-type]");
    const tbody = root.querySelector("[data-maint-body]");
    const pager = root.querySelector("[data-maint-pager]");

    function filteredMaint() {
      const qq = (q?.value || "").toLowerCase().trim();
      return M.mockMaintenance.filter((v) => {
        if (status?.value && v.status !== status.value) return false;
        if (type?.value) {
          const veh = M.mockVehicles.find((x) => x.plate === v.plate);
          if (veh && veh.type !== type.value) return false;
        }
        if (!qq) return true;
        return [v.plate, v.model].join(" ").toLowerCase().includes(qq);
      });
    }

    function paintMaint() {
      const rows = filteredMaint();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((v, i) => `
        <tr>
          <td><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>${esc(v.plate)}</strong></td>
          <td>${esc(v.model)}</td>
          <td>${esc(v.km)}</td>
          <td>${esc(v.lastService)}</td>
          <td>${esc(v.nextService)}</td>
          <td>${badge(v.status)}</td>
          <td class="aui-actions"><button type="button" class="aui-icon-btn" data-aui-toast="Phiếu bảo dưỡng (mô phỏng)."><i class="bi bi-eye"></i></button></td>
        </tr>`).join("");
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paintMaint(); });
      bindSelectAll(panelMaint || root);
      tbody.querySelectorAll("[data-aui-toast]").forEach((el) => el.addEventListener("click", () => toast(el.getAttribute("data-aui-toast"))));
    }
    [q, status, type].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paintMaint(); });
      el?.addEventListener("change", () => { page = 1; paintMaint(); });
    });
    root.querySelector("[data-create-service]")?.addEventListener("click", () => toast("Tạo phiếu bảo dưỡng (mô phỏng)."));
    paintMaint();

    /* incidents */
    let ipage = 1;
    const iq = root.querySelector("[data-inc-q]");
    const isev = root.querySelector("[data-inc-sev]");
    const ist = root.querySelector("[data-inc-status]");
    const ibody = root.querySelector("[data-inc-body]");
    const ipager = root.querySelector("[data-inc-pager]");

    function filteredInc() {
      const qq = (iq?.value || "").toLowerCase().trim();
      return M.mockIncidents.filter((x) => {
        if (isev?.value && x.severity !== isev.value) return false;
        if (ist?.value && x.status !== ist.value) return false;
        if (!qq) return true;
        return [x.id, x.vehicle, x.type].join(" ").toLowerCase().includes(qq);
      });
    }

    function paintInc() {
      const rows = filteredInc();
      const pg = paginate(rows, ipage, pageSize);
      ipage = pg.page;
      ibody.innerHTML = pg.slice.map((x, i) => `
        <tr>
          <td><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td>${esc(x.datetime)}</td>
          <td>${esc(x.vehicle)}</td>
          <td>${esc(t(x.type))}</td>
          <td>${badge(x.severity)}</td>
          <td>${badge(x.status)}</td>
          <td class="aui-actions"><button type="button" class="aui-icon-btn" data-aui-toast="Chi tiết sự cố (mô phỏng)."><i class="bi bi-eye"></i></button></td>
        </tr>`).join("");
      renderPager(ipager, { ...pg, pageSize }, (n) => { ipage = n; paintInc(); });
      bindSelectAll(panelInc || root);
      ibody.querySelectorAll("[data-aui-toast]").forEach((el) => el.addEventListener("click", () => toast(el.getAttribute("data-aui-toast"))));
    }
    [iq, isev, ist].forEach((el) => {
      el?.addEventListener("input", () => { ipage = 1; paintInc(); });
      el?.addEventListener("change", () => { ipage = 1; paintInc(); });
    });
    root.querySelector("[data-report-incident]")?.addEventListener("click", () => toast("Đã mở form báo cáo sự cố (mô phỏng)."));
    paintInc();
  }

  document.addEventListener("DOMContentLoaded", () => {
    initDashboard();
    initBookings();
    initVehicles();
    initCustomers();
    initDrivers();
    initPayments();
    initMaintenance();
  });

  window.AdminUi = { toast, openDrawer, closeDrawer, openModal, badge, esc, initials, t };
})();

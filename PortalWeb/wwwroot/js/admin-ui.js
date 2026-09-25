(function () {
  const M = window.DriveRentMock || {};

  const statusTone = {
    Pending: "amber", Confirmed: "green", Assigned: "blue",
    InProgress: "blue", "In Progress": "blue", Completed: "green", Cancelled: "red",
    Available: "green", Rented: "blue", "In Use": "blue", Maintenance: "amber",
    Inactive: "red", "Out of Service": "red",
    Active: "green", Locked: "red",
    Busy: "amber", Offline: "slate",
    Paid: "green", Failed: "red", Refunded: "slate",
    Signed: "green", Issued: "blue", Voided: "red",
    Healthy: "green", "Due Soon": "amber", Overdue: "red", "In Service": "blue",
    SanSang: "green", CanBaoTri: "amber", DangBaoTri: "blue", NgungHoatDong: "slate",
    Open: "amber", Investigating: "amber", Resolved: "green", Closed: "slate",
    Critical: "red", High: "amber", Medium: "blue", Low: "slate",
    Scheduled: "blue", Repair: "amber", Inspection: "slate",
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
    InProgress: "Đang thực hiện", "In Progress": "Đang thực hiện", Completed: "Hoàn thành", Cancelled: "Đã hủy",
    Available: "Có sẵn", Rented: "Đang sử dụng", "In Use": "Đang sử dụng", Maintenance: "Đang bảo trì",
    Inactive: "Ngừng hoạt động", "Out of Service": "Ngừng hoạt động", Active: "Đang hoạt động", Locked: "Đã khóa",
    Busy: "Đang bận", Offline: "Ngoại tuyến", Paid: "Đã thanh toán", Failed: "Thất bại", Refunded: "Đã hoàn tiền",
    Signed: "Đã ký", Issued: "Đã phát hành", Voided: "Đã vô hiệu",
    Healthy: "Tốt", "Due Soon": "Sắp đến hạn", Overdue: "Quá hạn", "In Service": "Đang bảo dưỡng",
    SanSang: "Sẵn sàng", CanBaoTri: "Cần bảo trì", DangBaoTri: "Đang bảo trì", NgungHoatDong: "Ngừng hoạt động",
    Open: "Đang mở", Investigating: "Đang điều tra", Resolved: "Đã xử lý", Closed: "Đã đóng",
    Critical: "Nghiêm trọng", High: "Cao", Medium: "Trung bình", Low: "Thấp",
    Scheduled: "Định kỳ", Repair: "Sửa chữa", Inspection: "Kiểm tra",
    Premium: "Cao cấp", Standard: "Tiêu chuẩn", Basic: "Cơ bản",
    SelfDrive: "Tự lái", WithDriver: "Có tài xế",
    "Self Drive": "Tự lái", "With Driver": "Có tài xế",
    "Credit Card": "Thẻ tín dụng", "Bank Transfer": "Chuyển khoản ngân hàng",
    Cash: "Tiền mặt", Momo: "Momo", VNPAY: "VNPAY", Deposit: "Tiền cọc", Balance: "Thanh toán còn lại",
    Accident: "Tai nạn", VehicleIssue: "Sự cố xe", CustomerIssue: "Sự cố khách", Other: "Khác",
    "Mechanical Issue": "Sự cố cơ khí", "Flat Tire": "Lốp xẹp",
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

  function formatViDate(isoOrDate) {
    if (!isoOrDate) return "";
    const s = String(isoOrDate);
    if (/^\d{4}-\d{2}-\d{2}/.test(s)) {
      const [y, m, d] = s.slice(0, 10).split("-");
      return `${d}/${m}/${y}`;
    }
    const dt = new Date(s);
    return Number.isNaN(dt.getTime()) ? "" : dt.toLocaleDateString("vi-VN");
  }

  function parseYmd(value) {
    if (!value) return null;
    const m = String(value).match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!m) return null;
    const dt = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), 0, 0, 0, 0);
    return Number.isNaN(dt.getTime()) ? null : dt;
  }

  function endOfDay(dt) {
    if (!dt) return null;
    return new Date(dt.getFullYear(), dt.getMonth(), dt.getDate(), 23, 59, 59, 999);
  }

  /** Shared date-range picker: open panel, Apply/Cancel, navigate or callback filter. */
  function bindDateRange(root, onApply) {
    const wrap = root?.matches?.("[data-daterange]") ? root : root?.querySelector?.("[data-daterange]");
    if (!wrap || wrap.dataset.daterangeBound === "1") return wrap;
    wrap.dataset.daterangeBound = "1";

    const toggle = wrap.querySelector("[data-daterange-toggle]");
    const panel = wrap.querySelector("[data-daterange-panel]");
    const fromEl = wrap.querySelector("[data-daterange-from]");
    const toEl = wrap.querySelector("[data-daterange-to]");
    const label = wrap.querySelector("[data-daterange-label]");
    const applyBtn = wrap.querySelector("[data-daterange-apply]");
    const cancelBtn = wrap.querySelector("[data-daterange-cancel]");
    const mode = wrap.getAttribute("data-daterange-mode") || "filter";
    const base = wrap.getAttribute("data-daterange-base") || location.pathname;

    let draftFrom = fromEl?.value || "";
    let draftTo = toEl?.value || "";

    function syncLabel(from, to) {
      if (!label) return;
      if (!from && !to) {
        label.textContent = "Tất cả thời gian";
        return;
      }
      const a = formatViDate(from) || "…";
      const b = formatViDate(to) || "…";
      label.textContent = `${a} - ${b}`;
    }

    function closePanel() {
      if (!panel) return;
      panel.hidden = true;
      if (fromEl) fromEl.value = draftFrom;
      if (toEl) toEl.value = draftTo;
    }

    function openPanel() {
      if (!panel) return;
      draftFrom = fromEl?.value || "";
      draftTo = toEl?.value || "";
      panel.hidden = false;
    }

    toggle?.addEventListener("click", (e) => {
      e.preventDefault();
      e.stopPropagation();
      if (panel?.hidden) openPanel();
      else closePanel();
    });

    panel?.addEventListener("click", (e) => e.stopPropagation());

    cancelBtn?.addEventListener("click", (e) => {
      e.preventDefault();
      e.stopPropagation();
      closePanel();
    });

    applyBtn?.addEventListener("click", (e) => {
      e.preventDefault();
      e.stopPropagation();
      let from = fromEl?.value || "";
      let to = toEl?.value || "";
      if (from && to && from > to) {
        const tmp = from;
        from = to;
        to = tmp;
        if (fromEl) fromEl.value = from;
        if (toEl) toEl.value = to;
      }
      draftFrom = from;
      draftTo = to;
      syncLabel(from, to);
      panel.hidden = true;

      if (mode === "navigate") {
        const url = new URL(base, location.origin);
        if (from) url.searchParams.set("from", from);
        else url.searchParams.delete("from");
        if (to) url.searchParams.set("to", to);
        else url.searchParams.delete("to");
        location.href = url.pathname + url.search;
        return;
      }

      onApply?.({ from, to, fromDate: parseYmd(from), toDate: endOfDay(parseYmd(to)) });
    });

    document.addEventListener("click", (e) => {
      if (!panel || panel.hidden) return;
      if (wrap.contains(e.target)) return;
      closePanel();
    });

    syncLabel(fromEl?.value || "", toEl?.value || "");
    return wrap;
  }

  function bindAllDateRanges() {
    document.querySelectorAll("[data-daterange]").forEach((el) => {
      if (el.getAttribute("data-daterange-mode") === "navigate") bindDateRange(el);
    });
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

  function openDrawer({ title, subtitle, tabs, renderTab, footerHtml, bindFooter }) {
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
      bindFooter?.(drawerFoot);
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

  function openModal({ title, body, okLabel, onOk, html }) {
    modalTitle.textContent = title || "Confirm";
    if (html) modalBody.innerHTML = body || "";
    else modalBody.textContent = body || "";
    modalOk.textContent = okLabel || "Confirm";
    modal.hidden = false;
    modalOk.onclick = () => {
      const result = onOk?.();
      if (result === false) return;
      if (result && typeof result.then === "function") {
        result.then((ok) => { if (ok !== false) modal.hidden = true; }).catch(() => {});
        return;
      }
      modal.hidden = true;
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

    let d = null;
    const payloadEl = document.getElementById("auiDashChartPayload");
    if (payloadEl) {
      try { d = JSON.parse(payloadEl.textContent || "null"); } catch (_) { d = null; }
    }
    if (!d) return;

    const revenueCanvas = document.getElementById("chartRevenue");
    if (revenueCanvas && d.revenue?.months?.length) {
      lineChart(revenueCanvas, d.revenue.months, d.revenue.values, "Doanh thu");
    }

    const statusCanvas = document.getElementById("chartBookingStatus");
    if (statusCanvas && Array.isArray(d.bookingStatus)) {
      const totalStatus = d.bookingStatus.reduce((s, x) => s + (x.value || 0), 0);
      donutChart(statusCanvas, d.bookingStatus.map((x) => ({ ...x, label: t(x.label) })));
      const center = document.getElementById("donutCenter");
      if (center) {
        center.innerHTML = `<strong>${Number(totalStatus).toLocaleString("vi-VN")}</strong><span>Tổng đơn thuê</span>`;
      }
    }

    const top = document.getElementById("topVehicles");
    if (top && Array.isArray(d.topVehicles)) {
      top.innerHTML = d.topVehicles.map((v) => `
        <div class="aui-vehicle-row">
          <div class="aui-vehicle-thumb"><i class="bi bi-car-front"></i></div>
          <div class="aui-vehicle-meta">
            <strong>${esc(v.name)}</strong>
            <small>${Number(v.bookings || 0).toLocaleString("vi-VN")} đơn thuê</small>
            <div class="aui-progress" style="margin-top:.35rem"><span style="width:${v.pct || 0}%"></span></div>
          </div>
        </div>`).join("");
    }
  }

  /* Page: Bookings */
  function initBookings() {
    const root = document.getElementById("auiBookings");
    if (!root) return;
    if (root.querySelector("[data-bookings-error]")) return;

    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const rental = root.querySelector("[data-filter-rental]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");
    let bookings = [];
    let rangeFrom = null;
    let rangeTo = null;

    const money = (n) => {
      if (n == null || n === "") return "—";
      return Number(n).toLocaleString("vi-VN") + " VNĐ";
    };
    const day = (iso) => {
      if (!iso) return "—";
      const d = new Date(iso);
      if (Number.isNaN(d.getTime())) return "—";
      return d.toLocaleDateString("vi-VN");
    };
    const when = (iso) => {
      if (!iso) return "—";
      const d = new Date(iso);
      if (Number.isNaN(d.getTime())) return "—";
      return d.toLocaleString("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric" });
    };
    const daysBetween = (a, b) => {
      const s = new Date(a); const e = new Date(b);
      if (Number.isNaN(s.getTime()) || Number.isNaN(e.getTime())) return "—";
      const ms = Math.max(0, e.getTime() - s.getTime());
      const days = Math.max(1, Math.ceil(ms / 86400000));
      return days + " ngày";
    };
    const vehicleLabel = (b) => {
      const av = b.assignedVehicle;
      if (av && (av.brand || av.model)) return `${av.brand || ""} ${av.model || ""}`.trim();
      return b.vehicleTypeName || "—";
    };
    const plateOf = (b) => b.assignedVehicle?.licensePlate || b.assignment?.licensePlate || "—";
    const canCancel = (st) => st === "Pending" || st === "Confirmed";
    const antiforgery = () =>
      document.querySelector("#auiBookingsAntiForgery input[name='__RequestVerificationToken']")?.value || "";

    function parsePayload() {
      const el = document.getElementById("auiBookingsPayload");
      if (!el) return [];
      try {
        const data = JSON.parse(el.textContent || "[]");
        return Array.isArray(data) ? data : [];
      } catch (_) {
        return [];
      }
    }

    function setKpis(list) {
      const set = (key, val) => {
        const el = root.querySelector(`[data-kpi="${key}"]`);
        if (el) el.textContent = Number(val).toLocaleString("vi-VN");
      };
      set("total", list.length);
      set("pending", list.filter((b) => b.status === "Pending").length);
      set("inProgress", list.filter((b) => b.status === "InProgress").length);
      set("completed", list.filter((b) => b.status === "Completed").length);
      set("cancelled", list.filter((b) => b.status === "Cancelled").length);
    }

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return bookings.filter((b) => {
        if (status?.value && b.status !== status.value) return false;
        if (rental?.value && (b.rentalMode || "WithDriver") !== rental.value) return false;
        if (rangeFrom || rangeTo) {
          const start = b.startDate ? new Date(b.startDate) : null;
          if (!start || Number.isNaN(start.getTime())) return false;
          if (rangeFrom && start < rangeFrom) return false;
          if (rangeTo && start > rangeTo) return false;
        }
        if (!qq) return true;
        const hay = [
          b.bookingId, b.customerName, vehicleLabel(b), plateOf(b), b.vehicleTypeName
        ].join(" ").toLowerCase();
        return hay.includes(qq);
      });
    }

    function dash(v) {
      return v == null || String(v).trim() === "" ? "—" : String(v);
    }

    function renderBookingTab(b, customer, payments, tab) {
      const mode = b.rentalMode || "WithDriver";
      const amount = b.finalAmount != null ? b.finalAmount : b.totalAmount;
      if (tab === "customer") {
        return `
          <div class="aui-section">
            <div class="aui-person" style="margin-bottom:1rem">
              <span class="aui-avatar">${esc(initials(b.customerName))}</span>
              <div><strong>${esc(b.customerName)}</strong><small>${esc(dash(customer?.email))}</small></div>
            </div>
            <dl class="aui-kv">
              <div><dt>Số điện thoại</dt><dd>${esc(dash(customer?.phone))}</dd></div>
              <div><dt>Mã khách hàng</dt><dd>#${esc(b.customerId)}</dd></div>
              <div><dt>Email</dt><dd>${esc(dash(customer?.email))}</dd></div>
            </dl>
          </div>`;
      }
      if (tab === "vehicle") {
        return `
          <div class="aui-section">
            <div class="aui-vehicle-row" style="border:0;padding:0;margin-bottom:1rem">
              <div class="aui-vehicle-thumb" style="width:64px;height:64px"><i class="bi bi-car-front-fill"></i></div>
              <div class="aui-vehicle-meta"><strong>${esc(vehicleLabel(b))}</strong><small>${esc(plateOf(b))}</small></div>
            </div>
            <dl class="aui-kv">
              <div><dt>Loại xe</dt><dd>${esc(dash(b.vehicleTypeName))}</dd></div>
              <div><dt>Biển số xe</dt><dd>${esc(plateOf(b))}</dd></div>
              <div><dt>Trạng thái xe</dt><dd>${esc(dash(b.assignedVehicle?.status ? t(b.assignedVehicle.status) : null))}</dd></div>
              <div><dt>Tài xế</dt><dd>${esc(dash(b.assignment?.driverName))}</dd></div>
            </dl>
          </div>`;
      }
      if (tab === "payments") {
        const feeRows = (b.fees || []).map((f) =>
          `<div><dt>${esc(f.feeType || "Phí")}${f.description ? " — " + esc(f.description) : ""}</dt><dd>${esc(money(f.amount))}</dd></div>`
        ).join("");
        const payRows = (payments || []).map((p) =>
          `<tr>
            <td>${esc(t(p.paymentType || "Deposit"))}</td>
            <td>${esc(money(p.amount))}</td>
            <td>${esc(t(p.method || ""))}</td>
            <td>${badge(p.status)}</td>
            <td>${esc(p.paidAt ? when(p.paidAt) : when(p.createdAt))}</td>
          </tr>`
        ).join("");
        return `
          <div class="aui-section">
            <h4>Chi tiết giá</h4>
            <dl class="aui-kv">
              <div><dt>Đơn giá theo ngày</dt><dd>${esc(b.quotedPricePerDay != null ? money(b.quotedPricePerDay) : "—")}</dd></div>
              <div><dt>Số ngày báo giá</dt><dd>${esc(b.quotedDays != null ? b.quotedDays : "—")}</dd></div>
              <div><dt>Phí tài xế / ngày</dt><dd>${esc(b.quotedDriverFeePerDay != null ? money(b.quotedDriverFeePerDay) : "—")}</dd></div>
              <div><dt>Tiền cọc (báo giá)</dt><dd>${esc(b.quotedDepositAmount != null ? money(b.quotedDepositAmount) : "—")}</dd></div>
              ${feeRows}
              <div><dt>Tổng tiền</dt><dd>${esc(money(b.totalAmount))}</dd></div>
              <div><dt>Số tiền cuối</dt><dd>${esc(b.finalAmount != null ? money(b.finalAmount) : "—")}</dd></div>
            </dl>
          </div>
          <div class="aui-section">
            <h4>Thanh toán</h4>
            ${(payments || []).length
              ? `<div class="aui-table-wrap"><table class="aui-table" style="min-width:420px">
                  <thead><tr><th>Loại</th><th>Số tiền</th><th>Phương thức</th><th>Trạng thái</th><th>Thời gian</th></tr></thead>
                  <tbody>${payRows}</tbody>
                </table></div>`
              : `<div class="aui-empty"><p>Chưa có thanh toán.</p></div>`}
          </div>`;
      }
      if (tab === "history") {
        return `
          <ul class="aui-timeline">
            <li><strong>Đơn thuê được tạo</strong><small>${esc(when(b.createdAt))}</small></li>
            <li><strong>Trạng thái hiện tại: ${esc(t(b.status))}</strong><small>Cập nhật từ hệ thống</small></li>
            ${b.cancellationReason
              ? `<li><strong>Lý do hủy</strong><small>${esc(b.cancellationReason)}</small></li>`
              : ""}
            ${b.notes
              ? `<li><strong>Ghi chú</strong><small>${esc(b.notes)}</small></li>`
              : ""}
          </ul>`;
      }
      return `
        <div class="aui-section">
          <h4>Thông tin đơn thuê</h4>
          <dl class="aui-kv">
            <div><dt>Hình thức thuê</dt><dd>${esc(t(mode))}</dd></div>
            <div><dt>Thời lượng</dt><dd>${esc(daysBetween(b.startDate, b.endDate))}</dd></div>
            <div><dt>Điểm nhận xe</dt><dd>${esc(dash(b.pickupAddress))}</dd></div>
            <div><dt>Điểm trả xe</dt><dd>${esc(dash(b.dropoffAddress))}</dd></div>
            <div class="full"><dt>Thời gian thuê</dt><dd>${esc(day(b.startDate) + " – " + day(b.endDate))}</dd></div>
            <div><dt>Tổng tiền</dt><dd>${esc(money(amount))}</dd></div>
          </dl>
        </div>
        <div class="aui-section">
          <h4>Thông tin khách hàng</h4>
          <div class="aui-person" style="margin-bottom:.75rem">
            <span class="aui-avatar">${esc(initials(b.customerName))}</span>
            <div>
              <strong>${esc(b.customerName)}</strong>
              <small>${esc(dash(customer?.phone))} · ${esc(dash(customer?.email))}</small>
            </div>
          </div>
        </div>
        <div class="aui-section">
          <h4>Thông tin xe</h4>
          <dl class="aui-kv">
            <div><dt>Mẫu xe</dt><dd>${esc(vehicleLabel(b))}</dd></div>
            <div><dt>Biển số xe</dt><dd>${esc(plateOf(b))}</dd></div>
            <div><dt>Loại xe</dt><dd>${esc(dash(b.vehicleTypeName))}</dd></div>
          </dl>
        </div>`;
    }

    async function fetchDetail(id) {
      const res = await fetch(`/Admin/Bookings?handler=Detail&id=${encodeURIComponent(id)}`, {
        headers: { Accept: "application/json" },
        credentials: "same-origin"
      });
      if (!res.ok) {
        let msg = "Không thể tải chi tiết đơn thuê.";
        try {
          const j = await res.json();
          if (j?.message) msg = j.message;
        } catch (_) { /* ignore */ }
        throw new Error(msg);
      }
      return res.json();
    }

    async function openBooking(seed) {
      openDrawer({
        title: "Chi tiết đơn thuê",
        subtitle: `#${seed.bookingId} · Đang tải...`,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "customer", label: "Khách hàng" },
          { key: "vehicle", label: "Xe" },
          { key: "payments", label: "Thanh toán" },
          { key: "history", label: "Lịch sử" }
        ],
        renderTab: () => `<div class="aui-empty"><i class="bi bi-arrow-repeat"></i><p>Đang tải dữ liệu...</p></div>`,
        footerHtml: ""
      });

      let detail;
      try {
        detail = await fetchDetail(seed.bookingId);
      } catch (err) {
        openDrawer({
          title: "Chi tiết đơn thuê",
          subtitle: `#${seed.bookingId}`,
          renderTab: () => `<div class="aui-empty" role="alert"><i class="bi bi-exclamation-triangle"></i><p>${esc(err.message || "Không thể tải chi tiết đơn thuê.")}</p></div>`,
          footerHtml: `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>`
        });
        return;
      }

      const b = detail.booking || seed;
      const customer = detail.customer || null;
      const payments = detail.payments || [];
      const cancelOk = canCancel(b.status);

      function paintDrawer() {
        openDrawer({
          title: "Chi tiết đơn thuê",
          subtitle: `#${b.bookingId} · Tạo lúc ${when(b.createdAt)}`,
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
              <span class="aui-muted" style="font-size:.82rem">#${esc(b.bookingId)}</span>
            </div>
            ${renderBookingTab(b, customer, payments, tab)}`,
          footerHtml: `
            ${cancelOk
              ? `<button type="button" class="aui-btn aui-btn-outline-danger" data-cancel="${esc(b.bookingId)}">Hủy đơn</button>`
              : `<button type="button" class="aui-btn aui-btn-outline-danger" disabled title="Chỉ hủy được khi Chờ xác nhận hoặc Đã xác nhận">Hủy đơn</button>`}
            <button type="button" class="aui-btn aui-btn-ghost" data-aui-toast="Gửi tin nhắn chưa được hỗ trợ.">Gửi tin nhắn</button>`
        });

        drawerFoot.querySelector("[data-cancel]")?.addEventListener("click", () => {
          openModal({
            title: "Hủy đơn",
            body: `Hủy đơn #${b.bookingId}? Chỉ áp dụng khi đơn đang Chờ xác nhận hoặc Đã xác nhận. Không hoàn tiền tự động.`,
            okLabel: "Hủy đơn",
            onOk: () => {
              (async () => {
                try {
                  const body = new URLSearchParams();
                  body.set("__RequestVerificationToken", antiforgery());
                  const res = await fetch(`/Admin/Bookings?handler=Cancel&id=${encodeURIComponent(b.bookingId)}`, {
                    method: "POST",
                    headers: {
                      RequestVerificationToken: antiforgery(),
                      Accept: "application/json"
                    },
                    body,
                    credentials: "same-origin"
                  });
                  const json = await res.json().catch(() => ({}));
                  if (!res.ok) {
                    toast(json.message || "Không thể hủy đơn thuê.");
                    return;
                  }
                  toast(`Đã hủy đơn #${b.bookingId}.`);
                  closeDrawer();
                  await reloadList();
                } catch (_) {
                  toast("Không thể hủy đơn thuê.");
                }
              })();
            }
          });
        });
      }

      paintDrawer();
    }

    async function reloadList() {
      try {
        const res = await fetch("/Admin/Bookings?handler=List", {
          headers: { Accept: "application/json" },
          credentials: "same-origin"
        });
        if (!res.ok) throw new Error("fail");
        const data = await res.json();
        bookings = Array.isArray(data) ? data : [];
        setKpis(bookings);
        page = 1;
        paint();
      } catch (_) {
        toast("Không thể tải lại danh sách đơn thuê.");
      }
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      if (!bookings.length) {
        tbody.innerHTML = `<tr><td colspan="10"><div class="aui-empty"><i class="bi bi-inbox"></i>Chưa có đơn thuê.</div></td></tr>`;
        pager.innerHTML = "";
        return;
      }
      tbody.innerHTML = pg.slice.map((b, i) => `
        <tr data-id="${esc(b.bookingId)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>#${esc(b.bookingId)}</strong></td>
          <td>${esc(b.customerName)}</td>
          <td>${esc(vehicleLabel(b))}</td>
          <td>${esc(t(b.rentalMode || "WithDriver"))}</td>
          <td>${esc(day(b.startDate) + " – " + day(b.endDate))}</td>
          <td>${esc(money(b.finalAmount != null ? b.finalAmount : b.totalAmount))}</td>
          <td>${badge(b.status)}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-view="${esc(b.bookingId)}" title="Xem"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="10"><div class="aui-empty"><i class="bi bi-inbox"></i>Không có đơn thuê phù hợp bộ lọc.</div></td></tr>`;

      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const id = Number(tr.getAttribute("data-id"));
          const b = bookings.find((x) => x.bookingId === id);
          if (b) openBooking(b);
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const id = Number(btn.getAttribute("data-view"));
          const b = bookings.find((x) => x.bookingId === id);
          if (b) openBooking(b);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    bookings = parsePayload();
    setKpis(bookings);
    [q, status, rental].forEach((el) => el?.addEventListener("input", () => { page = 1; paint(); }));
    [q, status, rental].forEach((el) => el?.addEventListener("change", () => { page = 1; paint(); }));
    bindDateRange(root, ({ fromDate, toDate }) => {
      rangeFrom = fromDate;
      rangeTo = toDate;
      page = 1;
      paint();
    });
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Xuất dữ liệu chưa được hỗ trợ."));
    root.querySelector("[data-new-booking]")?.addEventListener("click", () => toast("Admin tạo đơn thuê chưa được hỗ trợ trên hệ thống hiện tại."));
    paint();
  }

  /* Vehicles */
  function initVehicles() {
    const root = document.getElementById("auiVehicles");
    if (!root) return;
    if (root.querySelector("[data-vehicles-error]")) return;

    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const type = root.querySelector("[data-filter-type]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");
    let vehicles = [];
    let types = [];

    const antiforgery = () =>
      document.querySelector("#auiVehiclesAntiForgery input[name='__RequestVerificationToken']")?.value || "";

    const dayOnly = (iso) => {
      if (!iso) return "—";
      const s = String(iso);
      if (/^\d{4}-\d{2}-\d{2}/.test(s)) {
        const [y, m, d] = s.slice(0, 10).split("-");
        return `${d}/${m}/${y}`;
      }
      const dt = new Date(s);
      return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("vi-VN");
    };

    const modelLabel = (v) => `${v.brand || ""} ${v.model || ""}`.trim() || "—";

    function parseJson(id, fallback) {
      const el = document.getElementById(id);
      if (!el) return fallback;
      try {
        const data = JSON.parse(el.textContent || "null");
        return data == null ? fallback : data;
      } catch (_) {
        return fallback;
      }
    }

    function setKpis(list) {
      const set = (key, val) => {
        const el = root.querySelector(`[data-kpi="${key}"]`);
        if (el) el.textContent = Number(val).toLocaleString("vi-VN");
      };
      set("total", list.length);
      set("available", list.filter((v) => v.status === "Available").length);
      set("rented", list.filter((v) => v.status === "Rented").length);
      set("maintenance", list.filter((v) => v.status === "Maintenance").length);
      set("inactive", list.filter((v) => v.status === "Inactive").length);
    }

    function fillTypeFilter() {
      if (!type) return;
      const cur = type.value;
      type.innerHTML = `<option value="">Tất cả loại</option>` + types.map((t) =>
        `<option value="${esc(t.typeId)}">${esc(t.typeName)}</option>`
      ).join("");
      type.value = cur;
    }

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return vehicles.filter((v) => {
        if (status?.value && v.status !== status.value) return false;
        if (type?.value && String(v.typeId) !== String(type.value)) return false;
        if (!qq) return true;
        const hay = [
          v.licensePlate, v.brand, v.model, v.typeName, v.registrationNumber, v.vehicleId
        ].join(" ").toLowerCase();
        return hay.includes(qq);
      });
    }

    function statusOptions(current) {
      const isRented = current === "Rented";
      return `
        <option value="Available" ${current === "Available" ? "selected" : ""} ${isRented ? "disabled" : ""}>Có sẵn</option>
        <option value="Rented" ${isRented ? "selected" : ""} ${isRented ? "" : "disabled"}>Đang sử dụng</option>
        <option value="Maintenance" ${current === "Maintenance" ? "selected" : ""}>Đang bảo trì</option>
        <option value="Inactive" ${current === "Inactive" ? "selected" : ""}>Ngừng hoạt động</option>`;
    }

    function typeOptions(selectedId) {
      return types.map((t) =>
        `<option value="${esc(t.typeId)}" ${String(t.typeId) === String(selectedId) ? "selected" : ""}>${esc(t.typeName)}</option>`
      ).join("");
    }

    function dateInputValue(iso) {
      if (!iso) return "";
      const s = String(iso);
      return /^\d{4}-\d{2}-\d{2}/.test(s) ? s.slice(0, 10) : "";
    }

    function readForm() {
      const g = (name) => document.querySelector(`#auiDrawer [data-vf="${name}"]`);
      const val = (name) => (g(name)?.value || "").trim();
      const num = (name) => Number(val(name) || 0);
      const dateOrNull = (name) => {
        const v = val(name);
        return v ? v : null;
      };
      return {
        typeId: num("typeId"),
        licensePlate: val("licensePlate"),
        brand: val("brand"),
        model: val("model"),
        year: num("year"),
        color: val("color") || null,
        currentKm: num("currentKm"),
        status: val("status") || "Available",
        registrationNumber: val("registrationNumber") || null,
        registrationExpiryDate: dateOrNull("registrationExpiryDate"),
        inspectionExpiryDate: dateOrNull("inspectionExpiryDate"),
        insuranceExpiryDate: dateOrNull("insuranceExpiryDate")
      };
    }

    async function postJson(url, body) {
      const res = await fetch(url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          RequestVerificationToken: antiforgery()
        },
        body: JSON.stringify(body),
        credentials: "same-origin"
      });
      const json = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(json.message || "Thao tác thất bại.");
      return json;
    }

    async function reloadList() {
      const res = await fetch("/Admin/Vehicles?handler=List", {
        headers: { Accept: "application/json" },
        credentials: "same-origin"
      });
      if (!res.ok) throw new Error("Không thể tải dữ liệu xe.");
      const data = await res.json();
      vehicles = Array.isArray(data.vehicles) ? data.vehicles : [];
      types = Array.isArray(data.types) ? data.types : types;
      fillTypeFilter();
      setKpis(vehicles);
      page = 1;
      paint();
    }

    function openVehicleForm(v, mode) {
      const isEdit = mode === "edit" || mode === "view";
      const readOnly = mode === "view";
      const currentStatus = v?.status || "Available";

      openDrawer({
        title: mode === "view" ? "Chi tiết xe" : (isEdit ? "Chỉnh sửa xe" : "Thêm xe mới"),
        subtitle: v ? `${v.licensePlate} · #${v.vehicleId}` : "Tạo xe mới cho đội xe",
        renderTab: () => `
          <div class="aui-form-grid">
            <div class="aui-field"><label>Biển số xe *</label>
              <input class="aui-input" data-vf="licensePlate" value="${esc(v?.licensePlate || "")}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Số đăng ký</label>
              <input class="aui-input" data-vf="registrationNumber" value="${esc(v?.registrationNumber || "")}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Hãng *</label>
              <input class="aui-input" data-vf="brand" value="${esc(v?.brand || "")}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Model *</label>
              <input class="aui-input" data-vf="model" value="${esc(v?.model || "")}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Năm sản xuất *</label>
              <input class="aui-input" type="number" data-vf="year" value="${esc(v?.year || new Date().getFullYear())}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Loại xe *</label>
              <select class="aui-select" data-vf="typeId" ${readOnly ? "disabled" : ""}>
                ${typeOptions(v?.typeId)}
              </select>
            </div>
            ${isEdit ? `<div class="aui-field"><label>Trạng thái *</label>
              <select class="aui-select" data-vf="status" ${readOnly ? "disabled" : ""}>
                ${statusOptions(currentStatus)}
              </select>
              <small class="aui-muted" style="display:block;margin-top:.35rem">Xe đang cho thuê chỉ về Có sẵn sau khi trả xe. Không đặt Đang sử dụng thủ công.</small>
            </div>` : `<input type="hidden" data-vf="status" value="Available" />`}
            <div class="aui-field"><label>Km hiện tại *</label>
              <input class="aui-input" type="number" data-vf="currentKm" value="${esc(v?.currentKm ?? 0)}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Màu xe</label>
              <input class="aui-input" data-vf="color" value="${esc(v?.color || "")}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Hạn đăng ký</label>
              <input class="aui-input" type="date" data-vf="registrationExpiryDate" value="${esc(dateInputValue(v?.registrationExpiryDate))}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Hạn đăng kiểm</label>
              <input class="aui-input" type="date" data-vf="inspectionExpiryDate" value="${esc(dateInputValue(v?.inspectionExpiryDate))}" ${readOnly ? "readonly" : ""} /></div>
            <div class="aui-field"><label>Hạn bảo hiểm</label>
              <input class="aui-input" type="date" data-vf="insuranceExpiryDate" value="${esc(dateInputValue(v?.insuranceExpiryDate))}" ${readOnly ? "readonly" : ""} /></div>
          </div>
          ${isEdit ? `
          <div class="aui-section" style="margin-top:1rem">
            <h4>Thông tin</h4>
            <dl class="aui-kv">
              <div><dt>Mã xe</dt><dd>#${esc(v.vehicleId)}</dd></div>
              <div><dt>Loại</dt><dd>${esc(v.typeName || "—")}</dd></div>
              <div><dt>Trạng thái</dt><dd>${badge(v.status)}</dd></div>
              <div><dt>Số chỗ</dt><dd>${esc(v.seatCapacity ?? "—")}</dd></div>
            </dl>
          </div>` : ""}`,
        footerHtml: readOnly
          ? `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>
             <button type="button" class="aui-btn aui-btn-primary" data-to-edit>Chỉnh sửa</button>
             <button type="button" class="aui-btn aui-btn-outline-danger" data-delete>Xóa xe</button>`
          : `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Hủy</button>
             <button type="button" class="aui-btn aui-btn-primary" data-save>${isEdit ? "Lưu thay đổi" : "Thêm xe"}</button>
             ${isEdit ? `<button type="button" class="aui-btn aui-btn-outline-danger" data-delete>Xóa xe</button>` : ""}`
      });

      drawerFoot.querySelector("[data-to-edit]")?.addEventListener("click", () => openVehicleForm(v, "edit"));

      drawerFoot.querySelector("[data-save]")?.addEventListener("click", () => {
        const form = readForm();
        if (!form.licensePlate || !form.brand || !form.model || !form.typeId || !form.year) {
          toast("Vui lòng nhập đủ các trường bắt buộc.");
          return;
        }
        (async () => {
          try {
            if (isEdit && v) {
              await postJson(`/Admin/Vehicles?handler=Update&id=${encodeURIComponent(v.vehicleId)}`, {
                typeId: form.typeId,
                licensePlate: form.licensePlate,
                brand: form.brand,
                model: form.model,
                year: form.year,
                color: form.color,
                status: form.status,
                currentKm: form.currentKm,
                registrationNumber: form.registrationNumber,
                registrationExpiryDate: form.registrationExpiryDate,
                inspectionExpiryDate: form.inspectionExpiryDate,
                insuranceExpiryDate: form.insuranceExpiryDate
              });
              toast(`Đã cập nhật xe ${form.licensePlate}.`);
            } else {
              await postJson("/Admin/Vehicles?handler=Create", {
                typeId: form.typeId,
                licensePlate: form.licensePlate,
                brand: form.brand,
                model: form.model,
                year: form.year,
                color: form.color,
                currentKm: form.currentKm,
                registrationNumber: form.registrationNumber,
                registrationExpiryDate: form.registrationExpiryDate,
                inspectionExpiryDate: form.inspectionExpiryDate,
                insuranceExpiryDate: form.insuranceExpiryDate
              });
              toast(`Đã thêm xe ${form.licensePlate}.`);
            }
            closeDrawer();
            await reloadList();
          } catch (err) {
            toast(err.message || "Không thể lưu xe.");
          }
        })();
      });

      drawerFoot.querySelector("[data-delete]")?.addEventListener("click", () => {
        if (!v) return;
        openModal({
          title: "Xóa xe",
          body: `Xóa xe ${v.licensePlate}? Thao tác có thể bị từ chối nếu xe đang được thuê hoặc có lịch sử chuyến.`,
          okLabel: "Xóa xe",
          onOk: () => {
            (async () => {
              try {
                const body = new URLSearchParams();
                body.set("__RequestVerificationToken", antiforgery());
                const res = await fetch(`/Admin/Vehicles?handler=Delete&id=${encodeURIComponent(v.vehicleId)}`, {
                  method: "POST",
                  headers: {
                    RequestVerificationToken: antiforgery(),
                    Accept: "application/json"
                  },
                  body,
                  credentials: "same-origin"
                });
                const json = await res.json().catch(() => ({}));
                if (!res.ok) {
                  toast(json.message || "Không thể xóa xe.");
                  return;
                }
                toast(`Đã xóa xe ${v.licensePlate}.`);
                closeDrawer();
                await reloadList();
              } catch (_) {
                toast("Không thể xóa xe.");
              }
            })();
          }
        });
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      if (!vehicles.length) {
        tbody.innerHTML = `<tr><td colspan="8"><div class="aui-empty"><i class="bi bi-inbox"></i>Chưa có xe.</div></td></tr>`;
        pager.innerHTML = "";
        return;
      }
      tbody.innerHTML = pg.slice.map((v, i) => `
        <tr data-id="${esc(v.vehicleId)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>${esc(v.licensePlate)}</strong></td>
          <td>${esc(modelLabel(v))}</td>
          <td>${esc(v.typeName || "—")}</td>
          <td>${badge(v.status)}</td>
          <td>${esc(dayOnly(v.registrationExpiryDate))}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-edit="${esc(v.vehicleId)}" title="Chỉnh sửa"><i class="bi bi-pencil"></i></button>
            <button type="button" class="aui-icon-btn" data-view="${esc(v.vehicleId)}" title="Xem"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="8"><div class="aui-empty"><i class="bi bi-inbox"></i>Không tìm thấy xe.</div></td></tr>`;

      const find = (id) => vehicles.find((x) => String(x.vehicleId) === String(id));
      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const v = find(tr.getAttribute("data-id"));
          if (v) openVehicleForm(v, "view");
        });
      });
      tbody.querySelectorAll("[data-edit]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const v = find(btn.getAttribute("data-edit"));
          if (v) openVehicleForm(v, "edit");
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const v = find(btn.getAttribute("data-view"));
          if (v) openVehicleForm(v, "view");
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    vehicles = parseJson("auiVehiclesPayload", []);
    types = parseJson("auiVehicleTypesPayload", []);
    if (!Array.isArray(vehicles)) vehicles = [];
    if (!Array.isArray(types)) types = [];
    fillTypeFilter();
    setKpis(vehicles);
    [q, status, type].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    root.querySelector("[data-add-vehicle]")?.addEventListener("click", () => openVehicleForm(null, "create"));
    paint();
  }

  /* Customers */
  function initCustomers() {
    const root = document.getElementById("auiCustomers");
    if (!root) return;
    if (root.querySelector("[data-customers-error]")) return;

    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");
    let customers = [];

    const antiforgery = () =>
      document.querySelector("#auiCustomersAntiForgery input[name='__RequestVerificationToken']")?.value || "";

    const money = (n) => {
      const num = Number(n);
      if (Number.isNaN(num)) return "—";
      return num.toLocaleString("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 });
    };

    const dayOnly = (iso) => {
      if (!iso) return "—";
      const s = String(iso);
      if (/^\d{4}-\d{2}-\d{2}/.test(s)) {
        const [y, m, d] = s.slice(0, 10).split("-");
        return `${d}/${m}/${y}`;
      }
      const dt = new Date(s);
      return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("vi-VN");
    };

    const dateInputValue = (iso) => {
      if (!iso) return "";
      const s = String(iso);
      return /^\d{4}-\d{2}-\d{2}/.test(s) ? s.slice(0, 10) : "";
    };

    function accountStatus(c) {
      if (!c?.isActive) return "Inactive";
      if (c.isLocked) return "Locked";
      return "Active";
    }

    function accountStatusLabel(s) {
      return ({ Active: "Đang hoạt động", Locked: "Đã khóa", Inactive: "Đã vô hiệu hóa" })[s] || s;
    }

    function accountBadge(s) {
      const tone = statusTone[s] || "slate";
      return `<span class="aui-badge ${tone}">${esc(accountStatusLabel(s))}</span>`;
    }

    function parseJson(id, fallback) {
      const el = document.getElementById(id);
      if (!el) return fallback;
      try {
        const data = JSON.parse(el.textContent || "null");
        return data == null ? fallback : data;
      } catch (_) {
        return fallback;
      }
    }

    function setKpis(list) {
      const now = new Date();
      const y = now.getFullYear();
      const m = now.getMonth();
      const set = (key, val) => {
        const el = root.querySelector(`[data-kpi="${key}"]`);
        if (el) el.textContent = Number(val).toLocaleString("vi-VN");
      };
      set("total", list.length);
      set("active", list.filter((c) => c.isActive && !c.isLocked).length);
      set("locked", list.filter((c) => c.isActive && c.isLocked).length);
      set("newMonth", list.filter((c) => {
        if (!c.createdAt) return false;
        const d = new Date(c.createdAt);
        return !Number.isNaN(d.getTime()) && d.getFullYear() === y && d.getMonth() === m;
      }).length);
    }

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return customers.filter((c) => {
        const st = accountStatus(c);
        if (status?.value && st !== status.value) return false;
        if (!qq) return true;
        const hay = [c.fullName, c.email, c.phone, c.customerId].join(" ").toLowerCase();
        return hay.includes(qq);
      });
    }

    async function postJson(url, body) {
      const res = await fetch(url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          RequestVerificationToken: antiforgery()
        },
        body: JSON.stringify(body),
        credentials: "same-origin"
      });
      const json = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(json.message || "Thao tác thất bại.");
      return json;
    }

    async function reloadList() {
      const res = await fetch("/Admin/Customers?handler=List", {
        headers: { Accept: "application/json" },
        credentials: "same-origin"
      });
      if (!res.ok) throw new Error("Không thể tải dữ liệu khách hàng.");
      const data = await res.json();
      customers = Array.isArray(data.customers) ? data.customers : [];
      setKpis(customers);
      page = 1;
      paint();
    }

    function readCustomerForm(includePassword) {
      const g = (name) => document.querySelector(`#auiDrawer [data-cf="${name}"]`);
      const val = (name) => (g(name)?.value || "").trim();
      const dateOrNull = (name) => {
        const v = val(name);
        return v || null;
      };
      const body = {
        fullName: val("fullName"),
        email: val("email"),
        phone: val("phone"),
        address: val("address") || null,
        idNumber: val("idNumber") || null,
        dateOfBirth: dateOrNull("dateOfBirth")
      };
      if (includePassword) body.password = val("password");
      return body;
    }

    function customerFormHtml(c, mode) {
      const readOnly = mode === "view";
      const isCreate = mode === "create";
      return `
        <div class="aui-form-grid">
          <div class="aui-field"><label>Họ và tên *</label>
            <input class="aui-input" data-cf="fullName" value="${esc(c?.fullName || "")}" ${readOnly ? "readonly" : ""} /></div>
          <div class="aui-field"><label>Email *</label>
            <input class="aui-input" type="email" data-cf="email" value="${esc(c?.email || "")}" ${readOnly ? "readonly" : ""} /></div>
          <div class="aui-field"><label>Số điện thoại *</label>
            <input class="aui-input" data-cf="phone" value="${esc(c?.phone || "")}" ${readOnly ? "readonly" : ""} /></div>
          ${isCreate ? `<div class="aui-field"><label>Mật khẩu *</label>
            <input class="aui-input" type="password" data-cf="password" value="" autocomplete="new-password" /></div>` : ""}
          <div class="aui-field"><label>Số CCCD/CMND</label>
            <input class="aui-input" data-cf="idNumber" value="${esc(c?.idNumber || "")}" ${readOnly ? "readonly" : ""} /></div>
          <div class="aui-field"><label>Ngày sinh</label>
            <input class="aui-input" type="date" data-cf="dateOfBirth" value="${esc(dateInputValue(c?.dateOfBirth))}" ${readOnly ? "readonly" : ""} /></div>
          <div class="aui-field full"><label>Địa chỉ</label>
            <input class="aui-input" data-cf="address" value="${esc(c?.address || "")}" ${readOnly ? "readonly" : ""} /></div>
        </div>`;
    }

    function openCustomerForm(c, mode) {
      const isEdit = mode === "edit";
      const isCreate = mode === "create";
      openDrawer({
        title: isCreate ? "Thêm khách hàng" : (isEdit ? "Chỉnh sửa khách hàng" : "Chi tiết khách hàng"),
        subtitle: c ? `${c.fullName || ""} · #${c.customerId}` : "Tạo tài khoản khách hàng mới",
        renderTab: () => customerFormHtml(c, mode),
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Hủy</button>
          <button type="button" class="aui-btn aui-btn-primary" data-save>${isCreate ? "Thêm khách hàng" : "Lưu thay đổi"}</button>`
      });

      drawerFoot.querySelector("[data-save]")?.addEventListener("click", () => {
        const form = readCustomerForm(isCreate);
        if (!form.fullName || !form.email || !form.phone || (isCreate && !form.password)) {
          toast("Vui lòng nhập đủ các trường bắt buộc.");
          return;
        }
        (async () => {
          try {
            if (isEdit && c) {
              await postJson(`/Admin/Customers?handler=Update&id=${encodeURIComponent(c.customerId)}`, form);
              toast(`Đã cập nhật khách hàng ${form.fullName}.`);
            } else {
              await postJson("/Admin/Customers?handler=Create", form);
              toast(`Đã thêm khách hàng ${form.fullName}.`);
            }
            closeDrawer();
            await reloadList();
          } catch (err) {
            toast(err.message || "Không thể lưu khách hàng.");
          }
        })();
      });
    }

    async function openCustomer(c) {
      openDrawer({
        title: "Chi tiết khách hàng",
        subtitle: `#${c.customerId}`,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "bookings", label: "Đơn thuê" },
          { key: "payments", label: "Thanh toán" },
          { key: "notes", label: "Ghi chú" }
        ],
        renderTab: () => `<div class="aui-empty"><i class="bi bi-hourglass-split"></i>Đang tải thông tin khách hàng...</div>`,
        footerHtml: ""
      });

      let detail = { customer: c, bookings: [], payments: [], bookingsError: null, paymentsError: null };
      try {
        const res = await fetch(`/Admin/Customers?handler=Detail&id=${encodeURIComponent(c.customerId)}`, {
          headers: { Accept: "application/json" },
          credentials: "same-origin"
        });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.message || "Không thể tải thông tin khách hàng.");
        detail = {
          customer: json.customer || c,
          bookings: Array.isArray(json.bookings) ? json.bookings : [],
          payments: Array.isArray(json.payments) ? json.payments : [],
          bookingsError: json.bookingsError || null,
          paymentsError: json.paymentsError || null
        };
      } catch (err) {
        toast(err.message || "Không thể tải thông tin khách hàng.");
      }

      const cust = detail.customer;
      const st = accountStatus(cust);
      const bookingCount = detail.bookings.length;
      const paidSum = detail.payments
        .filter((p) => String(p.status || "").toLowerCase() === "paid" || p.status === "Paid")
        .reduce((sum, p) => sum + (Number(p.amount) || 0), 0);

      function paintDetail(tab) {
        if (tab === "notes") {
          return `<div class="aui-empty"><i class="bi bi-journal-text"></i>Chưa có dữ liệu ghi chú.</div>`;
        }
        if (tab === "bookings") {
          if (detail.bookingsError) {
            return `<div class="aui-empty"><i class="bi bi-exclamation-triangle"></i>${esc(detail.bookingsError)}</div>`;
          }
          if (!detail.bookings.length) {
            return `<div class="aui-empty"><i class="bi bi-calendar2"></i>Chưa có đơn thuê.</div>`;
          }
          return `<div class="aui-table-wrap"><table class="aui-table">
            <thead><tr><th>Mã</th><th>Xe</th><th>Nhận</th><th>Trả</th><th>Trạng thái</th><th>Tổng tiền</th></tr></thead>
            <tbody>${detail.bookings.map((b) => {
            const veh = b.assignedVehicle
              ? `${b.assignedVehicle.brand || ""} ${b.assignedVehicle.model || ""}`.trim() || b.assignedVehicle.licensePlate
              : (b.vehicleTypeName || "—");
            const plate = b.assignedVehicle?.licensePlate ? ` (${b.assignedVehicle.licensePlate})` : "";
            return `
              <tr>
                <td>#${esc(b.bookingId)}</td>
                <td>${esc(veh)}${esc(plate)}</td>
                <td>${esc(dayOnly(b.startDate))}</td>
                <td>${esc(dayOnly(b.endDate))}</td>
                <td>${badge(b.status)}</td>
                <td>${esc(money(b.finalAmount != null ? b.finalAmount : b.totalAmount))}</td>
              </tr>`;
          }).join("")}</tbody></table></div>`;
        }
        if (tab === "payments") {
          if (detail.paymentsError) {
            return `<div class="aui-empty"><i class="bi bi-exclamation-triangle"></i>${esc(detail.paymentsError)}</div>`;
          }
          if (!detail.payments.length) {
            return `<div class="aui-empty"><i class="bi bi-credit-card"></i>Chưa có thanh toán.</div>`;
          }
          return `<div class="aui-table-wrap"><table class="aui-table">
            <thead><tr><th>Mã</th><th>Đơn</th><th>Loại</th><th>Số tiền</th><th>Trạng thái</th><th>Ngày</th></tr></thead>
            <tbody>${detail.payments.map((p) => `
              <tr>
                <td>#${esc(p.paymentId)}</td>
                <td>#${esc(p.bookingId)}</td>
                <td>${esc(t(p.paymentType) || p.paymentType || "—")}</td>
                <td>${esc(money(p.amount))}</td>
                <td>${badge(p.status)}</td>
                <td>${esc(dayOnly(p.paidAt || p.createdAt))}</td>
              </tr>`).join("")}</tbody></table></div>`;
        }

        return `
          <div class="aui-person" style="margin-bottom:1rem">
            <span class="aui-avatar" style="width:52px;height:52px;font-size:1rem">${esc(initials(cust.fullName))}</span>
            <div>
              <strong style="font-size:1.05rem">${esc(cust.fullName)}</strong>
              <small>${esc(cust.email || "")}</small>
              <div style="margin-top:.35rem">${accountBadge(st)}</div>
            </div>
          </div>
          <dl class="aui-kv">
            <div><dt>Mã khách hàng</dt><dd>#${esc(cust.customerId)}</dd></div>
            <div><dt>Họ và tên</dt><dd>${esc(cust.fullName || "—")}</dd></div>
            <div><dt>Ngày sinh</dt><dd>${esc(dayOnly(cust.dateOfBirth))}</dd></div>
            <div><dt>Ngày tham gia</dt><dd>${esc(dayOnly(cust.createdAt))}</dd></div>
            <div><dt>Email</dt><dd>${esc(cust.email || "—")}</dd></div>
            <div><dt>Số điện thoại</dt><dd>${esc(cust.phone || "—")}</dd></div>
            <div class="full"><dt>Địa chỉ</dt><dd>${esc(cust.address || "—")}</dd></div>
            <div><dt>Số CCCD/CMND</dt><dd>${esc(cust.idNumber || "—")}</dd></div>
            <div><dt>Trạng thái tài khoản</dt><dd>${esc(accountStatusLabel(st))}</dd></div>
            ${cust.isLocked && cust.lockReason ? `<div class="full"><dt>Lý do khóa</dt><dd>${esc(cust.lockReason)}</dd></div>` : ""}
            ${!cust.isActive && cust.inactiveReason ? `<div class="full"><dt>Lý do vô hiệu hóa</dt><dd>${esc(cust.inactiveReason)}</dd></div>` : ""}
            <div><dt>Tổng đơn thuê</dt><dd>${esc(bookingCount)}</dd></div>
            <div><dt>Tổng đã thanh toán</dt><dd>${esc(money(paidSum))}</dd></div>
          </dl>`;
      }

      const lockBtn = cust.isLocked
        ? `<button type="button" class="aui-btn aui-btn-ghost" data-unlock>Mở khóa tài khoản</button>`
        : `<button type="button" class="aui-btn aui-btn-outline-danger" data-lock>Khóa tài khoản</button>`;
      const deactivateBtn = cust.isActive
        ? `<button type="button" class="aui-btn aui-btn-outline-danger" data-deactivate>Vô hiệu hóa</button>`
        : "";

      function bindCustomerFooter(foot) {
        foot.querySelector("[data-edit]")?.addEventListener("click", () => openCustomerForm(cust, "edit"));

        foot.querySelector("[data-lock]")?.addEventListener("click", () => {
          openModal({
            title: "Khóa tài khoản",
            html: true,
            body: `<p>Khóa tài khoản <strong>${esc(cust.fullName)}</strong>?</p>
              <label class="aui-field" style="display:block;margin-top:.75rem">
                <span>Lý do khóa *</span>
                <textarea class="aui-input" data-lock-reason rows="3" maxlength="255" style="width:100%;margin-top:.35rem"></textarea>
              </label>`,
            okLabel: "Khóa tài khoản",
            onOk: () => {
              const reason = (modalBody.querySelector("[data-lock-reason]")?.value || "").trim();
              if (!reason) {
                toast("Vui lòng nhập lý do khóa tài khoản.");
                return false;
              }
              return (async () => {
                try {
                  await postJson(`/Admin/Customers?handler=Lock&id=${encodeURIComponent(cust.customerId)}`, {
                    isLocked: true,
                    reason
                  });
                  toast(`Đã khóa tài khoản #${cust.customerId}.`);
                  closeDrawer();
                  await reloadList();
                  return true;
                } catch (err) {
                  toast(err.message || "Không thể khóa tài khoản.");
                  return false;
                }
              })();
            }
          });
        });

        foot.querySelector("[data-unlock]")?.addEventListener("click", () => {
          openModal({
            title: "Mở khóa tài khoản",
            body: `Mở khóa tài khoản ${cust.fullName}?`,
            okLabel: "Mở khóa",
            onOk: () => (async () => {
              try {
                await postJson(`/Admin/Customers?handler=Lock&id=${encodeURIComponent(cust.customerId)}`, {
                  isLocked: false
                });
                toast(`Đã mở khóa tài khoản #${cust.customerId}.`);
                closeDrawer();
                await reloadList();
              } catch (err) {
                toast(err.message || "Không thể mở khóa tài khoản.");
              }
            })()
          });
        });

        foot.querySelector("[data-deactivate]")?.addEventListener("click", () => {
          openModal({
            title: "Vô hiệu hóa tài khoản",
            html: true,
            body: `<p>Vô hiệu hóa tài khoản <strong>${esc(cust.fullName)}</strong>? Đây là soft-delete (không xóa lịch sử đơn/thanh toán).</p>
              <label class="aui-field" style="display:block;margin-top:.75rem">
                <span>Lý do vô hiệu hóa *</span>
                <textarea class="aui-input" data-deactivate-reason rows="3" maxlength="255" style="width:100%;margin-top:.35rem"></textarea>
              </label>`,
            okLabel: "Vô hiệu hóa",
            onOk: () => {
              const reason = (modalBody.querySelector("[data-deactivate-reason]")?.value || "").trim();
              if (!reason) {
                toast("Vui lòng nhập lý do vô hiệu hóa.");
                return false;
              }
              return (async () => {
                try {
                  await postJson(`/Admin/Customers?handler=Deactivate&id=${encodeURIComponent(cust.customerId)}`, { reason });
                  toast(`Đã vô hiệu hóa tài khoản #${cust.customerId}.`);
                  closeDrawer();
                  await reloadList();
                  return true;
                } catch (err) {
                  toast(err.message || "Không thể vô hiệu hóa tài khoản.");
                  return false;
                }
              })();
            }
          });
        });
      }

      openDrawer({
        title: "Chi tiết khách hàng",
        subtitle: `#${cust.customerId}`,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "bookings", label: "Đơn thuê" },
          { key: "payments", label: "Thanh toán" },
          { key: "notes", label: "Ghi chú" }
        ],
        renderTab: paintDetail,
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-edit>Chỉnh sửa</button>
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-toast="Gửi email chưa được hỗ trợ.">Gửi email</button>
          ${lockBtn}
          ${deactivateBtn}`,
        bindFooter: bindCustomerFooter
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((c, i) => {
        const st = accountStatus(c);
        return `
        <tr data-id="${esc(c.customerId)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><div class="aui-person"><span class="aui-avatar">${esc(initials(c.fullName))}</span><div><strong>${esc(c.fullName)}</strong><small>#${esc(c.customerId)}</small></div></div></td>
          <td>${esc(c.email || "—")}</td>
          <td>${esc(c.phone || "—")}</td>
          <td><span class="aui-muted">—</span></td>
          <td>${esc(dayOnly(c.createdAt))}</td>
          <td>${accountBadge(st)}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-view="${esc(c.customerId)}"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`;
      }).join("") || `<tr><td colspan="9"><div class="aui-empty"><i class="bi bi-inbox"></i>${customers.length ? "Không tìm thấy khách hàng." : "Chưa có khách hàng."}</div></td></tr>`;

      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const c = customers.find((x) => String(x.customerId) === tr.getAttribute("data-id"));
          if (c) openCustomer(c);
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const c = customers.find((x) => String(x.customerId) === btn.getAttribute("data-view"));
          if (c) openCustomer(c);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    [q, status].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Xuất dữ liệu chưa được hỗ trợ."));
    root.querySelector("[data-add-customer]")?.addEventListener("click", () => openCustomerForm(null, "create"));

    customers = parseJson("auiCustomersPayload", []);
    if (!Array.isArray(customers)) customers = [];
    setKpis(customers);
    paint();
  }

  /* Drivers */
  function initDrivers() {
    const root = document.getElementById("auiDrivers");
    if (!root) return;
    if (root.querySelector("[data-drivers-error]")) return;

    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const status = root.querySelector("[data-filter-status]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");
    let drivers = [];

    const antiforgery = () =>
      document.querySelector("#auiDriversAntiForgery input[name='__RequestVerificationToken']")?.value || "";

    const dayOnly = (iso) => {
      if (!iso) return "—";
      const s = String(iso);
      if (/^\d{4}-\d{2}-\d{2}/.test(s)) {
        const [y, m, d] = s.slice(0, 10).split("-");
        return `${d}/${m}/${y}`;
      }
      const dt = new Date(s);
      return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("vi-VN");
    };

    const ratingText = (n) => {
      const num = Number(n);
      if (Number.isNaN(num)) return "—";
      return num.toLocaleString("vi-VN", { minimumFractionDigits: 1, maximumFractionDigits: 2 });
    };

    function parseJson(id, fallback) {
      const el = document.getElementById(id);
      if (!el) return fallback;
      try {
        const data = JSON.parse(el.textContent || "null");
        return data == null ? fallback : data;
      } catch (_) {
        return fallback;
      }
    }

    function setKpis(list) {
      const set = (key, val) => {
        const el = root.querySelector(`[data-kpi="${key}"]`);
        if (el) el.textContent = Number(val).toLocaleString("vi-VN");
      };
      const active = list.filter((d) => d.isActive !== false);
      set("total", list.length);
      set("available", active.filter((d) => d.status === "Available").length);
      set("busy", active.filter((d) => d.status === "Busy").length);
      set("offline", active.filter((d) => d.status === "Offline").length);
    }

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      return drivers.filter((d) => {
        if (status?.value && d.status !== status.value) return false;
        if (!qq) return true;
        const hay = [d.fullName, d.email, d.phone, d.driverId, d.licenseNumber].join(" ").toLowerCase();
        return hay.includes(qq);
      });
    }

    async function postJson(url, body) {
      const res = await fetch(url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          RequestVerificationToken: antiforgery()
        },
        body: JSON.stringify(body),
        credentials: "same-origin"
      });
      const json = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(json.message || "Thao tác thất bại.");
      return json;
    }

    async function reloadList() {
      const res = await fetch("/Admin/Drivers?handler=List", {
        headers: { Accept: "application/json" },
        credentials: "same-origin"
      });
      if (!res.ok) throw new Error("Không thể tải dữ liệu tài xế.");
      const data = await res.json();
      drivers = Array.isArray(data.drivers) ? data.drivers : [];
      setKpis(drivers);
      page = 1;
      paint();
    }

    function statusOptions(current) {
      return `
        <option value="Available" ${current === "Available" ? "selected" : ""}>Có sẵn</option>
        <option value="Busy" ${current === "Busy" ? "selected" : ""}>Đang bận</option>
        <option value="Offline" ${current === "Offline" ? "selected" : ""}>Ngoại tuyến</option>`;
    }

    function readDriverForm(includePassword) {
      const g = (name) => document.querySelector(`#auiDrawer [data-df="${name}"]`);
      const val = (name) => (g(name)?.value || "").trim();
      if (includePassword) {
        return {
          fullName: val("fullName"),
          email: val("email"),
          phone: val("phone"),
          password: val("password")
        };
      }
      return {
        fullName: val("fullName") || null,
        phone: val("phone") || null,
        status: val("status") || null
      };
    }

    function driverFormHtml(d, mode) {
      const isCreate = mode === "create";
      return `
        <div class="aui-form-grid">
          <div class="aui-field"><label>Họ và tên *</label>
            <input class="aui-input" data-df="fullName" value="${esc(d?.fullName || "")}" /></div>
          ${isCreate ? `<div class="aui-field"><label>Email *</label>
            <input class="aui-input" type="email" data-df="email" value="" /></div>` : `
          <div class="aui-field"><label>Email</label>
            <input class="aui-input" value="${esc(d?.email || "")}" readonly /></div>`}
          <div class="aui-field"><label>Số điện thoại *</label>
            <input class="aui-input" data-df="phone" value="${esc(d?.phone || "")}" /></div>
          ${isCreate ? `<div class="aui-field"><label>Mật khẩu *</label>
            <input class="aui-input" type="password" data-df="password" value="" autocomplete="new-password" /></div>` : `
          <div class="aui-field"><label>Trạng thái *</label>
            <select class="aui-select" data-df="status">${statusOptions(d?.status || "Available")}</select>
            <small class="aui-muted" style="display:block;margin-top:.35rem">Không đặt Có sẵn/Ngoại tuyến khi tài xế còn chuyến chưa hoàn thành (theo quy tắc backend).</small>
          </div>`}
        </div>`;
    }

    function openDriverForm(d, mode) {
      const isCreate = mode === "create";
      openDrawer({
        title: isCreate ? "Thêm tài xế" : "Chỉnh sửa tài xế",
        subtitle: d ? `${d.fullName || ""} · #${d.driverId}` : "Tạo tài khoản tài xế mới",
        renderTab: () => driverFormHtml(d, mode),
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Hủy</button>
          <button type="button" class="aui-btn aui-btn-primary" data-save>${isCreate ? "Thêm tài xế" : "Lưu thay đổi"}</button>`
      });

      drawerFoot.querySelector("[data-save]")?.addEventListener("click", () => {
        const form = readDriverForm(isCreate);
        if (isCreate) {
          if (!form.fullName || !form.email || !form.phone || !form.password) {
            toast("Vui lòng nhập đủ các trường bắt buộc.");
            return;
          }
        } else if (!form.fullName || !form.phone || !form.status) {
          toast("Vui lòng nhập đủ các trường bắt buộc.");
          return;
        }
        (async () => {
          try {
            if (isCreate) {
              await postJson("/Admin/Drivers?handler=Create", form);
              toast(`Đã thêm tài xế ${form.fullName}.`);
            } else {
              await postJson(`/Admin/Drivers?handler=Update&id=${encodeURIComponent(d.driverId)}`, form);
              toast(`Đã cập nhật tài xế ${form.fullName}.`);
            }
            closeDrawer();
            await reloadList();
          } catch (err) {
            toast(err.message || "Không thể lưu tài xế.");
          }
        })();
      });
    }

    async function openDriver(seed) {
      openDrawer({
        title: "Hồ sơ tài xế",
        subtitle: `#${seed.driverId}`,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "performance", label: "Hiệu suất" },
          { key: "incidents", label: "Sự cố" },
          { key: "documents", label: "Tài liệu" }
        ],
        renderTab: () => `<div class="aui-empty"><i class="bi bi-hourglass-split"></i>Đang tải hồ sơ tài xế...</div>`,
        footerHtml: ""
      });

      let d = seed;
      try {
        const res = await fetch(`/Admin/Drivers?handler=Detail&id=${encodeURIComponent(seed.driverId)}`, {
          headers: { Accept: "application/json" },
          credentials: "same-origin"
        });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.message || "Không thể tải hồ sơ tài xế.");
        d = json.driver || seed;
      } catch (err) {
        toast(err.message || "Không thể tải hồ sơ tài xế.");
      }

      function paintDetail(tab) {
        if (tab === "performance") {
          return `<div class="aui-stat-row" style="grid-template-columns:1fr 1fr">
            <div class="aui-stat-mini"><span>Tổng chuyến</span><strong>${esc(d.totalTrips ?? 0)}</strong></div>
            <div class="aui-stat-mini"><span>Đánh giá trung bình</span><strong>★ ${esc(ratingText(d.averageRating))}</strong></div>
          </div>
          <p class="aui-muted" style="margin-top:1rem;font-size:.85rem">Các chỉ số khác (quãng đường, tỷ lệ hoàn thành) chưa được backend cung cấp.</p>`;
        }
        if (tab === "incidents") {
          return `<div class="aui-empty"><i class="bi bi-exclamation-triangle"></i>Chưa có API sự cố theo tài xế cho Admin.</div>`;
        }
        if (tab === "documents") {
          return `<dl class="aui-kv">
            <div><dt>Số giấy phép</dt><dd>${esc(d.licenseNumber || "—")}</dd></div>
            <div><dt>Hạn giấy phép</dt><dd>${esc(dayOnly(d.licenseExpiry))}</dd></div>
          </dl>
          <p class="aui-muted" style="margin-top:1rem;font-size:.85rem">Hệ thống chưa hỗ trợ tài liệu đính kèm khác.</p>`;
        }
        return `
          <div class="aui-person" style="margin-bottom:1rem">
            <span class="aui-avatar" style="width:52px;height:52px">${esc(initials(d.fullName))}</span>
            <div>
              <strong style="font-size:1.05rem">${esc(d.fullName)}</strong>
              <small>${esc(d.email || "")}</small>
              <div style="margin-top:.35rem">${badge(d.status)}
                ${d.isActive === false ? `<span class="aui-badge red">Đã vô hiệu hóa</span>` : ""}
                <span class="aui-badge amber">★ ${esc(ratingText(d.averageRating))}</span>
              </div>
            </div>
          </div>
          <dl class="aui-kv">
            <div><dt>Mã tài xế</dt><dd>#${esc(d.driverId)}</dd></div>
            <div><dt>Họ và tên</dt><dd>${esc(d.fullName || "—")}</dd></div>
            <div><dt>Email</dt><dd>${esc(d.email || "—")}</dd></div>
            <div><dt>Số điện thoại</dt><dd>${esc(d.phone || "—")}</dd></div>
            <div><dt>Trạng thái</dt><dd>${esc(t(d.status))}</dd></div>
            <div><dt>Đang hoạt động</dt><dd>${d.isActive === false ? "Không" : "Có"}</dd></div>
            <div><dt>Số giấy phép</dt><dd>${esc(d.licenseNumber || "—")}</dd></div>
            <div><dt>Hạn giấy phép</dt><dd>${esc(dayOnly(d.licenseExpiry))}</dd></div>
            <div><dt>Tổng chuyến</dt><dd>${esc(d.totalTrips ?? 0)}</dd></div>
            <div><dt>Đánh giá trung bình</dt><dd>★ ${esc(ratingText(d.averageRating))}</dd></div>
          </dl>`;
      }

      const deactivateBtn = d.isActive !== false
        ? `<button type="button" class="aui-btn aui-btn-outline-danger" data-deactivate>Vô hiệu hóa</button>`
        : "";

      function bindDriverFooter(foot) {
        foot.querySelector("[data-edit]")?.addEventListener("click", () => openDriverForm(d, "edit"));
        foot.querySelector("[data-deactivate]")?.addEventListener("click", () => {
          openModal({
            title: "Vô hiệu hóa tài xế",
            body: `Vô hiệu hóa tài xế ${d.fullName}? Backend sẽ khóa tài xế (IsActive=false). Không thể thực hiện nếu còn chuyến chưa hoàn thành.`,
            okLabel: "Vô hiệu hóa",
            onOk: () => (async () => {
              try {
                const body = new URLSearchParams();
                body.set("__RequestVerificationToken", antiforgery());
                const res = await fetch(`/Admin/Drivers?handler=Delete&id=${encodeURIComponent(d.driverId)}`, {
                  method: "POST",
                  headers: {
                    RequestVerificationToken: antiforgery(),
                    Accept: "application/json"
                  },
                  body,
                  credentials: "same-origin"
                });
                const json = await res.json().catch(() => ({}));
                if (!res.ok) {
                  toast(json.message || "Không thể vô hiệu hóa tài xế.");
                  return;
                }
                toast(`Đã vô hiệu hóa tài xế #${d.driverId}.`);
                closeDrawer();
                await reloadList();
              } catch (_) {
                toast("Không thể vô hiệu hóa tài xế.");
              }
            })()
          });
        });
      }

      openDrawer({
        title: "Hồ sơ tài xế",
        subtitle: `#${d.driverId}`,
        tabs: [
          { key: "overview", label: "Tổng quan" },
          { key: "performance", label: "Hiệu suất" },
          { key: "incidents", label: "Sự cố" },
          { key: "documents", label: "Tài liệu" }
        ],
        renderTab: paintDetail,
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-edit>Chỉnh sửa</button>
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-toast="Phân công chuyến chưa được hỗ trợ trên Admin UI.">Phân công chuyến</button>
          ${deactivateBtn}`,
        bindFooter: bindDriverFooter
      });
    }

    function paint() {
      const rows = filtered();
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((d, i) => `
        <tr data-id="${esc(d.driverId)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><div class="aui-person"><span class="aui-avatar">${esc(initials(d.fullName))}</span><div><strong>${esc(d.fullName)}</strong><small>#${esc(d.driverId)}${d.isActive === false ? " · Vô hiệu" : ""}</small></div></div></td>
          <td>${esc(d.phone || "—")}</td>
          <td>${badge(d.status)}</td>
          <td>★ ${esc(ratingText(d.averageRating))}</td>
          <td>${esc(d.totalTrips ?? 0)}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-view="${esc(d.driverId)}"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="8"><div class="aui-empty"><i class="bi bi-inbox"></i>${drivers.length ? "Không tìm thấy tài xế." : "Chưa có tài xế."}</div></td></tr>`;

      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const d = drivers.find((x) => String(x.driverId) === tr.getAttribute("data-id"));
          if (d) openDriver(d);
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const d = drivers.find((x) => String(x.driverId) === btn.getAttribute("data-view"));
          if (d) openDriver(d);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    [q, status].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Xuất dữ liệu chưa được hỗ trợ."));
    root.querySelector("[data-add-driver]")?.addEventListener("click", () => openDriverForm(null, "create"));

    drivers = parseJson("auiDriversPayload", []);
    if (!Array.isArray(drivers)) drivers = [];
    setKpis(drivers);
    paint();
  }

  /* Payments */
  function initPayments() {
    const root = document.getElementById("auiPayments");
    if (!root) return;
    if (root.querySelector("[data-payments-error]")) return;

    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-filter-q]");
    const payStatus = root.querySelector("[data-filter-pay]");
    const contract = root.querySelector("[data-filter-contract]");
    const fromEl = root.querySelector("[data-filter-from]");
    const toEl = root.querySelector("[data-filter-to]");
    const tbody = root.querySelector("[data-table-body]");
    const pager = root.querySelector("[data-pager]");

    let payments = [];
    let contracts = [];
    let bookings = [];
    let revenueOverview = [];
    let rows = [];
    let chartMethods = null;
    let chartTrend = null;

    const money = (n) => {
      const num = Number(n);
      if (Number.isNaN(num)) return "—";
      return num.toLocaleString("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 });
    };

    const when = (iso) => {
      if (!iso) return "—";
      const dt = new Date(iso);
      return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleString("vi-VN");
    };

    const methodLabel = (m) => {
      const map = {
        Cash: "Tiền mặt",
        BankTransfer: "Chuyển khoản",
        MoMo: "MoMo",
        VNPay: "VNPay",
        Momo: "MoMo",
        VNPAY: "VNPay"
      };
      return map[m] || t(m) || m || "—";
    };

    const methodColor = {
      Cash: "#16A34A",
      BankTransfer: "#2563EB",
      MoMo: "#D97706",
      VNPay: "#7C3AED",
      Momo: "#D97706",
      VNPAY: "#7C3AED"
    };

    function parseJson(id, fallback) {
      const el = document.getElementById(id);
      if (!el) return fallback;
      try {
        const data = JSON.parse(el.textContent || "null");
        return data == null ? fallback : data;
      } catch (_) {
        return fallback;
      }
    }

    function isPaidDeposit(p) {
      if (p.status !== "Paid") return false;
      const type = p.paymentType;
      return !type || type === "Deposit";
    }

    function paymentDate(p) {
      return p.paidAt || p.createdAt || null;
    }

    function rebuildRows() {
      const contractByBooking = new Map();
      contracts.forEach((c) => {
        if (!contractByBooking.has(c.bookingId)) contractByBooking.set(c.bookingId, c);
      });
      const bookingById = new Map(bookings.map((b) => [b.bookingId, b]));

      rows = payments.map((p) => {
        const c = contractByBooking.get(p.bookingId) || null;
        const b = bookingById.get(p.bookingId) || null;
        return {
          payment: p,
          contract: c,
          booking: b,
          customerName: c?.customerName || b?.customerName || "—",
          contractStatus: c?.status || null,
          txnLabel: p.transactionRef || `PAY-${p.paymentId}`,
          dateIso: paymentDate(p)
        };
      });
    }

    function setKpis() {
      const set = (key, val, isMoney) => {
        const el = root.querySelector(`[data-kpi="${key}"]`);
        if (!el) return;
        el.textContent = isMoney
          ? money(val)
          : Number(val).toLocaleString("vi-VN");
      };
      const revenue = payments.filter(isPaidDeposit).reduce((s, p) => s + (Number(p.amount) || 0), 0);
      set("revenue", revenue, true);
      set("transactions", payments.length, false);
      set("issued", contracts.filter((c) => c.status === "Issued").length, false);
      set("signed", contracts.filter((c) => c.status === "Signed").length, false);
    }

    function filtered() {
      const qq = (q?.value || "").toLowerCase().trim();
      const from = fromEl?.value ? new Date(fromEl.value + "T00:00:00") : null;
      const to = toEl?.value ? new Date(toEl.value + "T23:59:59") : null;
      return rows.filter((r) => {
        const p = r.payment;
        if (payStatus?.value && p.status !== payStatus.value) return false;
        if (contract?.value === "__none") {
          if (r.contractStatus) return false;
        } else if (contract?.value && r.contractStatus !== contract.value) {
          return false;
        }
        if (from || to) {
          if (!r.dateIso) return false;
          const d = new Date(r.dateIso);
          if (Number.isNaN(d.getTime())) return false;
          if (from && d < from) return false;
          if (to && d > to) return false;
        }
        if (!qq) return true;
        const hay = [r.txnLabel, r.customerName, p.bookingId, p.paymentId, p.transactionRef, p.method]
          .join(" ").toLowerCase();
        return hay.includes(qq);
      });
    }

    function paintMethodsChart() {
      const canvas = document.getElementById("chartPayMethods");
      const empty = root.querySelector("[data-methods-empty]");
      const counts = {};
      payments.forEach((p) => {
        const key = p.method || "—";
        counts[key] = (counts[key] || 0) + 1;
      });
      const items = Object.keys(counts).map((k) => ({
        label: methodLabel(k),
        value: counts[k],
        color: methodColor[k] || "#64748B"
      }));
      if (chartMethods) {
        chartMethods.destroy();
        chartMethods = null;
      }
      if (!items.length) {
        if (canvas) canvas.style.display = "none";
        if (empty) empty.hidden = false;
        return;
      }
      if (canvas) canvas.style.display = "";
      if (empty) empty.hidden = true;
      if (!window.Chart || !canvas) return;
      chartMethods = new Chart(canvas, {
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
          plugins: { legend: { position: "bottom", labels: { boxWidth: 10, font: { size: 11 } } } }
        }
      });
    }

    function paintTrendChart() {
      const canvas = document.getElementById("chartPayTrend");
      const empty = root.querySelector("[data-trend-empty]");
      let labels = [];
      let values = [];

      if (Array.isArray(revenueOverview) && revenueOverview.length) {
        labels = revenueOverview.map((x) => x.label || `${x.month}/${x.year}`);
        values = revenueOverview.map((x) => Number(x.amount) || 0);
      } else {
        const now = new Date();
        const buckets = [];
        for (let i = 11; i >= 0; i--) {
          const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
          buckets.push({ y: d.getFullYear(), m: d.getMonth(), label: `T${d.getMonth() + 1}`, amount: 0 });
        }
        payments.filter(isPaidDeposit).forEach((p) => {
          if (!p.paidAt) return;
          const dt = new Date(p.paidAt);
          if (Number.isNaN(dt.getTime())) return;
          const b = buckets.find((x) => x.y === dt.getFullYear() && x.m === dt.getMonth());
          if (b) b.amount += Number(p.amount) || 0;
        });
        labels = buckets.map((b) => b.label);
        values = buckets.map((b) => b.amount);
      }

      if (chartTrend) {
        chartTrend.destroy();
        chartTrend = null;
      }
      const hasData = values.some((v) => v > 0) || (Array.isArray(revenueOverview) && revenueOverview.length > 0);
      if (!hasData) {
        if (canvas) canvas.style.display = "none";
        if (empty) empty.hidden = false;
        return;
      }
      if (canvas) canvas.style.display = "";
      if (empty) empty.hidden = true;
      if (!window.Chart || !canvas) return;
      chartTrend = new Chart(canvas, {
        type: "line",
        data: {
          labels,
          datasets: [{
            label: "Cọc đã Paid",
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
            y: {
              grid: { color: "#E2E8F0" },
              ticks: { callback: (v) => Number(v).toLocaleString("vi-VN") }
            }
          }
        }
      });
    }

    function paintActivities() {
      const act = document.getElementById("contractActivities");
      const empty = root.querySelector("[data-activities-empty]");
      if (!act) return;
      const sorted = [...contracts].sort((a, b) => {
        const ta = new Date(a.signedAt || a.createdAt || 0).getTime();
        const tb = new Date(b.signedAt || b.createdAt || 0).getTime();
        return tb - ta;
      }).slice(0, 8);

      if (!sorted.length) {
        act.innerHTML = "";
        if (empty) empty.hidden = false;
        return;
      }
      if (empty) empty.hidden = true;
      act.innerHTML = sorted.map((c) => {
        const icon = c.status === "Signed" ? "bi-pen"
          : c.status === "Voided" ? "bi-x-circle"
          : "bi-file-earmark-text";
        const title = c.status === "Signed" ? "Hợp đồng đã ký"
          : c.status === "Voided" ? "Hợp đồng đã vô hiệu"
          : "Hợp đồng đã phát hành";
        const detail = `${c.contractNumber || ("#" + c.contractId)} · ${c.customerName || "—"} · Đơn #${c.bookingId}`;
        const time = when(c.signedAt || c.createdAt);
        return `<li>
          <span class="dot"><i class="bi ${icon}"></i></span>
          <div><strong>${esc(title)}</strong><small class="aui-muted" style="display:block">${esc(detail)} · ${esc(time)}</small></div>
        </li>`;
      }).join("");
    }

    async function openPayment(row) {
      openDrawer({
        title: "Chi tiết giao dịch",
        subtitle: row.txnLabel,
        renderTab: () => `<div class="aui-empty"><i class="bi bi-hourglass-split"></i>Đang tải chi tiết giao dịch...</div>`,
        footerHtml: `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>`
      });

      let detail = {
        payment: row.payment,
        contract: row.contract,
        booking: row.booking,
        customerName: row.customerName
      };
      try {
        const res = await fetch(`/Admin/Payments?handler=Detail&id=${encodeURIComponent(row.payment.paymentId)}`, {
          headers: { Accept: "application/json" },
          credentials: "same-origin"
        });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.message || "Không thể tải chi tiết giao dịch.");
        detail = {
          payment: json.payment || row.payment,
          contract: json.contract || row.contract,
          booking: json.booking || row.booking,
          customerName: json.customerName || row.customerName
        };
      } catch (err) {
        toast(err.message || "Không thể tải chi tiết giao dịch.");
      }

      const p = detail.payment;
      const c = detail.contract;
      openDrawer({
        title: "Chi tiết giao dịch",
        subtitle: p.transactionRef || `PAY-${p.paymentId}`,
        renderTab: () => `
          <dl class="aui-kv">
            <div><dt>Mã giao dịch</dt><dd>${esc(p.transactionRef || ("PAY-" + p.paymentId))}</dd></div>
            <div><dt>Mã thanh toán</dt><dd>#${esc(p.paymentId)}</dd></div>
            <div><dt>Mã đơn thuê</dt><dd>#${esc(p.bookingId)}</dd></div>
            <div><dt>Khách hàng</dt><dd>${esc(detail.customerName || "—")}</dd></div>
            <div><dt>Loại thanh toán</dt><dd>${esc(t(p.paymentType) || p.paymentType || "—")}</dd></div>
            <div><dt>Số tiền</dt><dd>${esc(money(p.amount))}</dd></div>
            <div><dt>Phương thức</dt><dd>${esc(methodLabel(p.method))}</dd></div>
            <div><dt>Trạng thái thanh toán</dt><dd>${badge(p.status)}</dd></div>
            <div><dt>Thanh toán lúc</dt><dd>${esc(when(p.paidAt))}</dd></div>
            <div><dt>Tạo lúc</dt><dd>${esc(when(p.createdAt))}</dd></div>
            <div><dt>Trạng thái hợp đồng</dt><dd>${c ? badge(c.status) : "—"}</dd></div>
            ${c ? `<div><dt>Số hợp đồng</dt><dd>${esc(c.contractNumber || "—")}</dd></div>
            <div><dt>Ký lúc</dt><dd>${esc(when(c.signedAt))}</dd></div>` : ""}
          </dl>`,
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-toast="Hoàn tiền chưa được hỗ trợ.">Hoàn tiền</button>`
      });
    }

    function paint() {
      const list = filtered();
      const pg = paginate(list, page, pageSize);
      page = pg.page;
      if (!rows.length) {
        tbody.innerHTML = `<tr><td colspan="11"><div class="aui-empty"><i class="bi bi-inbox"></i>Chưa có giao dịch.</div></td></tr>`;
        pager.innerHTML = "";
        return;
      }
      tbody.innerHTML = pg.slice.map((r, i) => `
        <tr data-id="${esc(r.payment.paymentId)}">
          <td onclick="event.stopPropagation()"><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>${esc(r.txnLabel)}</strong></td>
          <td>${esc(r.customerName)}</td>
          <td>#${esc(r.payment.bookingId)}</td>
          <td>${esc(money(r.payment.amount))}</td>
          <td>${esc(methodLabel(r.payment.method))}</td>
          <td>${badge(r.payment.status)}</td>
          <td>${r.contractStatus ? badge(r.contractStatus) : "—"}</td>
          <td>${esc(when(r.dateIso))}</td>
          <td class="aui-actions" onclick="event.stopPropagation()">
            <button type="button" class="aui-icon-btn" data-view="${esc(r.payment.paymentId)}" title="Chi tiết"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("") || `<tr><td colspan="11"><div class="aui-empty"><i class="bi bi-inbox"></i>Không tìm thấy giao dịch phù hợp.</div></td></tr>`;

      tbody.querySelectorAll("tr[data-id]").forEach((tr) => {
        tr.addEventListener("click", () => {
          const row = rows.find((x) => String(x.payment.paymentId) === tr.getAttribute("data-id"));
          if (row) openPayment(row);
        });
      });
      tbody.querySelectorAll("[data-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const row = rows.find((x) => String(x.payment.paymentId) === btn.getAttribute("data-view"));
          if (row) openPayment(row);
        });
      });
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paint(); });
      bindSelectAll(root);
    }

    function refreshUi() {
      rebuildRows();
      setKpis();
      paintMethodsChart();
      paintTrendChart();
      paintActivities();
      page = 1;
      paint();
    }

    [q, payStatus, contract].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paint(); });
      el?.addEventListener("change", () => { page = 1; paint(); });
    });
    bindDateRange(root, () => { page = 1; paint(); });
    root.querySelector("[data-export]")?.addEventListener("click", () => toast("Xuất báo cáo chưa được hỗ trợ."));

    payments = parseJson("auiPaymentsPayload", []);
    contracts = parseJson("auiContractsPayload", []);
    bookings = parseJson("auiBookingsPayload", []);
    revenueOverview = parseJson("auiRevenueOverviewPayload", []);
    if (!Array.isArray(payments)) payments = [];
    if (!Array.isArray(contracts)) contracts = [];
    if (!Array.isArray(bookings)) bookings = [];
    if (!Array.isArray(revenueOverview)) revenueOverview = [];
    refreshUi();
  }
  /* Maintenance + Incidents */
  function initMaintenance() {
    const root = document.getElementById("auiMaintenance");
    if (!root) return;
    if (root.querySelector("[data-maint-error]")) return;

    const params = new URLSearchParams(location.search);
    let mainTab = params.get("tab") === "incidents" ? "incidents" : "maintenance";
    let subTab = "fleet";
    let chartInst = null;

    const seg = root.querySelector("[data-main-seg]");
    const panelMaint = root.querySelector("[data-panel-maintenance]");
    const panelInc = root.querySelector("[data-panel-incidents]");
    const subSeg = root.querySelector("[data-sub-seg]");

    const antiforgery = () =>
      document.querySelector("#auiMaintenanceAntiForgery input[name='__RequestVerificationToken']")?.value || "";

    const money = (n) => {
      if (n == null || n === "") return "—";
      const num = Number(n);
      if (Number.isNaN(num)) return "—";
      return num.toLocaleString("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 });
    };

    const dayOnly = (iso) => {
      if (!iso) return "—";
      const s = String(iso);
      if (/^\d{4}-\d{2}-\d{2}/.test(s)) {
        const [y, m, d] = s.slice(0, 10).split("-");
        return `${d}/${m}/${y}`;
      }
      const dt = new Date(s);
      return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("vi-VN");
    };

    const when = (iso) => {
      if (!iso) return "—";
      const dt = new Date(iso);
      return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleString("vi-VN");
    };

    const kmText = (n) => {
      if (n == null || n === "") return "—";
      const num = Number(n);
      return Number.isNaN(num) ? "—" : `${num.toLocaleString("vi-VN")} km`;
    };

    function parseJson(id, fallback) {
      const el = document.getElementById(id);
      if (!el) return fallback;
      try {
        const data = JSON.parse(el.textContent || "null");
        return data == null ? fallback : data;
      } catch (_) {
        return fallback;
      }
    }

    let vehicles = parseJson("auiMaintVehiclesPayload", []);
    let profiles = parseJson("auiMaintProfilesPayload", []);
    let alerts = parseJson("auiMaintAlertsPayload", []);
    let histories = parseJson("auiMaintHistoriesPayload", []);
    let incidents = parseJson("auiMaintIncidentsPayload", []);
    let bookings = parseJson("auiMaintBookingsPayload", []);
    if (!Array.isArray(vehicles)) vehicles = [];
    if (!Array.isArray(profiles)) profiles = [];
    if (!Array.isArray(alerts)) alerts = [];
    if (!Array.isArray(histories)) histories = [];
    if (!Array.isArray(incidents)) incidents = [];
    if (!Array.isArray(bookings)) bookings = [];

    let fleetRows = [];
    let scheduleRows = [];
    let historyRows = [];
    let incidentRows = [];

    function profileByVehicleId(id) {
      return profiles.find((p) => (p.vehicle?.vehicleId ?? p.vehicleId) === id) || null;
    }

    function plateForIncident(inc) {
      const b = bookings.find((x) => x.bookingId === inc.bookingId);
      return b?.assignedVehicle?.licensePlate
        || b?.assignment?.licensePlate
        || (b ? `Đơn #${b.bookingId}` : `Đơn #${inc.bookingId}`);
    }

    function rebuild() {
      const bookingById = new Map(bookings.map((b) => [b.bookingId, b]));
      fleetRows = vehicles.map((v) => {
        const p = profileByVehicleId(v.vehicleId);
        const open = p?.openMaintenance || null;
        const latest = p?.latestMaintenance || null;
        const status = p?.maintenanceStatus || (v.status === "Inactive" ? "NgungHoatDong" : "SanSang");
        return {
          vehicle: v,
          profile: p,
          status,
          statusLabel: p?.maintenanceStatusLabel || t(status),
          plate: v.licensePlate,
          model: `${v.brand || ""} ${v.model || ""}`.trim() || "—",
          typeName: v.typeName || "",
          km: v.currentKm,
          lastService: latest?.completedDate || latest?.scheduledDate || null,
          nextService: open?.scheduledDate || null,
          open,
          latest,
          alertReason: p?.maintenanceAlertReason || null
        };
      });

      scheduleRows = fleetRows
        .filter((r) => r.open)
        .map((r) => ({
          ...r,
          record: r.open
        }))
        .sort((a, b) => new Date(a.record.scheduledDate) - new Date(b.record.scheduledDate));

      const vehicleById = new Map(vehicles.map((v) => [v.vehicleId, v]));
      historyRows = histories.map((rec) => {
        const v = vehicleById.get(rec.vehicleId);
        return {
          plate: v?.licensePlate || `#${rec.vehicleId}`,
          model: v ? `${v.brand || ""} ${v.model || ""}`.trim() : "—",
          vehicleId: rec.vehicleId,
          record: rec
        };
      }).sort((a, b) => {
        const ta = new Date(a.record.completedDate || a.record.scheduledDate || 0).getTime();
        const tb = new Date(b.record.completedDate || b.record.scheduledDate || 0).getTime();
        return tb - ta;
      });

      incidentRows = incidents.map((inc) => {
        const b = bookingById.get(inc.bookingId);
        return {
          incident: inc,
          booking: b || null,
          plate: plateForIncident(inc),
          vehicleLabel: b?.assignedVehicle
            ? `${b.assignedVehicle.brand || ""} ${b.assignedVehicle.model || ""}`.trim()
            : (b?.vehicleTypeName || "—")
        };
      });
    }

    function setKpis() {
      const set = (key, val, asDash) => {
        const el = root.querySelector(`[data-kpi="${key}"]`);
        if (!el) return;
        if (asDash) {
          el.textContent = "—";
          return;
        }
        el.textContent = Number(val).toLocaleString("vi-VN");
      };
      const dueCount = fleetRows.filter((r) => r.status === "CanBaoTri").length;
      const activeInc = incidents.filter((i) => i.status === "Open").length;
      set("vehicles", vehicles.length, false);
      set("due", dueCount, false);
      set("activeIncidents", activeInc, false);
      set("resolvedMonth", 0, true);

      const setStat = (key, val) => {
        const el = root.querySelector(`[data-inc-stat="${key}"]`);
        if (el) el.textContent = val;
      };
      setStat("active", activeInc.toLocaleString("vi-VN"));
      setStat("resolved", "—");
      setStat("avg", "—");
      setStat("delta", "—");
    }

    function fillTypeFilter() {
      const sel = root.querySelector("[data-maint-type]");
      if (!sel) return;
      const cur = sel.value;
      const names = [...new Set(vehicles.map((v) => v.typeName).filter(Boolean))].sort();
      sel.innerHTML = `<option value="">Tất cả loại xe</option>` +
        names.map((n) => `<option value="${esc(n)}">${esc(n)}</option>`).join("");
      if (cur && names.includes(cur)) sel.value = cur;
    }

    function switchMain(tab) {
      mainTab = tab;
      seg?.querySelectorAll("button").forEach((b) => b.classList.toggle("is-on", b.getAttribute("data-tab") === tab));
      if (panelMaint) panelMaint.hidden = tab !== "maintenance";
      if (panelInc) panelInc.hidden = tab !== "incidents";
    }

    function switchSub(tab) {
      subTab = tab;
      subSeg?.querySelectorAll("button").forEach((b) => b.classList.toggle("is-on", b.getAttribute("data-sub") === tab));
      root.querySelectorAll("[data-sub-panel]").forEach((p) => {
        p.hidden = p.getAttribute("data-sub-panel") !== tab;
      });
    }

    seg?.querySelectorAll("button").forEach((b) => {
      b.addEventListener("click", () => switchMain(b.getAttribute("data-tab")));
    });
    subSeg?.querySelectorAll("button").forEach((b) => {
      b.addEventListener("click", () => switchSub(b.getAttribute("data-sub")));
    });
    switchMain(mainTab);
    switchSub(subTab);

    let page = 1;
    const pageSize = 8;
    const q = root.querySelector("[data-maint-q]");
    const status = root.querySelector("[data-maint-status]");
    const type = root.querySelector("[data-maint-type]");
    const tbody = root.querySelector("[data-maint-body]");
    const pager = root.querySelector("[data-maint-pager]");
    const scheduleBody = root.querySelector("[data-schedule-body]");
    const historyBody = root.querySelector("[data-history-body]");

    function filteredMaint() {
      const qq = (q?.value || "").toLowerCase().trim();
      return fleetRows.filter((v) => {
        if (status?.value && v.status !== status.value) return false;
        if (type?.value && v.typeName !== type.value) return false;
        if (!qq) return true;
        return [v.plate, v.model, v.typeName].join(" ").toLowerCase().includes(qq);
      });
    }

    function paintChart() {
      const canvas = document.getElementById("chartMaintStatus");
      if (chartInst) {
        chartInst.destroy();
        chartInst = null;
      }
      const counts = {
        SanSang: fleetRows.filter((r) => r.status === "SanSang").length,
        CanBaoTri: fleetRows.filter((r) => r.status === "CanBaoTri").length,
        DangBaoTri: fleetRows.filter((r) => r.status === "DangBaoTri").length,
        NgungHoatDong: fleetRows.filter((r) => r.status === "NgungHoatDong").length
      };
      const data = [
        { label: "Sẵn sàng", value: counts.SanSang, color: "#16A34A" },
        { label: "Cần bảo trì", value: counts.CanBaoTri, color: "#D97706" },
        { label: "Đang bảo trì", value: counts.DangBaoTri, color: "#2563EB" },
        { label: "Ngừng hoạt động", value: counts.NgungHoatDong, color: "#64748B" }
      ];
      if (!window.Chart || !canvas) return;
      chartInst = new Chart(canvas, {
        type: "doughnut",
        data: {
          labels: data.map((d) => d.label),
          datasets: [{
            data: data.map((d) => d.value),
            backgroundColor: data.map((d) => d.color),
            borderWidth: 0
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          cutout: "68%"
        }
      });
    }

    function paintUpcoming() {
      const upcoming = document.getElementById("upcomingServices");
      if (!upcoming) return;
      if (!scheduleRows.length) {
        upcoming.innerHTML = `<div class="aui-empty" style="padding:1rem"><p>Chưa có lịch bảo dưỡng.</p></div>`;
        return;
      }
      upcoming.innerHTML = scheduleRows.slice(0, 8).map((s) => `
        <div class="aui-vehicle-row">
          <div class="aui-vehicle-thumb"><i class="bi bi-wrench"></i></div>
          <div class="aui-vehicle-meta">
            <strong>${esc(s.model)} · ${esc(s.plate)}</strong>
            <small>${esc(dayOnly(s.record.scheduledDate))} · ${esc(t(s.record.maintenanceType))}</small>
          </div>
        </div>`).join("");
    }

    function paintSchedule() {
      if (!scheduleBody) return;
      if (!scheduleRows.length) {
        scheduleBody.innerHTML = `<tr><td colspan="7"><div class="aui-empty"><p>Chưa có lịch bảo dưỡng.</p></div></td></tr>`;
        return;
      }
      scheduleBody.innerHTML = scheduleRows.map((s, i) => `
        <tr>
          <td>${i + 1}</td>
          <td><strong>${esc(s.plate)}</strong></td>
          <td>${esc(t(s.record.maintenanceType))}</td>
          <td>${esc(dayOnly(s.record.scheduledDate))}</td>
          <td>${esc(money(s.record.cost))}</td>
          <td>${esc(s.record.notes || "—")}</td>
          <td class="aui-actions">
            <button type="button" class="aui-icon-btn" data-open-vehicle="${s.vehicle.vehicleId}"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("");
      scheduleBody.querySelectorAll("[data-open-vehicle]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const row = fleetRows.find((r) => String(r.vehicle.vehicleId) === btn.getAttribute("data-open-vehicle"));
          if (row) openVehicleDetail(row);
        });
      });
    }

    function paintHistory() {
      if (!historyBody) return;
      const completed = historyRows.filter((h) => h.record.completedDate);
      if (!completed.length) {
        historyBody.innerHTML = `<tr><td colspan="7"><div class="aui-empty"><p>Chưa có lịch sử bảo dưỡng.</p></div></td></tr>`;
        return;
      }
      historyBody.innerHTML = completed.map((h, i) => `
        <tr>
          <td>${i + 1}</td>
          <td><strong>${esc(h.plate)}</strong></td>
          <td>${esc(t(h.record.maintenanceType))}</td>
          <td>${esc(dayOnly(h.record.scheduledDate))}</td>
          <td>${esc(dayOnly(h.record.completedDate))}</td>
          <td>${esc(money(h.record.cost))}</td>
          <td>${badge("Completed")}</td>
        </tr>`).join("");
    }

    function paintMaint() {
      if (!tbody) return;
      const rows = filteredMaint();
      if (!rows.length) {
        tbody.innerHTML = `<tr><td colspan="9"><div class="aui-empty"><p>Chưa có dữ liệu bảo dưỡng.</p></div></td></tr>`;
        if (pager) pager.innerHTML = "";
        return;
      }
      const pg = paginate(rows, page, pageSize);
      page = pg.page;
      tbody.innerHTML = pg.slice.map((v, i) => `
        <tr>
          <td><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td><strong>${esc(v.plate)}</strong></td>
          <td>${esc(v.model)}</td>
          <td>${esc(kmText(v.km))}</td>
          <td>${esc(dayOnly(v.lastService))}</td>
          <td>${esc(dayOnly(v.nextService))}</td>
          <td>${badge(v.status)}</td>
          <td class="aui-actions">
            <button type="button" class="aui-icon-btn" data-open-vehicle="${v.vehicle.vehicleId}"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`).join("");
      renderPager(pager, { ...pg, pageSize }, (n) => { page = n; paintMaint(); });
      bindSelectAll(panelMaint || root);
      tbody.querySelectorAll("[data-open-vehicle]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const row = fleetRows.find((r) => String(r.vehicle.vehicleId) === btn.getAttribute("data-open-vehicle"));
          if (row) openVehicleDetail(row);
        });
      });
    }

    async function postJson(url, body) {
      const res = await fetch(url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          RequestVerificationToken: antiforgery()
        },
        body: JSON.stringify(body),
        credentials: "same-origin"
      });
      const json = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(json.message || "Thao tác thất bại.");
      return json;
    }

    async function reloadAll() {
      const res = await fetch("/Admin/Maintenance?handler=List", {
        headers: { Accept: "application/json" },
        credentials: "same-origin"
      });
      if (!res.ok) throw new Error("Không thể tải dữ liệu bảo dưỡng.");
      const data = await res.json();
      vehicles = Array.isArray(data.vehicles) ? data.vehicles : [];
      profiles = Array.isArray(data.profiles) ? data.profiles : [];
      alerts = Array.isArray(data.alerts) ? data.alerts : [];
      histories = Array.isArray(data.histories) ? data.histories : [];
      incidents = Array.isArray(data.incidents) ? data.incidents : [];
      bookings = Array.isArray(data.bookings) ? data.bookings : [];
      rebuild();
      fillTypeFilter();
      setKpis();
      paintChart();
      paintUpcoming();
      paintSchedule();
      paintHistory();
      page = 1;
      paintMaint();
      ipage = 1;
      paintInc();
    }

    function openCreateService() {
      const options = vehicles.map((v) =>
        `<option value="${v.vehicleId}">${esc(v.licensePlate)} · ${esc((v.brand || "") + " " + (v.model || "")).trim()}</option>`
      ).join("");
      openDrawer({
        title: "Tạo phiếu bảo dưỡng",
        subtitle: "API bảo dưỡng xe",
        renderTab: () => `
          <div class="aui-form-grid">
            <div class="aui-field"><label>Xe</label>
              <select class="aui-select" data-mf="vehicleId"><option value="">Chọn xe</option>${options}</select>
            </div>
            <div class="aui-field"><label>Loại bảo dưỡng</label>
              <select class="aui-select" data-mf="maintenanceType">
                <option value="Scheduled">Định kỳ</option>
                <option value="Repair">Sửa chữa</option>
                <option value="Inspection">Kiểm tra</option>
              </select>
            </div>
            <div class="aui-field"><label>Ngày dự kiến</label>
              <input class="aui-input" type="date" data-mf="scheduledDate" />
            </div>
            <div class="aui-field"><label>Chi phí (tuỳ chọn)</label>
              <input class="aui-input" type="number" min="0" step="1000" data-mf="cost" placeholder="VND" />
            </div>
            <div class="aui-field" style="grid-column:1/-1"><label>Ghi chú</label>
              <textarea class="aui-input" rows="3" data-mf="notes" maxlength="500"></textarea>
            </div>
          </div>`,
        footerHtml: `
          <button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Huỷ</button>
          <button type="button" class="aui-btn aui-btn-primary" data-mf-save>Tạo phiếu</button>`,
        bindFooter: (foot) => {
          foot.querySelector("[data-mf-save]")?.addEventListener("click", () => {
            (async () => {
              try {
                const g = (n) => document.querySelector(`#auiDrawer [data-mf="${n}"]`);
                const vehicleId = Number(g("vehicleId")?.value || 0);
                const maintenanceType = g("maintenanceType")?.value || "";
                const scheduledDate = g("scheduledDate")?.value || "";
                const costRaw = (g("cost")?.value || "").trim();
                const notes = (g("notes")?.value || "").trim() || null;
                if (!vehicleId || !maintenanceType || !scheduledDate) {
                  toast("Vui lòng chọn xe, loại và ngày dự kiến.");
                  return;
                }
                const body = {
                  vehicleId,
                  maintenanceType,
                  scheduledDate: new Date(scheduledDate + "T00:00:00").toISOString(),
                  odometerAtMaintenance: null,
                  cost: costRaw === "" ? null : Number(costRaw),
                  notes
                };
                await postJson("/Admin/Maintenance?handler=Create", body);
                toast("Đã tạo phiếu bảo dưỡng.");
                closeDrawer();
                await reloadAll();
              } catch (err) {
                toast(err.message || "Không thể tạo phiếu bảo dưỡng.");
              }
            })();
          });
        }
      });
    }

    async function openVehicleDetail(row) {
      openDrawer({
        title: "Chi tiết bảo dưỡng",
        subtitle: row.plate,
        renderTab: () => `<div class="aui-empty"><i class="bi bi-hourglass-split"></i>Đang tải chi tiết...</div>`,
        footerHtml: `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>`
      });

      let detail = { profile: row.profile, history: [] };
      try {
        const res = await fetch(`/Admin/Maintenance?handler=VehicleDetail&id=${encodeURIComponent(row.vehicle.vehicleId)}`, {
          headers: { Accept: "application/json" },
          credentials: "same-origin"
        });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.message || "Không thể tải chi tiết.");
        detail = json;
      } catch (err) {
        toast(err.message || "Không thể tải chi tiết.");
      }

      const p = detail.profile || row.profile;
      const v = p?.vehicle || row.vehicle;
      const open = p?.openMaintenance || null;
      const history = Array.isArray(detail.history) ? detail.history : [];
      const historyHtml = history.length
        ? `<table class="aui-table"><thead><tr><th>Loại</th><th>Dự kiến</th><th>Hoàn thành</th><th>Chi phí</th></tr></thead><tbody>
            ${history.map((h) => `<tr>
              <td>${esc(t(h.maintenanceType))}</td>
              <td>${esc(dayOnly(h.scheduledDate))}</td>
              <td>${esc(dayOnly(h.completedDate))}</td>
              <td>${esc(money(h.cost))}</td>
            </tr>`).join("")}
          </tbody></table>`
        : `<div class="aui-empty"><p>Chưa có lịch sử bảo dưỡng.</p></div>`;

      openDrawer({
        title: "Chi tiết bảo dưỡng",
        subtitle: v.licensePlate || row.plate,
        tabs: [
          { id: "overview", label: "Tổng quan" },
          { id: "history", label: "Lịch sử" }
        ],
        renderTab: (id) => {
          if (id === "history") return historyHtml;
          return `
            <dl class="aui-kv">
              <div><dt>Biển số</dt><dd>${esc(v.licensePlate || row.plate)}</dd></div>
              <div><dt>Mẫu xe</dt><dd>${esc(`${v.brand || ""} ${v.model || ""}`.trim() || row.model)}</dd></div>
              <div><dt>Số KM</dt><dd>${esc(kmText(v.currentKm ?? row.km))}</dd></div>
              <div><dt>Trạng thái bảo trì</dt><dd>${badge(p?.maintenanceStatus || row.status)}</dd></div>
              <div><dt>Cảnh báo</dt><dd>${esc(p?.maintenanceAlertReason || "—")}</dd></div>
              <div><dt>Phiếu đang mở</dt><dd>${open ? esc(t(open.maintenanceType) + " · " + dayOnly(open.scheduledDate)) : "Không"}</dd></div>
              <div><dt>Chi phí phiếu mở</dt><dd>${open ? esc(money(open.cost)) : "—"}</dd></div>
              <div><dt>Ghi chú</dt><dd>${esc(open?.notes || "—")}</dd></div>
            </dl>`;
        },
        footerHtml: open
          ? `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>
             <button type="button" class="aui-btn aui-btn-primary" data-complete-maint>Hoàn thành bảo dưỡng</button>`
          : `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>`,
        bindFooter: (foot) => {
          foot.querySelector("[data-complete-maint]")?.addEventListener("click", () => {
            if (!open) return;
            (async () => {
              try {
                await postJson(
                  `/Admin/Maintenance?handler=Complete&vehicleId=${encodeURIComponent(v.vehicleId)}&maintenanceId=${encodeURIComponent(open.maintenanceId)}`,
                  { odometerAtMaintenance: null }
                );
                toast("Đã hoàn thành bảo dưỡng.");
                closeDrawer();
                await reloadAll();
              } catch (err) {
                toast(err.message || "Không thể hoàn thành bảo dưỡng.");
              }
            })();
          });
        }
      });
    }

    [q, status, type].forEach((el) => {
      el?.addEventListener("input", () => { page = 1; paintMaint(); });
      el?.addEventListener("change", () => { page = 1; paintMaint(); });
    });
    root.querySelector("[data-create-service]")?.addEventListener("click", openCreateService);

    /* incidents */
    let ipage = 1;
    const iq = root.querySelector("[data-inc-q]");
    const ist = root.querySelector("[data-inc-status]");
    const ibody = root.querySelector("[data-inc-body]");
    const ipager = root.querySelector("[data-inc-pager]");

    function filteredInc() {
      const qq = (iq?.value || "").toLowerCase().trim();
      return incidentRows.filter((x) => {
        const inc = x.incident;
        if (ist?.value && inc.status !== ist.value) return false;
        if (!qq) return true;
        return [inc.incidentId, x.plate, x.vehicleLabel, inc.incidentType, inc.description, inc.driverName]
          .join(" ").toLowerCase().includes(qq);
      });
    }

    function paintInc() {
      if (!ibody) return;
      const rows = filteredInc();
      if (!rows.length) {
        ibody.innerHTML = `<tr><td colspan="8"><div class="aui-empty"><p>Chưa có sự cố.</p></div></td></tr>`;
        if (ipager) ipager.innerHTML = "";
        return;
      }
      const pg = paginate(rows, ipage, pageSize);
      ipage = pg.page;
      ibody.innerHTML = pg.slice.map((x, i) => {
        const inc = x.incident;
        return `
        <tr>
          <td><input type="checkbox" data-row-check /></td>
          <td>${(pg.page - 1) * pageSize + i + 1}</td>
          <td>${esc(when(inc.occurredAt))}</td>
          <td>${esc(x.plate)}</td>
          <td>${esc(t(inc.incidentType))}</td>
          <td><span class="aui-muted">—</span></td>
          <td>${badge(inc.status)}</td>
          <td class="aui-actions">
            <button type="button" class="aui-icon-btn" data-open-inc="${inc.incidentId}"><i class="bi bi-eye"></i></button>
          </td>
        </tr>`;
      }).join("");
      renderPager(ipager, { ...pg, pageSize }, (n) => { ipage = n; paintInc(); });
      bindSelectAll(panelInc || root);
      ibody.querySelectorAll("[data-open-inc]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const row = incidentRows.find((r) => String(r.incident.incidentId) === btn.getAttribute("data-open-inc"));
          if (row) openIncidentDetail(row);
        });
      });
    }

    async function openIncidentDetail(row) {
      openDrawer({
        title: "Chi tiết sự cố",
        subtitle: `#${row.incident.incidentId}`,
        renderTab: () => `<div class="aui-empty"><i class="bi bi-hourglass-split"></i>Đang tải chi tiết...</div>`,
        footerHtml: `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>`
      });

      let detail = {
        incident: row.incident,
        licensePlate: row.plate,
        vehicleModel: row.vehicleLabel,
        booking: row.booking
      };
      try {
        const res = await fetch(`/Admin/Maintenance?handler=IncidentDetail&id=${encodeURIComponent(row.incident.incidentId)}`, {
          headers: { Accept: "application/json" },
          credentials: "same-origin"
        });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.message || "Không thể tải chi tiết sự cố.");
        detail = {
          incident: json.incident || row.incident,
          licensePlate: json.licensePlate || row.plate,
          vehicleModel: json.vehicleModel || row.vehicleLabel,
          booking: json.booking || row.booking
        };
      } catch (err) {
        toast(err.message || "Không thể tải chi tiết sự cố.");
      }

      const inc = detail.incident;
      openDrawer({
        title: "Chi tiết sự cố",
        subtitle: `#${inc.incidentId}`,
        renderTab: () => `
          <dl class="aui-kv">
            <div><dt>Mã sự cố</dt><dd>#${esc(inc.incidentId)}</dd></div>
            <div><dt>Đơn thuê</dt><dd>#${esc(inc.bookingId)}</dd></div>
            <div><dt>Xe</dt><dd>${esc(detail.licensePlate || "—")}</dd></div>
            <div><dt>Mẫu / loại</dt><dd>${esc(detail.vehicleModel || "—")}</dd></div>
            <div><dt>Tài xế</dt><dd>${esc(inc.driverName || "—")}</dd></div>
            <div><dt>Loại</dt><dd>${esc(t(inc.incidentType))}</dd></div>
            <div><dt>Mức độ</dt><dd>—</dd></div>
            <div><dt>Trạng thái</dt><dd>${badge(inc.status)}</dd></div>
            <div><dt>Thời điểm</dt><dd>${esc(when(inc.occurredAt))}</dd></div>
            <div><dt>Tạo lúc</dt><dd>${esc(when(inc.createdAt))}</dd></div>
            <div><dt>Mô tả</dt><dd>${esc(inc.description || "—")}</dd></div>
          </dl>
          <p class="aui-muted" style="margin-top:1rem">Hành động giải quyết/đóng: chưa hỗ trợ trên backend.</p>`,
        footerHtml: `<button type="button" class="aui-btn aui-btn-ghost" data-aui-drawer-close>Đóng</button>`
      });
    }

    [iq, ist].forEach((el) => {
      el?.addEventListener("input", () => { ipage = 1; paintInc(); });
      el?.addEventListener("change", () => { ipage = 1; paintInc(); });
    });
    root.querySelector("[data-report-incident]")?.addEventListener("click", () => toast("Chưa hỗ trợ."));

    rebuild();
    fillTypeFilter();
    setKpis();
    paintChart();
    paintUpcoming();
    paintSchedule();
    paintHistory();
    paintMaint();
    paintInc();
  }

  document.addEventListener("DOMContentLoaded", () => {
    bindAllDateRanges();
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

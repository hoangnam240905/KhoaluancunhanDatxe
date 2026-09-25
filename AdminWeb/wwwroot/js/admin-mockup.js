(() => {
  const M = window.AdminMock;
  if (!M) return;

  const toastEl = document.getElementById("amToast");
  const drawer = document.getElementById("amDrawer");
  const drawerBody = document.getElementById("amDrawerBody");
  const drawerFoot = document.getElementById("amDrawerFoot");
  const drawerTitle = document.getElementById("amDrawerTitle");
  const drawerSub = document.getElementById("amDrawerSub");
  const modal = document.getElementById("amModal");
  const modalTitle = document.getElementById("amModalTitle");
  const modalBody = document.getElementById("amModalBody");
  const modalConfirm = document.getElementById("amModalConfirm");

  const toast = (msg) => {
    if (!toastEl) return;
    toastEl.textContent = msg;
    toastEl.hidden = false;
    window.clearTimeout(toast._t);
    toast._t = window.setTimeout(() => { toastEl.hidden = true; }, 2600);
  };

  const openDrawer = ({ title, sub, bodyHtml, footHtml }) => {
    if (!drawer) return;
    drawerTitle.textContent = title || "Chi tiết";
    drawerSub.textContent = sub || "";
    drawerBody.innerHTML = bodyHtml || "";
    drawerFoot.innerHTML = footHtml || "";
    document.body.classList.add("am-drawer-open");
    drawer.setAttribute("aria-hidden", "false");
  };

  const closeDrawer = () => {
    document.body.classList.remove("am-drawer-open");
    drawer?.setAttribute("aria-hidden", "true");
  };

  let modalAction = null;
  const openModal = ({ title, bodyHtml, confirmText, onConfirm }) => {
    if (!modal) return;
    modalTitle.textContent = title || "Thao tác";
    modalBody.innerHTML = bodyHtml || "";
    modalConfirm.textContent = confirmText || "Xác nhận";
    modalAction = onConfirm || null;
    modal.hidden = false;
  };
  const closeModal = () => { if (modal) modal.hidden = true; modalAction = null; };

  document.querySelector("[data-am-toggle]")?.addEventListener("click", () => document.body.classList.toggle("am-nav-open"));
  document.querySelector("[data-am-backdrop]")?.addEventListener("click", () => document.body.classList.remove("am-nav-open"));
  document.querySelector("[data-am-drawer-close]")?.addEventListener("click", closeDrawer);
  document.querySelector("[data-am-drawer-backdrop]")?.addEventListener("click", closeDrawer);
  document.querySelectorAll("[data-am-modal-close]").forEach((el) => el.addEventListener("click", closeModal));
  modalConfirm?.addEventListener("click", () => {
    if (typeof modalAction === "function") modalAction();
    closeModal();
  });
  document.querySelectorAll("[data-am-toast]").forEach((btn) => {
    btn.addEventListener("click", () => toast(btn.getAttribute("data-am-toast") || "Thao tác mô phỏng."));
  });

  const paginate = (rows, page, pageSize) => {
    const total = rows.length;
    const pages = Math.max(1, Math.ceil(total / pageSize));
    const safe = Math.min(Math.max(1, page), pages);
    const start = (safe - 1) * pageSize;
    return { page: safe, pages, total, slice: rows.slice(start, start + pageSize) };
  };

  const renderPager = (host, state, onChange) => {
    if (!host) return;
    const buttons = [];
    buttons.push(`<button type="button" class="am-page-btn" data-p="prev" ${state.page <= 1 ? "disabled" : ""}>Trước</button>`);
    for (let i = 1; i <= state.pages; i++) {
      buttons.push(`<button type="button" class="am-page-btn ${i === state.page ? "is-on" : ""}" data-p="${i}">${i}</button>`);
    }
    buttons.push(`<button type="button" class="am-page-btn" data-p="next" ${state.page >= state.pages ? "disabled" : ""}>Sau</button>`);
    host.innerHTML = `
      <div class="am-muted">Hiển thị ${state.slice.length}/${state.total} bản ghi</div>
      <div class="am-pager-pages">${buttons.join("")}</div>`;
    host.querySelectorAll("[data-p]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const v = btn.getAttribute("data-p");
        let next = state.page;
        if (v === "prev") next -= 1;
        else if (v === "next") next += 1;
        else next = Number(v);
        onChange(next);
      });
    });
  };

  const bookingActions = (b) => {
    const bits = [`<button type="button" class="am-btn am-btn-ghost am-btn-sm" data-act="detail" data-id="${b.id}">Xem chi tiết</button>`];
    if (b.status === "Pending") {
      bits.push(`<button type="button" class="am-btn am-btn-success am-btn-sm" data-act="confirm" data-id="${b.id}">Xác nhận đơn</button>`);
      bits.push(`<button type="button" class="am-btn am-btn-danger-outline am-btn-sm" data-act="reject" data-id="${b.id}">Không duyệt</button>`);
    }
    if (b.status === "Confirmed" || b.status === "Assigned") {
      bits.push(`<button type="button" class="am-btn am-btn-info am-btn-sm" data-act="handover" data-id="${b.id}">Giao xe</button>`);
    }
    if (b.status !== "Completed" && b.status !== "Cancelled") {
      bits.push(`<button type="button" class="am-btn am-btn-danger am-btn-sm" data-act="cancel" data-id="${b.id}">Hủy</button>`);
    }
    return bits.join("");
  };

  const bookingDrawer = (b) => {
    const reasonBlock = b.status === "Cancelled"
      ? `<div class="am-section"><h3>Hủy đơn</h3>
          <dl class="am-kv"><dt>Trạng thái</dt><dd>${M.badgeHtml("bookingStatus", b.status)}</dd>
          <dt>Lý do hủy</dt><dd>${b.cancelReason ? b.cancelReason : "Không có lý do hủy được ghi nhận."}</dd></dl></div>`
      : "";
    openDrawer({
      title: `Đơn ${b.id}`,
      sub: M.labels.bookingStatus[b.status] || b.status,
      bodyHtml: `
        <div class="am-section"><h3>Thông tin chung</h3>
          <dl class="am-kv">
            <dt>Mã đơn</dt><dd class="am-strong">${b.id}</dd>
            <dt>Hình thức</dt><dd>${M.labels.mode[b.mode]}</dd>
            <dt>Thời gian</dt><dd>${b.start} → ${b.end}</dd>
            <dt>Tổng tiền</dt><dd>${M.money(b.amount)}</dd>
            <dt>Trạng thái</dt><dd>${M.badgeHtml("bookingStatus", b.status)}</dd>
          </dl>
        </div>
        <div class="am-section"><h3>Khách hàng</h3>
          <dl class="am-kv"><dt>Họ tên</dt><dd>${b.customer}</dd><dt>SĐT</dt><dd>${b.phone}</dd></dl>
        </div>
        <div class="am-section"><h3>Xe</h3>
          <dl class="am-kv"><dt>Loại xe</dt><dd>${b.vehicleType}</dd><dt>Xe</dt><dd>${b.vehicle}</dd></dl>
        </div>
        <div class="am-section"><h3>Tài xế</h3>
          <dl class="am-kv"><dt>Tài xế</dt><dd>${b.driver || "Chưa phân công"}</dd></dl>
        </div>
        <div class="am-section"><h3>Thanh toán & hợp đồng</h3>
          <dl class="am-kv">
            <dt>Tiền cọc</dt><dd>${M.badgeHtml("deposit", b.deposit)}</dd>
            <dt>Hợp đồng</dt><dd>${M.badgeHtml("contract", b.contract)}</dd>
          </dl>
        </div>${reasonBlock}`,
      footHtml: bookingActions(b)
    });
  };

  /* ---------- Dashboard ---------- */
  const initDashboard = () => {
    const root = document.querySelector("[data-am-dashboard]");
    if (!root) return;
    const kpis = [
      { label: "Tổng số xe", value: M.vehicles.length, tip: "Mock" },
      { label: "Xe sẵn sàng", value: M.vehicles.filter(v => v.status === "Available").length, tip: "Mock" },
      { label: "Xe đang thuê", value: M.vehicles.filter(v => v.status === "Rented").length, tip: "Mock" },
      { label: "Tổng khách hàng", value: M.customers.length, tip: "Mock" },
      { label: "Booking đang hoạt động", value: M.bookings.filter(b => ["Assigned", "InProgress", "Confirmed"].includes(b.status)).length, tip: "Mock" },
      { label: "Booking chờ xử lý", value: M.bookings.filter(b => b.status === "Pending").length, tip: "Mock" },
      { label: "Thanh toán đã Paid", value: M.payments.filter(p => p.status === "Paid").length, tip: "Mock" },
      { label: "Doanh thu cọc (mock)", value: M.money(M.payments.filter(p => p.status === "Paid").reduce((s, p) => s + p.amount, 0)), tip: "Không phải doanh thu thật" }
    ];
    root.querySelector("[data-am-kpis]").innerHTML = kpis.map((k, i) => `
      <article class="am-kpi">
        <div class="am-kpi-icon"><i class="bi ${["bi-truck","bi-check-circle","bi-arrow-repeat","bi-people","bi-activity","bi-hourglass-split","bi-credit-card","bi-cash-coin"][i]}"></i></div>
        <small>${k.label}</small><strong>${k.value}</strong>
        <div class="am-kpi-foot">${k.tip}</div>
      </article>`).join("");

    const recent = M.bookings.slice(0, 5);
    root.querySelector("[data-am-recent]").innerHTML = recent.map(b => `
      <div class="am-list-item">
        <div class="am-stack"><span class="am-strong">${b.id}</span><span class="am-muted">${b.customer} · ${M.labels.mode[b.mode]}</span></div>
        ${M.badgeHtml("bookingStatus", b.status)}
      </div>`).join("");

    // Simple CSS bars instead of external chart dependency if Chart.js missing
    const trendHost = root.querySelector("[data-am-trend]");
    const trend = [12, 18, 15, 22, 19, 25, 21];
    const max = Math.max(...trend);
    trendHost.innerHTML = `<div style="display:flex;align-items:flex-end;gap:10px;height:180px;padding-top:8px">
      ${trend.map((v, i) => `<div style="flex:1;text-align:center">
        <div style="height:${Math.round(v / max * 140)}px;background:linear-gradient(180deg,#60a5fa,#2563eb);border-radius:8px 8px 4px 4px"></div>
        <small class="am-muted">T${i + 1}</small>
      </div>`).join("")}
    </div>`;

    const statusHost = root.querySelector("[data-am-status-bars]");
    const counts = ["Pending", "Confirmed", "Assigned", "InProgress", "Completed", "Cancelled"]
      .map(s => ({ s, n: M.bookings.filter(b => b.status === s).length }));
    const cmax = Math.max(...counts.map(c => c.n), 1);
    statusHost.innerHTML = counts.map(c => `
      <div style="display:grid;grid-template-columns:110px 1fr 28px;gap:8px;align-items:center;margin-bottom:8px">
        <span class="am-muted">${M.labels.bookingStatus[c.s]}</span>
        <div style="background:#e2e8f0;border-radius:999px;height:8px;overflow:hidden"><div style="width:${(c.n / cmax) * 100}%;height:100%;background:#2563eb"></div></div>
        <strong>${c.n}</strong>
      </div>`).join("");

    const fleetHost = root.querySelector("[data-am-fleet]");
    const fleet = ["Available", "Rented", "Maintenance", "Inactive"]
      .map(s => ({ s, n: M.vehicles.filter(v => v.status === s).length }));
    fleetHost.innerHTML = fleet.map(f => `
      <div class="am-list-item">
        <span>${M.labels.vehicle[f.s]}</span>
        <strong>${f.n}</strong>
      </div>`).join("");
  };

  /* ---------- Bookings ---------- */
  const initBookings = () => {
    const root = document.querySelector("[data-am-bookings]");
    if (!root) return;
    let page = 1;
    const pageSize = 6;
    const form = root.querySelector("[data-am-filter]");
    const tbody = root.querySelector("[data-am-tbody]");
    const pager = root.querySelector("[data-am-pager]");
    const tabs = root.querySelector("[data-am-tabs]");
    let tab = "all";

    const kpis = [
      { key: "Pending", label: "Chờ xác nhận" },
      { key: "Confirmed", label: "Đã xác nhận" },
      { key: "Assigned", label: "Cần phân công" },
      { key: "InProgress", label: "Đang thực hiện" }
    ];
    root.querySelector("[data-am-kpis]").innerHTML = kpis.map(k => `
      <button type="button" class="am-kpi is-clickable" data-kpi="${k.key}">
        <small>${k.label}</small>
        <strong>${M.bookings.filter(b => b.status === k.key).length}</strong>
      </button>`).join("");

    const statuses = ["all", "Pending", "Confirmed", "Assigned", "InProgress", "Completed", "Cancelled"];
    tabs.innerHTML = statuses.map(s => `
      <button type="button" class="am-tab ${tab === s ? "is-on" : ""}" data-tab="${s}">
        ${s === "all" ? "Tất cả" : M.labels.bookingStatus[s]}
      </button>`).join("");

    const filtered = () => {
      const fd = new FormData(form);
      const q = String(fd.get("q") || "").trim().toLowerCase();
      const status = String(fd.get("status") || "");
      const mode = String(fd.get("mode") || "");
      const type = String(fd.get("type") || "");
      return M.bookings.filter(b => {
        if (tab !== "all" && b.status !== tab) return false;
        if (status && b.status !== status) return false;
        if (mode && b.mode !== mode) return false;
        if (type && b.vehicleType !== type) return false;
        if (q && !(b.id + b.customer).toLowerCase().includes(q)) return false;
        return true;
      });
    };

    const render = () => {
      const rows = filtered();
      const state = paginate(rows, page, pageSize);
      page = state.page;
      tbody.innerHTML = state.slice.map(b => `
        <tr>
          <td class="am-strong">${b.id}</td>
          <td><div class="am-stack"><span class="am-strong">${b.customer}</span><span class="am-muted">${b.phone}</span></div></td>
          <td>${M.labels.mode[b.mode]}</td>
          <td>${b.vehicleType}<div class="am-muted">${b.vehicle}</div></td>
          <td><div class="am-stack"><span>${b.start}</span><span class="am-muted">${b.end}</span></div></td>
          <td>${M.badgeHtml("contract", b.contract)}</td>
          <td>${M.badgeHtml("deposit", b.deposit)}</td>
          <td>${M.badgeHtml("bookingStatus", b.status)}</td>
          <td><div class="am-actions">${bookingActions(b)}</div></td>
        </tr>`).join("") || `<tr><td colspan="9"><div class="am-empty">Không có đơn phù hợp bộ lọc.</div></td></tr>`;
      renderPager(pager, state, (p) => { page = p; render(); });
      tabs.querySelectorAll("[data-tab]").forEach(el => el.classList.toggle("is-on", el.getAttribute("data-tab") === tab));
    };

    form.addEventListener("submit", (e) => { e.preventDefault(); page = 1; render(); });
    form.addEventListener("reset", () => { window.setTimeout(() => { tab = "all"; page = 1; render(); }, 0); });
    tabs.addEventListener("click", (e) => {
      const btn = e.target.closest("[data-tab]");
      if (!btn) return;
      tab = btn.getAttribute("data-tab");
      page = 1;
      render();
    });
    root.querySelector("[data-am-kpis]").addEventListener("click", (e) => {
      const btn = e.target.closest("[data-kpi]");
      if (!btn) return;
      tab = btn.getAttribute("data-kpi");
      page = 1;
      render();
    });
    root.addEventListener("click", (e) => {
      const btn = e.target.closest("[data-act]");
      if (!btn) return;
      const b = M.bookings.find(x => x.id === btn.getAttribute("data-id"));
      if (!b) return;
      const act = btn.getAttribute("data-act");
      if (act === "detail") return bookingDrawer(b);
      openModal({
        title: act === "confirm" ? "Xác nhận đơn" : act === "reject" ? "Không duyệt đơn" : act === "handover" ? "Giao xe" : "Hủy đơn",
        bodyHtml: `<p>Thao tác mô phỏng cho <strong>${b.id}</strong>.</p><p class="am-muted">Không gọi API / không đổi dữ liệu hệ thống.</p>`,
        confirmText: "Tiếp tục (mô phỏng)",
        onConfirm: () => toast(`Đã mô phỏng thao tác «${act}» cho ${b.id}.`)
      });
    });
    drawerFoot?.addEventListener("click", (e) => {
      const btn = e.target.closest("[data-act]");
      if (!btn || !document.querySelector("[data-am-bookings]")) return;
      btn.click();
    });
    render();
  };

  /* ---------- Vehicles ---------- */
  const initVehicles = () => {
    const root = document.querySelector("[data-am-vehicles]");
    if (!root) return;
    let page = 1;
    const form = root.querySelector("[data-am-filter]");
    const tbody = root.querySelector("[data-am-tbody]");
    const pager = root.querySelector("[data-am-pager]");
    const counts = {
      total: M.vehicles.length,
      Available: M.vehicles.filter(v => v.status === "Available").length,
      Rented: M.vehicles.filter(v => v.status === "Rented").length,
      Maintenance: M.vehicles.filter(v => v.status === "Maintenance").length,
      Inactive: M.vehicles.filter(v => v.status === "Inactive").length
    };
    root.querySelector("[data-am-kpis]").innerHTML = [
      ["Tổng số xe", counts.total], ["Sẵn sàng", counts.Available], ["Đang thuê", counts.Rented],
      ["Bảo trì", counts.Maintenance], ["Không khả dụng", counts.Inactive]
    ].map(([l, v]) => `<article class="am-kpi"><small>${l}</small><strong>${v}</strong></article>`).join("");

    const filtered = () => {
      const fd = new FormData(form);
      const plate = String(fd.get("plate") || "").toLowerCase();
      const brand = String(fd.get("brand") || "");
      const model = String(fd.get("model") || "").toLowerCase();
      const type = String(fd.get("type") || "");
      const status = String(fd.get("status") || "");
      return M.vehicles.filter(v => {
        if (plate && !v.plate.toLowerCase().includes(plate)) return false;
        if (brand && v.brand !== brand) return false;
        if (model && !v.model.toLowerCase().includes(model)) return false;
        if (type && v.type !== type) return false;
        if (status && v.status !== status) return false;
        return true;
      });
    };

    const render = () => {
      const state = paginate(filtered(), page, 6);
      page = state.page;
      tbody.innerHTML = state.slice.map(v => `
        <tr>
          <td class="am-strong">${v.plate}</td>
          <td>${v.name}<div class="am-muted">${v.brand} ${v.model}</div></td>
          <td>${v.type}</td>
          <td>${v.km.toLocaleString("vi-VN")} km</td>
          <td>${M.badgeHtml("vehicle", v.status)}</td>
          <td>${v.booking}</td>
          <td>${v.maintenance}</td>
          <td>${v.warn === "Không" ? `<span class="am-muted">Không</span>` : `<span class="am-badge am-badge-yellow">${v.warn}</span>`}</td>
          <td><button type="button" class="am-btn am-btn-ghost am-btn-sm" data-vid="${v.id}">Xem chi tiết</button></td>
        </tr>`).join("");
      renderPager(pager, state, (p) => { page = p; render(); });
    };

    form.addEventListener("submit", (e) => { e.preventDefault(); page = 1; render(); });
    form.addEventListener("reset", () => window.setTimeout(() => { page = 1; render(); }, 0));
    root.addEventListener("click", (e) => {
      const btn = e.target.closest("[data-vid]");
      if (!btn) return;
      const v = M.vehicles.find(x => x.id === btn.getAttribute("data-vid"));
      if (!v) return;
      openDrawer({
        title: v.name,
        sub: v.plate,
        bodyHtml: `<div class="am-section"><h3>Thông tin xe</h3>
          <dl class="am-kv">
            <dt>Biển số</dt><dd>${v.plate}</dd>
            <dt>Hãng / Model</dt><dd>${v.brand} ${v.model}</dd>
            <dt>Loại xe</dt><dd>${v.type}</dd>
            <dt>Số km</dt><dd>${v.km.toLocaleString("vi-VN")} km</dd>
            <dt>Trạng thái</dt><dd>${M.badgeHtml("vehicle", v.status)}</dd>
            <dt>Booking hiện tại</dt><dd>${v.booking}</dd>
            <dt>Bảo trì gần nhất</dt><dd>${v.maintenance}</dd>
            <dt>Cảnh báo</dt><dd>${v.warn}</dd>
          </dl></div>`,
        footHtml: `<button type="button" class="am-btn am-btn-ghost" data-am-toast="Chỉnh sửa xe (mô phỏng).">Sửa</button>
                   <button type="button" class="am-btn am-btn-primary" data-am-toast="Lịch bảo trì (mô phỏng).">Lên lịch bảo trì</button>`
      });
      drawerFoot.querySelectorAll("[data-am-toast]").forEach(b => b.addEventListener("click", () => toast(b.getAttribute("data-am-toast"))));
    });
    render();
  };

  /* ---------- Customers ---------- */
  const initCustomers = () => {
    const root = document.querySelector("[data-am-customers]");
    if (!root) return;
    let page = 1;
    const form = root.querySelector("[data-am-filter]");
    const tbody = root.querySelector("[data-am-tbody]");
    const pager = root.querySelector("[data-am-pager]");

    const filtered = () => {
      const q = String(new FormData(form).get("q") || "").toLowerCase();
      return M.customers.filter(c => !q || (c.id + c.name + c.email + c.phone).toLowerCase().includes(q));
    };

    const render = () => {
      const state = paginate(filtered(), page, 6);
      page = state.page;
      tbody.innerHTML = state.slice.map(c => `
        <tr>
          <td class="am-strong">${c.id}</td>
          <td class="am-strong">${c.name}</td>
          <td>${c.email}</td>
          <td>${c.phone}</td>
          <td>${c.total}</td>
          <td>${c.active}</td>
          <td>${M.money(c.spend)}</td>
          <td>${M.badgeHtml("customer", c.status)}</td>
          <td><button type="button" class="am-btn am-btn-ghost am-btn-sm" data-cid="${c.id}">Xem chi tiết</button></td>
        </tr>`).join("");
      renderPager(pager, state, (p) => { page = p; render(); });
    };

    form.addEventListener("submit", (e) => { e.preventDefault(); page = 1; render(); });
    form.addEventListener("reset", () => window.setTimeout(() => { page = 1; render(); }, 0));
    root.addEventListener("click", (e) => {
      const btn = e.target.closest("[data-cid]");
      if (!btn) return;
      const c = M.customers.find(x => x.id === btn.getAttribute("data-cid"));
      if (!c) return;
      const cancelHtml = c.cancels.length
        ? c.cancels.map(x => `<div class="am-list-item"><span>${x.id}</span><span class="am-muted">${x.reason || "Không có lý do hủy được ghi nhận."}</span></div>`).join("")
        : `<div class="am-muted">Chưa có lịch sử hủy đơn.</div>`;
      openDrawer({
        title: c.name,
        sub: c.id,
        bodyHtml: `
          <div class="am-section"><h3>Thông tin cá nhân</h3>
            <dl class="am-kv"><dt>Email</dt><dd>${c.email}</dd><dt>SĐT</dt><dd>${c.phone}</dd><dt>Trạng thái</dt><dd>${M.badgeHtml("customer", c.status)}</dd></dl>
          </div>
          <div class="am-section"><h3>Lịch sử Booking</h3>
            <dl class="am-kv"><dt>Tổng đơn</dt><dd>${c.total}</dd><dt>Đang hoạt động</dt><dd>${c.active}</dd></dl>
          </div>
          <div class="am-section"><h3>Thanh toán</h3>
            <dl class="am-kv"><dt>Tổng chi tiêu (mock)</dt><dd>${M.money(c.spend)}</dd></dl>
          </div>
          <div class="am-section"><h3>Lịch sử hủy đơn</h3>${cancelHtml}</div>`,
        footHtml: `<button type="button" class="am-btn am-btn-ghost" data-am-toast="Khóa tài khoản (mô phỏng).">Khóa tài khoản</button>`
      });
      drawerFoot.querySelectorAll("[data-am-toast]").forEach(b => b.addEventListener("click", () => toast(b.getAttribute("data-am-toast"))));
    });
    render();
  };

  /* ---------- Drivers ---------- */
  const initDrivers = () => {
    const root = document.querySelector("[data-am-drivers]");
    if (!root) return;
    let page = 1;
    const form = root.querySelector("[data-am-filter]");
    const tbody = root.querySelector("[data-am-tbody]");
    const pager = root.querySelector("[data-am-pager]");
    root.querySelector("[data-am-kpis]").innerHTML = [
      ["Tổng tài xế", M.drivers.length],
      ["Sẵn sàng", M.drivers.filter(d => d.status === "Available").length],
      ["Đã phân công", M.drivers.filter(d => d.status === "Busy").length],
      ["Đang chạy", M.drivers.filter(d => d.status === "Busy").length],
      ["Không hoạt động", M.drivers.filter(d => d.status === "Offline").length]
    ].map(([l, v]) => `<article class="am-kpi"><small>${l}</small><strong>${v}</strong></article>`).join("");

    const filtered = () => {
      const fd = new FormData(form);
      const q = String(fd.get("q") || "").toLowerCase();
      const status = String(fd.get("status") || "");
      return M.drivers.filter(d => {
        if (status && d.status !== status) return false;
        if (q && !(d.name + d.phone + d.license).toLowerCase().includes(q)) return false;
        return true;
      });
    };

    const render = () => {
      const state = paginate(filtered(), page, 6);
      page = state.page;
      tbody.innerHTML = state.slice.map(d => `
        <tr>
          <td class="am-strong">${d.name}<div class="am-muted">${d.id}</div></td>
          <td>${d.phone}</td>
          <td>${d.license}</td>
          <td>${M.badgeHtml("driver", d.status)}</td>
          <td>${d.booking}</td>
          <td>${d.vehicle}</td>
          <td>${d.assigns}</td>
          <td><button type="button" class="am-btn am-btn-ghost am-btn-sm" data-did="${d.id}">Xem chi tiết</button></td>
        </tr>`).join("");
      renderPager(pager, state, (p) => { page = p; render(); });
    };

    form.addEventListener("submit", (e) => { e.preventDefault(); page = 1; render(); });
    form.addEventListener("reset", () => window.setTimeout(() => { page = 1; render(); }, 0));
    root.addEventListener("click", (e) => {
      const btn = e.target.closest("[data-did]");
      if (!btn) return;
      const d = M.drivers.find(x => x.id === btn.getAttribute("data-did"));
      if (!d) return;
      openDrawer({
        title: d.name,
        sub: d.id,
        bodyHtml: `<div class="am-section"><h3>Thông tin tài xế</h3>
          <dl class="am-kv"><dt>SĐT</dt><dd>${d.phone}</dd><dt>GPLX</dt><dd>${d.license}</dd>
          <dt>Trạng thái</dt><dd>${M.badgeHtml("driver", d.status)}</dd></dl></div>
          <div class="am-section"><h3>Phân công hiện tại</h3>
          <dl class="am-kv"><dt>Booking</dt><dd>${d.booking}</dd><dt>Xe</dt><dd>${d.vehicle}</dd>
          <dt>Tổng phân công</dt><dd>${d.assigns}</dd></dl></div>`,
        footHtml: `<button type="button" class="am-btn am-btn-primary" data-am-toast="Phân công tài xế (mô phỏng).">Phân công</button>`
      });
      drawerFoot.querySelectorAll("[data-am-toast]").forEach(b => b.addEventListener("click", () => toast(b.getAttribute("data-am-toast"))));
    });
    render();
  };

  /* ---------- Payments & Contracts ---------- */
  const initPayments = () => {
    const root = document.querySelector("[data-am-payments]");
    if (!root) return;
    let tab = "payments";
    let page = 1;
    const form = root.querySelector("[data-am-filter]");
    const tbody = root.querySelector("[data-am-tbody]");
    const thead = root.querySelector("[data-am-thead]");
    const pager = root.querySelector("[data-am-pager]");
    const tabs = root.querySelector("[data-am-tabs]");

    const render = () => {
      tabs.querySelectorAll("[data-tab]").forEach(el => el.classList.toggle("is-on", el.getAttribute("data-tab") === tab));
      form.hidden = tab !== "payments";
      if (tab === "payments") {
        thead.innerHTML = `<tr><th>Mã giao dịch</th><th>Mã đơn</th><th>Khách hàng</th><th>Số tiền</th><th>Phương thức</th><th>Trạng thái</th><th>Ngày tạo</th><th>Thao tác</th></tr>`;
        const status = String(new FormData(form).get("status") || "");
        const rows = M.payments.filter(p => !status || p.status === status);
        const state = paginate(rows, page, 6);
        page = state.page;
        tbody.innerHTML = state.slice.map(p => `
          <tr>
            <td class="am-strong">${p.id}</td><td>${p.booking}</td><td>${p.customer}</td>
            <td class="am-strong">${M.money(p.amount)}</td><td>${p.method}</td>
            <td>${M.badgeHtml("payment", p.status)}</td><td>${p.created}</td>
            <td><button type="button" class="am-btn am-btn-ghost am-btn-sm" data-am-toast="Xem thanh toán ${p.id} (mô phỏng).">Xem</button></td>
          </tr>`).join("");
        renderPager(pager, state, (p) => { page = p; render(); });
      } else {
        thead.innerHTML = `<tr><th>Mã hợp đồng</th><th>Mã đơn</th><th>Khách hàng</th><th>Xe</th><th>Trạng thái</th><th>Ngày tạo</th><th>Thao tác</th></tr>`;
        const state = paginate(M.contracts, page, 6);
        page = state.page;
        tbody.innerHTML = state.slice.map(c => `
          <tr>
            <td class="am-strong">${c.id}</td><td>${c.booking}</td><td>${c.customer}</td><td>${c.vehicle}</td>
            <td>${M.badgeHtml("contract", c.status)}</td><td>${c.created}</td>
            <td><button type="button" class="am-btn am-btn-ghost am-btn-sm" data-am-toast="Xem hợp đồng ${c.id} (mô phỏng).">Xem</button></td>
          </tr>`).join("");
        renderPager(pager, state, (p) => { page = p; render(); });
      }
      root.querySelectorAll("[data-am-toast]").forEach(b => b.addEventListener("click", () => toast(b.getAttribute("data-am-toast"))));
    };

    tabs.addEventListener("click", (e) => {
      const btn = e.target.closest("[data-tab]");
      if (!btn) return;
      tab = btn.getAttribute("data-tab");
      page = 1;
      render();
    });
    form.addEventListener("submit", (e) => { e.preventDefault(); page = 1; render(); });
    form.addEventListener("reset", () => window.setTimeout(() => { page = 1; render(); }, 0));
    render();
  };

  /* ---------- Maintenance & Incidents ---------- */
  const initMaintenance = () => {
    const root = document.querySelector("[data-am-maintenance]");
    if (!root) return;
    const maintBody = root.querySelector("[data-am-maint-body]");
    const incidentBody = root.querySelector("[data-am-incident-body]");
    maintBody.innerHTML = M.maintenance.map(r => `
      <tr>
        <td class="am-strong">${r.plate}</td><td>${r.name}</td><td>${r.km.toLocaleString("vi-VN")} km</td>
        <td>${r.last}</td><td>${r.next}</td>
        <td>${M.badgeHtml("maint", r.status)}</td>
        <td>${r.warn === "Không" ? `<span class="am-muted">Không</span>` : `<span class="am-badge am-badge-yellow">${r.warn}</span>`}</td>
        <td><button type="button" class="am-btn am-btn-ghost am-btn-sm" data-am-toast="Xem bảo trì ${r.plate} (mô phỏng).">Xem</button></td>
      </tr>`).join("");
    incidentBody.innerHTML = M.incidents.map(i => `
      <tr>
        <td class="am-strong">${i.id}</td><td>${i.vehicle}</td><td>${i.booking}</td>
        <td>${M.badgeHtml("level", i.level)}</td><td>${i.desc}</td><td>${i.reported}</td>
        <td>${M.badgeHtml("incident", i.status)}</td>
        <td><button type="button" class="am-btn am-btn-ghost am-btn-sm" data-am-toast="Xử lý sự cố ${i.id} (mô phỏng).">Xử lý</button></td>
      </tr>`).join("");
    root.querySelectorAll("[data-am-toast]").forEach(b => b.addEventListener("click", () => toast(b.getAttribute("data-am-toast"))));
  };

  /* ---------- Settings ---------- */
  const initSettings = () => {
    const root = document.querySelector("[data-am-settings]");
    if (!root) return;
    root.querySelectorAll("[data-am-toast]").forEach(b => b.addEventListener("click", () => toast(b.getAttribute("data-am-toast"))));
    root.querySelector("[data-am-save]")?.addEventListener("click", () => {
      openModal({
        title: "Lưu cài đặt",
        bodyHtml: `<p>Thay đổi chỉ lưu trên giao diện mô phỏng.</p>`,
        confirmText: "Lưu (mô phỏng)",
        onConfirm: () => toast("Đã lưu cài đặt (mô phỏng).")
      });
    });
  };

  initDashboard();
  initBookings();
  initVehicles();
  initCustomers();
  initDrivers();
  initPayments();
  initMaintenance();
  initSettings();

  window.AdminMockUi = { toast, openDrawer, openModal, closeDrawer, closeModal };
})();

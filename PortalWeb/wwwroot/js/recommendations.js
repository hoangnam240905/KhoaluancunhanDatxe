(() => {
  const root = document.querySelector("[data-cr-page]");
  if (!root) return;

  const fetchUrl = root.getAttribute("data-cr-fetch");
  const bookBase = root.getAttribute("data-cr-book") || "/Customer/Recommendations?handler=Book";
  const searchUrl = root.getAttribute("data-cr-search") || "/Search";

  const elLoading = root.querySelector("[data-cr-loading]");
  const elError = root.querySelector("[data-cr-error]");
  const elErrorMsg = root.querySelector("[data-cr-error-msg]");
  const elEmpty = root.querySelector("[data-cr-empty]");
  const elList = root.querySelector("[data-cr-list]");
  const elSegment = root.querySelector("[data-cr-segment]");
  const elSegmentValue = root.querySelector("[data-cr-segment-value]");
  const btnRetry = root.querySelector("[data-cr-retry]");
  const elNotice = root.querySelector("[data-cr-notice]");

  function show(which) {
    if (elLoading) elLoading.hidden = which !== "loading";
    if (elError) elError.hidden = which !== "error";
    if (elEmpty) elEmpty.hidden = which !== "empty";
    if (elList) elList.hidden = which !== "list";
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function imageForSegment(segment) {
    const n = String(segment || "").toLowerCase();
    if (n.includes("limousine") || n.includes("luxury")) return "/images/home/limousine.jpg";
    if (n.includes("suv")) return "/images/home/suv.jpg";
    if (n.includes("van") || n.includes("mpv")) return "/images/home/van.jpg";
    return "/images/home/sedan.jpg";
  }

  function scoreText(score) {
    const n = Number(score);
    if (!Number.isFinite(n)) return "—";
    return n.toFixed(4);
  }

  // Current status only — recommendations are not filtered by schedule; booking still checks dates.
  function statusBadge(status) {
    const map = {
      Available: ["cr-status-ok", "Đang trống"],
      Rented: ["cr-status-busy", "Đang có khách thuê — kiểm tra lịch khi đặt"],
      Maintenance: ["cr-status-off", "Đang bảo trì"],
      Inactive: ["cr-status-off", "Ngừng cho thuê"]
    };
    const [cls, label] = map[status] || ["cr-status-off", status ? escapeHtml(status) : "Chưa rõ trạng thái"];
    return `<span class="cr-status ${cls}" data-cr-status="${escapeHtml(status || "")}">${label}</span>`;
  }

  // Real rental price per day; "Vượt ngân sách" compares it with the per-day budget in the profile (hint, not a filter).
  function priceChips(pricePerDay, overBudget) {
    const n = Number(pricePerDay);
    const price = Number.isFinite(n) && n > 0
      ? `<span class="cr-price">${n.toLocaleString("vi-VN")}đ/ngày</span>`
      : "";
    const over = overBudget === true
      ? `<span class="cr-status cr-status-busy" data-cr-over-budget>Vượt ngân sách</span>`
      : "";
    return price + over;
  }

  // Seats vs party size, and maintenance lock — hints only, booking still checks availability.
  function fitChips(item, partySize) {
    const bits = [];
    if (item.seatsOk === false && partySize) {
      bits.push(`<span class="cr-status cr-status-busy" data-cr-seats-short>Không đủ chỗ cho ${Number(partySize)} người</span>`);
    } else if (item.seatsOk === true && item.seats) {
      bits.push(`<span class="cr-status cr-status-ok" data-cr-seats-ok>${Number(item.seats)} chỗ – đủ cho ${Number(partySize)} người</span>`);
    }
    if (item.maintenanceDue === true) {
      bits.push(`<span class="cr-status cr-status-off" data-cr-maintenance-due>Đến hạn bảo trì – chưa nhận đặt</span>`);
    }
    return bits.join("");
  }

  // Shown when the car matches the customer's past rentals (history boost > 0).
  function historyChip(affinity) {
    const n = Number(affinity);
    if (!Number.isFinite(n) || n <= 0) return "";
    return `<span class="cr-status cr-status-history" data-cr-history="${n.toFixed(2)}">Giống xe bạn từng thuê</span>`;
  }

  function bookHref(vehicleId) {
    const url = new URL(bookBase, window.location.origin);
    url.searchParams.set("vehicleId", String(vehicleId));
    return url.pathname + url.search;
  }

  function renderItems(items, partySize) {
    if (!elList) return;
    elList.innerHTML = items.map((item, index) => {
      const brand = escapeHtml(item.brand);
      const model = escapeHtml(item.model);
      const segment = escapeHtml(item.segment);
      const carId = escapeHtml(item.carId);
      const vehicleId = Number(item.vehicleId) || 0;
      const img = imageForSegment(item.segment);
      // Large-party rule additions are not ranked by V7: own badge, no "Điểm khớp".
      const badge = item.fromBusinessRule ? "Đủ chỗ cho nhóm lớn" : index === 0 ? "Đề xuất hàng đầu" : "Gợi ý";
      const badgeClass = index === 0 || item.fromBusinessRule ? "" : "ch-badge-alt";
      return `
        <article class="ch-card">
          <div class="ch-card-media">
            <img src="${img}" alt="${brand} ${model}" onerror="this.onerror=null;this.src='/images/home/sedan.jpg';" />
            <span class="ch-badge ${badgeClass}">${badge}</span>
          </div>
          <div class="ch-card-body">
            <h3>${brand} ${model}</h3>
            <div class="cr-meta-row">
              <span><i class="bi bi-tags"></i> ${segment}</span>
              ${item.fromBusinessRule ? "" : `<span class="cr-score">Điểm khớp <span>${scoreText(item.finalScore)}</span></span>`}
            </div>
            <div class="cr-meta-row">${fitChips(item, partySize)}</div>
            <div class="cr-meta-row">${statusBadge(item.vehicleStatus)}${historyChip(item.historyAffinity)}</div>
            <div class="cr-meta-row">${priceChips(item.pricePerDay, item.overBudget)}</div>
            <div class="cr-debug">VehicleId ${vehicleId} · ${carId}</div>
            <div class="ch-price cr-card-actions">
              <a class="btn btn-primary btn-sm" href="${bookHref(vehicleId)}">Đặt xe này</a>
              <a class="btn btn-outline-primary btn-sm" href="${searchUrl}">Xem thêm xe</a>
            </div>
          </div>
        </article>`;
    }).join("");
  }

  async function load() {
    if (!fetchUrl) {
      show("error");
      if (elErrorMsg) elErrorMsg.textContent = "Thiếu địa chỉ tải gợi ý.";
      return;
    }

    show("loading");
    if (elSegment) elSegment.hidden = true;

    try {
      const res = await fetch(fetchUrl, {
        headers: { Accept: "application/json" },
        credentials: "same-origin"
      });

      if (res.status === 401 || res.status === 403) {
        window.location.href = "/Account/Login?returnUrl=" + encodeURIComponent(window.location.pathname);
        return;
      }

      let payload = null;
      try { payload = await res.json(); } catch { payload = null; }

      if (res.status === 503 || !res.ok) {
        show("error");
        if (elErrorMsg) {
          elErrorMsg.textContent =
            (payload && (payload.message || payload.Message)) ||
            "Dịch vụ gợi ý tạm thời không khả dụng. Bạn vẫn có thể tìm xe bình thường.";
        }
        return;
      }

      const predicted =
        (payload && (payload.predictedSegment || payload.PredictedSegment)) || "";
      const raw =
        (payload && (payload.recommendations || payload.Recommendations)) || [];

      const seen = new Set();
      const items = [];
      for (const row of raw) {
        const id = Number(row.vehicleId ?? row.VehicleId);
        if (!id || seen.has(id)) continue;
        seen.add(id);
        items.push({
          vehicleId: id,
          carId: row.carId ?? row.CarId ?? "",
          brand: row.brand ?? row.Brand ?? "",
          model: row.model ?? row.Model ?? "",
          segment: row.segment ?? row.Segment ?? "",
          finalScore: row.finalScore ?? row.FinalScore ?? 0,
          vehicleStatus: row.vehicleStatus ?? row.VehicleStatus ?? "",
          historyAffinity: row.historyAffinity ?? row.HistoryAffinity ?? 0,
          pricePerDay: row.pricePerDay ?? row.PricePerDay ?? null,
          overBudget: row.overBudget ?? row.OverBudget ?? null,
          seats: row.seats ?? row.Seats ?? null,
          seatsOk: row.seatsOk ?? row.SeatsOk ?? null,
          maintenanceDue: row.maintenanceDue ?? row.MaintenanceDue ?? null,
          fromBusinessRule: row.fromBusinessRule ?? row.FromBusinessRule ?? false
        });
        if (items.length >= 5) break;
      }

      if (elSegment && elSegmentValue) {
        if (predicted) {
          elSegmentValue.textContent = predicted;
          elSegment.hidden = false;
        } else {
          elSegment.hidden = true;
        }
      }

      if (items.length === 0) {
        show("empty");
        return;
      }

      const partySize = Number(payload && (payload.partySize ?? payload.PartySize)) || 0;
      if (elNotice) {
        // No car fits the party (V7 catalog stops at 7 seats and the fleet has nothing bigger free).
        const noneFits = partySize > 0 && !items.some((i) => i.seatsOk === true);
        elNotice.hidden = !noneFits;
        if (noneFits) {
          elNotice.textContent =
            `Chưa có xe nào đủ chỗ cho ${partySize} người. Vui lòng liên hệ điều phối để được hỗ trợ thuê xe lớn hoặc nhiều xe.`;
        }
      }

      renderItems(items, partySize);
      show("list");
    } catch {
      show("error");
      if (elErrorMsg) {
        elErrorMsg.textContent =
          "Không kết nối được dịch vụ gợi ý. Bạn vẫn có thể tìm xe bình thường.";
      }
    }
  }

  if (btnRetry) btnRetry.addEventListener("click", () => load());
  load();
})();

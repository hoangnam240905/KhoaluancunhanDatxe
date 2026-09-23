/* Admin Mockup — frontend-only sample data. Not from Backend/API/DB. */
window.AdminMock = {
  bookings: [
    { id: "BK-1042", customer: "Lê Văn Khách", phone: "0901 111 222", mode: "SelfDrive", vehicleType: "Sedan", vehicle: "Toyota Vios · 51B-67890", start: "2026-09-25 08:00", end: "2026-09-27 18:00", contract: "Unsigned", deposit: "Waiting", status: "Pending", driver: null, amount: 2400000, cancelReason: null },
    { id: "BK-1041", customer: "Phạm Thị Mai", phone: "0902 333 444", mode: "WithDriver", vehicleType: "SUV", vehicle: "Ford Everest · 51D-22222", start: "2026-09-24 07:00", end: "2026-09-26 20:00", contract: "Signed", deposit: "Paid", status: "Confirmed", driver: null, amount: 4800000, cancelReason: null },
    { id: "BK-1038", customer: "Nguyễn Minh An", phone: "0903 555 666", mode: "WithDriver", vehicleType: "Sedan", vehicle: "Hyundai Accent · 51C-11111", start: "2026-09-23 09:00", end: "2026-09-24 21:00", contract: "Signed", deposit: "Paid", status: "Assigned", driver: "Hoàng Văn Tài", amount: 1600000, cancelReason: null },
    { id: "BK-1035", customer: "Trần Quốc Bảo", phone: "0904 777 888", mode: "SelfDrive", vehicleType: "Van", vehicle: "Hyundai Solati · 51E-33333", start: "2026-09-22 06:00", end: "2026-09-23 22:00", contract: "Signed", deposit: "Paid", status: "InProgress", driver: null, amount: 5200000, cancelReason: null },
    { id: "BK-1030", customer: "Lê Văn Khách", phone: "0901 111 222", mode: "SelfDrive", vehicleType: "SUV", vehicle: "Toyota Fortuner · 51C-99999", start: "2026-09-10 08:00", end: "2026-09-12 18:00", contract: "Signed", deposit: "Paid", status: "Completed", driver: null, amount: 3600000, cancelReason: null },
    { id: "BK-1028", customer: "Đỗ Thu Hà", phone: "0905 999 000", mode: "WithDriver", vehicleType: "Sedan", vehicle: "Toyota Vios · 51B-67890", start: "2026-09-08 10:00", end: "2026-09-09 18:00", contract: "Voided", deposit: "Paid", status: "Cancelled", driver: null, amount: 1200000, cancelReason: "Đổi lịch công tác" },
    { id: "BK-1025", customer: "Phạm Thị Mai", phone: "0902 333 444", mode: "SelfDrive", vehicleType: "Sedan", vehicle: "—", start: "2026-09-05 08:00", end: "2026-09-06 18:00", contract: "Unsigned", deposit: "Waiting", status: "Cancelled", driver: null, amount: 900000, cancelReason: null },
    { id: "BK-1022", customer: "Võ Hải Đăng", phone: "0912 345 678", mode: "WithDriver", vehicleType: "MPV", vehicle: "Kia Carnival · 51F-44444", start: "2026-09-28 08:00", end: "2026-09-30 18:00", contract: "Signed", deposit: "Paid", status: "Confirmed", driver: null, amount: 6100000, cancelReason: null },
    { id: "BK-1019", customer: "Ngô Bảo Châu", phone: "0933 222 111", mode: "SelfDrive", vehicleType: "Sedan", vehicle: "Honda City · 51G-55555", start: "2026-09-18 07:30", end: "2026-09-19 19:00", contract: "Signed", deposit: "Paid", status: "Completed", driver: null, amount: 1500000, cancelReason: null },
    { id: "BK-1015", customer: "Lý Hoàng Nam", phone: "0988 777 666", mode: "WithDriver", vehicleType: "SUV", vehicle: "Mazda CX-5 · 51H-66666", start: "2026-09-26 08:00", end: "2026-09-27 20:00", contract: "Unsigned", deposit: "Waiting", status: "Pending", driver: null, amount: 2800000, cancelReason: null }
  ],

  vehicles: [
    { id: "VH-01", plate: "51A-12345", name: "Toyota Vios", brand: "Toyota", model: "Vios", type: "Sedan", km: 48200, status: "Rented", booking: "BK-1038", maintenance: "2026-07-12", warn: "Không" },
    { id: "VH-02", plate: "51B-67890", name: "Hyundai Accent", brand: "Hyundai", model: "Accent", type: "Sedan", km: 35100, status: "Available", booking: "—", maintenance: "2026-08-01", warn: "Không" },
    { id: "VH-03", plate: "51C-11111", name: "Toyota Fortuner", brand: "Toyota", model: "Fortuner", type: "SUV", km: 61200, status: "Available", booking: "—", maintenance: "2026-06-20", warn: "Sắp đến hạn" },
    { id: "VH-04", plate: "51D-22222", name: "Ford Everest", brand: "Ford", model: "Everest", type: "SUV", km: 55000, status: "Rented", booking: "BK-1041", maintenance: "2026-08-15", warn: "Không" },
    { id: "VH-05", plate: "51E-33333", name: "Hyundai Solati", brand: "Hyundai", model: "Solati", type: "Van", km: 95200, status: "Rented", booking: "BK-1035", maintenance: "2026-05-30", warn: "KM cao" },
    { id: "VH-06", plate: "51F-44444", name: "Kia Carnival", brand: "Kia", model: "Carnival", type: "MPV", km: 28900, status: "Maintenance", booking: "—", maintenance: "2026-09-18", warn: "Đang bảo trì" },
    { id: "VH-07", plate: "51G-55555", name: "Honda City", brand: "Honda", model: "City", type: "Sedan", km: 22100, status: "Available", booking: "—", maintenance: "2026-08-22", warn: "Không" },
    { id: "VH-08", plate: "51H-66666", name: "Mazda CX-5", brand: "Mazda", model: "CX-5", type: "SUV", km: 40300, status: "Inactive", booking: "—", maintenance: "2026-04-10", warn: "Ngưng hoạt động" }
  ],

  customers: [
    { id: "KH-03", name: "Lê Văn Khách", email: "customer1@gmail.com", phone: "0901 111 222", total: 6, active: 1, spend: 12800000, status: "Active", cancels: [{ id: "BK-1028", reason: "Đổi lịch công tác" }] },
    { id: "KH-04", name: "Phạm Thị Mai", email: "customer2@gmail.com", phone: "0902 333 444", total: 4, active: 1, spend: 9200000, status: "Active", cancels: [{ id: "BK-1025", reason: null }] },
    { id: "KH-05", name: "Nguyễn Minh An", email: "an.nguyen@email.vn", phone: "0903 555 666", total: 3, active: 1, spend: 5100000, status: "Active", cancels: [] },
    { id: "KH-06", name: "Trần Quốc Bảo", email: "bao.tran@email.vn", phone: "0904 777 888", total: 2, active: 1, spend: 7400000, status: "Active", cancels: [] },
    { id: "KH-07", name: "Đỗ Thu Hà", email: "ha.do@email.vn", phone: "0905 999 000", total: 5, active: 0, spend: 8600000, status: "Locked", cancels: [] },
    { id: "KH-08", name: "Võ Hải Đăng", email: "dang.vo@email.vn", phone: "0912 345 678", total: 1, active: 1, spend: 6100000, status: "Active", cancels: [] }
  ],

  drivers: [
    { id: "TX-01", name: "Hoàng Văn Tài", phone: "0923 000 001", license: "B2 · 790012345", status: "Busy", booking: "BK-1038", vehicle: "51A-12345", assigns: 18 },
    { id: "TX-02", name: "Nguyễn Văn Lái", phone: "0923 000 002", license: "B2 · 790045678", status: "Available", booking: "—", vehicle: "—", assigns: 12 },
    { id: "TX-03", name: "Phạm Đức Hùng", phone: "0923 000 003", license: "B2 · 790078901", status: "Offline", booking: "—", vehicle: "—", assigns: 7 },
    { id: "TX-04", name: "Trần Minh Khoa", phone: "0923 000 004", license: "D · 790099887", status: "Busy", booking: "BK-1041", vehicle: "51D-22222", assigns: 21 },
    { id: "TX-05", name: "Lê Quốc Việt", phone: "0923 000 005", license: "B2 · 790055443", status: "Available", booking: "—", vehicle: "—", assigns: 9 }
  ],

  payments: [
    { id: "TT-501", booking: "BK-1041", customer: "Phạm Thị Mai", amount: 2400000, method: "BankTransfer", status: "Paid", created: "2026-09-21 09:12" },
    { id: "TT-500", booking: "BK-1038", customer: "Nguyễn Minh An", amount: 800000, method: "MoMo", status: "Paid", created: "2026-09-20 14:05" },
    { id: "TT-498", booking: "BK-1042", customer: "Lê Văn Khách", amount: 1200000, method: "Cash", status: "Pending", created: "2026-09-22 08:40" },
    { id: "TT-495", booking: "BK-1035", customer: "Trần Quốc Bảo", amount: 2600000, method: "VNPay", status: "Paid", created: "2026-09-19 11:20" },
    { id: "TT-490", booking: "BK-1028", customer: "Đỗ Thu Hà", amount: 600000, method: "BankTransfer", status: "Failed", created: "2026-09-08 16:33" },
    { id: "TT-488", booking: "BK-1015", customer: "Lý Hoàng Nam", amount: 1400000, method: "Cash", status: "Pending", created: "2026-09-22 10:05" }
  ],

  contracts: [
    { id: "HD-301", booking: "BK-1041", customer: "Phạm Thị Mai", vehicle: "51D-22222", status: "Signed", created: "2026-09-21 08:50" },
    { id: "HD-298", booking: "BK-1038", customer: "Nguyễn Minh An", vehicle: "51A-12345", status: "Signed", created: "2026-09-20 13:40" },
    { id: "HD-295", booking: "BK-1042", customer: "Lê Văn Khách", vehicle: "—", status: "Issued", created: "2026-09-22 08:20" },
    { id: "HD-290", booking: "BK-1035", customer: "Trần Quốc Bảo", vehicle: "51E-33333", status: "Signed", created: "2026-09-19 10:55" },
    { id: "HD-280", booking: "BK-1028", customer: "Đỗ Thu Hà", vehicle: "51B-67890", status: "Voided", created: "2026-09-08 15:10" }
  ],

  maintenance: [
    { plate: "51F-44444", name: "Kia Carnival", km: 28900, last: "2026-09-18", next: "2026-12-18", status: "InProgress", warn: "Đang bảo trì" },
    { plate: "51C-11111", name: "Toyota Fortuner", km: 61200, last: "2026-06-20", next: "2026-09-25", status: "DueSoon", warn: "Sắp đến hạn" },
    { plate: "51E-33333", name: "Hyundai Solati", km: 95200, last: "2026-05-30", next: "2026-10-01", status: "DueSoon", warn: "KM cao" },
    { plate: "51B-67890", name: "Hyundai Accent", km: 35100, last: "2026-08-01", next: "2026-11-01", status: "Ok", warn: "Không" },
    { plate: "51A-12345", name: "Toyota Vios", km: 48200, last: "2026-07-12", next: "2026-10-12", status: "Ok", warn: "Không" }
  ],

  incidents: [
    { id: "SC-12", vehicle: "51E-33333", booking: "BK-1035", level: "Medium", desc: "Khách báo tiếng ồn bất thường khi phanh", reported: "2026-09-22 11:05", status: "Open" },
    { id: "SC-11", vehicle: "51A-12345", booking: "BK-1038", level: "Low", desc: "Trầy nhẹ cản trước", reported: "2026-09-21 19:40", status: "InReview" },
    { id: "SC-10", vehicle: "51D-22222", booking: "BK-1041", level: "High", desc: "Đèn báo động cơ sáng", reported: "2026-09-20 08:15", status: "Open" },
    { id: "SC-09", vehicle: "51F-44444", booking: "—", level: "Critical", desc: "Rò rỉ dầu hộp số khi bảo trì", reported: "2026-09-18 14:20", status: "Resolved" }
  ],

  labels: {
    bookingStatus: { Pending: "Chờ xác nhận", Confirmed: "Đã xác nhận", Assigned: "Đã phân công", InProgress: "Đang thực hiện", Completed: "Hoàn thành", Cancelled: "Đã hủy" },
    contract: { Unsigned: "Chưa ký", Issued: "Đã lập", Signed: "Đã ký", Voided: "Đã hủy hiệu lực" },
    deposit: { Waiting: "Chờ cọc", Paid: "Đã cọc", Failed: "Thất bại" },
    vehicle: { Available: "Sẵn sàng", Rented: "Đang thuê", Maintenance: "Bảo trì", Inactive: "Không khả dụng" },
    driver: { Available: "Sẵn sàng", Busy: "Đã phân công", Offline: "Không hoạt động" },
    payment: { Paid: "Đã thanh toán", Pending: "Chờ thanh toán", Failed: "Thất bại" },
    maint: { Ok: "Ổn định", DueSoon: "Sắp đến hạn", InProgress: "Đang bảo trì" },
    incident: { Open: "Mới", InReview: "Đang xử lý", Resolved: "Đã xử lý" },
    level: { Low: "Thấp", Medium: "Trung bình", High: "Cao", Critical: "Nghiêm trọng" },
    mode: { SelfDrive: "Tự lái", WithDriver: "Có tài xế" },
    customer: { Active: "Hoạt động", Locked: "Tạm khóa" }
  },

  badge: {
    Pending: "am-badge-yellow", Confirmed: "am-badge-blue", Assigned: "am-badge-blue", InProgress: "am-badge-cyan",
    Completed: "am-badge-green", Cancelled: "am-badge-red",
    Unsigned: "am-badge-gray", Issued: "am-badge-yellow", Signed: "am-badge-green", Voided: "am-badge-gray",
    Waiting: "am-badge-yellow", Paid: "am-badge-green", Failed: "am-badge-red",
    Available: "am-badge-green", Rented: "am-badge-blue", Maintenance: "am-badge-yellow", Inactive: "am-badge-gray",
    Busy: "am-badge-blue", Offline: "am-badge-gray",
    Ok: "am-badge-green", DueSoon: "am-badge-yellow", InProgressMaint: "am-badge-cyan",
    Open: "am-badge-yellow", InReview: "am-badge-blue", Resolved: "am-badge-green",
    Low: "am-badge-gray", Medium: "am-badge-yellow", High: "am-badge-red", Critical: "am-badge-red",
    Active: "am-badge-green", Locked: "am-badge-red"
  },

  money(n) {
    return Number(n || 0).toLocaleString("vi-VN") + " đ";
  },

  badgeHtml(kind, value) {
    const map = this.labels[kind] || {};
    const cls = this.badge[value] || "am-badge-gray";
    const label = map[value] || value || "—";
    return `<span class="am-badge ${cls}">${label}</span>`;
  }
};

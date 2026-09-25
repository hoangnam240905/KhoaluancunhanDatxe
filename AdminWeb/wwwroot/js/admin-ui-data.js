window.DriveRentMock = (function () {
  const months = ["T1","T2","T3","T4","T5","T6","T7","T8","T9","T10","T11","T12"];
  const revenueByMonth = [28500,31200,29800,34100,36500,39200,41000,43800,45600,48320,47100,50200];

  const mockDashboardData = {
    welcomeName: "Đạt",
    dateRange: "Sep 14, 2026 - Oct 14, 2026",
    kpis: [
      { label: "Total Revenue", value: "$48,320", change: "+12.5%", tone: "up", note: "vs. previous month", icon: "bi-cash-stack", color: "blue" },
      { label: "Total Bookings", value: "1,842", change: "+8.3%", tone: "up", note: "vs. previous month", icon: "bi-calendar2-check", color: "green" },
      { label: "Active Fleet", value: "120", change: "+4.3%", tone: "up", note: "vs. previous month", icon: "bi-truck-front", color: "purple" },
      { label: "Total Customers", value: "892", change: "+15.7%", tone: "up", note: "vs. previous month", icon: "bi-people", color: "amber" }
    ],
    revenue: { months, values: revenueByMonth },
    topVehicles: [
      { name: "Toyota Vios", bookings: 286, pct: 92 },
      { name: "Honda City", bookings: 241, pct: 78 },
      { name: "Toyota Camry", bookings: 198, pct: 64 },
      { name: "Mitsubishi Xpander", bookings: 176, pct: 57 },
      { name: "Ford Everest", bookings: 152, pct: 49 }
    ],
    bookingStatus: [
      { label: "Confirmed", value: 742, color: "#16A34A" },
      { label: "Pending", value: 126, color: "#D97706" },
      { label: "In Progress", value: 318, color: "#2563EB" },
      { label: "Cancelled", value: 104, color: "#DC2626" }
    ],
    recentBookings: [
      { id: "BK-20260914-001", customer: "Nguyễn Văn An", vehicle: "Toyota Vios", pickup: "Sep 14, 2026", status: "Pending", amount: "$120.00" },
      { id: "BK-20260914-002", customer: "Trần Thị Mai", vehicle: "Honda City", pickup: "Sep 14, 2026", status: "Confirmed", amount: "$180.00" },
      { id: "BK-20260913-015", customer: "Lê Quang Huy", vehicle: "Toyota Fortuner", pickup: "Sep 13, 2026", status: "In Progress", amount: "$320.00" },
      { id: "BK-20260912-044", customer: "Phạm Thu Hà", vehicle: "Mazda CX-5", pickup: "Sep 12, 2026", status: "Completed", amount: "$240.00" },
      { id: "BK-20260911-028", customer: "Hoàng Minh Tuấn", vehicle: "Kia Carnival", pickup: "Sep 11, 2026", status: "Cancelled", amount: "$410.00" }
    ],
    upcomingPickups: [
      { customer: "Nguyễn Văn An", vehicle: "Toyota Vios", datetime: "Sep 14, 09:00", status: "Pending" },
      { customer: "Trần Thị Mai", vehicle: "Honda City", datetime: "Sep 14, 10:30", status: "Confirmed" },
      { customer: "Đỗ Bảo Ngọc", vehicle: "Hyundai Accent", datetime: "Sep 14, 13:00", status: "Confirmed" },
      { customer: "Vũ Đức Anh", vehicle: "VinFast VF 8", datetime: "Sep 15, 08:00", status: "Pending" },
      { customer: "Bùi Thanh Trúc", vehicle: "Ford Everest", datetime: "Sep 15, 11:00", status: "Confirmed" }
    ]
  };

  const mockBookings = [
    { id: "BK-20260914-001", customer: "Nguyễn Văn An", phone: "0901 234 567", email: "nguyenvanan@email.com", membership: "Premium", vehicle: "Toyota Vios", plate: "51A-123.45", type: "Sedan", transmission: "Automatic", seats: 5, rentalType: "Self Drive", dateRange: "Sep 14 - Sep 16", duration: "2 ngày", pickup: "Tan Son Nhat Airport", dropoff: "District 1 Office", status: "Pending", amount: "$120.00", created: "Sep 12, 2026 14:22", dailyRate: "$50.00", insurance: "$15.00", airportFee: "$10.00", discount: "-$5.00" },
    { id: "BK-20260914-002", customer: "Trần Thị Mai", phone: "0912 345 678", email: "tranthimai@email.com", membership: "Standard", vehicle: "Honda City", plate: "51B-234.56", type: "Sedan", transmission: "Automatic", seats: 5, rentalType: "Self Drive", dateRange: "Sep 14 - Sep 17", duration: "3 ngày", pickup: "District 3 Hub", dropoff: "District 3 Hub", status: "Confirmed", amount: "$180.00", created: "Sep 12, 2026 16:05", dailyRate: "$55.00", insurance: "$18.00", airportFee: "$0.00", discount: "-$3.00" },
    { id: "BK-20260913-015", customer: "Lê Quang Huy", phone: "0933 456 789", email: "lequanghuy@email.com", membership: "Premium", vehicle: "Toyota Fortuner", plate: "51C-345.67", type: "SUV", transmission: "Automatic", seats: 7, rentalType: "With Driver", dateRange: "Sep 13 - Sep 15", duration: "2 ngày", pickup: "District 7", dropoff: "Thu Duc", status: "In Progress", amount: "$320.00", created: "Sep 11, 2026 09:40", dailyRate: "$140.00", insurance: "$25.00", airportFee: "$0.00", discount: "-$5.00" },
    { id: "BK-20260912-044", customer: "Phạm Thu Hà", phone: "0944 567 890", email: "phamthuha@email.com", membership: "Basic", vehicle: "Mazda CX-5", plate: "51D-456.78", type: "SUV", transmission: "Automatic", seats: 5, rentalType: "Self Drive", dateRange: "Sep 12 - Sep 14", duration: "2 ngày", pickup: "Binh Thanh", dropoff: "Binh Thanh", status: "Completed", amount: "$240.00", created: "Sep 10, 2026 11:18", dailyRate: "$110.00", insurance: "$20.00", airportFee: "$0.00", discount: "$0.00" },
    { id: "BK-20260911-028", customer: "Hoàng Minh Tuấn", phone: "0955 678 901", email: "hoangminhtuan@email.com", membership: "Standard", vehicle: "Kia Carnival", plate: "51E-567.89", type: "MPV", transmission: "Automatic", seats: 7, rentalType: "With Driver", dateRange: "Sep 11 - Sep 13", duration: "2 ngày", pickup: "Airport T2", dropoff: "Airport T2", status: "Cancelled", amount: "$410.00", created: "Sep 09, 2026 18:02", dailyRate: "$180.00", insurance: "$30.00", airportFee: "$20.00", discount: "$0.00" },
    { id: "BK-20260910-061", customer: "Đỗ Bảo Ngọc", phone: "0966 789 012", email: "dobaongoc@email.com", membership: "Premium", vehicle: "Hyundai Accent", plate: "51F-678.90", type: "Sedan", transmission: "Manual", seats: 5, rentalType: "Self Drive", dateRange: "Sep 10 - Sep 12", duration: "2 ngày", pickup: "Go Vap", dropoff: "Go Vap", status: "Completed", amount: "$95.00", created: "Sep 08, 2026 08:55", dailyRate: "$42.00", insurance: "$12.00", airportFee: "$0.00", discount: "-$1.00" },
    { id: "BK-20260909-077", customer: "Vũ Đức Anh", phone: "0977 890 123", email: "vuducanh@email.com", membership: "Standard", vehicle: "VinFast VF 8", plate: "51G-789.01", type: "EV SUV", transmission: "Automatic", seats: 5, rentalType: "Self Drive", dateRange: "Sep 15 - Sep 18", duration: "3 ngày", pickup: "District 1", dropoff: "District 2", status: "Confirmed", amount: "$360.00", created: "Sep 07, 2026 13:30", dailyRate: "$110.00", insurance: "$22.00", airportFee: "$0.00", discount: "-$12.00" },
    { id: "BK-20260908-090", customer: "Bùi Thanh Trúc", phone: "0988 901 234", email: "buithanhtruc@email.com", membership: "Basic", vehicle: "Ford Everest", plate: "51H-890.12", type: "SUV", transmission: "Automatic", seats: 7, rentalType: "With Driver", dateRange: "Sep 08 - Sep 10", duration: "2 ngày", pickup: "Tan Binh", dropoff: "Tan Binh", status: "Completed", amount: "$290.00", created: "Sep 06, 2026 10:12", dailyRate: "$130.00", insurance: "$20.00", airportFee: "$0.00", discount: "$0.00" },
    { id: "BK-20260907-103", customer: "Ngô Nhật Nam", phone: "0902 112 233", email: "ngonhatnam@email.com", membership: "Premium", vehicle: "Toyota Camry", plate: "51K-901.23", type: "Sedan", transmission: "Automatic", seats: 5, rentalType: "Self Drive", dateRange: "Sep 07 - Sep 09", duration: "2 ngày", pickup: "Phu Nhuan", dropoff: "Phu Nhuan", status: "In Progress", amount: "$210.00", created: "Sep 05, 2026 15:44", dailyRate: "$95.00", insurance: "$18.00", airportFee: "$0.00", discount: "-$8.00" },
    { id: "BK-20260906-118", customer: "Lý Mỹ Linh", phone: "0903 223 344", email: "lymylinh@email.com", membership: "Standard", vehicle: "Mitsubishi Xpander", plate: "51L-012.34", type: "MPV", transmission: "Automatic", seats: 7, rentalType: "Self Drive", dateRange: "Sep 16 - Sep 19", duration: "3 ngày", pickup: "District 10", dropoff: "District 10", status: "Pending", amount: "$225.00", created: "Sep 04, 2026 09:20", dailyRate: "$70.00", insurance: "$15.00", airportFee: "$0.00", discount: "$0.00" },
    { id: "BK-20260905-130", customer: "Trịnh Quốc Bảo", phone: "0904 334 455", email: "trinhquocbao@email.com", membership: "Basic", vehicle: "Toyota Vios", plate: "51A-123.45", type: "Sedan", transmission: "Automatic", seats: 5, rentalType: "Self Drive", dateRange: "Sep 05 - Sep 06", duration: "1 ngày", pickup: "District 5", dropoff: "District 5", status: "Completed", amount: "$65.00", created: "Sep 03, 2026 17:08", dailyRate: "$50.00", insurance: "$10.00", airportFee: "$5.00", discount: "$0.00" },
    { id: "BK-20260904-145", customer: "Huỳnh Gia Hân", phone: "0905 445 566", email: "huynhgiahan@email.com", membership: "Premium", vehicle: "Honda City", plate: "51B-234.56", type: "Sedan", transmission: "Automatic", seats: 5, rentalType: "With Driver", dateRange: "Sep 04 - Sep 07", duration: "3 ngày", pickup: "Airport T1", dropoff: "District 1", status: "Cancelled", amount: "$270.00", created: "Sep 02, 2026 12:50", dailyRate: "$80.00", insurance: "$18.00", airportFee: "$12.00", discount: "$0.00" }
  ];

  const mockVehicles = [
    { plate: "51A-123.45", model: "Toyota Vios", year: 2023, type: "Sedan", status: "Available", regNo: "REG-VIOS-01", regDate: "Jan 12, 2023", expiry: "Jan 12, 2027", color: "Trắng", vin: "JTDBR32E720012345", notes: "Sedan đô thị phổ biến" },
    { plate: "51B-234.56", model: "Honda City", year: 2022, type: "Sedan", status: "In Use", regNo: "REG-CITY-02", regDate: "Mar 05, 2022", expiry: "Mar 05, 2026", color: "Bạc", vin: "MRHGM56E0NP123456", notes: "" },
    { plate: "51C-345.67", model: "Toyota Camry", year: 2024, type: "Sedan", status: "Available", regNo: "REG-CAM-03", regDate: "Feb 18, 2024", expiry: "Feb 18, 2028", color: "Đen", vin: "4T1B11HK5RU123456", notes: "Sedan cao cấp" },
    { plate: "51D-456.78", model: "Mitsubishi Xpander", year: 2023, type: "MPV", status: "Maintenance", regNo: "REG-XPA-04", regDate: "May 22, 2023", expiry: "May 22, 2027", color: "Xám", vin: "MMABXXTTR0N123456", notes: "Bảo dưỡng theo lịch" },
    { plate: "51E-567.89", model: "Ford Everest", year: 2022, type: "SUV", status: "In Use", regNo: "REG-EVE-05", regDate: "Jul 09, 2022", expiry: "Jul 09, 2026", color: "Xanh dương", vin: "MNABSXXXE0N123456", notes: "" },
    { plate: "51F-678.90", model: "Kia Carnival", year: 2024, type: "MPV", status: "Available", regNo: "REG-CAR-06", regDate: "Apr 01, 2024", expiry: "Apr 01, 2028", color: "Trắng ngọc", vin: "KNACH81F0R6123456", notes: "MPV gia đình" },
    { plate: "51G-789.01", model: "Mazda CX-5", year: 2023, type: "SUV", status: "Available", regNo: "REG-CX5-07", regDate: "Aug 14, 2023", expiry: "Aug 14, 2027", color: "Đỏ", vin: "JM3KFADM0P0123456", notes: "" },
    { plate: "51H-890.12", model: "Hyundai Accent", year: 2021, type: "Sedan", status: "Out of Service", regNo: "REG-ACC-08", regDate: "Nov 20, 2021", expiry: "Nov 20, 2025", color: "Trắng", vin: "KMHCT41DABU123456", notes: "Sửa chữa sau tai nạn" },
    { plate: "51K-901.23", model: "Toyota Fortuner", year: 2023, type: "SUV", status: "In Use", regNo: "REG-FOR-09", regDate: "Jun 30, 2023", expiry: "Jun 30, 2027", color: "Đồng", vin: "MHFBA8FS0P0123456", notes: "" },
    { plate: "51L-012.34", model: "VinFast VF 8", year: 2024, type: "EV SUV", status: "Available", regNo: "REG-VF8-10", regDate: "Sep 01, 2024", expiry: "Sep 01, 2028", color: "Xanh lá", vin: "RLXVF8EV0R0123456", notes: "Đội xe điện" }
  ];

  const mockCustomers = [
    { id: "CUS-1001", name: "Nguyễn Văn An", email: "nguyenvanan@email.com", phone: "0901 234 567", membership: "Premium", joinDate: "Jan 14, 2024", status: "Active", location: "District 1, HCMC", dob: "Mar 12, 1992", address: "12 Nguyen Hue, District 1", bookings: 24, spent: "$3,240" },
    { id: "CUS-1002", name: "Trần Thị Mai", email: "tranthimai@email.com", phone: "0912 345 678", membership: "Standard", joinDate: "Feb 02, 2024", status: "Active", location: "District 3, HCMC", dob: "Jul 08, 1995", address: "88 Vo Van Tan, District 3", bookings: 11, spent: "$1,180" },
    { id: "CUS-1003", name: "Lê Quang Huy", email: "lequanghuy@email.com", phone: "0933 456 789", membership: "Premium", joinDate: "Mar 18, 2023", status: "Active", location: "Thu Duc, HCMC", dob: "Nov 21, 1988", address: "45 Vo Nguyen Giap, Thu Duc", bookings: 31, spent: "$5,620" },
    { id: "CUS-1004", name: "Phạm Thu Hà", email: "phamthuha@email.com", phone: "0944 567 890", membership: "Basic", joinDate: "May 09, 2025", status: "Active", location: "Binh Thanh, HCMC", dob: "Apr 03, 1998", address: "9 Xo Viet Nghe Tinh", bookings: 4, spent: "$420" },
    { id: "CUS-1005", name: "Hoàng Minh Tuấn", email: "hoangminhtuan@email.com", phone: "0955 678 901", membership: "Standard", joinDate: "Jun 22, 2024", status: "Locked", location: "Tan Binh, HCMC", dob: "Sep 30, 1990", address: "120 Cong Hoa, Tan Binh", bookings: 8, spent: "$960" },
    { id: "CUS-1006", name: "Đỗ Bảo Ngọc", email: "dobaongoc@email.com", phone: "0966 789 012", membership: "Premium", joinDate: "Aug 01, 2023", status: "Active", location: "Go Vap, HCMC", dob: "Dec 15, 1993", address: "67 Quang Trung, Go Vap", bookings: 19, spent: "$2,450" },
    { id: "CUS-1007", name: "Vũ Đức Anh", email: "vuducanh@email.com", phone: "0977 890 123", membership: "Standard", joinDate: "Sep 12, 2024", status: "Active", location: "District 2, HCMC", dob: "Jan 27, 1991", address: "22 Nguyen Van Huong", bookings: 7, spent: "$890" },
    { id: "CUS-1008", name: "Bùi Thanh Trúc", email: "buithanhtruc@email.com", phone: "0988 901 234", membership: "Basic", joinDate: "Oct 05, 2025", status: "Active", location: "District 7, HCMC", dob: "May 19, 1997", address: "5 Nguyen Luong Bang", bookings: 3, spent: "$310" },
    { id: "CUS-1009", name: "Ngô Nhật Nam", email: "ngonhatnam@email.com", phone: "0902 112 233", membership: "Premium", joinDate: "Nov 11, 2023", status: "Active", location: "Phu Nhuan, HCMC", dob: "Feb 02, 1989", address: "33 Phan Xich Long", bookings: 22, spent: "$2,980" },
    { id: "CUS-1010", name: "Lý Mỹ Linh", email: "lymylinh@email.com", phone: "0903 223 344", membership: "Standard", joinDate: "Dec 20, 2024", status: "Locked", location: "District 10, HCMC", dob: "Aug 14, 1996", address: "18 Su Van Hanh", bookings: 6, spent: "$540" }
  ];

  const mockDrivers = [
    { id: "DRV-201", name: "Nguyễn Hữu Phước", phone: "0906 111 222", email: "phuoc.driver@driverent.vn", status: "Available", rating: 4.9, trips: 312, city: "HCMC", location: "District 1", license: "B2-889912", joined: "Jan 10, 2023", vehicle: "Toyota Fortuner · 51K-901.23", distance: "48,200 km", completion: "98%", activity: [
      { title: "Hoàn thành chuyến", time: "Hôm nay, 08:40" },
      { title: "Khách hàng đánh giá 5 sao", time: "Hôm qua, 19:12" },
      { title: "Hoàn thành chuyến", time: "Hôm qua, 14:05" },
      { title: "Báo cáo sự cố nhỏ", time: "Sep 12, 11:20" }
    ]},
    { id: "DRV-202", name: "Trần Văn Khoa", phone: "0907 222 333", email: "khoa.driver@driverent.vn", status: "Busy", rating: 4.7, trips: 278, city: "HCMC", location: "Thu Duc", license: "B2-778801", joined: "Mar 04, 2023", vehicle: "Kia Carnival · 51F-678.90", distance: "41,050 km", completion: "96%", activity: [
      { title: "Hoàn thành chuyến", time: "Hôm nay, 07:15" },
      { title: "Bắt đầu chuyến", time: "Hôm nay, 09:00" },
      { title: "Khách hàng đánh giá 5 sao", time: "Sep 13, 18:40" },
      { title: "Hoàn thành chuyến", time: "Sep 13, 12:10" }
    ]},
    { id: "DRV-203", name: "Lê Minh Đức", phone: "0908 333 444", email: "duc.driver@driverent.vn", status: "Offline", rating: 4.5, trips: 190, city: "HCMC", location: "Binh Thanh", license: "B2-667700", joined: "Jun 18, 2023", vehicle: "—", distance: "29,800 km", completion: "94%", activity: [
      { title: "Chuyển ngoại tuyến", time: "Hôm nay, 06:00" },
      { title: "Hoàn thành chuyến", time: "Sep 13, 21:05" },
      { title: "Khách hàng đánh giá 4 sao", time: "Sep 13, 20:50" },
      { title: "Hoàn thành chuyến", time: "Sep 12, 16:30" }
    ]},
    { id: "DRV-204", name: "Phạm Quốc Việt", phone: "0909 444 555", email: "viet.driver@driverent.vn", status: "Available", rating: 4.8, trips: 255, city: "HCMC", location: "Tan Binh", license: "B2-556611", joined: "Aug 22, 2023", vehicle: "Ford Everest · 51E-567.89", distance: "36,400 km", completion: "97%", activity: [
      { title: "Hoàn thành chuyến", time: "Hôm nay, 10:20" },
      { title: "Khách hàng đánh giá 5 sao", time: "Hôm nay, 10:05" },
      { title: "Hoàn thành chuyến", time: "Sep 13, 15:40" },
      { title: "Báo cáo sự cố nhỏ", time: "Sep 10, 09:15" }
    ]},
    { id: "DRV-205", name: "Hoàng Anh Dũng", phone: "0910 555 666", email: "dung.driver@driverent.vn", status: "Busy", rating: 4.6, trips: 221, city: "HCMC", location: "District 7", license: "B2-445522", joined: "Oct 01, 2023", vehicle: "Honda City · 51B-234.56", distance: "33,100 km", completion: "95%", activity: [
      { title: "Bắt đầu chuyến", time: "Hôm nay, 08:10" },
      { title: "Hoàn thành chuyến", time: "Hôm qua, 22:00" },
      { title: "Khách hàng đánh giá 5 sao", time: "Hôm qua, 21:45" },
      { title: "Hoàn thành chuyến", time: "Sep 12, 13:25" }
    ]},
    { id: "DRV-206", name: "Đặng Thành Long", phone: "0911 666 777", email: "long.driver@driverent.vn", status: "Available", rating: 4.9, trips: 340, city: "Hanoi", location: "Cau Giay", license: "B2-334433", joined: "Dec 12, 2022", vehicle: "Toyota Camry · 51C-345.67", distance: "52,700 km", completion: "99%", activity: [
      { title: "Hoàn thành chuyến", time: "Hôm nay, 11:30" },
      { title: "Khách hàng đánh giá 5 sao", time: "Hôm nay, 11:20" },
      { title: "Hoàn thành chuyến", time: "Sep 13, 17:00" },
      { title: "Hoàn thành chuyến", time: "Sep 12, 09:45" }
    ]},
    { id: "DRV-207", name: "Võ Nhật Hào", phone: "0912 777 888", email: "hao.driver@driverent.vn", status: "Offline", rating: 4.4, trips: 145, city: "Da Nang", location: "Hai Chau", license: "B2-223344", joined: "Feb 14, 2024", vehicle: "—", distance: "18,900 km", completion: "93%", activity: [
      { title: "Chuyển ngoại tuyến", time: "Sep 13, 20:00" },
      { title: "Hoàn thành chuyến", time: "Sep 13, 18:15" },
      { title: "Khách hàng đánh giá 4 sao", time: "Sep 13, 18:00" },
      { title: "Hoàn thành chuyến", time: "Sep 11, 14:40" }
    ]},
    { id: "DRV-208", name: "Bùi Khánh Duy", phone: "0913 888 999", email: "duy.driver@driverent.vn", status: "Busy", rating: 4.7, trips: 198, city: "HCMC", location: "District 5", license: "B2-112255", joined: "Apr 09, 2024", vehicle: "Mitsubishi Xpander · 51D-456.78", distance: "24,600 km", completion: "96%", activity: [
      { title: "Bắt đầu chuyến", time: "Hôm nay, 09:45" },
      { title: "Hoàn thành chuyến", time: "Hôm qua, 16:20" },
      { title: "Khách hàng đánh giá 5 sao", time: "Hôm qua, 16:05" },
      { title: "Hoàn thành chuyến", time: "Sep 12, 11:50" }
    ]}
  ];

  const mockPayments = [
    { id: "TXN-9001", customer: "Nguyễn Văn An", bookingId: "BK-20260914-001", deposit: "$60.00", method: "Credit Card", paymentStatus: "Pending", contractStatus: "Issued", datetime: "Sep 12, 2026 14:25" },
    { id: "TXN-9002", customer: "Trần Thị Mai", bookingId: "BK-20260914-002", deposit: "$90.00", method: "Momo", paymentStatus: "Paid", contractStatus: "Signed", datetime: "Sep 12, 2026 16:10" },
    { id: "TXN-9003", customer: "Lê Quang Huy", bookingId: "BK-20260913-015", deposit: "$160.00", method: "VNPAY", paymentStatus: "Paid", contractStatus: "Signed", datetime: "Sep 11, 2026 09:45" },
    { id: "TXN-9004", customer: "Phạm Thu Hà", bookingId: "BK-20260912-044", deposit: "$120.00", method: "Bank Transfer", paymentStatus: "Paid", contractStatus: "Signed", datetime: "Sep 10, 2026 11:22" },
    { id: "TXN-9005", customer: "Hoàng Minh Tuấn", bookingId: "BK-20260911-028", deposit: "$200.00", method: "Cash", paymentStatus: "Failed", contractStatus: "Voided", datetime: "Sep 09, 2026 18:10" },
    { id: "TXN-9006", customer: "Đỗ Bảo Ngọc", bookingId: "BK-20260910-061", deposit: "$50.00", method: "Credit Card", paymentStatus: "Paid", contractStatus: "Signed", datetime: "Sep 08, 2026 09:00" },
    { id: "TXN-9007", customer: "Vũ Đức Anh", bookingId: "BK-20260909-077", deposit: "$180.00", method: "Momo", paymentStatus: "Pending", contractStatus: "Issued", datetime: "Sep 07, 2026 13:40" },
    { id: "TXN-9008", customer: "Bùi Thanh Trúc", bookingId: "BK-20260908-090", deposit: "$145.00", method: "Bank Transfer", paymentStatus: "Paid", contractStatus: "Signed", datetime: "Sep 06, 2026 10:20" },
    { id: "TXN-9009", customer: "Ngô Nhật Nam", bookingId: "BK-20260907-103", deposit: "$105.00", method: "VNPAY", paymentStatus: "Paid", contractStatus: "Signed", datetime: "Sep 05, 2026 15:50" },
    { id: "TXN-9010", customer: "Lý Mỹ Linh", bookingId: "BK-20260906-118", deposit: "$110.00", method: "Credit Card", paymentStatus: "Pending", contractStatus: "Issued", datetime: "Sep 04, 2026 09:30" },
    { id: "TXN-9011", customer: "Trịnh Quốc Bảo", bookingId: "BK-20260905-130", deposit: "$35.00", method: "Cash", paymentStatus: "Paid", contractStatus: "Signed", datetime: "Sep 03, 2026 17:15" },
    { id: "TXN-9012", customer: "Huỳnh Gia Hân", bookingId: "BK-20260904-145", deposit: "$135.00", method: "Momo", paymentStatus: "Failed", contractStatus: "Voided", datetime: "Sep 02, 2026 13:05" }
  ];

  const mockMaintenance = [
    { plate: "51A-123.45", model: "Toyota Vios", km: "42,180", lastService: "Jul 20, 2026", nextService: "Oct 20, 2026", status: "Healthy" },
    { plate: "51B-234.56", model: "Honda City", km: "58,420", lastService: "Jun 12, 2026", nextService: "Sep 18, 2026", status: "Due Soon" },
    { plate: "51C-345.67", model: "Toyota Camry", km: "21,050", lastService: "Aug 02, 2026", nextService: "Nov 02, 2026", status: "Healthy" },
    { plate: "51D-456.78", model: "Mitsubishi Xpander", km: "67,900", lastService: "May 05, 2026", nextService: "Aug 05, 2026", status: "Overdue" },
    { plate: "51E-567.89", model: "Ford Everest", km: "73,210", lastService: "Sep 01, 2026", nextService: "Dec 01, 2026", status: "In Service" },
    { plate: "51F-678.90", model: "Kia Carnival", km: "18,640", lastService: "Aug 22, 2026", nextService: "Nov 22, 2026", status: "Healthy" },
    { plate: "51G-789.01", model: "Mazda CX-5", km: "39,880", lastService: "Jul 08, 2026", nextService: "Sep 25, 2026", status: "Due Soon" },
    { plate: "51H-890.12", model: "Hyundai Accent", km: "91,300", lastService: "Apr 14, 2026", nextService: "Jul 14, 2026", status: "Overdue" },
    { plate: "51K-901.23", model: "Toyota Fortuner", km: "54,100", lastService: "Aug 28, 2026", nextService: "Nov 28, 2026", status: "Healthy" },
    { plate: "51L-012.34", model: "VinFast VF 8", km: "12,450", lastService: "Sep 05, 2026", nextService: "Dec 05, 2026", status: "Healthy" }
  ];

  const mockIncidents = [
    { id: "INC-501", datetime: "Sep 14, 2026 08:20", vehicle: "Mitsubishi Xpander · 51D-456.78", type: "Engine Overheat", severity: "High", status: "Investigating" },
    { id: "INC-502", datetime: "Sep 13, 2026 16:45", vehicle: "Hyundai Accent · 51H-890.12", type: "Accident", severity: "Critical", status: "In Progress" },
    { id: "INC-503", datetime: "Sep 12, 2026 11:10", vehicle: "Toyota Vios · 51A-123.45", type: "Minor Scratch", severity: "Low", status: "Resolved" },
    { id: "INC-504", datetime: "Sep 11, 2026 19:30", vehicle: "Ford Everest · 51E-567.89", type: "Flat Tire", severity: "Medium", status: "Closed" },
    { id: "INC-505", datetime: "Sep 10, 2026 09:05", vehicle: "Honda City · 51B-234.56", type: "Customer Damage", severity: "Medium", status: "Resolved" },
    { id: "INC-506", datetime: "Sep 09, 2026 14:55", vehicle: "Kia Carnival · 51F-678.90", type: "Brake Problem", severity: "High", status: "In Progress" },
    { id: "INC-507", datetime: "Sep 08, 2026 07:40", vehicle: "Mazda CX-5 · 51G-789.01", type: "Battery Issue", severity: "Medium", status: "Investigating" },
    { id: "INC-508", datetime: "Sep 07, 2026 21:15", vehicle: "Toyota Fortuner · 51K-901.23", type: "Electrical Issue", severity: "Low", status: "Closed" },
    { id: "INC-509", datetime: "Sep 06, 2026 12:00", vehicle: "VinFast VF 8 · 51L-012.34", type: "Mechanical Issue", severity: "High", status: "Resolved" },
    { id: "INC-510", datetime: "Sep 05, 2026 17:25", vehicle: "Toyota Camry · 51C-345.67", type: "Minor Scratch", severity: "Low", status: "Closed" }
  ];

  const paymentMethodBreakdown = [
    { label: "Credit Card", value: 38, color: "#2563EB" },
    { label: "Bank Transfer", value: 22, color: "#7C3AED" },
    { label: "Momo", value: 18, color: "#D97706" },
    { label: "VNPAY", value: 14, color: "#16A34A" },
    { label: "Cash", value: 8, color: "#64748B" }
  ];

  const contractActivities = [
    { title: "Hợp đồng đã ký", detail: "TXN-9002 · Trần Thị Mai", time: "2 giờ trước", icon: "bi-pen" },
    { title: "Hợp đồng đã phát hành", detail: "TXN-9001 · Nguyễn Văn An", time: "4 giờ trước", icon: "bi-file-earmark-text" },
    { title: "Hợp đồng đã vô hiệu", detail: "TXN-9005 · Hoàng Minh Tuấn", time: "1 ngày trước", icon: "bi-x-circle" },
    { title: "Hợp đồng đã ký", detail: "TXN-9003 · Lê Quang Huy", time: "1 ngày trước", icon: "bi-pen" },
    { title: "Hợp đồng đã phát hành", detail: "TXN-9007 · Vũ Đức Anh", time: "2 ngày trước", icon: "bi-file-earmark-text" }
  ];

  const upcomingServices = [
    { plate: "51B-234.56", model: "Honda City", date: "18/09/2026", type: "Thay dầu" },
    { plate: "51G-789.01", model: "Mazda CX-5", date: "25/09/2026", type: "Đảo lốp" },
    { plate: "51A-123.45", model: "Toyota Vios", date: "20/10/2026", type: "Kiểm tra toàn diện" },
    { plate: "51F-678.90", model: "Kia Carnival", date: "22/11/2026", type: "Kiểm tra phanh" },
    { plate: "51C-345.67", model: "Toyota Camry", date: "02/11/2026", type: "Kiểm tra ắc quy" }
  ];

  return {
    mockDashboardData,
    mockBookings,
    mockVehicles,
    mockCustomers,
    mockDrivers,
    mockPayments,
    mockMaintenance,
    mockIncidents,
    paymentMethodBreakdown,
    contractActivities,
    upcomingServices,
    revenueByMonth,
    months
  };
})();

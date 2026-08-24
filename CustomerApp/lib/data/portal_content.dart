class PopularRoute {
  final String title;
  final String from;
  final String to;
  final int distanceKm;
  final String duration;
  final String suggestedVehicle;
  final int vehicleTypeId;
  final double estimatedPrice;
  final String description;
  final String icon;

  const PopularRoute({
    required this.title,
    required this.from,
    required this.to,
    required this.distanceKm,
    required this.duration,
    required this.suggestedVehicle,
    required this.vehicleTypeId,
    required this.estimatedPrice,
    required this.description,
    required this.icon,
  });
}

class FeatureItem {
  final String icon;
  final String title;
  final String description;

  const FeatureItem({
    required this.icon,
    required this.title,
    required this.description,
  });
}

class ProcessStep {
  final int step;
  final String title;
  final String description;

  const ProcessStep({
    required this.step,
    required this.title,
    required this.description,
  });
}

class PortalContent {
  static const popularRoutes = [
    PopularRoute(
      title: 'Vung Tau bien xanh',
      from: 'TP.HCM',
      to: 'Vung Tau',
      distanceKm: 120,
      duration: '1 ngay',
      suggestedVehicle: '4-7 cho',
      vehicleTypeId: 2,
      estimatedPrice: 1200000,
      description: 'Di bien cuoi tuan, phu hop gia dinh va nhom ban.',
      icon: '🏖️',
    ),
    PopularRoute(
      title: 'Da Lat thong gio',
      from: 'TP.HCM',
      to: 'Da Lat',
      distanceKm: 300,
      duration: '2-3 ngay',
      suggestedVehicle: '7-16 cho',
      vehicleTypeId: 2,
      estimatedPrice: 3500000,
      description: 'Kham pha thac Datanla, lang hoa, thoi tiet mat me.',
      icon: '🌲',
    ),
    PopularRoute(
      title: 'Nha Trang nang vang',
      from: 'TP.HCM',
      to: 'Nha Trang',
      distanceKm: 450,
      duration: '3-4 ngay',
      suggestedVehicle: '7-16 cho',
      vehicleTypeId: 3,
      estimatedPrice: 5000000,
      description: 'Tam bien, VinWonders, am thuc bien dao.',
      icon: '🌊',
    ),
    PopularRoute(
      title: 'Mui Ne cat trang',
      from: 'TP.HCM',
      to: 'Phan Thiet - Mui Ne',
      distanceKm: 250,
      duration: '2 ngay',
      suggestedVehicle: '7 cho SUV',
      vehicleTypeId: 2,
      estimatedPrice: 2800000,
      description: 'Doi cat bay, san ho do, resort nghi duong.',
      icon: '🏜️',
    ),
    PopularRoute(
      title: 'Can Tho mien Tay',
      from: 'TP.HCM',
      to: 'Can Tho',
      distanceKm: 170,
      duration: '1-2 ngay',
      suggestedVehicle: '7-16 cho',
      vehicleTypeId: 3,
      estimatedPrice: 1800000,
      description: 'Cho noi Cai Rang, vuon trai, am thuc mien Tay.',
      icon: '🛶',
    ),
    PopularRoute(
      title: 'Tour noi thanh',
      from: 'TP.HCM',
      to: 'Quan 1 - Thu Duc',
      distanceKm: 40,
      duration: 'Nua ngay',
      suggestedVehicle: '4 cho Sedan',
      vehicleTypeId: 1,
      estimatedPrice: 600000,
      description: 'Tham quan Dinh Doc Lap, Bui Vien, Landmark 81.',
      icon: '🏙️',
    ),
  ];

  static const features = [
    FeatureItem(icon: '🌐', title: 'Dat xe 24/7', description: 'Dat xe moi luc tu app, khong can den van phong.'),
    FeatureItem(icon: '🚗', title: 'Xe 4-16 cho', description: 'Sedan, SUV, Van, Limousine cho ca nhan va doan.'),
    FeatureItem(icon: '👨‍✈️', title: 'Tai xe chuyen nghiep', description: 'Lai xe co bang, kinh nghiem tour du lich.'),
    FeatureItem(icon: '💰', title: 'Gia minh bach', description: 'Bao gia theo ngay + km truoc khi xac nhan.'),
    FeatureItem(icon: '📍', title: 'Theo doi don', description: 'Xem trang thai don tu dat den hoan thanh.'),
    FeatureItem(icon: '⭐', title: 'Danh gia dich vu', description: 'Danh gia chuyen di sau khi ket thuc.'),
  ];

  static const steps = [
    ProcessStep(step: 1, title: 'Chon tuyen / xe', description: 'Xem goi y tuyen pho bien va bang gia.'),
    ProcessStep(step: 2, title: 'Gui yeu cau', description: 'Dien diem don, tra, thoi gian.'),
    ProcessStep(step: 3, title: 'Xac nhan', description: 'Dieu phoi xac nhan va gan tai xe.'),
    ProcessStep(step: 4, title: 'Di & danh gia', description: 'Thuc hien chuyen va danh gia dich vu.'),
  ];
}

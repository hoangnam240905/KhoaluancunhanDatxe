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
      title: 'Vũng Tàu biển xanh',
      from: 'TP.HCM',
      to: 'Vũng Tàu',
      distanceKm: 120,
      duration: '1 ngày',
      suggestedVehicle: '4-7 chỗ',
      vehicleTypeId: 2,
      estimatedPrice: 1200000,
      description: 'Đi biển cuối tuần, phù hợp gia đình và nhóm bạn.',
      icon: '🏖️',
    ),
    PopularRoute(
      title: 'Đà Lạt thông gió',
      from: 'TP.HCM',
      to: 'Đà Lạt',
      distanceKm: 300,
      duration: '2-3 ngày',
      suggestedVehicle: '7-16 chỗ',
      vehicleTypeId: 2,
      estimatedPrice: 3500000,
      description: 'Khám phá thác Datanla, làng hoa, thời tiết mát mẻ.',
      icon: '🌲',
    ),
    PopularRoute(
      title: 'Nha Trang nắng vàng',
      from: 'TP.HCM',
      to: 'Nha Trang',
      distanceKm: 450,
      duration: '3-4 ngày',
      suggestedVehicle: '7-16 chỗ',
      vehicleTypeId: 3,
      estimatedPrice: 5000000,
      description: 'Tắm biển, VinWonders, ẩm thực biển đảo.',
      icon: '🌊',
    ),
    PopularRoute(
      title: 'Mũi Né cát trắng',
      from: 'TP.HCM',
      to: 'Phan Thiết - Mũi Né',
      distanceKm: 250,
      duration: '2 ngày',
      suggestedVehicle: '7 chỗ SUV',
      vehicleTypeId: 2,
      estimatedPrice: 2800000,
      description: 'Đồi cát bay, san hô đỏ, resort nghỉ dưỡng.',
      icon: '🏜️',
    ),
    PopularRoute(
      title: 'Cần Thơ miền Tây',
      from: 'TP.HCM',
      to: 'Cần Thơ',
      distanceKm: 170,
      duration: '1-2 ngày',
      suggestedVehicle: '7-16 chỗ',
      vehicleTypeId: 3,
      estimatedPrice: 1800000,
      description: 'Chợ nổi Cái Răng, vườn trái, ẩm thực miền Tây.',
      icon: '🛶',
    ),
    PopularRoute(
      title: 'Tour nội thành',
      from: 'TP.HCM',
      to: 'Quận 1 - Thủ Đức',
      distanceKm: 40,
      duration: 'Nửa ngày',
      suggestedVehicle: '4 chỗ Sedan',
      vehicleTypeId: 1,
      estimatedPrice: 600000,
      description: 'Tham quan Dinh Độc Lập, Bùi Viện, Landmark 81.',
      icon: '🏙️',
    ),
  ];

  static const features = [
    FeatureItem(icon: '🌐', title: 'Đặt xe 24/7', description: 'Đặt xe mọi lúc từ app, không cần đến văn phòng.'),
    FeatureItem(icon: '🚗', title: 'Xe 4-16 chỗ', description: 'Sedan, SUV, Van, Limousine cho cá nhân và đoàn.'),
    FeatureItem(icon: '👨‍✈️', title: 'Tài xế chuyên nghiệp', description: 'Lái xe có bằng, kinh nghiệm tour du lịch.'),
    FeatureItem(icon: '💰', title: 'Giá minh bạch', description: 'Báo giá theo ngày + km trước khi xác nhận.'),
    FeatureItem(icon: '📍', title: 'Theo dõi đơn', description: 'Xem trạng thái đơn từ đặt đến hoàn thành.'),
    FeatureItem(icon: '⭐', title: 'Đánh giá dịch vụ', description: 'Đánh giá chuyến đi sau khi kết thúc.'),
  ];

  static const steps = [
    ProcessStep(step: 1, title: 'Chọn tuyến / xe', description: 'Xem gợi ý tuyến phổ biến và bảng giá.'),
    ProcessStep(step: 2, title: 'Gửi yêu cầu', description: 'Điền điểm đón, trả, thời gian.'),
    ProcessStep(step: 3, title: 'Xác nhận', description: 'Điều phối xác nhận và gán tài xế.'),
    ProcessStep(step: 4, title: 'Đi & đánh giá', description: 'Thực hiện chuyến và đánh giá dịch vụ.'),
  ];
}

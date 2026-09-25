import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'create_booking_screen.dart';
import 'login_screen.dart';

class VehicleDetailScreen extends StatefulWidget {
  final ApiService api;
  final VehicleType vehicleType;
  final List<VehicleType> allTypes;
  final bool isLoggedIn;
  final String? initialRentalMode;

  const VehicleDetailScreen({
    super.key,
    required this.api,
    required this.vehicleType,
    required this.allTypes,
    required this.isLoggedIn,
    this.initialRentalMode,
  });

  @override
  State<VehicleDetailScreen> createState() => _VehicleDetailScreenState();
}

class _VehicleDetailScreenState extends State<VehicleDetailScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  late String _rentalMode;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
    _rentalMode = widget.initialRentalMode ?? 'WithDriver';
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  void _openBooking() {
    if (!widget.isLoggedIn) {
      Navigator.push(
        context,
        MaterialPageRoute(builder: (_) => const LoginScreen()),
      );
      return;
    }
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => CreateBookingScreen(
          api: widget.api,
          vehicleTypes: widget.allTypes,
          vehicleType: widget.vehicleType,
          initialRentalMode: _rentalMode,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final v = widget.vehicleType;
    final isSelf = _rentalMode == 'SelfDrive';
    final pricePerDay = v.pricePerDay;
    final driverFee = isSelf ? 0.0 : 300000.0;
    final totalDaily = pricePerDay + driverFee;
    final deposit = pricePerDay * 0.3;

    return Scaffold(
      body: NestedScrollView(
        headerSliverBuilder: (context, innerBoxIsScrolled) => [
          SliverAppBar(
            expandedHeight: 280,
            pinned: true,
            flexibleSpace: FlexibleSpaceBar(
              background: Stack(
                fit: StackFit.expand,
                children: [
                  VehicleImage(
                    imageUrl: v.imageUrl,
                    typeName: v.typeName,
                    seatCapacity: v.seatCapacity,
                    height: 280,
                    borderRadius: BorderRadius.zero,
                  ),
                  Container(
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: [
                          Colors.black.withValues(alpha: 0.6),
                          Colors.transparent,
                          Colors.black.withValues(alpha: 0.8),
                        ],
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                      ),
                    ),
                  ),
                  Positioned(
                    bottom: 16,
                    left: 20,
                    right: 20,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: AppColors.primary,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            '${v.seatCapacity} Chỗ ngồi',
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 12,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          v.typeName,
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 24,
                            fontWeight: FontWeight.w900,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
        body: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
          children: [
            // Mode Toggle
            const Text(
              'Chọn gói dịch vụ',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 8),
            RentalModeToggle(
              value: _rentalMode,
              onChanged: (val) => setState(() => _rentalMode = val),
            ),
            const SizedBox(height: 16),

            // Pricing summary card
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: AppColors.border),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.04),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  ),
                ],
              ),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'Giá thuê ước tính',
                            style: TextStyle(color: AppColors.muted, fontSize: 13),
                          ),
                          const SizedBox(height: 4),
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.baseline,
                            textBaseline: TextBaseline.alphabetic,
                            children: [
                              Text(
                                Formatters.vnd(totalDaily),
                                style: const TextStyle(
                                  fontSize: 22,
                                  fontWeight: FontWeight.w900,
                                  color: AppColors.primary,
                                ),
                              ),
                              const Text(
                                ' / ngày',
                                style: TextStyle(color: AppColors.muted, fontSize: 13),
                              ),
                            ],
                          ),
                        ],
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          color: const Color(0xFFEFF6FF),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          isSelf ? 'Gói tự lái' : 'Gói có tài xế',
                          style: const TextStyle(
                            color: AppColors.primary,
                            fontWeight: FontWeight.w700,
                            fontSize: 12,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const Divider(height: 24, color: AppColors.border),
                  _priceRow('Tiền cọc quy định (30%)', Formatters.vnd(deposit)),
                  if (!isSelf) ...[
                    const SizedBox(height: 6),
                    _priceRow('Phí dịch vụ tài xế', '${Formatters.vnd(driverFee)}/ngày'),
                  ],
                  if (isSelf) ...[
                    const SizedBox(height: 6),
                    const _PriceTextRow(title: 'Km miễn phí quy định', value: '200 km/ngày'),
                    const SizedBox(height: 6),
                    _priceRow('Phí km vượt quy định', '${Formatters.vnd(v.pricePerKm)}/km'),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Tab Bar for Info
            Container(
              decoration: BoxDecoration(
                color: const Color(0xFFF1F5F9),
                borderRadius: BorderRadius.circular(12),
              ),
              child: TabBar(
                controller: _tabController,
                indicator: BoxDecoration(
                  borderRadius: BorderRadius.circular(10),
                  color: Colors.white,
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.05),
                      blurRadius: 4,
                      offset: const Offset(0, 2),
                    ),
                  ],
                ),
                indicatorSize: TabBarIndicatorSize.tab,
                labelColor: AppColors.primary,
                unselectedLabelColor: AppColors.muted,
                labelStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
                dividerColor: Colors.transparent,
                tabs: const [
                  Tab(text: 'Thông số'),
                  Tab(text: 'Tiện ích'),
                  Tab(text: 'Chính sách'),
                ],
              ),
            ),
            const SizedBox(height: 16),

            // Tab Content
            SizedBox(
              height: 230,
              child: TabBarView(
                controller: _tabController,
                children: [
                  _specsTab(v),
                  _featuresTab(v),
                  _policyTab(isSelf, deposit),
                ],
              ),
            ),

            const SizedBox(height: 16),

            // Customer Reviews sample (Mirror from Web)
            const SectionHeader(
              icon: Icons.star_rounded,
              title: 'Đánh giá từ khách hàng',
              subtitle: 'Điểm trung bình 4.9/5.0 từ hơn 100+ lượt thuê',
            ),
            const SizedBox(height: 12),
            _reviewItem(
              name: 'Anh Hoàng Nam',
              time: '2 ngày trước',
              rating: 5,
              content: 'Xe mới, máy êm, nội thất rất sạch sẽ. Tài xế đón đúng giờ và nhiệt tình chu đáo.',
            ),
            const SizedBox(height: 8),
            _reviewItem(
              name: 'Chị Mai Linh',
              time: 'Tuần trước',
              rating: 5,
              content: 'Thủ tục thuê xe và hoàn cọc rất nhanh gọn qua app. Đi công tác rất yên tâm.',
            ),
          ],
        ),
      ),
      bottomSheet: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          border: const Border(top: BorderSide(color: AppColors.border)),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.08),
              blurRadius: 10,
              offset: const Offset(0, -4),
            ),
          ],
        ),
        child: SafeArea(
          child: Row(
            children: [
              Expanded(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Tổng chi phí dự kiến', style: TextStyle(color: AppColors.muted, fontSize: 12)),
                    Text(
                      Formatters.vnd(totalDaily),
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w900,
                        color: AppColors.text,
                      ),
                    ),
                  ],
                ),
              ),
              FilledButton.icon(
                onPressed: _openBooking,
                icon: const Icon(Icons.calendar_today_outlined, size: 18),
                label: Text(widget.isLoggedIn ? 'Đặt xe ngay' : 'Đăng nhập để đặt'),
                style: FilledButton.styleFrom(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _priceRow(String title, String value) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: const TextStyle(color: AppColors.muted, fontSize: 13)),
        Text(value, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: AppColors.text)),
      ],
    );
  }

  Widget _specsTab(VehicleType v) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: [
          _specRow(Icons.people_outline, 'Số chỗ ngồi', '${v.seatCapacity} người lớn'),
          const Divider(height: 16, color: AppColors.border),
          _specRow(Icons.settings_outlined, 'Hộp số', 'Tự động 6-8 cấp'),
          const Divider(height: 16, color: AppColors.border),
          _specRow(Icons.local_gas_station_outlined, 'Nhiên liệu', 'Xăng / Dầu tiết kiệm'),
          const Divider(height: 16, color: AppColors.border),
          _specRow(Icons.sensor_door_outlined, 'Cửa ra vào', '4 - 5 cửa tiện nghi'),
        ],
      ),
    );
  }

  Widget _featuresTab(VehicleType v) {
    final features = [
      'Điều hòa làm mát 2 vùng độc lập',
      'Màn hình giải trí Apple CarPlay / Android Auto',
      'Camera lùi và cảm biến va chạm',
      'Định vị GPS hỗ trợ dẫn đường',
      'Bảo hiểm xe và hành khách toàn diện',
    ];

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: ListView.separated(
        itemCount: features.length,
        separatorBuilder: (context, index) => const SizedBox(height: 8),
        itemBuilder: (context, i) => Row(
          children: [
            const Icon(Icons.check_circle_outline, color: AppColors.success, size: 18),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                features[i],
                style: const TextStyle(fontSize: 13, color: AppColors.text),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _policyTab(bool isSelf, num deposit) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: ListView(
        children: [
          _policyRow('Đặt cọc:', 'Cần thanh toán trước ${Formatters.vnd(deposit)} để giữ lịch xe.'),
          const SizedBox(height: 8),
          _policyRow('Hợp đồng:', 'Ký hợp đồng thuê xe điện tử trực tiếp trên ứng dụng.'),
          const SizedBox(height: 8),
          _policyRow('Hủy đơn:', 'Hủy miễn phí trước 24h so với giờ khởi hành.'),
          const SizedBox(height: 8),
          _policyRow(isSelf ? 'Giấy tờ tự lái:' : 'Đưa đón:', isSelf
              ? 'Yêu cầu CCCD gắn chip và Giấy phép lái xe hạng B2 trở lên.'
              : 'Tài xế liên hệ trước 30 phút và đón đúng địa điểm yêu cầu.'),
        ],
      ),
    );
  }

  Widget _specRow(IconData icon, String label, String val) {
    return Row(
      children: [
        Icon(icon, size: 18, color: AppColors.primary),
        const SizedBox(width: 10),
        Text(label, style: const TextStyle(color: AppColors.muted, fontSize: 13)),
        const Spacer(),
        Text(val, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
      ],
    );
  }

  Widget _policyRow(String bold, String normal) {
    return RichText(
      text: TextSpan(
        style: const TextStyle(fontSize: 13, color: AppColors.text, height: 1.4),
        children: [
          TextSpan(text: '$bold ', style: const TextStyle(fontWeight: FontWeight.w700)),
          TextSpan(text: normal),
        ],
      ),
    );
  }

  Widget _reviewItem({
    required String name,
    required String time,
    required int rating,
    required String content,
  }) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13)),
              Row(
                children: List.generate(
                  5,
                  (i) => Icon(
                    i < rating ? Icons.star_rounded : Icons.star_outline_rounded,
                    color: Colors.amber,
                    size: 16,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Text(content, style: const TextStyle(fontSize: 12, color: AppColors.text)),
          const SizedBox(height: 4),
          Text(time, style: const TextStyle(fontSize: 11, color: AppColors.muted)),
        ],
      ),
    );
  }
}

class _PriceTextRow extends StatelessWidget {
  final String title;
  final String value;

  const _PriceTextRow({required this.title, required this.value});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: const TextStyle(color: AppColors.muted, fontSize: 13)),
        Text(value, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: AppColors.text)),
      ],
    );
  }
}

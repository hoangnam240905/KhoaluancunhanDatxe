import 'package:flutter/material.dart';
import '../data/portal_content.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'create_booking_screen.dart';
import 'login_screen.dart';
import 'vehicle_detail_screen.dart';

class HomeScreen extends StatefulWidget {
  final ApiService api;
  final String? userName;

  const HomeScreen({super.key, required this.api, this.userName});

  bool get isLoggedIn => userName != null && userName!.isNotEmpty;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  List<VehicleType> _types = [];
  List<VehicleTypeRecommendation> _recommended = [];
  bool _loading = true;
  bool _recommendLoading = false;
  String? _error;
  String? _recommendError;
  String _rentalMode = 'WithDriver';
  String _selectedCategory = 'Tất cả';
  DateTime _start = DateTime.now().add(const Duration(days: 1));
  DateTime _end = DateTime.now().add(const Duration(days: 2));
  final _seats = TextEditingController();
  final _priceMax = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _types = await widget.api.getVehicleTypes();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _requireLoginThen(VoidCallback action) {
    if (widget.isLoggedIn) {
      action();
      return;
    }
    Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => const LoginScreen()),
    );
  }

  void _openDetail(VehicleType type) {
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => VehicleDetailScreen(
          api: widget.api,
          vehicleType: type,
          allTypes: _types,
          isLoggedIn: widget.isLoggedIn,
          initialRentalMode: _rentalMode,
        ),
      ),
    );
  }

  void _openBooking({
    VehicleType? type,
    PopularRoute? route,
    DateTime? start,
    DateTime? end,
    bool fromRecommendation = false,
  }) {
    _requireLoginThen(() {
      Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => CreateBookingScreen(
            api: widget.api,
            vehicleTypes: _types,
            vehicleType: type,
            initialRentalMode: _rentalMode,
            pickup: route?.from,
            dropoff: route?.to,
            distanceKm: route?.distanceKm.toDouble(),
            initialStart: start,
            initialEnd: end,
            fromRecommendation: fromRecommendation,
          ),
        ),
      );
    });
  }

  List<VehicleType> get _filteredTypes {
    if (_selectedCategory == 'Tất cả') return _types;
    if (_selectedCategory == '4 chỗ') {
      return _types.where((t) => t.seatCapacity <= 4).toList();
    }
    if (_selectedCategory == '7 chỗ') {
      return _types.where((t) => t.seatCapacity > 4 && t.seatCapacity <= 7).toList();
    }
    if (_selectedCategory == '16 chỗ') {
      return _types.where((t) => t.seatCapacity > 7 && t.seatCapacity <= 16 && !t.typeName.contains('Limousine')).toList();
    }
    if (_selectedCategory == 'Limousine') {
      return _types.where((t) => t.typeName.toLowerCase().contains('limousine')).toList();
    }
    return _types;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: RefreshIndicator(
        onRefresh: _load,
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              pinned: true,
              expandedHeight: 210,
              backgroundColor: AppColors.primaryDark,
              foregroundColor: Colors.white,
              actions: [
                if (!widget.isLoggedIn)
                  Padding(
                    padding: const EdgeInsets.only(right: 12),
                    child: FilledButton.tonal(
                      style: FilledButton.styleFrom(
                        minimumSize: const Size(80, 36),
                        padding: const EdgeInsets.symmetric(horizontal: 14),
                      ),
                      onPressed: () => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => const LoginScreen()),
                      ),
                      child: const Text('Đăng nhập'),
                    ),
                  ),
              ],
              flexibleSpace: FlexibleSpaceBar(
                background: Stack(
                  fit: StackFit.expand,
                  children: [
                    Image.asset(
                      'assets/images/home/hero.jpg',
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) => const SizedBox(),
                    ),
                    Container(
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            const Color(0xFF0F172A).withValues(alpha: 0.88),
                            const Color(0xFF1E3A8A).withValues(alpha: 0.82),
                            const Color(0xFF2563EB).withValues(alpha: 0.70),
                          ],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(20, 78, 20, 16),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(6),
                                decoration: BoxDecoration(
                                  color: Colors.white.withValues(alpha: 0.15),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: const Icon(Icons.car_rental, color: Colors.white, size: 18),
                              ),
                              const SizedBox(width: 8),
                              Text(
                                widget.isLoggedIn
                                    ? 'Xin chào, ${widget.userName}'
                                    : 'DriveX Car Rental',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 14,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 10),
                          const Text(
                            'Thuê xe du lịch\n4 - 16 chỗ cao cấp',
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 24,
                              fontWeight: FontWeight.w900,
                              height: 1.2,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            SliverToBoxAdapter(child: _body()),
          ],
        ),
      ),
    );
  }

  Widget _body() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SectionHeader(
            icon: Icons.swap_horiz_rounded,
            title: 'Hình thức thuê xe',
            subtitle: 'Lựa chọn gói tự lái hoặc có tài xế phục vụ',
          ),
          const SizedBox(height: 12),
          RentalModeToggle(
            value: _rentalMode,
            onChanged: (v) => setState(() => _rentalMode = v),
          ),
          const SizedBox(height: 24),

          // Smart Recommender
          const SectionHeader(
            icon: Icons.auto_awesome,
            title: 'Gợi ý xe thông minh',
            subtitle: 'Đề xuất xe phù hợp theo lịch trình và ngân sách của bạn',
          ),
          const SizedBox(height: 12),
          _recommendForm(),
          if (_recommendError != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(_recommendError!, style: const TextStyle(color: Colors.red)),
            ),
          if (_recommendLoading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: AppLoading(message: 'Đang tìm xe tốt nhất...'),
            )
          else
            ..._recommended.map(_recommendCard),

          const SizedBox(height: 28),

          // Vehicle Catalog
          SectionHeader(
            icon: Icons.directions_car_filled_outlined,
            title: 'Danh mục xe',
            subtitle: 'Bảng giá niêm yết minh bạch theo ngày và km',
            trailing: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                '${_filteredTypes.length} xe',
                style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700, fontSize: 12),
              ),
            ),
          ),
          const SizedBox(height: 12),

          // Category Chips
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: ['Tất cả', '4 chỗ', '7 chỗ', '16 chỗ', 'Limousine'].map((cat) {
                final isSelected = _selectedCategory == cat;
                return Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: FilterChip(
                    label: Text(cat),
                    selected: isSelected,
                    onSelected: (_) => setState(() => _selectedCategory = cat),
                    selectedColor: AppColors.primary.withValues(alpha: 0.15),
                    labelStyle: TextStyle(
                      color: isSelected ? AppColors.primary : AppColors.muted,
                      fontWeight: isSelected ? FontWeight.w800 : FontWeight.w500,
                    ),
                    side: BorderSide(
                      color: isSelected ? AppColors.primary : AppColors.border,
                    ),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                  ),
                );
              }).toList(),
            ),
          ),
          const SizedBox(height: 12),

          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 32),
              child: AppLoading(message: 'Đang tải loại xe...'),
            )
          else if (_error != null)
            AppErrorState(message: _error!, onRetry: _load)
          else if (_filteredTypes.isEmpty)
            const AppEmptyState(
              icon: Icons.directions_car_outlined,
              title: 'Không tìm thấy xe phù hợp',
              subtitle: 'Hãy chọn phân khúc xe khác hoặc kiểm tra kết nối API.',
            )
          else
            ..._filteredTypes.map(_vehicleCard),

          const SizedBox(height: 28),

          // Popular Routes
          const SectionHeader(
            icon: Icons.map_outlined,
            title: 'Tuyến đường phổ biến',
            subtitle: 'Điểm đón/trả và lộ trình được điền sẵn tiện lợi',
          ),
          const SizedBox(height: 12),
          ...PortalContent.popularRoutes.map(_routeCard),
        ],
      ),
    );
  }

  @override
  void dispose() {
    _seats.dispose();
    _priceMax.dispose();
    super.dispose();
  }

  Future<void> _pickStart() async {
    final date = await showDatePicker(
      context: context,
      initialDate: _start,
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (date == null) return;
    setState(() => _start = DateTime(date.year, date.month, date.day, 8));
  }

  Future<void> _pickEnd() async {
    final date = await showDatePicker(
      context: context,
      initialDate: _end,
      firstDate: _start,
      lastDate: DateTime.now().add(const Duration(days: 400)),
    );
    if (date == null) return;
    setState(() => _end = DateTime(date.year, date.month, date.day, 18));
  }

  Future<void> _loadRecommended() async {
    setState(() {
      _recommendLoading = true;
      _recommendError = null;
    });
    try {
      final seats = int.tryParse(_seats.text.trim());
      final price = double.tryParse(_priceMax.text.trim());
      _recommended = await widget.api.getRecommended(
        startDate: _start,
        endDate: _end,
        seats: seats,
        priceMax: price,
      );
      if (_recommended.isEmpty) {
        _recommendError = 'Không có loại xe khả dụng trong khoảng thời gian này.';
      }
    } catch (e) {
      _recommendError = e.toString().replaceFirst('Exception: ', '');
      _recommended = [];
    } finally {
      if (mounted) setState(() => _recommendLoading = false);
    }
  }

  Widget _recommendForm() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _pickStart,
                  icon: const Icon(Icons.calendar_today, size: 16),
                  label: Text(
                    'Từ: ${Formatters.rentalDt(_start)}',
                    style: const TextStyle(fontSize: 12),
                  ),
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 12),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _pickEnd,
                  icon: const Icon(Icons.event, size: 16),
                  label: Text(
                    'Đến: ${Formatters.rentalDt(_end)}',
                    style: const TextStyle(fontSize: 12),
                  ),
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 12),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _seats,
                  keyboardType: TextInputType.number,
                  decoration: InputDecoration(
                    labelText: 'Số chỗ tối thiểu',
                    prefixIcon: const Icon(Icons.person_outline, size: 18),
                    isDense: true,
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  controller: _priceMax,
                  keyboardType: TextInputType.number,
                  decoration: InputDecoration(
                    labelText: 'Giá tối đa/ngày',
                    prefixIcon: const Icon(Icons.payments_outlined, size: 18),
                    isDense: true,
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              onPressed: _loadRecommended,
              icon: const Icon(Icons.search, size: 18),
              label: const Text('Tìm xe gợi ý tốt nhất'),
              style: FilledButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 12),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _recommendCard(VehicleTypeRecommendation item) {
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFFF0FDF4),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFFBBF7D0)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(item.typeName, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: AppColors.success,
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Text(
                    'Điểm: ${item.score.toStringAsFixed(1)}',
                    style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Row(
              children: [
                const Icon(Icons.star_rounded, color: Colors.amber, size: 18),
                const SizedBox(width: 4),
                Text(
                  '${item.avgRating.toStringAsFixed(1)} · ${item.availableCount} xe sẵn sàng',
                  style: const TextStyle(color: AppColors.muted, fontSize: 13, fontWeight: FontWeight.w600),
                ),
              ],
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: () => _openBooking(
                type: item.toVehicleType(),
                start: _start,
                end: _end,
                fromRecommendation: true,
              ),
              icon: const Icon(Icons.check, size: 16),
              label: Text(widget.isLoggedIn ? 'Chọn xe này và Đặt' : 'Đăng nhập để đặt'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.success,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _vehicleCard(VehicleType type) {
    final dailyPrice = type.pricePerDay;

    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: AppCard(
        padding: EdgeInsets.zero,
        onTap: () => _openDetail(type),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Stack(
              children: [
                VehicleImage(
                  imageUrl: type.imageUrl,
                  typeName: type.typeName,
                  seatCapacity: type.seatCapacity,
                  height: 180,
                ),
                Positioned(
                  top: 12,
                  right: 12,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: Colors.black.withValues(alpha: 0.7),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Text(
                      '${type.seatCapacity} Chỗ',
                      style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w700),
                    ),
                  ),
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(
                          type.typeName,
                          style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
                        ),
                      ),
                      Text(
                        Formatters.vnd(dailyPrice),
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w900,
                          color: AppColors.primary,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        type.description ?? 'Xe gia đình & du lịch hiện đại',
                        style: const TextStyle(color: AppColors.muted, fontSize: 13),
                      ),
                      const Text(
                        '/ ngày',
                        style: TextStyle(color: AppColors.muted, fontSize: 12),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  const Wrap(
                    spacing: 6,
                    runSpacing: 4,
                    children: [
                      VehicleSpecChip(icon: Icons.airline_seat_recline_normal, label: 'Tiện nghi'),
                      VehicleSpecChip(icon: Icons.ac_unit, label: 'Điều hòa'),
                      VehicleSpecChip(icon: Icons.security, label: 'Bảo hiểm'),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: () => _openDetail(type),
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 12),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          ),
                          child: const Text('Xem chi tiết'),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: FilledButton(
                          onPressed: () => _openBooking(type: type),
                          style: FilledButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 12),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          ),
                          child: Text(widget.isLoggedIn ? 'Đặt xe' : 'Đăng nhập'),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _routeEmoji(String icon) {
    return Container(
      color: const Color(0xFFEFF6FF),
      child: Center(
        child: Text(icon, style: const TextStyle(fontSize: 26)),
      ),
    );
  }

  Widget _routeCard(PopularRoute route) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.03),
              blurRadius: 8,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: SizedBox(
                width: 76,
                height: 76,
                child: route.image != null
                    ? Image.asset(
                        route.image!,
                        fit: BoxFit.cover,
                        errorBuilder: (_, _, _) => _routeEmoji(route.icon),
                      )
                    : _routeEmoji(route.icon),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    route.title,
                    style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14),
                  ),
                  const SizedBox(height: 3),
                  Row(
                    children: [
                      Flexible(
                        child: Text(
                          route.from,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: AppColors.text),
                        ),
                      ),
                      const Padding(
                        padding: EdgeInsets.symmetric(horizontal: 4),
                        child: Icon(Icons.arrow_forward, size: 12, color: AppColors.primary),
                      ),
                      Flexible(
                        child: Text(
                          route.to,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: AppColors.primary),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 3),
                  Text(
                    '${route.distanceKm} km · ${route.duration}',
                    style: const TextStyle(color: AppColors.muted, fontSize: 12),
                  ),
                ],
              ),
            ),
            FilledButton.tonal(
              onPressed: () => _openBooking(route: route),
              style: FilledButton.styleFrom(
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              ),
              child: const Text('Đặt tuyến'),
            ),
          ],
        ),
      ),
    );
  }
}


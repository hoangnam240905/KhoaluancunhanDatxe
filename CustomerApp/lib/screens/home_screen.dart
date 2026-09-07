import 'package:flutter/material.dart';
import '../data/portal_content.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'create_booking_screen.dart';
import 'login_screen.dart';

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
                  TextButton(
                    onPressed: () => Navigator.push(
                      context,
                      MaterialPageRoute(builder: (_) => const LoginScreen()),
                    ),
                    child: const Text(
                      'Đăng nhập',
                      style: TextStyle(color: Colors.white),
                    ),
                  ),
              ],
              flexibleSpace: FlexibleSpaceBar(
                background: Container(
                  decoration: const BoxDecoration(
                    gradient: LinearGradient(
                      colors: [
                        Color(0xFF0F172A),
                        Color(0xFF1E3A8A),
                        Color(0xFF7C3AED),
                      ],
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                    ),
                  ),
                  padding: const EdgeInsets.fromLTRB(20, 88, 20, 16),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        widget.isLoggedIn
                            ? 'Xin chào, ${widget.userName}'
                            : 'Car Rental',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: Colors.white70,
                          fontSize: 14,
                        ),
                      ),
                      const SizedBox(height: 6),
                      const Text(
                        'Thuê xe du lịch\n4-16 chỗ',
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 26,
                          fontWeight: FontWeight.w800,
                          height: 1.15,
                        ),
                      ),
                    ],
                  ),
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
          const Text(
            'Hình thức thuê',
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 10),
          RentalModeToggle(
            value: _rentalMode,
            onChanged: (v) => setState(() => _rentalMode = v),
          ),
          const SizedBox(height: 24),
          const Text(
            'Gợi ý cho bạn',
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          const Text(
            'Rule-based theo đánh giá, lịch sử đặt và xe còn lịch trống.',
            style: TextStyle(color: AppColors.muted, fontSize: 13),
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
              child: AppLoading(message: 'Đang gợi ý...'),
            )
          else
            ..._recommended.map(_recommendCard),
          const SizedBox(height: 24),
          const Text(
            'Chọn loại xe',
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          const Text(
            'Giá theo ngày và km do hệ thống cung cấp',
            style: TextStyle(color: AppColors.muted, fontSize: 13),
          ),
          const SizedBox(height: 12),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 32),
              child: AppLoading(message: 'Đang tải loại xe...'),
            )
          else if (_error != null)
            AppErrorState(message: _error!, onRetry: _load)
          else if (_types.isEmpty)
            const AppEmptyState(
              icon: Icons.directions_car_outlined,
              title: 'Chưa có loại xe',
              subtitle: 'Kiểm tra Backend đang chạy tại cổng 5199.',
            )
          else
            ..._types.map(_vehicleCard),
          const SizedBox(height: 24),
          const Text(
            'Tuyến gợi ý',
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          const Text(
            'Điểm đón/trả sẽ được điền sẵn khi đặt xe',
            style: TextStyle(color: AppColors.muted, fontSize: 13),
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
    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: OutlinedButton(
                onPressed: _pickStart,
                child: Text('Từ ${Formatters.dt(_start)}'),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: OutlinedButton(
                onPressed: _pickEnd,
                child: Text('Đến ${Formatters.dt(_end)}'),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _seats,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Số chỗ tối thiểu',
                  isDense: true,
                ),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: TextField(
                controller: _priceMax,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Giá/ngày tối đa',
                  isDense: true,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: _loadRecommended,
            child: const Text('Gợi ý'),
          ),
        ),
      ],
    );
  }

  Widget _recommendCard(VehicleTypeRecommendation item) {
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(item.typeName, style: const TextStyle(fontWeight: FontWeight.w800)),
            const SizedBox(height: 4),
            Text(
              'Điểm ${item.score.toStringAsFixed(2)} · Rating ${item.avgRating.toStringAsFixed(2)} · ${item.availableCount} xe còn lịch',
              style: const TextStyle(color: AppColors.muted, fontSize: 13),
            ),
            const SizedBox(height: 8),
            FilledButton(
              onPressed: () => _openBooking(
                type: item.toVehicleType(),
                start: _start,
                end: _end,
                fromRecommendation: true,
              ),
              child: Text(widget.isLoggedIn ? 'Chọn và đặt' : 'Đăng nhập để đặt'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _vehicleCard(VehicleType type) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: AppCard(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            VehicleImage(imageUrl: type.imageUrl, height: 132),
            const SizedBox(height: 12),
            Text(
              type.typeName,
              style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 4),
            Text(
              '${type.seatCapacity} chỗ'
              '${type.description == null || type.description!.isEmpty ? '' : ' · ${type.description}'}',
              style: const TextStyle(color: AppColors.muted, fontSize: 13),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: Text(
                    '${Formatters.vnd(type.pricePerDay)}/ngày',
                    style: const TextStyle(fontWeight: FontWeight.w800),
                  ),
                ),
                Text(
                  '${Formatters.vnd(type.pricePerKm)}/km',
                  style: const TextStyle(
                    color: AppColors.muted,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: () => _openBooking(type: type),
              child: Text(widget.isLoggedIn ? 'Đặt xe' : 'Đăng nhập để đặt'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _routeCard(PopularRoute route) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: AppCard(
        child: Row(
          children: [
            Text(route.icon, style: const TextStyle(fontSize: 28)),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    route.title,
                    style: const TextStyle(fontWeight: FontWeight.w800),
                  ),
                  Text(
                    '${route.from} → ${route.to}',
                    style: const TextStyle(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  Text(
                    '${route.distanceKm} km · ${route.duration}',
                    style: const TextStyle(
                      color: AppColors.muted,
                      fontSize: 12,
                    ),
                  ),
                ],
              ),
            ),
            TextButton(
              onPressed: () => _openBooking(route: route),
              child: const Text('Đặt'),
            ),
          ],
        ),
      ),
    );
  }
}

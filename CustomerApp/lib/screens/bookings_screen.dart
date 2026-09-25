import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'booking_detail_screen.dart';
import 'login_screen.dart';

class BookingsScreen extends StatefulWidget {
  final ApiService api;
  final bool isLoggedIn;

  const BookingsScreen({super.key, required this.api, this.isLoggedIn = true});

  @override
  State<BookingsScreen> createState() => _BookingsScreenState();
}

class _BookingsScreenState extends State<BookingsScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  List<Booking> _bookings = [];
  bool _loading = true;
  String? _error;

  static const _filterTabs = [
    'Tất cả',
    'Chờ duyệt',
    'Đang diễn ra',
    'Hoàn thành',
    'Đã hủy',
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: _filterTabs.length, vsync: this);
    _load();
    widget.api.realtime.addListener(_onRealtime);
  }

  @override
  void dispose() {
    _tabController.dispose();
    widget.api.realtime.removeListener(_onRealtime);
    super.dispose();
  }

  void _onRealtime() {
    if (widget.isLoggedIn) _load();
  }

  @override
  void didUpdateWidget(covariant BookingsScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.isLoggedIn != widget.isLoggedIn) {
      _load();
    }
  }

  Future<void> _load() async {
    if (!widget.isLoggedIn) {
      setState(() {
        _loading = false;
        _bookings = [];
        _error = null;
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _bookings = await widget.api.getBookings();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    }
    if (mounted) setState(() => _loading = false);
  }

  List<Booking> _filterByTab(int index) {
    switch (index) {
      case 1: // Chờ duyệt
        return _bookings.where((b) => b.status == 'Pending' || b.status == 'Confirmed').toList();
      case 2: // Đang diễn ra
        return _bookings.where((b) => b.status == 'Assigned' || b.status == 'InProgress').toList();
      case 3: // Hoàn thành
        return _bookings.where((b) => b.status == 'Completed').toList();
      case 4: // Đã hủy
        return _bookings.where((b) => b.status == 'Cancelled').toList();
      default:
        return _bookings;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Lịch sử đơn thuê'),
        bottom: widget.isLoggedIn
            ? TabBar(
                controller: _tabController,
                isScrollable: true,
                tabAlignment: TabAlignment.start,
                labelColor: AppColors.primary,
                unselectedLabelColor: AppColors.muted,
                indicatorColor: AppColors.primary,
                labelStyle: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14),
                tabs: _filterTabs.map((title) => Tab(text: title)).toList(),
              )
            : null,
      ),
      body: !widget.isLoggedIn
          ? Center(
              child: AppEmptyState(
                icon: Icons.lock_outline,
                title: 'Đăng nhập để xem đơn',
                subtitle: 'Đơn thuê của bạn sẽ xuất hiện tại đây sau khi đăng nhập.',
                actionLabel: 'Đăng nhập ngay',
                onAction: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const LoginScreen()),
                ),
              ),
            )
          : _loading
              ? const Center(child: AppLoading(message: 'Đang tải danh sách đơn thuê...'))
              : _error != null
                  ? Center(child: AppErrorState(message: _error!, onRetry: _load))
                  : TabBarView(
                      controller: _tabController,
                      children: List.generate(
                        _filterTabs.length,
                        (index) => _buildBookingList(_filterByTab(index)),
                      ),
                    ),
    );
  }

  Widget _buildBookingList(List<Booking> list) {
    if (list.isEmpty) {
      return RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          children: const [
            SizedBox(height: 80),
            AppEmptyState(
              icon: Icons.receipt_long_outlined,
              title: 'Không có đơn thuê',
              subtitle: 'Chưa có đơn thuê nào trong mục này.',
            ),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
        itemCount: list.length,
        itemBuilder: (context, i) => _card(list[i]),
      ),
    );
  }

  Widget _card(Booking b) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.03),
              blurRadius: 10,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Material(
          color: Colors.transparent,
          child: InkWell(
            borderRadius: BorderRadius.circular(16),
            onTap: () async {
              await Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => BookingDetailScreen(api: widget.api, booking: b),
                ),
              );
              _load();
            },
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                            decoration: BoxDecoration(
                              color: const Color(0xFFEFF6FF),
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: Text(
                              '#${b.bookingId}',
                              style: const TextStyle(
                                color: AppColors.primary,
                                fontWeight: FontWeight.w800,
                                fontSize: 12,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(
                            b.vehicleTypeName,
                            style: const TextStyle(
                              fontWeight: FontWeight.w800,
                              fontSize: 16,
                            ),
                          ),
                        ],
                      ),
                      StatusChip(status: b.status),
                    ],
                  ),
                  const SizedBox(height: 12),
                  // Lộ trình
                  Row(
                    children: [
                      const Icon(Icons.circle, size: 8, color: AppColors.primary),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          b.pickupAddress,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                        ),
                      ),
                    ],
                  ),
                  Padding(
                    padding: const EdgeInsets.only(left: 3),
                    child: Container(height: 12, width: 2, color: const Color(0xFFCBD5E1)),
                  ),
                  Row(
                    children: [
                      const Icon(Icons.location_on, size: 10, color: AppColors.danger),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          b.dropoffAddress,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                        ),
                      ),
                    ],
                  ),
                  const Divider(height: 20, color: AppColors.border),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Khởi hành: ${Formatters.rentalDt(b.startDate)}',
                            style: const TextStyle(fontSize: 12, color: AppColors.muted),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            Formatters.rentalModeLabel(b.rentalMode),
                            style: const TextStyle(
                              color: AppColors.primaryDark,
                              fontWeight: FontWeight.w700,
                              fontSize: 12,
                            ),
                          ),
                        ],
                      ),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          const Text('Tổng chi phí', style: TextStyle(fontSize: 11, color: AppColors.muted)),
                          Text(
                            Formatters.vnd(b.finalAmount ?? b.totalAmount),
                            style: const TextStyle(
                              fontWeight: FontWeight.w900,
                              fontSize: 16,
                              color: AppColors.primary,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

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

class _BookingsScreenState extends State<BookingsScreen> {
  List<Booking> _bookings = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
    widget.api.realtime.addListener(_onRealtime);
  }

  @override
  void dispose() {
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Đơn thuê')),
      body: !widget.isLoggedIn
          ? Center(
              child: AppEmptyState(
                icon: Icons.lock_outline,
                title: 'Đăng nhập để xem đơn',
                subtitle:
                    'Đơn thuê của bạn sẽ xuất hiện tại đây sau khi đặt xe.',
                actionLabel: 'Đăng nhập',
                onAction: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const LoginScreen()),
                ),
              ),
            )
          : _loading
          ? const Center(child: AppLoading(message: 'Đang tải đơn thuê...'))
          : RefreshIndicator(
              onRefresh: _load,
              child: _error != null
                  ? ListView(
                      children: [
                        const SizedBox(height: 80),
                        AppErrorState(message: _error!, onRetry: _load),
                      ],
                    )
                  : _bookings.isEmpty
                  ? ListView(
                      children: const [
                        SizedBox(height: 80),
                        AppEmptyState(
                          icon: Icons.receipt_long_outlined,
                          title: 'Chưa có đơn thuê',
                          subtitle: 'Đặt xe từ trang chủ để tạo đơn đầu tiên.',
                        ),
                      ],
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                      itemCount: _bookings.length,
                      itemBuilder: (context, i) => _card(_bookings[i]),
                    ),
            ),
    );
  }

  Widget _card(Booking b) {
    final assignmentLine = b.isSelfDrive
        ? (b.assignedVehicle == null
              ? 'Đang chờ điều phối gán xe'
              : 'Xe: ${b.assignedVehicle!.licensePlate} · ${b.assignedVehicle!.brand} ${b.assignedVehicle!.model}')
        : (b.assignment == null
              ? 'Tài xế chưa phân công'
              : 'Tài xế: ${b.assignment!.driverName} · ${b.assignment!.licensePlate}');

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: AppCard(
        onTap: () async {
          await Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => BookingDetailScreen(api: widget.api, booking: b),
            ),
          );
          _load();
        },
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    '#${b.bookingId} · ${b.vehicleTypeName}',
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 16,
                    ),
                  ),
                ),
                StatusChip(status: b.status),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              Formatters.rentalModeLabel(b.rentalMode),
              style: const TextStyle(
                color: AppColors.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 10),
            Text('${b.pickupAddress} → ${b.dropoffAddress}'),
            const SizedBox(height: 4),
            Text(
              '${Formatters.rentalDt(b.startDate)}  →  ${Formatters.rentalDt(b.endDate)}',
              style: const TextStyle(color: AppColors.muted, fontSize: 13),
            ),
            const SizedBox(height: 8),
            Text(assignmentLine, style: const TextStyle(fontSize: 13)),
            const SizedBox(height: 8),
            Text(
              Formatters.vnd(b.totalAmount),
              style: const TextStyle(
                fontWeight: FontWeight.w800,
                color: AppColors.primary,
              ),
            ),
            if (b.finalAmount != null) ...[
              const SizedBox(height: 2),
              Text(
                'Giá chốt: ${Formatters.vnd(b.finalAmount!)}',
                style: const TextStyle(fontSize: 13, color: AppColors.muted),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

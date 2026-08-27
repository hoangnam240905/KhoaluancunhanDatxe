import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'trip_detail_screen.dart';

class TripsScreen extends StatefulWidget {
  final ApiService api;
  final String userName;

  const TripsScreen({super.key, required this.api, required this.userName});

  @override
  State<TripsScreen> createState() => _TripsScreenState();
}

class _TripsScreenState extends State<TripsScreen> {
  List<DriverBooking> _trips = [];
  bool _loading = true;
  String? _error;

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
      _trips = await widget.api.getMyTrips();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _open(DriverBooking trip) async {
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => TripDetailScreen(api: widget.api, trip: trip),
      ),
    );
    _load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Chuyến của tôi', style: TextStyle(fontSize: 18)),
            Text(
              widget.userName,
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w400),
            ),
          ],
        ),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
        ],
      ),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải chuyến...'))
          : RefreshIndicator(
              onRefresh: _load,
              child: _error != null
                  ? ListView(
                      children: [
                        const SizedBox(height: 80),
                        AppErrorState(message: _error!, onRetry: _load),
                      ],
                    )
                  : _trips.isEmpty
                  ? ListView(
                      children: const [
                        SizedBox(height: 80),
                        AppEmptyState(
                          icon: Icons.route_outlined,
                          title: 'Chưa có chuyến',
                          subtitle:
                              'Khi điều phối gán bạn vào đơn Có tài xế, chuyến sẽ hiện tại đây.',
                        ),
                      ],
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                      itemCount: _trips.length,
                      itemBuilder: (context, i) => _card(_trips[i]),
                    ),
            ),
    );
  }

  Widget _card(DriverBooking t) {
    final a = t.assignment;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: AppCard(
        onTap: () => _open(t),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    '#${t.bookingId} · ${t.vehicleTypeName}',
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 16,
                    ),
                  ),
                ),
                if (a != null) AssignmentBadge(status: a.status),
              ],
            ),
            const SizedBox(height: 8),
            Text('Khách: ${t.customerName}'),
            const SizedBox(height: 4),
            Text(
              '${t.pickupAddress} → ${t.dropoffAddress}',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: 6),
            Text(
              '${Formatters.dt(t.startDate)}  →  ${Formatters.dt(t.endDate)}',
              style: const TextStyle(color: AppColors.muted, fontSize: 13),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                const Icon(
                  Icons.directions_car,
                  size: 16,
                  color: AppColors.primary,
                ),
                const SizedBox(width: 6),
                Text(
                  a?.licensePlate ?? '—',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                const Spacer(),
                Text(
                  Formatters.bookingStatus(t.status),
                  style: const TextStyle(color: AppColors.muted, fontSize: 12),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

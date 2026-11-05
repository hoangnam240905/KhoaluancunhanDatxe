import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'trip_detail_screen.dart';

class ScheduleScreen extends StatefulWidget {
  const ScheduleScreen({super.key, required this.api});
  final ApiService api;

  @override
  State<ScheduleScreen> createState() => _ScheduleScreenState();
}

class _ScheduleScreenState extends State<ScheduleScreen> {
  List<DriverBooking> _trips = [];
  DateTime _selectedDate = DateUtils.dateOnly(DateTime.now());
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

  void _onRealtime() => _load();

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

  List<DriverBooking> get _tripsForDate {
    final dayStart = DateUtils.dateOnly(_selectedDate);
    final dayEnd = dayStart.add(const Duration(days: 1));
    return _trips.where((trip) {
      final start = trip.startDate.toLocal();
      final end = trip.endDate.toLocal();
      return start.isBefore(dayEnd) && end.isAfter(dayStart);
    }).toList()..sort((a, b) => a.startDate.compareTo(b.startDate));
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
    final trips = _tripsForDate;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Lịch chuyến'),
        actions: [
          IconButton(
            onPressed: _load,
            icon: const Icon(Icons.refresh),
            tooltip: 'Tải lại',
          ),
        ],
      ),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải lịch chuyến...'))
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
                children: [
                  AppCard(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 8,
                    ),
                    child: CalendarDatePicker(
                      initialDate: _selectedDate,
                      firstDate: DateTime(DateTime.now().year - 2),
                      lastDate: DateTime(DateTime.now().year + 5),
                      onDateChanged: (value) => setState(
                        () => _selectedDate = DateUtils.dateOnly(value),
                      ),
                    ),
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Chuyến ngày ${Formatters.rentalDt(_selectedDate).split(' ').first}',
                          style: const TextStyle(
                            fontSize: 17,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      Text(
                        '${trips.length} chuyến',
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  if (_error != null)
                    AppErrorState(message: _error!, onRetry: _load)
                  else if (trips.isEmpty)
                    const AppEmptyState(
                      icon: Icons.event_busy_outlined,
                      title: 'Không có chuyến trong ngày này',
                      subtitle:
                          'Chọn ngày khác để xem lịch các chuyến được giao.',
                    )
                  else
                    ...trips.map(_tripCard),
                ],
              ),
            ),
    );
  }

  Widget _tripCard(DriverBooking trip) => Padding(
    padding: const EdgeInsets.only(bottom: 10),
    child: AppCard(
      onTap: () => _open(trip),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  '#${trip.bookingId} · ${trip.vehicleTypeName}',
                  style: const TextStyle(fontWeight: FontWeight.w800),
                ),
              ),
              if (trip.assignment != null)
                AssignmentBadge(status: trip.assignment!.status),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            '${Formatters.rentalDt(trip.startDate)} – ${Formatters.rentalDt(trip.endDate)}',
            style: TextStyle(
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            '${trip.pickupAddress} → ${trip.dropoffAddress}',
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
          const SizedBox(height: 6),
          Text(
            'Khách: ${trip.customerName}',
            style: TextStyle(
              color: Theme.of(context).colorScheme.onSurfaceVariant,
              fontSize: 13,
            ),
          ),
        ],
      ),
    ),
  );
}

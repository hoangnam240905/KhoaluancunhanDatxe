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
  bool _calendarMode = true;
  DateTime _displayedMonth = DateTime(DateTime.now().year, DateTime.now().month);
  DateTime _selectedDate = DateTime.now();
  String? _error;

  static const _months = [
    'Tháng 1',
    'Tháng 2',
    'Tháng 3',
    'Tháng 4',
    'Tháng 5',
    'Tháng 6',
    'Tháng 7',
    'Tháng 8',
    'Tháng 9',
    'Tháng 10',
    'Tháng 11',
    'Tháng 12',
  ];
  static const _weekdays = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

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
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final trips = await widget.api.getMyTrips();
      trips.sort((a, b) => a.startDate.compareTo(b.startDate));
      _trips = trips;
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
            Text(
              _calendarMode ? 'Lịch chuyến' : 'Chuyến của tôi',
              style: const TextStyle(fontSize: 18),
            ),
            Text(
              widget.userName,
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w400),
            ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: _calendarMode ? 'Chuyển sang danh sách' : 'Xem lịch',
            icon: Icon(
              _calendarMode
                  ? Icons.view_agenda_outlined
                  : Icons.calendar_month_outlined,
            ),
            onPressed: () => setState(() => _calendarMode = !_calendarMode),
          ),
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
                  : _calendarMode
                  ? _calendarView()
                  : ListView.builder(
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                      itemCount: _trips.length,
                      itemBuilder: (context, i) => _card(_trips[i]),
                    ),
            ),
    );
  }

  DateTime _dateOnly(DateTime value) =>
      DateTime(value.year, value.month, value.day);

  bool _sameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  List<DriverBooking> _tripsOn(DateTime date) {
    final day = _dateOnly(date);
    return _trips.where((trip) {
      final start = _dateOnly(trip.startDate);
      final end = _dateOnly(trip.endDate);
      return !day.isBefore(start) && !day.isAfter(end);
    }).toList();
  }

  void _moveMonth(int offset) {
    setState(() {
      _displayedMonth = DateTime(
        _displayedMonth.year,
        _displayedMonth.month + offset,
      );
      _selectedDate = _displayedMonth;
    });
  }

  Widget _calendarView() {
    final palette = AppPalette.of(context);
    final onPrimary = Theme.of(context).colorScheme.onPrimary;
    final selectedTrips = _tripsOn(_selectedDate);
    final offset =
        DateTime(_displayedMonth.year, _displayedMonth.month, 1).weekday - 1;
    final daysInMonth =
        DateTime(_displayedMonth.year, _displayedMonth.month + 1, 0).day;
    final cellCount = ((offset + daysInMonth + 6) ~/ 7) * 7;

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      children: [
        AppCard(
          child: Column(
            children: [
              Row(
                children: [
                  IconButton(
                    tooltip: 'Tháng trước',
                    onPressed: () => _moveMonth(-1),
                    icon: const Icon(Icons.chevron_left),
                  ),
                  Expanded(
                    child: Center(
                      child: Text(
                        '${_months[_displayedMonth.month - 1]} ${_displayedMonth.year}',
                        style: const TextStyle(
                          fontSize: 17,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                  ),
                  IconButton(
                    tooltip: 'Tháng sau',
                    onPressed: () => _moveMonth(1),
                    icon: const Icon(Icons.chevron_right),
                  ),
                  TextButton(
                    onPressed: () => setState(() {
                      _displayedMonth =
                          DateTime(DateTime.now().year, DateTime.now().month);
                      _selectedDate = DateTime.now();
                    }),
                    child: const Text('Hôm nay'),
                  ),
                ],
              ),
              Row(
                children: _weekdays
                    .map(
                      (day) => Expanded(
                        child: Center(
                          child: Padding(
                            padding: const EdgeInsets.symmetric(vertical: 8),
                            child: Text(
                              day,
                              style: TextStyle(
                                color: palette.muted,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                        ),
                      ),
                    )
                    .toList(),
              ),
              GridView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: cellCount,
                gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 7,
                  mainAxisExtent: 48,
                ),
                itemBuilder: (context, index) {
                  final dayNumber = index - offset + 1;
                  if (dayNumber < 1 || dayNumber > daysInMonth) {
                    return const SizedBox.shrink();
                  }
                  final date = DateTime(
                    _displayedMonth.year,
                    _displayedMonth.month,
                    dayNumber,
                  );
                  final count = _tripsOn(date).length;
                  final selected = _sameDay(date, _selectedDate);
                  final today = _sameDay(date, DateTime.now());
                  return Semantics(
                    button: true,
                    label: '$dayNumber, $count chuyến',
                    selected: selected,
                    child: InkWell(
                      borderRadius: BorderRadius.circular(12),
                      onTap: () => setState(() => _selectedDate = date),
                      child: Container(
                        margin: const EdgeInsets.all(2),
                        decoration: BoxDecoration(
                          color: selected ? palette.primary : null,
                          borderRadius: BorderRadius.circular(12),
                          border: today && !selected
                              ? Border.all(color: palette.primary)
                              : null,
                        ),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Text(
                              '$dayNumber',
                              style: TextStyle(
                                fontWeight: selected || today
                                    ? FontWeight.w800
                                    : FontWeight.w500,
                              ),
                            ),
                            if (count > 0)
                              Text(
                                '$count chuyến',
                                style: TextStyle(
                                  fontSize: 9,
                                  color: selected
                                      ? onPrimary
                                      : palette.primary,
                                  height: 1.1,
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
            ],
          ),
        ),
        const SizedBox(height: 18),
        Text(
          'Chuyến ngày ${_selectedDate.day.toString().padLeft(2, '0')}/${_selectedDate.month.toString().padLeft(2, '0')}/${_selectedDate.year}',
          style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 10),
        if (selectedTrips.isEmpty)
          AppCard(
            child: Text(
              'Không có chuyến được phân công trong ngày này.',
              style: TextStyle(color: palette.muted),
            ),
          )
        else
          ...selectedTrips.map(_card),
      ],
    );
  }

  Widget _card(DriverBooking t) {
    final palette = AppPalette.of(context);
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
              '${Formatters.rentalDt(t.startDate)}  →  ${Formatters.rentalDt(t.endDate)}',
              style: TextStyle(color: palette.muted, fontSize: 13),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                  Icon(
                  Icons.directions_car,
                  size: 16,
                  color: palette.primary,
                ),
                const SizedBox(width: 6),
                Text(
                  a?.licensePlate ?? '—',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                const Spacer(),
                Text(
                  Formatters.bookingStatus(t.status),
                  style: TextStyle(color: palette.muted, fontSize: 12),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

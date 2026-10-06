import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import '../widgets/complete_trip_dialog.dart';
import '../widgets/incident_report_dialog.dart';

class TripDetailScreen extends StatefulWidget {
  final ApiService api;
  final DriverBooking trip;

  const TripDetailScreen({super.key, required this.api, required this.trip});

  @override
  State<TripDetailScreen> createState() => _TripDetailScreenState();
}

class _TripDetailScreenState extends State<TripDetailScreen> {
  late DriverBooking _trip;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _trip = widget.trip;
    widget.api.realtime.addListener(_onRealtime);
  }

  @override
  void dispose() {
    widget.api.realtime.removeListener(_onRealtime);
    super.dispose();
  }

  void _onRealtime() {
    _reload();
  }

  Future<void> _reload() async {
    final trips = await widget.api.getMyTrips();
    DriverBooking? updated;
    for (final t in trips) {
      if (t.bookingId == _trip.bookingId) {
        updated = t;
        break;
      }
    }
    if (!mounted) return;
    final found = updated;
    if (found != null) {
      setState(() => _trip = found);
    }
  }

  Future<void> _run(Future<String> Function() action) async {
    setState(() => _busy = true);
    try {
      final message = await action();
      await _reload();
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(message)));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _complete() async {
    final aid = _trip.assignmentId;
    if (aid == null) return;
    final message = await showDialog<String>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => CompleteTripDialog(
        title: 'Báo cáo tình trạng sau chuyến',
        description:
            'Ghi nhận số km, nhiên liệu và tình trạng thực tế của xe khi kết thúc chuyến.',
        submitLabel: 'Gửi báo cáo trả xe',
        onSubmit:
            ({
              required odometerKm,
              required fuelLevel,
              exteriorCondition,
              technicalCondition,
              notes,
            }) => widget.api.completeTrip(
              aid,
              odometerKm: odometerKm,
              fuelLevel: fuelLevel,
              exteriorCondition: exteriorCondition,
              technicalCondition: technicalCondition,
              notes: notes,
            ),
      ),
    );
    if (!mounted || message == null) return;
    try {
      await _reload();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
      return;
    }
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _start() async {
    final aid = _trip.assignmentId;
    if (aid == null) return;
    final currentKm = _trip.assignedVehicle?.currentKm;
    if (currentKm == null || currentKm <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Chưa có số km hiện tại trong hồ sơ xe được phân công.'),
        ),
      );
      return;
    }
    final message = await showDialog<String>(
      context: context,
      barrierDismissible: false,
      builder: (_) => CompleteTripDialog(
        title: 'Kiểm tra đầu chuyến',
        description:
            'Đối chiếu với tình trạng xe điều phối đã kiểm tra. Số km tự lấy từ hồ sơ xe; nhập nhiên liệu và ghi lại khác biệt nếu có.',
        submitLabel: 'Ghi nhận và bắt đầu',
        initialOdometerKm: currentKm.toDouble(),
        onSubmit:
            ({
              required odometerKm,
              required fuelLevel,
              exteriorCondition,
              technicalCondition,
              notes,
            }) => widget.api.startTrip(
              aid,
              odometerKm: odometerKm,
              fuelLevel: fuelLevel,
              exteriorCondition: exteriorCondition,
              technicalCondition: technicalCondition,
              notes: notes,
            ),
      ),
    );
    if (!mounted || message == null) return;
    try {
      await _reload();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
      return;
    }
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _reportIncident() async {
    final aid = _trip.assignmentId;
    if (aid == null) return;
    final message = await showDialog<String>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => IncidentReportDialog(
        onSubmit: ({required incidentType, required description}) async {
          await widget.api.reportIncident(
            assignmentId: aid,
            incidentType: incidentType,
            description: description,
          );
          return 'Đã gửi báo cáo sự cố.';
        },
      ),
    );
    if (!mounted || message == null) return;
    try {
      await _reload();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
      return;
    }
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final t = _trip;
    final a = t.assignment;
    return Scaffold(
      appBar: AppBar(title: Text('Chuyến #${t.bookingId}')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('THÔNG TIN CHUYẾN', style: _section),
                const SizedBox(height: 10),
                _row('Mã đơn', '#${t.bookingId}'),
                _row('Trạng thái đơn', Formatters.bookingStatus(t.status)),
                _row(
                  'Trạng thái chuyến',
                  a == null ? '—' : Formatters.assignmentStatus(a.status),
                ),
                _row('Hình thức', 'Có tài xế'),
              ],
            ),
          ),
          const SizedBox(height: 12),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('KHÁCH HÀNG', style: _section),
                const SizedBox(height: 10),
                _row('Tên khách', t.customerName),
              ],
            ),
          ),
          const SizedBox(height: 12),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('XE', style: _section),
                const SizedBox(height: 10),
                _row('Loại xe', t.vehicleTypeName),
                _row(
                  'Hãng / model',
                  t.assignedVehicle == null
                      ? '—'
                      : '${t.assignedVehicle!.brand} ${t.assignedVehicle!.model}'
                            .trim(),
                ),
                _row('Biển số', a?.licensePlate ?? '—'),
              ],
            ),
          ),
          const SizedBox(height: 12),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('LỘ TRÌNH', style: _section),
                const SizedBox(height: 10),
                _row('Điểm đón', t.pickupAddress),
                _row('Điểm trả', t.dropoffAddress),
              ],
            ),
          ),
          const SizedBox(height: 12),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('THỜI GIAN', style: _section),
                const SizedBox(height: 10),
                _row('Giờ nhận', Formatters.rentalDt(t.startDate)),
                _row('Giờ trả', Formatters.rentalDt(t.endDate)),
              ],
            ),
          ),
          if (t.notes != null && t.notes!.trim().isNotEmpty) ...[
            const SizedBox(height: 12),
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('GHI CHÚ', style: _section),
                  const SizedBox(height: 8),
                  Text(t.notes!),
                ],
              ),
            ),
          ],
          const SizedBox(height: 12),
          _inspectionHistory(t.inspections),
          const SizedBox(height: 16),
          _timeline(a?.status),
          const SizedBox(height: 20),
          if (_busy) const Center(child: AppLoading()),
          if (!_busy) ..._actions(a),
          const SizedBox(height: 12),
        ],
      ),
    );
  }

  Widget _inspectionHistory(List<VehicleInspection> inspections) {
    final handover = inspections.where((i) => i.inspectionType == 'Handover');
    final returned = inspections.where((i) => i.inspectionType == 'Return');
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('BIÊN BẢN TÌNH TRẠNG XE', style: _section),
          const SizedBox(height: 10),
          _inspectionSummary('Trước chuyến · Điều phối bàn giao', handover),
          const SizedBox(height: 12),
          _inspectionSummary('Sau chuyến · Tài xế trả xe', returned),
        ],
      ),
    );
  }

  Widget _inspectionSummary(
    String title,
    Iterable<VehicleInspection> records,
  ) {
    final palette = AppPalette.of(context);
    final inspection = records.isEmpty ? null : records.last;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: 6),
        if (inspection == null)
          Text('Chưa có biên bản.', style: TextStyle(color: palette.muted))
        else ...[
          Text(
            'Ghi nhận lúc ${Formatters.dt(inspection.actualAt)}',
            style: TextStyle(color: palette.muted, fontSize: 12),
          ),
          if (inspection.odometerKm != null)
            _row('Số km', '${inspection.odometerKm!.toStringAsFixed(0)} km'),
          if (inspection.fuelLevel != null)
            _row('Nhiên liệu', '${inspection.fuelLevel!.toStringAsFixed(0)}%'),
          if ((inspection.exteriorCondition ?? inspection.condition)
                  ?.trim()
                  .isNotEmpty ==
              true)
            _row(
              'Ngoại thất',
              (inspection.exteriorCondition ?? inspection.condition)!.trim(),
            ),
          if (inspection.technicalCondition?.trim().isNotEmpty == true)
            _row('Kỹ thuật', inspection.technicalCondition!.trim()),
          if (inspection.notes?.trim().isNotEmpty == true)
            _row('Ghi chú', inspection.notes!.trim()),
        ],
      ],
    );
  }

  List<Widget> _actions(TripAssignment? a) {
    final palette = AppPalette.of(context);
    final aid = a?.assignmentId;
    if (aid == null) return [];
    switch (a!.status) {
      case 'Assigned':
        return [
          FilledButton(
            onPressed: () => _run(() => widget.api.acceptTrip(aid)),
            child: const Text('Nhận chuyến'),
          ),
          const SizedBox(height: 8),
          OutlinedButton(
            onPressed: _reportIncident,
            child: const Text('Báo cáo sự cố'),
          ),
        ];
      case 'Accepted':
        return [
          FilledButton(
            onPressed: _start,
            child: const Text('Kiểm tra xe & bắt đầu'),
          ),
          const SizedBox(height: 8),
          OutlinedButton(
            onPressed: _reportIncident,
            child: const Text('Báo cáo sự cố'),
          ),
        ];
      case 'InProgress':
        return [
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: palette.success,
              foregroundColor:
                  Theme.of(context).brightness == Brightness.dark
                      ? palette.background
                      : Theme.of(context).colorScheme.onPrimary,
            ),
            onPressed: _complete,
            child: const Text('Hoàn thành chuyến'),
          ),
          const SizedBox(height: 8),
          OutlinedButton(
            onPressed: _reportIncident,
            child: const Text('Báo cáo sự cố'),
          ),
        ];
      case 'Completed':
        return [
          OutlinedButton(
            onPressed: _reportIncident,
            child: const Text('Báo cáo sự cố'),
          ),
        ];
      default:
        return [];
    }
  }

  Widget _timeline(String? status) {
    final palette = AppPalette.of(context);
    const steps = ['Assigned', 'Accepted', 'InProgress', 'Completed'];
    final current = steps.indexOf(status ?? '');
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('TIẾN TRÌNH', style: _section),
          const SizedBox(height: 12),
          ...List.generate(steps.length, (i) {
            final done = current >= i;
            return Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  Icon(
                    done ? Icons.check_circle : Icons.radio_button_unchecked,
                    color: done ? palette.success : palette.muted,
                    size: 20,
                  ),
                  const SizedBox(width: 8),
                  Text(
                    Formatters.assignmentStatus(steps[i]),
                    style: TextStyle(
                      fontWeight: done ? FontWeight.w800 : FontWeight.w500,
                      color: done ? palette.text : palette.muted,
                    ),
                  ),
                ],
              ),
            );
          }),
        ],
      ),
    );
  }

  Widget _row(String label, String value) {
    final palette = AppPalette.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 130,
            child: Text(label, style: TextStyle(color: palette.muted)),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
          ),
        ],
      ),
    );
  }

  static const _section = TextStyle(
    fontWeight: FontWeight.w800,
    fontSize: 12,
    letterSpacing: 0.4,
  );
}

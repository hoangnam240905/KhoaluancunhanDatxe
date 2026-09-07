import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';

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
    final odo = TextEditingController();
    final fuel = TextEditingController();
    final exterior = TextEditingController();
    final technical = TextEditingController();
    final notes = TextEditingController();
    String? localError;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setLocal) {
            return AlertDialog(
              title: const Text('Hoàn thành chuyến'),
              content: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Ghi nhận dữ liệu vận hành khi trả xe: số km, nhiên liệu, ngoại thất, kỹ thuật. Trường để trống thì không gửi.',
                      style: TextStyle(fontSize: 13),
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Thời điểm do máy chủ ghi UTC. Ứng dụng không tính giá chốt.',
                      style: TextStyle(color: AppColors.muted, fontSize: 12),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: odo,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      decoration: const InputDecoration(
                        labelText: 'Số km (odometerKm)',
                      ),
                    ),
                    TextField(
                      controller: fuel,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      decoration: const InputDecoration(
                        labelText: 'Nhiên liệu % (fuelLevel)',
                      ),
                    ),
                    TextField(
                      controller: exterior,
                      maxLength: 100,
                      decoration: const InputDecoration(
                        labelText: 'Tình trạng ngoại thất',
                      ),
                    ),
                    TextField(
                      controller: technical,
                      maxLength: 100,
                      decoration: const InputDecoration(
                        labelText: 'Tình trạng kỹ thuật',
                      ),
                    ),
                    TextField(
                      controller: notes,
                      maxLength: 500,
                      maxLines: 2,
                      decoration: const InputDecoration(
                        labelText: 'Ghi chú (notes)',
                      ),
                    ),
                    if (localError != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        localError!,
                        style: const TextStyle(color: AppColors.danger),
                      ),
                    ],
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(context, false),
                  child: const Text('Hủy'),
                ),
                FilledButton(
                  onPressed: () {
                    final parsed = _parseCompleteFields(odo.text, fuel.text);
                    if (parsed.error != null) {
                      setLocal(() => localError = parsed.error);
                      return;
                    }
                    Navigator.pop(context, true);
                  },
                  child: const Text('Hoàn thành'),
                ),
              ],
            );
          },
        );
      },
    );
    final odoText = odo.text;
    final fuelText = fuel.text;
    final exteriorText = exterior.text;
    final technicalText = technical.text;
    final notesText = notes.text;
    odo.dispose();
    fuel.dispose();
    exterior.dispose();
    technical.dispose();
    notes.dispose();
    if (ok != true || !mounted) return;
    final parsed = _parseCompleteFields(odoText, fuelText);
    if (parsed.error != null) return;
    await _run(
      () => widget.api.completeTrip(
        aid,
        odometerKm: parsed.odometerKm,
        fuelLevel: parsed.fuelLevel,
        exteriorCondition: exteriorText,
        technicalCondition: technicalText,
        notes: notesText,
      ),
    );
  }

  ({double? odometerKm, double? fuelLevel, String? error}) _parseCompleteFields(
    String odoRaw,
    String fuelRaw,
  ) {
    double? odometerKm;
    double? fuelLevel;
    final odo = odoRaw.trim().replaceAll(',', '.');
    if (odo.isNotEmpty) {
      odometerKm = double.tryParse(odo);
      if (odometerKm == null || odometerKm < 0) {
        return (
          odometerKm: null,
          fuelLevel: null,
          error: 'Số km không hợp lệ.',
        );
      }
    }
    final fuel = fuelRaw.trim().replaceAll(',', '.');
    if (fuel.isNotEmpty) {
      fuelLevel = double.tryParse(fuel);
      if (fuelLevel == null || fuelLevel < 0 || fuelLevel > 100) {
        return (
          odometerKm: null,
          fuelLevel: null,
          error: 'Nhiên liệu phải từ 0 đến 100.',
        );
      }
    }
    return (odometerKm: odometerKm, fuelLevel: fuelLevel, error: null);
  }

  Future<void> _reportIncident() async {
    final aid = _trip.assignmentId;
    if (aid == null) return;
    var type = 'VehicleIssue';
    final description = TextEditingController();
    String? localError;
    const types = {
      'Accident': 'Tai nạn',
      'VehicleIssue': 'Sự cố xe',
      'CustomerIssue': 'Sự cố khách',
      'Other': 'Khác',
    };
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setLocal) {
            return AlertDialog(
              title: const Text('Báo cáo sự cố'),
              content: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    RadioGroup<String>(
                      groupValue: type,
                      onChanged: (v) => setLocal(() => type = v ?? type),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          ...types.entries.map(
                            (e) => RadioListTile<String>(
                              dense: true,
                              title: Text(e.value),
                              value: e.key,
                            ),
                          ),
                        ],
                      ),
                    ),
                    TextField(
                      controller: description,
                      maxLines: 3,
                      maxLength: 500,
                      decoration: const InputDecoration(
                        labelText: 'Mô tả',
                      ),
                    ),
                    if (localError != null)
                      Text(
                        localError!,
                        style: const TextStyle(color: AppColors.danger),
                      ),
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(context, false),
                  child: const Text('Hủy'),
                ),
                FilledButton(
                  onPressed: () {
                    if (description.text.trim().isEmpty) {
                      setLocal(() => localError = 'Nhập mô tả sự cố.');
                      return;
                    }
                    Navigator.pop(context, true);
                  },
                  child: const Text('Gửi'),
                ),
              ],
            );
          },
        );
      },
    );
    final text = description.text;
    description.dispose();
    if (ok != true || !mounted) return;
    await _run(
      () async {
        await widget.api.reportIncident(
          assignmentId: aid,
          incidentType: type,
          description: text,
        );
        return 'Đã gửi báo cáo sự cố.';
      },
    );
  }

  @override
  Widget build(BuildContext context) {
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
                _row('Giờ nhận', Formatters.dt(t.startDate)),
                _row('Giờ trả', Formatters.dt(t.endDate)),
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

  List<Widget> _actions(TripAssignment? a) {
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
            onPressed: () => _run(() => widget.api.startTrip(aid)),
            child: const Text('Bắt đầu chuyến'),
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
            style: FilledButton.styleFrom(backgroundColor: AppColors.success),
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
                    color: done ? AppColors.success : AppColors.muted,
                    size: 20,
                  ),
                  const SizedBox(width: 8),
                  Text(
                    Formatters.assignmentStatus(steps[i]),
                    style: TextStyle(
                      fontWeight: done ? FontWeight.w800 : FontWeight.w500,
                      color: done ? AppColors.text : AppColors.muted,
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
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 130,
            child: Text(label, style: const TextStyle(color: AppColors.muted)),
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
    color: AppColors.muted,
  );
}

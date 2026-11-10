import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import '../widgets/complete_trip_dialog.dart';
import '../widgets/dispute_dialog.dart';
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
  List<VehicleInspection> _inspections = [];
  List<InspectionDispute> _disputes = [];
  List<VehicleChange> _vehicleChanges = [];
  bool _loadingInspections = true;
  String? _inspectionError;

  /// Dispatcher's check before the trip (plan1.md Phần A); the driver confirms it or reports a mismatch.
  VehicleInspection? get _preHandover {
    for (final i in _inspections) {
      if (i.isPreHandover) return i;
    }
    return null;
  }

  InspectionDispute? get _openDispute {
    for (final d in _disputes) {
      if (d.isOpen) return d;
    }
    return null;
  }

  InspectionDispute? get _lastResolvedDispute {
    for (final d in _disputes) {
      if (!d.isOpen) return d;
    }
    return null;
  }

  bool get _waitingToStart {
    final s = _trip.assignment?.status;
    return s == 'Assigned' || s == 'Accepted';
  }

  @override
  void initState() {
    super.initState();
    _trip = widget.trip;
    widget.api.realtime.addListener(_onRealtime);
    _loadInspections();
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
    _loadInspections();
  }

  Future<void> _loadInspections() async {
    if (mounted) {
      setState(() {
        _loadingInspections = true;
        _inspectionError = null;
      });
    }
    try {
      final items = await widget.api.getBookingInspections(_trip.bookingId);
      final aid = _trip.assignmentId;
      final disputes = aid == null
          ? <InspectionDispute>[]
          : await widget.api.getTripDisputes(aid);
      List<VehicleChange> changes = [];
      try {
        changes = await widget.api.getVehicleChanges(_trip.bookingId);
      } catch (_) {
        changes = [];
      }
      if (mounted) {
        setState(() {
          _inspections = items;
          _disputes = disputes;
          _vehicleChanges = changes;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(
          () => _inspectionError = e.toString().replaceFirst('Exception: ', ''),
        );
      }
    } finally {
      if (mounted) setState(() => _loadingInspections = false);
    }
  }

  /// Confirm the dispatcher's record against the real car; this starts the trip.
  Future<void> _startWithInspection() async {
    final aid = _trip.assignmentId;
    final pre = _preHandover;
    if (aid == null || pre == null || _openDispute != null) return;
    final message = await showDialog<String>(
      context: context,
      barrierDismissible: false,
      builder: (_) => CompleteTripDialog(
        title: 'Đối chiếu với xe thật',
        description:
            'Số liệu điền sẵn theo biên bản điều phối ghi. Kiểm tra xe thật: đúng thì xác nhận để bắt đầu chuyến. '
            'Nếu xe khác biên bản, bấm Hủy rồi chọn "Báo sai lệch".',
        submitLabel: 'Xác nhận đúng, bắt đầu chuyến',
        odometerLabel: 'Số km trên đồng hồ (bắt buộc)',
        fuelLabel: 'Nhiên liệu % (bắt buộc)',
        initialOdometer: pre.odometerKm,
        initialFuel: pre.fuelLevel,
        initialExterior: pre.exteriorCondition ?? pre.condition,
        initialTechnical: pre.technicalCondition,
        showServerTimeNote: false,
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
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(message)));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
  }

  /// "Báo sai lệch": the car does not match the dispatcher's record. The trip does not start.
  Future<void> _raiseDispute() async {
    final aid = _trip.assignmentId;
    if (aid == null) return;
    final note = await showDialog<String>(
      context: context,
      builder: (_) => const DisputeDialog(),
    );
    if (note == null || !mounted) return;
    await _run(() => widget.api.raiseDispute(aid, note));
  }

  Future<void> _remindDispatcher() async {
    final aid = _trip.assignmentId;
    if (aid == null) return;
    await _run(() => widget.api.remindDispatcher(aid));
  }

  /// Before the trip: the dispatcher's record, a pending dispute, or "no record yet".
  Widget _preCheckSection() {
    final theme = Theme.of(context);
    final pre = _preHandover;
    final open = _openDispute;
    final resolved = _lastResolvedDispute;
    final muted = TextStyle(color: theme.colorScheme.onSurfaceVariant);
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('KIỂM TRA XE TRƯỚC CHUYẾN', style: _section),
          const SizedBox(height: 10),
          if (_loadingInspections)
            const LinearProgressIndicator()
          else if (pre == null) ...[
            const Text(
              'Chưa có biên bản kiểm tra của điều phối.',
              style: TextStyle(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 4),
            Text(
              'Điều phối cần ghi tình trạng xe trước khi bạn bắt đầu chuyến.',
              style: muted,
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: _busy ? null : _remindDispatcher,
              icon: const Icon(Icons.notifications_active_outlined),
              label: const Text('Nhắc điều phối'),
            ),
          ] else ...[
            _row('Số km', pre.odometerKm?.toStringAsFixed(0) ?? '—'),
            _row(
              'Nhiên liệu',
              pre.fuelLevel == null ? '—' : '${pre.fuelLevel!.toStringAsFixed(0)}%',
            ),
            _row('Ngoại thất', pre.exteriorCondition ?? pre.condition ?? '—'),
            _row('Kỹ thuật', pre.technicalCondition ?? '—'),
            if (pre.notes?.isNotEmpty == true) _row('Ghi chú', pre.notes!),
            _row('Người lập', '${pre.createdByName ?? 'Không rõ'} (Điều phối)'),
            if (pre.updatedByName != null) _row('Người sửa', pre.updatedByName!),
            if (open != null)
              Container(
                width: double.infinity,
                margin: const EdgeInsets.only(top: 6),
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppColors.danger.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  'Bạn đã báo sai lệch: ${open.note}\nĐang chờ điều phối sửa biên bản.',
                  style: const TextStyle(color: AppColors.danger),
                ),
              )
            else ...[
              if (resolved != null)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(
                    'Điều phối ${resolved.resolvedByName ?? ''} đã xử lý sai lệch'
                    '${resolved.resolutionNote?.isNotEmpty == true ? ': ${resolved.resolutionNote}' : '.'} '
                    'Kiểm tra lại xe rồi xác nhận.',
                    style: muted,
                  ),
                ),
              const SizedBox(height: 4),
              Text(
                'Kiểm tra xe thật. Đúng thì bấm "Kiểm tra & bắt đầu chuyến", sai thì "Báo sai lệch".',
                style: muted,
              ),
            ],
          ],
        ],
      ),
    );
  }

  Widget _inspectionSection() {
    final theme = Theme.of(context);
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('BIÊN BẢN KIỂM XE', style: _section),
          const SizedBox(height: 8),
          if (_loadingInspections)
            const LinearProgressIndicator()
          else if (_inspectionError != null)
            Row(
              children: [
                Expanded(
                  child: Text(
                    _inspectionError!,
                    style: TextStyle(color: theme.colorScheme.error),
                  ),
                ),
                TextButton(
                  onPressed: _loadInspections,
                  child: const Text('Thử lại'),
                ),
              ],
            )
          else if (_inspections.isEmpty)
            Text(
              'Chưa có biên bản kiểm xe.',
              style: TextStyle(color: theme.colorScheme.onSurfaceVariant),
            )
          else
            ..._inspections.map(_inspectionTile),
        ],
      ),
    );
  }

  Widget _inspectionTile(VehicleInspection inspection) {
    final type = inspection.isPreHandover
        ? 'kiểm tra trước giao (điều phối)'
        : inspection.isHandover
        ? 'giao xe'
        : 'nhận xe';
    final verification = switch (inspection.verificationStatus) {
      'Pending' => 'Đã gửi — chờ điều phối xác nhận',
      'Confirmed' => 'Điều phối đã xác nhận đúng',
      'Mismatch' =>
        'Điều phối ghi nhận có lệch${inspection.verificationNote?.isNotEmpty == true ? ': ${inspection.verificationNote}' : ''}',
      _ => null,
    };
    final details = <String>[
      if (inspection.createdByName != null)
        'Người lập: ${inspection.createdByName}',
      if (inspection.updatedByName != null)
        'Người sửa: ${inspection.updatedByName}',
      ?verification,
      if (inspection.odometerKm != null)
        'Số km: ${inspection.odometerKm!.toStringAsFixed(0)}',
      if (inspection.fuelLevel != null)
        'Nhiên liệu: ${inspection.fuelLevel!.toStringAsFixed(0)}%',
      if ((inspection.exteriorCondition ?? inspection.condition)?.isNotEmpty ==
          true)
        'Ngoại thất: ${inspection.exteriorCondition ?? inspection.condition}',
      if (inspection.technicalCondition?.isNotEmpty == true)
        'Kỹ thuật: ${inspection.technicalCondition}',
      if (inspection.notes?.isNotEmpty == true) 'Ghi chú: ${inspection.notes}',
    ];
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Icon(
        Icons.fact_check_outlined,
        color: Theme.of(context).colorScheme.primary,
      ),
      title: Text(
        'Biên bản $type',
        style: const TextStyle(fontWeight: FontWeight.w700),
      ),
      subtitle: Text(
        '${Formatters.dt(inspection.actualAt)}${details.isEmpty ? '' : '\n${details.join(' · ')}'}',
      ),
      isThreeLine: details.isNotEmpty,
    );
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
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
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
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
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
          if (_vehicleChanges.isNotEmpty) ...[
            _vehicleChangesSection(),
            const SizedBox(height: 12),
          ],
          if (_waitingToStart) ...[
            _preCheckSection(),
            const SizedBox(height: 12),
          ],
          _inspectionSection(),
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
    final canCheck = _preHandover != null && _openDispute == null && !_loadingInspections;
    final disputeButton = OutlinedButton.icon(
      style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger),
      onPressed: canCheck ? _raiseDispute : null,
      icon: const Icon(Icons.report_problem_outlined),
      label: const Text('Báo sai lệch'),
    );
    switch (a!.status) {
      case 'Assigned':
        return [
          FilledButton(
            onPressed: () => _run(() => widget.api.acceptTrip(aid)),
            child: const Text('Nhận chuyến'),
          ),
          const SizedBox(height: 8),
          disputeButton,
          const SizedBox(height: 8),
          OutlinedButton(
            onPressed: _reportIncident,
            child: const Text('Báo cáo sự cố'),
          ),
        ];
      case 'Accepted':
        return [
          FilledButton(
            onPressed: canCheck ? _startWithInspection : null,
            child: const Text('Kiểm tra & bắt đầu chuyến'),
          ),
          if (!canCheck && !_loadingInspections)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                _preHandover == null
                    ? 'Chờ điều phối ghi biên bản kiểm tra trước giao.'
                    : 'Chờ điều phối xử lý sai lệch bạn đã báo.',
                textAlign: TextAlign.center,
                style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant),
              ),
            ),
          const SizedBox(height: 8),
          disputeButton,
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
                    color: done
                        ? AppColors.success
                        : Theme.of(context).colorScheme.onSurfaceVariant,
                    size: 20,
                  ),
                  const SizedBox(width: 8),
                  Text(
                    Formatters.assignmentStatus(steps[i]),
                    style: TextStyle(
                      fontWeight: done ? FontWeight.w800 : FontWeight.w500,
                      color: done
                          ? Theme.of(context).colorScheme.onSurface
                          : Theme.of(context).colorScheme.onSurfaceVariant,
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

  Widget _vehicleChangesSection() {
    String num(double? v) => v == null ? '—' : v.toStringAsFixed(0);
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('PHỤ LỤC ĐỔI XE', style: _section),
          const SizedBox(height: 10),
          for (var i = 0; i < _vehicleChanges.length; i++) ...[
            if (i > 0) const Divider(height: 20),
            Text(
              'Lần đổi ${i + 1} · ${Formatters.dt(_vehicleChanges[i].createdAt)}',
              style: const TextStyle(fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 8),
            _row('Xe cũ', '${_vehicleChanges[i].oldVehicle} (${_vehicleChanges[i].oldTypeName})'),
            _row('Xe mới', '${_vehicleChanges[i].newVehicle} (${_vehicleChanges[i].newTypeName})'),
            _row(
              'Lý do',
              (_vehicleChanges[i].note ?? '').isEmpty
                  ? _vehicleChanges[i].reasonLabel
                  : '${_vehicleChanges[i].reasonLabel} — ${_vehicleChanges[i].note}',
            ),
            _row('Thời điểm', _vehicleChanges[i].duringTrip ? 'Giữa chuyến' : 'Trước khi bắt đầu chuyến'),
            if (_vehicleChanges[i].duringTrip) ...[
              _row('Xe cũ khi đổi',
                  '${num(_vehicleChanges[i].oldOdometerKm)} km · ${num(_vehicleChanges[i].oldFuelLevel)}% nhiên liệu'),
              _row('Xe mới khi nhận',
                  '${num(_vehicleChanges[i].newOdometerKm)} km · ${num(_vehicleChanges[i].newFuelLevel)}% nhiên liệu'),
            ],
            _row('Người duyệt', _vehicleChanges[i].approvedByName ?? 'Điều phối viên'),
          ],
          if (_vehicleChanges.last.duringTrip)
            const Text(
              'Khi trả xe, nhập số km của xe mới; hệ thống cộng km của cả hai xe.',
              style: TextStyle(color: AppColors.muted, fontSize: 12),
            )
          else if (_waitingToStart)
            const Text(
              'Đối chiếu biên bản trước giao của xe mới trước khi bắt đầu chuyến.',
              style: TextStyle(color: AppColors.muted, fontSize: 12),
            ),
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
            child: Text(
              label,
              style: TextStyle(
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
          ),
          Expanded(
            child: Text(value, style: TextStyle(fontWeight: FontWeight.w700)),
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

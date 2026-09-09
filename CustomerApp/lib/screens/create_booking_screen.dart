import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/booking_pricing.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'booking_detail_screen.dart';

class CreateBookingScreen extends StatefulWidget {
  final ApiService api;
  final String initialRentalMode;
  final VehicleType? vehicleType;
  final List<VehicleType>? vehicleTypes;
  final String? pickup;
  final String? dropoff;
  final double? distanceKm;
  final DateTime? initialStart;
  final DateTime? initialEnd;
  final bool fromRecommendation;

  const CreateBookingScreen({
    super.key,
    required this.api,
    this.initialRentalMode = 'WithDriver',
    this.vehicleType,
    this.vehicleTypes,
    this.pickup,
    this.dropoff,
    this.distanceKm,
    this.initialStart,
    this.initialEnd,
    this.fromRecommendation = false,
  });

  @override
  State<CreateBookingScreen> createState() => _CreateBookingScreenState();
}

class _CreateBookingScreenState extends State<CreateBookingScreen> {
  static const _titles = [
    'Hình thức thuê',
    'Loại xe',
    'Thời gian',
    'Địa điểm',
    'Xác nhận',
  ];

  final _page = PageController();
  int _step = 0;
  late String _rentalMode;
  VehicleType? _type;
  List<VehicleType> _types = [];
  Vehicle? _vehicle;
  List<Vehicle> _vehicles = [];
  bool _loadingVehicles = false;
  bool _loadingTypes = false;
  String? _typesError;

  DateTime? _start;
  DateTime? _end;
  late final TextEditingController _pickup;
  late final TextEditingController _dropoff;
  late final TextEditingController _distance;
  final _notes = TextEditingController();
  bool _submitting = false;
  String? _formError;
  BookingQuote? _quote;
  bool _quoteLoading = false;
  String? _quoteError;

  @override
  void initState() {
    super.initState();
    _rentalMode = widget.initialRentalMode == 'SelfDrive'
        ? 'SelfDrive'
        : 'WithDriver';
    _type = widget.vehicleType;
    _types = widget.vehicleTypes ?? [];
    _pickup = TextEditingController(text: widget.pickup ?? '');
    _dropoff = TextEditingController(text: widget.dropoff ?? '');
    _distance = TextEditingController(
      text: widget.distanceKm == null
          ? ''
          : widget.distanceKm!.toStringAsFixed(0),
    );
    if (_types.isEmpty) {
      _loadTypes();
    } else {
      _loadVehicles();
    }
    _start = widget.initialStart;
    _end = widget.initialEnd;
  }

  @override
  void dispose() {
    _page.dispose();
    _pickup.dispose();
    _dropoff.dispose();
    _distance.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _loadTypes() async {
    setState(() {
      _loadingTypes = true;
      _typesError = null;
    });
    try {
      _types = await widget.api.getVehicleTypes();
      _type ??= _types.isEmpty ? null : _types.first;
      await _loadVehicles();
    } catch (e) {
      _typesError = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (mounted) setState(() => _loadingTypes = false);
    }
  }

  Future<void> _loadVehicles() async {
    final typeId = _type?.typeId;
    if (typeId == null) {
      setState(() {
        _vehicles = [];
        _vehicle = null;
        _loadingVehicles = false;
      });
      return;
    }
    setState(() => _loadingVehicles = true);
    try {
      final all = await widget.api.getVehicles(status: 'Available');
      if (!mounted) return;
      final filtered = all.where((v) => v.typeId == typeId).toList();
      setState(() {
        _vehicles = filtered;
        if (_vehicle != null &&
            filtered.every((v) => v.vehicleId != _vehicle!.vehicleId)) {
          _vehicle = null;
        }
        _loadingVehicles = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _vehicles = [];
        _loadingVehicles = false;
      });
    }
  }

  Future<void> _pickDateTime({required bool start}) async {
    final now = DateTime.now();
    final initial =
        (start ? _start : _end) ?? now.add(Duration(days: start ? 1 : 2));
    final date = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: now,
      lastDate: now.add(const Duration(days: 365)),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
    );
    if (time == null || !mounted) return;
    final selected = DateTime(
      date.year,
      date.month,
      date.day,
      time.hour,
      time.minute,
    );
    setState(() {
      if (start) {
        _start = selected;
        if (_end != null && !_end!.isAfter(_start!)) {
          _end = _start!.add(const Duration(hours: 8));
        }
      } else {
        _end = selected;
      }
      _formError = null;
      _quote = null;
      _quoteError = null;
    });
  }

  String? _validateStep() {
    switch (_step) {
      case 1:
        if (_type == null) return 'Vui lòng chọn loại xe.';
      case 2:
        if (_start == null || _end == null) {
          return 'Vui lòng chọn thời gian nhận và trả xe.';
        }
        final clock = DateTime.now();
        final today = DateTime(clock.year, clock.month, clock.day);
        final startDay = DateTime(_start!.year, _start!.month, _start!.day);
        if (startDay.isBefore(today)) {
          return 'Ngày bắt đầu không được trong quá khứ.';
        }
        if (!_end!.isAfter(_start!)) {
          return 'Thời gian trả phải sau thời gian nhận.';
        }
      case 3:
        if (_pickup.text.trim().isEmpty) return 'Vui lòng nhập điểm đón.';
        if (_dropoff.text.trim().isEmpty) return 'Vui lòng nhập điểm trả.';
        final km = _distance.text.trim();
        if (km.isNotEmpty && double.tryParse(km.replaceAll(',', '.')) == null) {
          return 'Km dự kiến không hợp lệ.';
        }
    }
    return null;
  }

  String? _validateSubmit() {
    if (_type == null) return 'Vui lòng chọn loại xe.';
    if (_start == null || _end == null) {
      return 'Vui lòng chọn thời gian nhận và trả xe.';
    }
    final clock = DateTime.now();
    final today = DateTime(clock.year, clock.month, clock.day);
    final startDay = DateTime(_start!.year, _start!.month, _start!.day);
    if (startDay.isBefore(today)) {
      return 'Ngày bắt đầu không được trong quá khứ.';
    }
    if (!_end!.isAfter(_start!)) {
      return 'Thời gian trả phải sau thời gian nhận.';
    }
    if (_pickup.text.trim().isEmpty) return 'Vui lòng nhập điểm đón.';
    if (_dropoff.text.trim().isEmpty) return 'Vui lòng nhập điểm trả.';
    final km = _distance.text.trim();
    if (km.isNotEmpty && double.tryParse(km.replaceAll(',', '.')) == null) {
      return 'Km dự kiến không hợp lệ.';
    }
    return null;
  }

  void _next() {
    final error = _validateStep();
    if (error != null) {
      setState(() => _formError = error);
      return;
    }
    setState(() => _formError = null);
    if (_step < 4) {
      _page.nextPage(
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      );
    }
  }

  void _back() {
    if (_step == 0) {
      Navigator.pop(context);
      return;
    }
    _page.previousPage(
      duration: const Duration(milliseconds: 250),
      curve: Curves.easeOut,
    );
  }

  double? get _distanceValue {
    final raw = _distance.text.trim().replaceAll(',', '.');
    if (raw.isEmpty) return null;
    return double.tryParse(raw);
  }

  Future<void> _loadQuote() async {
    if (_type == null || _start == null || _end == null) return;
    if (!_end!.isAfter(_start!)) return;
    setState(() {
      _quoteLoading = true;
      _quoteError = null;
      _quote = null;
    });
    try {
      final quote = await widget.api.getQuote(
        vehicleTypeId: _type!.typeId,
        startDate: _start!,
        endDate: _end!,
        rentalMode: _rentalMode,
        estimatedDistance: _distanceValue,
      );
      if (!mounted) return;
      setState(() {
        _quote = quote;
        _quoteLoading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _quoteError = e.toString().replaceFirst('Exception: ', '');
        _quoteLoading = false;
      });
    }
  }

  Future<void> _submit() async {
    if (_submitting) return;
    final error = _validateSubmit();
    if (error != null || _type == null || _start == null || _end == null) {
      setState(() => _formError = error ?? 'Vui lòng kiểm tra lại thông tin đặt xe.');
      return;
    }
    setState(() {
      _submitting = true;
      _formError = null;
    });
    try {
      final booking = await widget.api.createBooking(
        vehicleTypeId: _type!.typeId,
        rentalMode: _rentalMode,
        pickupAddress: _pickup.text.trim(),
        dropoffAddress: _dropoff.text.trim(),
        startDate: _start!,
        endDate: _end!,
        estimatedDistance: _distanceValue,
        notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
        fromRecommendation: widget.fromRecommendation,
        vehicleId: _vehicle?.vehicleId,
      );
      if (!mounted) return;
      if (booking.bookingId <= 0) {
        setState(() {
          _formError = 'Không thể tạo đơn thuê. Vui lòng thử lại.';
          _submitting = false;
        });
        return;
      }
      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (ctx) => AlertDialog(
          title: const Text('🎉 Đặt xe thành công!'),
          content: Text(
            'Mã đơn: #${booking.bookingId}\n'
            'Trạng thái: ${Formatters.statusLabel(booking.status)}\n'
            'Loại xe: ${booking.vehicleTypeName}\n'
            'Hình thức: ${Formatters.rentalModeLabel(booking.rentalMode)}\n'
            'Ngày bắt đầu: ${Formatters.rentalDt(booking.startDate)}\n'
            'Ngày kết thúc: ${Formatters.rentalDt(booking.endDate)}',
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.pop(ctx);
                Navigator.pop(context, true);
              },
              child: const Text('Đóng'),
            ),
            FilledButton(
              onPressed: () {
                Navigator.pop(ctx);
                Navigator.pushReplacement(
                  context,
                  MaterialPageRoute(
                    builder: (_) => BookingDetailScreen(
                      api: widget.api,
                      booking: booking,
                    ),
                  ),
                );
              },
              child: const Text('Xem chi tiết đơn'),
            ),
          ],
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _formError = _friendlyCreateError(e));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  String _friendlyCreateError(Object error) {
    final raw = error.toString().replaceFirst('Exception: ', '').trim();
    final lower = raw.toLowerCase();
    if (raw.isEmpty ||
        raw.startsWith('Lỗi API') ||
        raw.startsWith('Loi API') ||
        RegExp(r'^Lỗi \d+').hasMatch(raw) ||
        lower.contains('socket') ||
        lower.contains('timed out') ||
        lower.contains('timeout') ||
        lower.contains('connection') ||
        lower.contains('failed host') ||
        lower.contains('xmlhttprequest') ||
        lower.contains('exception') ||
        lower.contains('stacktrace') ||
        raw.length > 280) {
      return '⚠️ Đặt xe không thành công\nKhông thể tạo đơn thuê. Vui lòng thử lại.';
    }
    return '⚠️ Đặt xe không thành công\n$raw';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Đặt xe · ${_step + 1}/5'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: _back,
        ),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _titles[_step],
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 18,
                  ),
                ),
                const SizedBox(height: 8),
                LinearProgressIndicator(
                  value: (_step + 1) / 5,
                  minHeight: 6,
                  borderRadius: BorderRadius.circular(8),
                ),
              ],
            ),
          ),
          Expanded(
            child: PageView(
              controller: _page,
              physics: const NeverScrollableScrollPhysics(),
              onPageChanged: (i) {
                setState(() => _step = i);
                if (i == 4) _loadQuote();
              },
              children: [
                _modeStep(),
                _vehicleStep(),
                _timeStep(),
                _locationStep(),
                _confirmStep(),
              ],
            ),
          ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
              child: Column(
                children: [
                  if (_formError != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Text(
                        _formError!,
                        style: const TextStyle(color: AppColors.danger),
                      ),
                    ),
                  Row(
                    children: [
                      if (_step > 0)
                        Expanded(
                          child: OutlinedButton(
                            onPressed: _submitting ? null : _back,
                            child: const Text('Quay lại'),
                          ),
                        ),
                      if (_step > 0) const SizedBox(width: 10),
                      Expanded(
                        flex: 2,
                        child: FilledButton(
                          onPressed: _submitting
                              ? null
                              : (_step == 4 ? _submit : _next),
                          child: _submitting
                              ? const Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    SizedBox(
                                      height: 18,
                                      width: 18,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                        color: Colors.white,
                                      ),
                                    ),
                                    SizedBox(width: 8),
                                    Text('Đang xử lý...'),
                                  ],
                                )
                              : Text(
                                  _step == 4
                                      ? 'Gửi yêu cầu đặt xe'
                                      : 'Tiếp tục',
                                ),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _modeStep() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const Text(
          'Chọn cách bạn muốn thuê xe',
          style: TextStyle(color: AppColors.muted),
        ),
        const SizedBox(height: 12),
          RentalModeToggle(
          value: _rentalMode,
          onChanged: (v) => setState(() {
            _rentalMode = v;
            _quote = null;
            _quoteError = null;
          }),
        ),
      ],
    );
  }

  Widget _vehicleStep() {
    if (_loadingTypes) {
      return const Center(child: AppLoading(message: 'Đang tải loại xe...'));
    }
    if (_typesError != null) {
      return AppErrorState(message: _typesError!, onRetry: _loadTypes);
    }
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        ..._types.map((type) {
        final selected = _type?.typeId == type.typeId;
        return Padding(
          padding: const EdgeInsets.only(bottom: 10),
          child: AppCard(
            onTap: () {
              setState(() {
                _type = type;
                _vehicle = null;
                _quote = null;
                _quoteError = null;
              });
              _loadVehicles();
            },
            child: Row(
              children: [
                Icon(
                  selected
                      ? Icons.radio_button_checked
                      : Icons.radio_button_off,
                  color: selected ? AppColors.primary : AppColors.muted,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        type.typeName,
                        style: const TextStyle(fontWeight: FontWeight.w800),
                      ),
                      Text(
                        '${type.seatCapacity} chỗ · ${Formatters.vnd(type.pricePerDay)}/ngày · ${Formatters.vnd(type.pricePerKm)}/km',
                        style: const TextStyle(
                          color: AppColors.muted,
                          fontSize: 13,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        );
      }),
        const SizedBox(height: 8),
        const Text(
          'Xe cụ thể (không bắt buộc)',
          style: TextStyle(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 4),
        const Text(
          'Nếu chọn xe, hệ thống sẽ giữ xe này khi bạn thanh toán cọc.',
          style: TextStyle(color: AppColors.muted, fontSize: 13),
        ),
        const SizedBox(height: 10),
        if (_loadingVehicles)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Center(child: CircularProgressIndicator()),
          )
        else ...[
          Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: AppCard(
              onTap: () => setState(() => _vehicle = null),
              child: Row(
                children: [
                  Icon(
                    _vehicle == null
                        ? Icons.radio_button_checked
                        : Icons.radio_button_off,
                    color: _vehicle == null
                        ? AppColors.primary
                        : AppColors.muted,
                  ),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Text('Để điều phối chọn sau'),
                  ),
                ],
              ),
            ),
          ),
          ..._vehicles.map((vehicle) {
            final selected = _vehicle?.vehicleId == vehicle.vehicleId;
            return Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: AppCard(
                onTap: () => setState(() => _vehicle = vehicle),
                child: Row(
                  children: [
                    Icon(
                      selected
                          ? Icons.radio_button_checked
                          : Icons.radio_button_off,
                      color: selected ? AppColors.primary : AppColors.muted,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        vehicle.label,
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ),
                  ],
                ),
              ),
            );
          }),
        ],
      ],
    );
  }

  Widget _timeStep() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _timeTile('Ngày giờ nhận xe', _start, () => _pickDateTime(start: true)),
        const SizedBox(height: 12),
        _timeTile('Ngày giờ trả xe', _end, () => _pickDateTime(start: false)),
        if (_start != null && _end != null && _end!.isAfter(_start!)) ...[
          const SizedBox(height: 16),
          Text(
            'Thời gian thuê: ${BookingPricing.rentalDays(_start!, _end!)} ngày (làm tròn theo hệ thống)',
            style: const TextStyle(color: AppColors.muted),
          ),
        ],
      ],
    );
  }

  Widget _timeTile(String label, DateTime? value, VoidCallback onTap) {
    return AppCard(
      onTap: onTap,
      child: Row(
        children: [
          const Icon(Icons.schedule, color: AppColors.primary),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: const TextStyle(color: AppColors.muted, fontSize: 12),
                ),
                Text(
                  value == null ? 'Chọn thời gian' : Formatters.rentalDt(value),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
              ],
            ),
          ),
          const Icon(Icons.chevron_right),
        ],
      ),
    );
  }

  Widget _locationStep() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        TextField(
          controller: _pickup,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(
            labelText: 'Điểm đón',
            hintText: 'TP.HCM',
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _dropoff,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(
            labelText: 'Điểm trả',
            hintText: 'Vũng Tàu',
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _distance,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          inputFormatters: [
            FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')),
          ],
          decoration: const InputDecoration(
            labelText: 'Km dự kiến (không bắt buộc)',
          ),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _notes,
          maxLines: 3,
          decoration: const InputDecoration(
            labelText: 'Ghi chú (không bắt buộc)',
            hintText: 'Hành lý, điểm đón cụ thể...',
          ),
        ),
      ],
    );
  }

  Widget _confirmStep() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        AppCard(
          child: Column(
            children: [
              _row('Hình thức', Formatters.rentalModeLabel(_rentalMode)),
              _row('Loại xe', _type?.typeName ?? '—'),
              _row('Xe cụ thể', _vehicle?.label ?? 'Để điều phối chọn sau'),
              _row('Nhận xe', _start == null ? '—' : Formatters.rentalDt(_start!)),
              _row('Trả xe', _end == null ? '—' : Formatters.rentalDt(_end!)),
              _row(
                'Điểm đón',
                _pickup.text.trim().isEmpty ? '—' : _pickup.text.trim(),
              ),
              _row(
                'Điểm trả',
                _dropoff.text.trim().isEmpty ? '—' : _dropoff.text.trim(),
              ),
              _row(
                'Km dự kiến',
                _distanceValue == null
                    ? 'Không nhập'
                    : '${_distanceValue!.toStringAsFixed(0)} km',
              ),
              _row(
                'Ghi chú',
                _notes.text.trim().isEmpty ? 'Không' : _notes.text.trim(),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Giá dự kiến',
                style: TextStyle(fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 8),
              if (_quoteLoading)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 12),
                  child: Center(child: CircularProgressIndicator()),
                )
              else if (_quoteError != null)
                Text(
                  _quoteError!,
                  style: const TextStyle(color: AppColors.danger),
                )
              else if (_quote != null) ...[
                if (Formatters.isSelfDrive(_quote!.rentalMode)) ...[
                  _row(
                    'Giá thuê xe',
                    Formatters.vnd(_quote!.quotedPricePerDay),
                  ),
                  _row('Số ngày', '${_quote!.quotedDays}'),
                  _row(
                    'Km miễn phí',
                    '${_quote!.includedKm.toStringAsFixed(0)} km',
                  ),
                  _row(
                    'Km dự kiến',
                    '${_quote!.estimatedDistance.toStringAsFixed(0)} km',
                  ),
                  _row('Km vượt', '${_quote!.extraKm.toStringAsFixed(0)} km'),
                  _row('Phí km vượt', Formatters.vnd(_quote!.extraKmPrice)),
                  _row('Tạm tính', Formatters.vnd(_quote!.totalAmount)),
                  _row('Cọc dự kiến', Formatters.vnd(_quote!.depositAmount)),
                ] else ...[
                  _row(
                    'Giá thuê xe',
                    Formatters.vnd(_quote!.quotedPricePerDay),
                  ),
                  _row('Số ngày', '${_quote!.quotedDays}'),
                  _row('Phí tài xế', Formatters.vnd(_quote!.driverAmount)),
                  _row(
                    'Km dự kiến',
                    '${_quote!.estimatedDistance.toStringAsFixed(0)} km',
                  ),
                  _row('Phí km', Formatters.vnd(_quote!.distanceAmount)),
                  _row('Tạm tính', Formatters.vnd(_quote!.totalAmount)),
                  _row('Cọc dự kiến', Formatters.vnd(_quote!.depositAmount)),
                ],
                const SizedBox(height: 4),
                Text(
                  Formatters.vnd(_quote!.totalAmount),
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: AppColors.primary,
                  ),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Tổng tạm tính từ máy chủ, chưa gồm cọc. Ứng dụng không gửi tổng tiền khi đặt xe.',
                  style: TextStyle(color: AppColors.muted, fontSize: 12),
                ),
              ] else
                const Text('—'),
            ],
          ),
        ),
      ],
    );
  }

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
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
}

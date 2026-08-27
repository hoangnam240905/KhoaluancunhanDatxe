import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';

class BookingDetailScreen extends StatefulWidget {
  final ApiService api;
  final Booking booking;

  const BookingDetailScreen({
    super.key,
    required this.api,
    required this.booking,
  });

  @override
  State<BookingDetailScreen> createState() => _BookingDetailScreenState();
}

class _BookingDetailScreenState extends State<BookingDetailScreen> {
  late Booking _booking;
  bool _reviewed = false;
  int _rating = 5;
  final _comment = TextEditingController();
  bool _submitting = false;
  List<Payment> _payments = [];
  bool _paymentsLoading = true;
  String? _paymentsError;
  bool _creatingPayment = false;

  static const _paymentMethods = ['Cash', 'BankTransfer', 'MoMo', 'VNPay'];

  @override
  void initState() {
    super.initState();
    _booking = widget.booking;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _loadPayments();
    });
  }

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  bool get _canReview =>
      !_reviewed &&
      _booking.status == 'Completed' &&
      !_booking.isSelfDrive &&
      _booking.assignment != null;

  bool get _rentalModeValid =>
      _booking.rentalMode == 'WithDriver' || _booking.rentalMode == 'SelfDrive';

  bool get _hasBlockingDeposit => _payments.any((p) => p.blocksNewDeposit);

  bool get _canPayDeposit =>
      _rentalModeValid &&
      _booking.quotedDepositAmount != null &&
      !_hasBlockingDeposit &&
      !_paymentsLoading &&
      !_creatingPayment;

  String get _paymentSummaryLabel {
    if (_payments.any((p) => p.status == 'Paid')) {
      return Formatters.paymentStatusLabel('Paid');
    }
    if (_payments.any((p) => p.status == 'Pending')) {
      return Formatters.paymentStatusLabel('Pending');
    }
    if (_payments.any((p) => p.status == 'Failed')) {
      return Formatters.paymentStatusLabel('Failed');
    }
    if (_payments.any((p) => p.status == 'Refunded')) {
      return Formatters.paymentStatusLabel('Refunded');
    }
    return 'Chưa thanh toán';
  }

  Future<void> _loadPayments() async {
    setState(() {
      _paymentsLoading = true;
      _paymentsError = null;
    });
    try {
      final booking = await widget.api.getBooking(_booking.bookingId);
      final payments = await widget.api.getBookingPayments(_booking.bookingId);
      if (!mounted) return;
      setState(() {
        _booking = booking;
        _payments = payments;
        _paymentsLoading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _paymentsError = e.toString().replaceFirst('Exception: ', '');
        _paymentsLoading = false;
      });
    }
  }

  Future<void> _submitReview() async {
    setState(() => _submitting = true);
    try {
      await widget.api.createReview(
        bookingId: _booking.bookingId,
        rating: _rating,
        comment: _comment.text.trim().isEmpty ? null : _comment.text.trim(),
      );
      if (!mounted) return;
      setState(() => _reviewed = true);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã gửi đánh giá. Cảm ơn bạn!')),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final b = _booking;
    return Scaffold(
      appBar: AppBar(title: Text('Đơn #${b.bookingId}')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        b.vehicleTypeName,
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                    StatusChip(status: b.status),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  Formatters.rentalModeLabel(b.rentalMode),
                  style: const TextStyle(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          AppCard(
            child: Column(
              children: [
                _row('Điểm đón', b.pickupAddress),
                _row('Điểm trả', b.dropoffAddress),
                _row('Nhận xe', Formatters.dt(b.startDate)),
                _row('Trả xe', Formatters.dt(b.endDate)),
                if (b.estimatedDistance != null)
                  _row(
                    'Km dự kiến',
                    '${b.estimatedDistance!.toStringAsFixed(0)} km',
                  ),
                _row('Tổng tiền', Formatters.vnd(b.totalAmount)),
                if (b.notes != null && b.notes!.isNotEmpty)
                  _row('Ghi chú', b.notes!),
              ],
            ),
          ),
          const SizedBox(height: 12),
          AppCard(child: _assignmentSection(b)),
          const SizedBox(height: 12),
          _paymentSection(),
          const SizedBox(height: 12),
          _reviewSection(),
        ],
      ),
    );
  }

  Widget _assignmentSection(Booking b) {
    if (b.isSelfDrive) {
      final v = b.assignedVehicle;
      if (v == null) {
        return const Text(
          'Đang chờ điều phối gán xe',
          style: TextStyle(color: AppColors.muted, fontWeight: FontWeight.w600),
        );
      }
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Xe đã gán',
            style: TextStyle(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 8),
          _row('Biển số', v.licensePlate),
          _row('Xe', '${v.brand} ${v.model}'),
          _row('Trạng thái xe', v.status),
        ],
      );
    }

    final a = b.assignment;
    if (a == null) {
      return const Text(
        'Tài xế chưa phân công',
        style: TextStyle(color: AppColors.muted, fontWeight: FontWeight.w600),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Tài xế & xe',
          style: TextStyle(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        _row('Tài xế', a.driverName),
        _row(
          'Điện thoại',
          (a.driverPhone == null || a.driverPhone!.isEmpty)
              ? '—'
              : a.driverPhone!,
        ),
        _row('Biển số', a.licensePlate),
        _row('Trạng thái chuyến', Formatters.statusLabel(a.status)),
      ],
    );
  }

  Widget _paymentSection() {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Thanh toán',
            style: TextStyle(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 8),
          if (_booking.quotedDepositAmount != null)
            _row('Tiền cọc', Formatters.vnd(_booking.quotedDepositAmount!))
          else
            const Padding(
              padding: EdgeInsets.only(bottom: 10),
              child: Text(
                'Đơn hàng chưa có thông tin tiền cọc.',
                style: TextStyle(color: AppColors.muted),
              ),
            ),
          _row('Trạng thái thanh toán', _paymentSummaryLabel),
          if (_paymentsLoading)
            const AppLoading(message: 'Đang tải thanh toán...')
          else if (_paymentsError != null)
            AppErrorState(message: _paymentsError!, onRetry: _loadPayments)
          else if (_payments.isEmpty)
            const Padding(
              padding: EdgeInsets.only(bottom: 8),
              child: Text(
                'Chưa có khoản thanh toán.',
                style: TextStyle(color: AppColors.muted),
              ),
            )
          else
            ..._payments.map(_paymentTile),
          if (_canPayDeposit) ...[
            const SizedBox(height: 8),
            FilledButton(
              onPressed: _creatingPayment ? null : _openDepositDialog,
              child: const Text('Thanh toán cọc'),
            ),
          ],
        ],
      ),
    );
  }

  Widget _paymentTile(Payment p) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: AppColors.bg,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          children: [
            _row('Loại', Formatters.paymentTypeLabel(p.paymentType)),
            _row('Số tiền', Formatters.vnd(p.amount)),
            _row('Phương thức', Formatters.paymentMethodLabel(p.method)),
            _row('Trạng thái', Formatters.paymentStatusLabel(p.status)),
            if (p.transactionRef != null && p.transactionRef!.isNotEmpty)
              _row('Mã giao dịch', p.transactionRef!),
            if (p.paidAt != null)
              _row('Thanh toán lúc', Formatters.dt(p.paidAt!)),
            _row('Tạo lúc', Formatters.dt(p.createdAt)),
          ],
        ),
      ),
    );
  }

  Future<void> _openDepositDialog() async {
    var method = 'BankTransfer';
    final refController = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) {
        return StatefulBuilder(
          builder: (ctx, setLocal) {
            return AlertDialog(
              title: const Text('Thanh toán cọc'),
              content: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Số tiền cọc: ${Formatters.vnd(_booking.quotedDepositAmount!)}',
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 6),
                    const Text(
                      'Số tiền do hệ thống tính sẵn. Yêu cầu sẽ ở trạng thái chờ thanh toán.',
                      style: TextStyle(color: AppColors.muted, fontSize: 13),
                    ),
                    const SizedBox(height: 12),
                    const Text(
                      'Phương thức',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                    ..._paymentMethods.map(
                      (m) => ListTile(
                        dense: true,
                        contentPadding: EdgeInsets.zero,
                        leading: Icon(
                          method == m
                              ? Icons.radio_button_checked
                              : Icons.radio_button_off,
                          color: method == m
                              ? AppColors.primary
                              : AppColors.muted,
                        ),
                        title: Text(Formatters.paymentMethodLabel(m)),
                        onTap: () => setLocal(() => method = m),
                      ),
                    ),
                    if (method == 'MoMo' || method == 'VNPay')
                      const Padding(
                        padding: EdgeInsets.only(bottom: 8),
                        child: Text(
                          'Chưa kết nối cổng thanh toán. Chỉ ghi nhận phương thức đã chọn.',
                          style: TextStyle(
                            color: AppColors.muted,
                            fontSize: 12,
                          ),
                        ),
                      ),
                    TextField(
                      controller: refController,
                      decoration: const InputDecoration(
                        labelText: 'Mã giao dịch (không bắt buộc)',
                      ),
                    ),
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(ctx, false),
                  child: const Text('Hủy'),
                ),
                FilledButton(
                  onPressed: () => Navigator.pop(ctx, true),
                  child: const Text('Tạo yêu cầu'),
                ),
              ],
            );
          },
        );
      },
    );
    final ref = refController.text;
    refController.dispose();
    if (confirmed != true || !mounted) return;
    await _createDeposit(method, ref);
  }

  Future<void> _createDeposit(String method, String transactionRef) async {
    setState(() => _creatingPayment = true);
    try {
      await widget.api.createDepositPayment(
        bookingId: _booking.bookingId,
        method: method,
        transactionRef: transactionRef,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã tạo yêu cầu thanh toán cọc.')),
      );
      await _loadPayments();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    } finally {
      if (mounted) setState(() => _creatingPayment = false);
    }
  }

  Widget _reviewSection() {
    if (_booking.isSelfDrive && _booking.status == 'Completed') {
      return const AppCard(
        child: Text(
          'Đơn tự lái không đánh giá tài xế trên hệ thống hiện tại.',
          style: TextStyle(color: AppColors.muted),
        ),
      );
    }
    if (!_canReview && !_reviewed) return const SizedBox.shrink();
    if (_reviewed) {
      return const AppCard(
        child: Row(
          children: [
            Icon(Icons.check_circle, color: AppColors.success),
            SizedBox(width: 8),
            Text('Bạn đã gửi đánh giá cho đơn này.'),
          ],
        ),
      );
    }
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Đánh giá chuyến đi',
            style: TextStyle(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 8),
          Row(
            children: List.generate(5, (i) {
              final star = i + 1;
              return IconButton(
                onPressed: () => setState(() => _rating = star),
                icon: Icon(
                  star <= _rating ? Icons.star : Icons.star_border,
                  color: const Color(0xFFF59E0B),
                ),
              );
            }),
          ),
          TextField(
            controller: _comment,
            maxLines: 3,
            decoration: const InputDecoration(
              labelText: 'Nhận xét (không bắt buộc)',
            ),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _submitting ? null : _submitReview,
            child: _submitting
                ? const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Text('Gửi đánh giá'),
          ),
        ],
      ),
    );
  }

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(label, style: const TextStyle(color: AppColors.muted)),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}

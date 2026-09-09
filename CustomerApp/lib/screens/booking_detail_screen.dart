import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../utils/review_rules.dart';
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
  RentalContract? _contract;
  bool _paymentsLoading = true;
  String? _paymentsError;
  bool _creatingPayment = false;
  bool _contractBusy = false;

  static const _paymentMethods = ['Cash', 'BankTransfer', 'MoMo', 'VNPay'];

  @override
  void initState() {
    super.initState();
    _booking = widget.booking;
    widget.api.realtime.addListener(_onRealtime);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _loadPayments();
    });
  }

  @override
  void dispose() {
    widget.api.realtime.removeListener(_onRealtime);
    _comment.dispose();
    super.dispose();
  }

  void _onRealtime() {
    _loadPayments();
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
      _booking.status != 'Cancelled' &&
      !_hasBlockingDeposit &&
      !_paymentsLoading &&
      !_creatingPayment;

  bool get _canIssueContract =>
      _contract == null &&
      _booking.status != 'Cancelled' &&
      !_contractBusy &&
      !_paymentsLoading;

  bool get _canSignContract =>
      _contract != null &&
      _contract!.canSign &&
      _booking.status != 'Cancelled' &&
      !_contractBusy;

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
      RentalContract? contract;
      try {
        contract = await widget.api.getContract(_booking.bookingId);
      } catch (_) {
        contract = null;
      }
      if (!mounted) return;
      setState(() {
        _booking = booking;
        _payments = payments;
        _contract = contract;
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
    final comment = ReviewRules.normalizeComment(_comment.text);
    final error = ReviewRules.validateComment(_rating, _comment.text);
    if (error != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
      return;
    }
    setState(() => _submitting = true);
    try {
      await widget.api.createReview(
        bookingId: _booking.bookingId,
        rating: _rating,
        comment: comment,
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
                _row('Nhận xe', Formatters.rentalDt(b.startDate)),
                _row('Trả xe', Formatters.rentalDt(b.endDate)),
                if (b.estimatedDistance != null)
                  _row(
                    'Km dự kiến',
                    '${b.estimatedDistance!.toStringAsFixed(0)} km',
                  ),
                _row('Giá lúc đặt', Formatters.vnd(b.totalAmount)),
                if (b.finalAmount != null)
                  _row('Giá chốt', Formatters.vnd(b.finalAmount!)),
                if (b.notes != null && b.notes!.isNotEmpty)
                  _row('Ghi chú', b.notes!),
              ],
            ),
          ),
          if (b.hasPriceSnapshot) ...[
            const SizedBox(height: 12),
            AppCard(child: _snapshotSection(b)),
          ],
          const SizedBox(height: 12),
          AppCard(child: _assignmentSection(b)),
          const SizedBox(height: 12),
          AppCard(child: _feesSection(b)),
          const SizedBox(height: 12),
          AppCard(child: _inspectionsSection(b)),
          const SizedBox(height: 12),
          _contractSection(),
          const SizedBox(height: 12),
          _paymentSection(),
          const SizedBox(height: 12),
          _reviewSection(),
        ],
      ),
    );
  }

  Widget _snapshotSection(Booking b) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Đơn giá lúc đặt',
          style: TextStyle(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        if (b.quotedPricePerDay != null)
          _row('Giá thuê/ngày', Formatters.vnd(b.quotedPricePerDay!)),
        if (b.quotedDays != null) _row('Số ngày', '${b.quotedDays}'),
        if (b.quotedPricePerKm != null)
          _row('Giá km', Formatters.vnd(b.quotedPricePerKm!)),
        if (!b.isSelfDrive && b.quotedDriverFeePerDay != null)
          _row('Phí tài xế/ngày', Formatters.vnd(b.quotedDriverFeePerDay!)),
        if (b.isSelfDrive && b.quotedSelfDriveIncludedKmPerDay != null)
          _row(
            'Km miễn phí/ngày',
            '${b.quotedSelfDriveIncludedKmPerDay!.toStringAsFixed(0)} km',
          ),
        if (b.isSelfDrive && b.quotedSelfDriveExtraKmPrice != null)
          _row(
            'Đơn giá km vượt',
            Formatters.vnd(b.quotedSelfDriveExtraKmPrice!),
          ),
        if (b.quotedDepositAmount != null)
          _row('Cọc lúc đặt', Formatters.vnd(b.quotedDepositAmount!)),
        const SizedBox(height: 4),
        const Text(
          'Số liệu máy chủ lúc đặt. Ứng dụng không tính lại từ bảng giá hiện tại.',
          style: TextStyle(color: AppColors.muted, fontSize: 12),
        ),
      ],
    );
  }

  Widget _feesSection(Booking b) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Phí phát sinh',
          style: TextStyle(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        if (b.fees.isEmpty)
          const Text(
            'Không có phí phát sinh.',
            style: TextStyle(color: AppColors.muted),
          )
        else ...[
          ...b.fees.map(_feeTile),
          if (b.totalFees != null)
            _row('Tổng phí (server)', Formatters.vnd(b.totalFees!)),
          if (b.finalBaseAmount != null)
            _row('Giá chốt trước phí', Formatters.vnd(b.finalBaseAmount!)),
          const SizedBox(height: 4),
          const Text(
            'Km vượt đã nằm trong giá chốt. Không cộng ExtraKm thêm một lần nữa.',
            style: TextStyle(color: AppColors.muted, fontSize: 12),
          ),
        ],
      ],
    );
  }

  Widget _feeTile(BookingFee fee) {
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
            _row('Loại', Formatters.feeTypeLabel(fee.feeType)),
            _row('Số tiền', Formatters.vnd(fee.amount)),
            if (fee.description != null && fee.description!.isNotEmpty)
              _row('Mô tả', fee.description!),
            if (fee.isIncludedInBase)
              const Padding(
                padding: EdgeInsets.only(top: 4),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    'Đã gồm trong giá chốt — không cộng thêm.',
                    style: TextStyle(color: AppColors.muted, fontSize: 12),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _inspectionsSection(Booking b) {
    final handover = b.inspections.where((i) => i.inspectionType == 'Handover');
    final returned = b.inspections.where((i) => i.inspectionType == 'Return');
    final handoverRow = handover.isEmpty ? null : handover.last;
    final returnRow = returned.isEmpty ? null : returned.last;
    num? actualKm;
    if (handoverRow?.odometerKm != null && returnRow?.odometerKm != null) {
      actualKm = returnRow!.odometerKm! - handoverRow!.odometerKm!;
      if (actualKm < 0) actualKm = 0;
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Tình trạng xe',
          style: TextStyle(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        if (b.inspections.isEmpty)
          const Text(
            'Chưa có dữ liệu kiểm xe.',
            style: TextStyle(color: AppColors.muted),
          )
        else ...[
          if (handoverRow != null)
            _inspectionTile(
              '🚗 Tình trạng xe khi nhận',
              handoverRow,
              kmLabel: 'KM lúc nhận',
            ),
          if (returnRow != null)
            _inspectionTile(
              '🔧 Tình trạng xe khi trả',
              returnRow,
              kmLabel: 'KM lúc trả',
              actualKm: actualKm,
            ),
        ],
      ],
    );
  }

  Widget _inspectionTile(
    String title,
    VehicleInspection i, {
    required String kmLabel,
    num? actualKm,
  }) {
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
            _row('Loại', title),
            _row('Thời điểm', Formatters.dt(i.actualAt)),
            if (i.odometerKm != null)
              _row(kmLabel, '${i.odometerKm!.toStringAsFixed(0)} km'),
            if (i.fuelLevel != null)
              _row('Nhiên liệu', '${i.fuelLevel!.toStringAsFixed(0)}%'),
            if (i.exteriorCondition != null && i.exteriorCondition!.isNotEmpty)
              _row('Ngoại thất', i.exteriorCondition!),
            if (i.technicalCondition != null && i.technicalCondition!.isNotEmpty)
              _row('Kỹ thuật', i.technicalCondition!),
            if (i.notes != null && i.notes!.isNotEmpty) _row('Ghi chú', i.notes!),
            if (actualKm != null)
              _row('KM thực tế', '${actualKm.toStringAsFixed(0)} km'),
          ],
        ),
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
          _row('Trạng thái xe', Formatters.vehicleStatusLabel(v.status)),
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
            if (p.status == 'Pending' && _booking.status != 'Cancelled')
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    FilledButton(
                      onPressed: _contractBusy
                          ? null
                          : () => _simulatePayment(p.paymentId, success: true),
                      child: const Text('Mô phỏng thành công'),
                    ),
                    OutlinedButton(
                      onPressed: _contractBusy
                          ? null
                          : () => _simulatePayment(p.paymentId, success: false),
                      child: const Text('Mô phỏng thất bại'),
                    ),
                  ],
                ),
              ),
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

  Widget _contractSection() {
    final c = _contract;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Hợp đồng điện tử',
            style: TextStyle(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 8),
          if (_paymentsLoading)
            const AppLoading(message: 'Đang tải hợp đồng...')
          else if (c == null) ...[
            const Text(
              'Chưa có hợp đồng cho đơn này.',
              style: TextStyle(color: AppColors.muted),
            ),
            if (_canIssueContract) ...[
              const SizedBox(height: 8),
              FilledButton(
                onPressed: _issueContract,
                child: const Text('Lập hợp đồng'),
              ),
            ],
          ] else ...[
            _row('Số HĐ', c.contractNumber),
            _row('Trạng thái', Formatters.contractStatusLabel(c.status)),
            _row('Khách', c.customerName),
            _row('Loại xe', c.vehicleTypeName),
            _row('Hình thức', Formatters.rentalModeLabel(c.rentalMode)),
            _row('Giá', Formatters.vnd(c.totalAmount)),
            if (c.depositAmount != null)
              _row('Cọc', Formatters.vnd(c.depositAmount!)),
            if (c.signedAt != null) _row('Ký lúc', Formatters.dt(c.signedAt!)),
            if (_canSignContract) ...[
              const SizedBox(height: 8),
              FilledButton(
                onPressed: _signContract,
                child: const Text('Mô phỏng ký hợp đồng'),
              ),
            ],
          ],
        ],
      ),
    );
  }

  Future<void> _issueContract() async {
    setState(() => _contractBusy = true);
    try {
      await widget.api.createContract(_booking.bookingId);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã lập hợp đồng điện tử.')),
      );
      await _loadPayments();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    } finally {
      if (mounted) setState(() => _contractBusy = false);
    }
  }

  Future<void> _signContract() async {
    final id = _contract?.contractId;
    if (id == null) return;
    setState(() => _contractBusy = true);
    try {
      await widget.api.simulateSignContract(id);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã mô phỏng ký hợp đồng.')),
      );
      await _loadPayments();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    } finally {
      if (mounted) setState(() => _contractBusy = false);
    }
  }

  Future<void> _simulatePayment(int paymentId, {required bool success}) async {
    setState(() => _contractBusy = true);
    try {
      if (success) {
        await widget.api.simulatePaymentSuccess(paymentId);
      } else {
        await widget.api.simulatePaymentFailure(paymentId);
      }
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            success
                ? 'Mô phỏng thanh toán thành công.'
                : 'Mô phỏng thanh toán thất bại.',
          ),
        ),
      );
      await _loadPayments();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    } finally {
      if (mounted) setState(() => _contractBusy = false);
    }
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
            maxLength: ReviewRules.commentMaxLength,
            decoration: InputDecoration(
              labelText: _rating <= 3
                  ? 'Nhận xét (bắt buộc)'
                  : 'Nhận xét (không bắt buộc)',
              helperText: 'Bắt buộc với 1–3 sao. Tối đa 500 ký tự.',
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

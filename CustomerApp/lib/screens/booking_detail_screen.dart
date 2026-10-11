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
    final depositPayment = _payments.where((p) => p.status == 'Paid').firstOrNull;
    final depositStatus = depositPayment != null ? 'Paid' : (_payments.any((p) => p.status == 'Pending') ? 'Pending' : null);
    final remainingAmount = b.finalAmount != null
        ? (b.finalAmount! - (depositPayment?.amount ?? 0))
        : ((b.quotedDepositAmount != null) ? (b.totalAmount - b.quotedDepositAmount!) : null);

    return Scaffold(
      appBar: AppBar(
        title: Text('Chi tiết đơn #${b.bookingId}'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _loadPayments,
            tooltip: 'Làm mới',
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadPayments,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // Timeline tiến trình trực quan
            BookingTimelineWidget(status: b.status, isCancelled: b.status == 'Cancelled'),
            const SizedBox(height: 16),

            // Financial Summary
            FinancialSummaryBox(
              totalAmount: b.finalAmount ?? b.totalAmount,
              depositAmount: b.quotedDepositAmount,
              remainingAmount: remainingAmount,
              depositStatus: depositStatus,
            ),
            const SizedBox(height: 16),

            // Thông tin hành trình
            _journeyCard(b),
            const SizedBox(height: 16),

            // Thông tin xe & tài xế
            _assignmentSection(b),
            const SizedBox(height: 16),

            // Hợp đồng điện tử
            _contractSection(),
            const SizedBox(height: 16),

            // Thanh toán
            _paymentSection(),

            // Phụ phí phát sinh (nếu có)
            if (b.fees.isNotEmpty) ...[
              const SizedBox(height: 16),
              AppCard(child: _feesSection(b)),
            ],

            // Biên bản bàn giao / trả xe (nếu có)
            if (b.inspections.isNotEmpty) ...[
              const SizedBox(height: 16),
              AppCard(child: _inspectionsSection(b)),
            ],

            // Đơn giá lúc đặt (Snapshot)
            if (b.hasPriceSnapshot) ...[
              const SizedBox(height: 16),
              AppCard(child: _snapshotSection(b)),
            ],

            // Đánh giá khi chuyến đi hoàn thành
            if (b.status == 'Completed') ...[
              const SizedBox(height: 16),
              _reviewSection(),
            ],
            const SizedBox(height: 32),
          ],
        ),
      ),
    );
  }

  Widget _journeyCard(Booking b) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Row(
                children: [
                  Icon(Icons.route_outlined, color: AppColors.primary, size: 20),
                  SizedBox(width: 8),
                  Text('Lộ trình di chuyển', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                ],
              ),
              StatusChip(status: b.status),
            ],
          ),
          const SizedBox(height: 16),
          _timelinePoint(Icons.trip_origin, AppColors.primary, 'Điểm đón', b.pickupAddress, Formatters.rentalDt(b.startDate)),
          Container(
            margin: const EdgeInsets.only(left: 10),
            height: 24,
            width: 2,
            color: const Color(0xFFCBD5E1),
          ),
          _timelinePoint(Icons.location_on, AppColors.danger, 'Điểm trả', b.dropoffAddress, Formatters.rentalDt(b.endDate)),
          const Divider(height: 24, color: AppColors.border),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _infoTile('Hình thức', Formatters.rentalModeLabel(b.rentalMode)),
              if (b.estimatedDistance != null)
                _infoTile('Quãng đường', '${b.estimatedDistance!.toStringAsFixed(0)} km'),
              _infoTile('Xe yêu cầu', b.vehicleTypeName),
            ],
          ),
          if (b.notes != null && b.notes!.isNotEmpty) ...[
            const SizedBox(height: 10),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: const Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(
                'Ghi chú: ${b.notes!}',
                style: const TextStyle(fontSize: 12, color: AppColors.muted),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _timelinePoint(IconData icon, Color iconColor, String title, String address, String time) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 22, color: iconColor),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: const TextStyle(color: AppColors.muted, fontSize: 11, fontWeight: FontWeight.w600)),
              const SizedBox(height: 2),
              Text(address, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
              const SizedBox(height: 2),
              Text(time, style: const TextStyle(color: AppColors.primary, fontSize: 12, fontWeight: FontWeight.w600)),
            ],
          ),
        ),
      ],
    );
  }

  Widget _infoTile(String label, String val) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: AppColors.muted, fontSize: 11)),
        const SizedBox(height: 2),
        Text(val, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
      ],
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
        return Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFFFFBEB),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: const Color(0xFFFDE68A)),
          ),
          child: const Row(
            children: [
              Icon(Icons.hourglass_top_rounded, color: AppColors.warning, size: 24),
              SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Đang điều phối xe', style: TextStyle(fontWeight: FontWeight.w800, color: AppColors.warning)),
                    SizedBox(height: 2),
                    Text('Bộ phận điều hành đang chuẩn bị xe tự lái tốt nhất cho bạn.', style: TextStyle(fontSize: 12, color: AppColors.text)),
                  ],
                ),
              ),
            ],
          ),
        );
      }
      return Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionHeader(
              icon: Icons.directions_car_filled,
              title: 'Xe bàn giao',
              subtitle: 'Thông tin phương tiện được phân công cho bạn',
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: Colors.amber.shade100,
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: Colors.amber.shade700, width: 1.5),
                  ),
                  child: Text(
                    v.licensePlate,
                    style: TextStyle(fontWeight: FontWeight.w900, color: Colors.amber.shade900, fontSize: 14, letterSpacing: 1),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('${v.brand} ${v.model}', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                      Text('Mã định danh xe: #${v.vehicleId}', style: const TextStyle(fontSize: 12, color: AppColors.muted)),
                    ],
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: AppColors.success.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Text(
                    Formatters.vehicleStatusLabel(v.status),
                    style: const TextStyle(color: AppColors.success, fontWeight: FontWeight.w700, fontSize: 11),
                  ),
                ),
              ],
            ),
          ],
        ),
      );
    }

    final a = b.assignment;
    if (a == null) {
      return Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFFFFFBEB),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFFFDE68A)),
        ),
        child: const Row(
          children: [
            Icon(Icons.person_search_rounded, color: AppColors.warning, size: 24),
            SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Đang điều phối tài xế', style: TextStyle(fontWeight: FontWeight.w800, color: AppColors.warning)),
                  SizedBox(height: 2),
                  Text('Điều phối viên đang phân công tài xế và phương tiện cho chuyến đi.', style: TextStyle(fontSize: 12, color: AppColors.text)),
                ],
              ),
            ),
          ],
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SectionHeader(
            icon: Icons.person_pin_circle_rounded,
            title: 'Tài xế & Phương tiện',
            subtitle: 'Thông tin tài xế đón bạn trong chuyến đi',
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              CircleAvatar(
                radius: 24,
                backgroundColor: AppColors.primary.withValues(alpha: 0.15),
                child: Text(
                  a.driverName.isNotEmpty ? a.driverName[0].toUpperCase() : 'T',
                  style: const TextStyle(fontWeight: FontWeight.w900, color: AppColors.primary, fontSize: 18),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(a.driverName, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                    const SizedBox(height: 2),
                    Text(
                      a.driverPhone != null && a.driverPhone!.isNotEmpty ? a.driverPhone! : 'Chưa có SĐT',
                      style: const TextStyle(fontSize: 13, color: AppColors.muted),
                    ),
                  ],
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: Colors.amber.shade100,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Colors.amber.shade700, width: 1.5),
                ),
                child: Text(
                  a.licensePlate,
                  style: TextStyle(fontWeight: FontWeight.w900, color: Colors.amber.shade900, fontSize: 13, letterSpacing: 1),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            decoration: BoxDecoration(
              color: const Color(0xFFF1F5F9),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Trạng thái chuyến:', style: TextStyle(fontSize: 12, color: AppColors.muted)),
                Text(
                  Formatters.statusLabel(a.status),
                  style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.primary),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _paymentSection() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SectionHeader(
            icon: Icons.payments_outlined,
            title: 'Thanh toán cọc',
            subtitle: 'Giao dịch đặt cọc bảo đảm của đơn thuê',
            trailing: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: const Color(0xFFEFF6FF),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                _paymentSummaryLabel,
                style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700, fontSize: 11),
              ),
            ),
          ),
          const SizedBox(height: 14),
          if (_booking.quotedDepositAmount != null)
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Tiền cọc yêu cầu:', style: TextStyle(color: AppColors.muted, fontSize: 13)),
                  Text(
                    Formatters.vnd(_booking.quotedDepositAmount!),
                    style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 15, color: AppColors.primary),
                  ),
                ],
              ),
            )
          else
            const Padding(
              padding: EdgeInsets.only(bottom: 10),
              child: Text(
                'Đơn hàng chưa có thông tin tiền cọc.',
                style: TextStyle(color: AppColors.muted),
              ),
            ),
          const SizedBox(height: 12),
          if (_paymentsLoading)
            const AppLoading(message: 'Đang tải thanh toán...')
          else if (_paymentsError != null)
            AppErrorState(message: _paymentsError!, onRetry: _loadPayments)
          else if (_payments.isEmpty)
            const Padding(
              padding: EdgeInsets.only(bottom: 8),
              child: Text(
                'Chưa có khoản thanh toán nào được tạo.',
                style: TextStyle(color: AppColors.muted, fontSize: 13),
              ),
            )
          else
            ..._payments.map(_paymentTile),
          if (_canPayDeposit) ...[
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: _creatingPayment ? null : _openDepositDialog,
                icon: const Icon(Icons.payment, size: 18),
                label: const Text('Thanh toán tiền cọc ngay'),
                style: FilledButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _paymentTile(Payment p) {
    final isPaid = p.status == 'Paid';
    final isFailed = p.status == 'Failed';
    final statusColor = isPaid ? AppColors.success : (isFailed ? AppColors.danger : AppColors.warning);

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: const Color(0xFFF8FAFC),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Icon(
                      p.method == 'MoMo' || p.method == 'VNPay' ? Icons.account_balance_wallet : Icons.account_balance,
                      size: 18,
                      color: AppColors.primary,
                    ),
                    const SizedBox(width: 8),
                    Text(
                      Formatters.paymentMethodLabel(p.method),
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
                    ),
                  ],
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: statusColor.withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(4),
                  ),
                  child: Text(
                    Formatters.paymentStatusLabel(p.status),
                    style: TextStyle(color: statusColor, fontWeight: FontWeight.w700, fontSize: 11),
                  ),
                ),
              ],
            ),
            const Divider(height: 16, color: AppColors.border),
            _row('Mã thanh toán', '#${p.paymentId}'),
            _row('Số tiền', Formatters.vnd(p.amount)),
            if (p.transactionRef != null && p.transactionRef!.isNotEmpty)
              _row('Mã giao dịch', p.transactionRef!),
            if (p.paidAt != null)
              _row('Thời gian', Formatters.dt(p.paidAt!)),
            if (p.status == 'Pending' && _booking.status != 'Cancelled')
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Row(
                  children: [
                    Expanded(
                      child: FilledButton.icon(
                        onPressed: _contractBusy
                            ? null
                            : () => _simulatePayment(p.paymentId, success: true),
                        icon: const Icon(Icons.check_circle_outline, size: 16),
                        label: const Text('Mô phỏng Đã cọc'),
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.success,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: _contractBusy
                            ? null
                            : () => _simulatePayment(p.paymentId, success: false),
                        icon: const Icon(Icons.highlight_off, size: 16),
                        label: const Text('Mô phỏng Thất bại'),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: AppColors.danger,
                          side: const BorderSide(color: AppColors.danger),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                        ),
                      ),
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
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SectionHeader(
            icon: Icons.description_outlined,
            title: 'Hợp đồng điện tử',
            subtitle: 'Cam kết thỏa thuận thuê xe trực tuyến',
            trailing: c != null
                ? Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: (c.status == 'Signed' ? AppColors.success : AppColors.primary).withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      Formatters.contractStatusLabel(c.status),
                      style: TextStyle(
                        color: c.status == 'Signed' ? AppColors.success : AppColors.primary,
                        fontWeight: FontWeight.w700,
                        fontSize: 11,
                      ),
                    ),
                  )
                : null,
          ),
          const SizedBox(height: 12),
          if (_paymentsLoading)
            const AppLoading(message: 'Đang tải hợp đồng...')
          else if (c == null) ...[
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: const Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColors.border),
              ),
              child: const Row(
                children: [
                  Icon(Icons.info_outline, color: AppColors.muted, size: 20),
                  SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'Đơn chưa có hợp đồng. Bạn có thể tạo hợp đồng ngay để bảo đảm quyền lợi.',
                      style: TextStyle(color: AppColors.muted, fontSize: 12),
                    ),
                  ),
                ],
              ),
            ),
            if (_canIssueContract) ...[
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: _issueContract,
                  icon: const Icon(Icons.note_add_outlined, size: 18),
                  label: const Text('Lập hợp đồng thuê xe'),
                  style: FilledButton.styleFrom(
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ),
            ],
          ] else ...[
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: const Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColors.border),
              ),
              child: Column(
                children: [
                  _row('Số hợp đồng', c.contractNumber),
                  _row('Bên thuê', c.customerName),
                  _row('Loại xe thuê', c.vehicleTypeName),
                  _row('Hình thức', Formatters.rentalModeLabel(c.rentalMode)),
                  _row('Giá trị hợp đồng', Formatters.vnd(c.totalAmount)),
                  if (c.depositAmount != null)
                    _row('Tiền cọc cam kết', Formatters.vnd(c.depositAmount!)),
                  if (c.signedAt != null)
                    _row('Thời điểm ký', Formatters.dt(c.signedAt!)),
                ],
              ),
            ),
            if (_canSignContract) ...[
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: _signContract,
                  icon: const Icon(Icons.draw_outlined, size: 18),
                  label: const Text('Ký hợp đồng điện tử'),
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.success,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
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

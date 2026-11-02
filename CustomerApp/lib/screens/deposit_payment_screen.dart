import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../utils/formatters.dart';

class DepositPaymentScreen extends StatefulWidget {
  final Booking booking;
  final ApiService api;

  const DepositPaymentScreen({
    super.key,
    required this.booking,
    required this.api,
  });

  @override
  State<DepositPaymentScreen> createState() => _DepositPaymentScreenState();
}

class _DepositPaymentScreenState extends State<DepositPaymentScreen> {
  late Booking _booking;
  bool _loading = true;
  bool _submitting = false;
  String? _error;
  String _selectedMethod = 'MoMo'; // 'Card', 'MoMo', 'VNPay', 'BankTransfer'
  String _selectedTab = 'wallet'; // 'card', 'wallet', 'transfer'
  bool _paymentSuccess = false;

  @override
  void initState() {
    super.initState();
    _booking = widget.booking;
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final b = await widget.api.getBooking(_booking.bookingId);
      final payments = await widget.api.getBookingPayments(_booking.bookingId);
      if (!mounted) return;
      final paidDeposit = payments.any(
          (p) => p.paymentType == 'Deposit' && p.status == 'Paid');
      setState(() {
        _booking = b;
        _paymentSuccess = paidDeposit;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString().replaceFirst('Exception: ', '');
        _loading = false;
      });
    }
  }

  Future<void> _handlePayment() async {
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final methodCode = _selectedMethod;
      final ref = 'SIM-${DateTime.now().millisecondsSinceEpoch}';
      final p = await widget.api.createDepositPayment(
        bookingId: _booking.bookingId,
        method: methodCode,
        transactionRef: ref,
      );

      // Simulate payment success immediately as requested by demo workflow
      await widget.api.simulatePaymentSuccess(p.paymentId);

      if (!mounted) return;
      setState(() {
        _paymentSuccess = true;
        _submitting = false;
      });

      _showSuccessDialog();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString().replaceFirst('Exception: ', '');
        _submitting = false;
      });
    }
  }

  void _showSuccessDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Row(
          children: [
            Icon(Icons.check_circle, color: Color(0xFF16A34A), size: 28),
            SizedBox(width: 10),
            Text('Thanh toán thành công!'),
          ],
        ),
        content: const Text(
          'Tiền cọc của bạn đã được ghi nhận. Xe đã được giữ trên hệ thống theo đơn thuê.',
          style: TextStyle(fontSize: 14, color: Color(0xFF334155)),
        ),
        actions: [
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: const Color(0xFF2563EB),
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(10)),
            ),
            onPressed: () {
              Navigator.pop(ctx);
              Navigator.pop(context, true);
            },
            child: const Text('Xem đơn thuê'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final b = _booking;
    final depositAmount =
        b.quotedDepositAmount ?? (b.totalAmount * 0.3);
    final remainingAmount =
        (b.totalAmount - depositAmount) > 0 ? (b.totalAmount - depositAmount) : 0.0;

    final vehicleName = b.assignedVehicle != null
        ? '${b.assignedVehicle!.brand} ${b.assignedVehicle!.model}'
        : b.vehicleTypeName;
    final plate = b.assignedVehicle?.licensePlate ?? b.assignment?.licensePlate;
    final days = b.quotedDays ??
        (b.endDate.difference(b.startDate).inDays <= 0
            ? 1
            : b.endDate.difference(b.startDate).inDays);

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF0F172A)),
          onPressed: () => Navigator.pop(context, true),
        ),
        title: const Text(
          'Thanh toán tiền cọc',
          style: TextStyle(
            color: Color(0xFF0F172A),
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: false,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // ── Header Eyebrow & Title ─────────────────────────
                  const Text(
                    'THANH TOÁN',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF2563EB),
                      letterSpacing: 0.5,
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Thanh toán tiền cọc',
                    style: TextStyle(
                      fontSize: 24,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Hoàn tất thanh toán tiền cọc để xác nhận đơn thuê của bạn.',
                    style: TextStyle(
                      fontSize: 13,
                      color: Color(0xFF64748B),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // ── Steps indicator ────────────────────────────────
                  _stepIndicator(),
                  const SizedBox(height: 16),

                  // ── Info Card (Mã đơn, Xe, Ngày, Quay lại) ─────────
                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(
                              'MÃ ĐƠN THUÊ #${b.bookingId}',
                              style: const TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w700,
                                color: Color(0xFF0F172A),
                              ),
                            ),
                            TextButton.icon(
                              onPressed: () => Navigator.pop(context, true),
                              icon: const Icon(Icons.arrow_back, size: 14),
                              label: const Text(
                                'Quay lại chi tiết đơn',
                                style: TextStyle(fontSize: 12),
                              ),
                              style: TextButton.styleFrom(
                                padding: EdgeInsets.zero,
                                foregroundColor: const Color(0xFF2563EB),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '$vehicleName · ${Formatters.dt(b.startDate)} → ${Formatters.dt(b.endDate)}',
                          style: const TextStyle(
                            fontSize: 12.5,
                            color: Color(0xFF334155),
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          'Hình thức thuê: ${Formatters.rentalModeLabel(b.rentalMode)}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: Color(0xFF64748B),
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          '${b.pickupAddress} → ${b.dropoffAddress}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: Color(0xFF64748B),
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  if (_error != null) ...[
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: const Color(0xFFFEF2F2),
                        borderRadius: BorderRadius.circular(10),
                        border: Border.all(color: const Color(0xFFFCA5A5)),
                      ),
                      child: Text(
                        _error!,
                        style: const TextStyle(
                            color: Color(0xFFDC2626), fontSize: 13),
                      ),
                    ),
                    const SizedBox(height: 16),
                  ],

                  // ── Card Thông tin xe ──────────────────────────────
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    child: Row(
                      children: [
                        Container(
                          width: 80,
                          height: 60,
                          decoration: BoxDecoration(
                            color: const Color(0xFFEFF6FF),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: const Icon(
                            Icons.directions_car_filled,
                            color: Color(0xFF2563EB),
                            size: 36,
                          ),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                vehicleName,
                                style: const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w700,
                                  color: Color(0xFF0F172A),
                                ),
                              ),
                              const SizedBox(height: 3),
                              Text(
                                '${b.vehicleTypeName}${plate != null ? " · $plate" : ""} · $days ngày',
                                style: const TextStyle(
                                  fontSize: 13,
                                  color: Color(0xFF64748B),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  // ── Card Tiền cọc cần thanh toán ──────────────────
                  Container(
                    padding: const EdgeInsets.all(18),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Thanh toán tiền cọc',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF0F172A),
                          ),
                        ),
                        const SizedBox(height: 6),
                        const Text(
                          'Bạn cần thanh toán tiền cọc theo số tiền backend đã ghi nhận để hoàn tất bước xác nhận đặt xe.',
                          style: TextStyle(
                            fontSize: 13,
                            color: Color(0xFF64748B),
                            height: 1.4,
                          ),
                        ),
                        const SizedBox(height: 12),
                        const Text(
                          'Tiền cọc cần thanh toán',
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w500,
                            color: Color(0xFF64748B),
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          Formatters.vnd(depositAmount),
                          style: const TextStyle(
                            fontSize: 26,
                            fontWeight: FontWeight.w800,
                            color: Color(0xFF2563EB),
                          ),
                        ),
                        const SizedBox(height: 8),
                        const Row(
                          children: [
                            Icon(Icons.shield_outlined,
                                size: 14, color: Color(0xFF64748B)),
                            SizedBox(width: 6),
                            Text(
                              'Thông tin thanh toán được bảo vệ trong quá trình xử lý.',
                              style: TextStyle(
                                fontSize: 11.5,
                                color: Color(0xFF64748B),
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  // ── Card Chọn phương thức thanh toán ───────────────
                  Container(
                    padding: const EdgeInsets.all(18),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Chọn phương thức thanh toán',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF0F172A),
                          ),
                        ),
                        const SizedBox(height: 14),

                        // 3 Tabs
                        Row(
                          children: [
                            _methodTab('card', 'Thẻ ngân hàng', Icons.credit_card),
                            const SizedBox(width: 8),
                            _methodTab('wallet', 'Ví điện tử',
                                Icons.account_balance_wallet_outlined),
                            const SizedBox(width: 8),
                            _methodTab(
                                'transfer', 'Chuyển khoản', Icons.account_balance),
                          ],
                        ),
                        const SizedBox(height: 14),

                        // Sub options for wallet or other
                        if (_selectedTab == 'wallet') ...[
                          _walletOption('MoMo', 'MoMo', Icons.phone_android),
                          const SizedBox(height: 8),
                          _walletOption(
                              'VNPay', 'VNPay', Icons.qr_code_scanner),
                        ] else if (_selectedTab == 'card') ...[
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: const Color(0xFFF8FAFC),
                              borderRadius: BorderRadius.circular(10),
                              border: Border.all(color: const Color(0xFFE2E8F0)),
                            ),
                            child: const Row(
                              children: [
                                Icon(Icons.credit_card,
                                    color: Color(0xFF2563EB), size: 20),
                                SizedBox(width: 8),
                                Text(
                                  'Thẻ ATM / Visa / Mastercard nội địa',
                                  style: TextStyle(
                                      fontSize: 13,
                                      fontWeight: FontWeight.w500),
                                ),
                              ],
                            ),
                          ),
                        ] else ...[
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: const Color(0xFFF8FAFC),
                              borderRadius: BorderRadius.circular(10),
                              border: Border.all(color: const Color(0xFFE2E8F0)),
                            ),
                            child: const Row(
                              children: [
                                Icon(Icons.account_balance,
                                    color: Color(0xFF2563EB), size: 20),
                                SizedBox(width: 8),
                                Text(
                                  'Chuyển khoản liên ngân hàng 24/7',
                                  style: TextStyle(
                                      fontSize: 13,
                                      fontWeight: FontWeight.w500),
                                ),
                              ],
                            ),
                          ),
                        ],
                        const SizedBox(height: 10),
                        const Text(
                          'Phương thức được gửi tới API mô phỏng hiện có — chưa kết nối cổng thanh toán thật.',
                          style: TextStyle(
                            fontSize: 11.5,
                            color: Color(0xFF94A3B8),
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  // ── Box Thanh toán mô phỏng (Notice) ───────────────
                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFFFBEB),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: const Color(0xFFFDE68A)),
                    ),
                    child: const Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Thanh toán mô phỏng',
                          style: TextStyle(
                            fontSize: 12.5,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFFB45309),
                          ),
                        ),
                        SizedBox(height: 2),
                        Text(
                          'Nút xác nhận gọi API thanh toán cọc hiện có. Xe chỉ được giữ khi backend xác nhận hold.',
                          style: TextStyle(
                            fontSize: 12,
                            color: Color(0xFF92400E),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  // ── Card Tóm tắt thanh toán ────────────────────────
                  Container(
                    padding: const EdgeInsets.all(18),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text(
                          'Tóm tắt thanh toán',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF0F172A),
                          ),
                        ),
                        const SizedBox(height: 12),
                        _sumRow('Giá thuê xe', Formatters.vnd(b.totalAmount)),
                        _sumRow('Tổng tiền thuê', Formatters.vnd(b.totalAmount),
                            isBold: true),
                        const Divider(height: 18, color: Color(0xFFE2E8F0)),
                        _sumRow('Tiền cọc', Formatters.vnd(depositAmount),
                            isHighlight: true),
                        _sumRow('Còn lại', Formatters.vnd(remainingAmount)),
                        const SizedBox(height: 18),

                        // Nút Thanh toán
                        FilledButton(
                          onPressed: (_paymentSuccess || _submitting)
                              ? null
                              : _handlePayment,
                          style: FilledButton.styleFrom(
                            backgroundColor: const Color(0xFF2563EB),
                            disabledBackgroundColor: const Color(0xFF94A3B8),
                            padding: const EdgeInsets.symmetric(vertical: 16),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                          ),
                          child: _submitting
                              ? const SizedBox(
                                  height: 20,
                                  width: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : Text(
                                  _paymentSuccess
                                      ? 'Đã thanh toán cọc'
                                      : 'Thanh toán ${Formatters.vnd(depositAmount)}',
                                  style: const TextStyle(
                                    fontSize: 15,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                        ),
                        const SizedBox(height: 14),

                        // Badges bảo vệ
                        _badgeItem(Icons.verified_user_outlined,
                            'Bảo mật thông tin thanh toán'),
                        const SizedBox(height: 4),
                        _badgeItem(
                            Icons.credit_card_off_outlined, 'Không lưu thông tin thẻ'),
                        const SizedBox(height: 4),
                        _badgeItem(Icons.sync_alt,
                            'Quy trình thanh toán minh bạch'),
                      ],
                    ),
                  ),
                  const SizedBox(height: 30),
                ],
              ),
            ),
    );
  }

  Widget _stepIndicator() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          _stepItem(1, 'Thông tin đơn', isDone: true),
          const Icon(Icons.chevron_right, size: 16, color: Color(0xFFCBD5E1)),
          _stepItem(2, 'Tiền cọc',
              isOn: !_paymentSuccess, isDone: _paymentSuccess),
          const Icon(Icons.chevron_right, size: 16, color: Color(0xFFCBD5E1)),
          _stepItem(3, 'Hoàn tất', isOn: _paymentSuccess),
        ],
      ),
    );
  }

  Widget _stepItem(int num, String label,
      {bool isOn = false, bool isDone = false}) {
    Color bg = const Color(0xFFF1F5F9);
    Color fg = const Color(0xFF64748B);
    if (isOn) {
      bg = const Color(0xFF2563EB);
      fg = Colors.white;
    } else if (isDone) {
      bg = const Color(0xFF16A34A);
      fg = Colors.white;
    }

    return Row(
      children: [
        Container(
          width: 20,
          height: 20,
          decoration: BoxDecoration(
            color: bg,
            shape: BoxShape.circle,
          ),
          alignment: Alignment.center,
          child: isDone
              ? const Icon(Icons.check, size: 12, color: Colors.white)
              : Text(
                  '$num',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                    color: fg,
                  ),
                ),
        ),
        const SizedBox(width: 6),
        Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: isOn || isDone ? FontWeight.w700 : FontWeight.w500,
            color: isOn
                ? const Color(0xFF2563EB)
                : (isDone ? const Color(0xFF16A34A) : const Color(0xFF64748B)),
          ),
        ),
      ],
    );
  }

  Widget _methodTab(String key, String title, IconData icon) {
    final isSelected = _selectedTab == key;
    return Expanded(
      child: GestureDetector(
        onTap: () {
          setState(() {
            _selectedTab = key;
            if (key == 'card') _selectedMethod = 'BankTransfer';
            if (key == 'transfer') _selectedMethod = 'BankTransfer';
            if (key == 'wallet') _selectedMethod = 'MoMo';
          });
        },
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 4),
          decoration: BoxDecoration(
            color: isSelected ? const Color(0xFFEFF6FF) : Colors.white,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(
              color: isSelected
                  ? const Color(0xFF2563EB)
                  : const Color(0xFFE2E8F0),
              width: isSelected ? 1.5 : 1.0,
            ),
          ),
          child: Column(
            children: [
              Icon(
                icon,
                size: 22,
                color: isSelected
                    ? const Color(0xFF2563EB)
                    : const Color(0xFF64748B),
              ),
              const SizedBox(height: 4),
              Text(
                title,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 11.5,
                  fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
                  color: isSelected
                      ? const Color(0xFF2563EB)
                      : const Color(0xFF334155),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _walletOption(String code, String name, IconData icon) {
    final isSelected = _selectedMethod == code;
    return GestureDetector(
      onTap: () => setState(() => _selectedMethod = code),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: isSelected ? const Color(0xFFEFF6FF) : Colors.white,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: isSelected
                ? const Color(0xFF2563EB)
                : const Color(0xFFE2E8F0),
            width: isSelected ? 1.5 : 1,
          ),
        ),
        child: Row(
          children: [
            Icon(icon,
                size: 20,
                color: isSelected
                    ? const Color(0xFF2563EB)
                    : const Color(0xFF64748B)),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                name,
                style: TextStyle(
                  fontSize: 13.5,
                  fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
                  color: const Color(0xFF0F172A),
                ),
              ),
            ),
            if (isSelected)
              const Icon(Icons.check_circle,
                  size: 18, color: Color(0xFF2563EB)),
          ],
        ),
      ),
    );
  }

  Widget _sumRow(String label, String value,
      {bool isBold = false, bool isHighlight = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 13, color: Color(0xFF64748B)),
          ),
          Text(
            value,
            style: TextStyle(
              fontSize: 13.5,
              fontWeight: isBold || isHighlight
                  ? FontWeight.w700
                  : FontWeight.w500,
              color: isHighlight
                  ? const Color(0xFF2563EB)
                  : const Color(0xFF0F172A),
            ),
          ),
        ],
      ),
    );
  }

  Widget _badgeItem(IconData icon, String text) {
    return Row(
      children: [
        Icon(icon, size: 14, color: const Color(0xFF64748B)),
        const SizedBox(width: 6),
        Text(
          text,
          style: const TextStyle(
            fontSize: 11.5,
            color: Color(0xFF64748B),
          ),
        ),
      ],
    );
  }
}

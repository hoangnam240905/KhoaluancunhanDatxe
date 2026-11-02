import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../utils/formatters.dart';
import 'deposit_payment_screen.dart';

class ContractScreen extends StatefulWidget {
  final Booking booking;
  final ApiService api;

  const ContractScreen({
    super.key,
    required this.booking,
    required this.api,
  });

  @override
  State<ContractScreen> createState() => _ContractScreenState();
}

class _ContractScreenState extends State<ContractScreen> {
  late Booking _booking;
  RentalContract? _contract;
  bool _loading = true;
  bool _actionBusy = false;
  bool _agreed = false;
  String? _error;

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
      RentalContract? c;
      try {
        c = await widget.api.getContract(_booking.bookingId);
      } catch (_) {
        c = null;
      }
      if (!mounted) return;
      setState(() {
        _booking = b;
        _contract = c;
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

  Future<void> _issueContract() async {
    setState(() => _actionBusy = true);
    try {
      final c = await widget.api.createContract(_booking.bookingId);
      if (!mounted) return;
      setState(() {
        _contract = c;
        _actionBusy = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã lập hợp đồng thuê xe thành công.')),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _actionBusy = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
  }

  Future<void> _signContract() async {
    final c = _contract;
    if (c == null) return;
    if (!_agreed) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
              'Vui lòng đọc và tích chọn đồng ý với nội dung hợp đồng trước khi ký.'),
        ),
      );
      return;
    }

    setState(() => _actionBusy = true);
    try {
      final signed = await widget.api.simulateSignContract(c.contractId);
      if (!mounted) return;
      setState(() {
        _contract = signed;
        _actionBusy = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Ký hợp đồng điện tử thành công!'),
          backgroundColor: Color(0xFF16A34A),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _actionBusy = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
  }

  void _goToDepositPayment() {
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => DepositPaymentScreen(
          booking: _booking,
          api: widget.api,
        ),
      ),
    ).then((_) => _loadData());
  }

  @override
  Widget build(BuildContext context) {
    final b = _booking;
    final c = _contract;
    final isSigned = c?.status == 'Signed';
    final isIssued = c?.status == 'Issued';
    final hasContract = c != null;
    final canSign = isIssued && b.status != 'Cancelled';

    final vehicleName = b.assignedVehicle != null
        ? '${b.assignedVehicle!.brand} ${b.assignedVehicle!.model}'
        : b.vehicleTypeName;
    final plate = b.assignedVehicle?.licensePlate ?? b.assignment?.licensePlate;
    final vehicleMeta = plate != null ? '$vehicleName - $plate' : vehicleName;

    final depositAmount = c?.depositAmount ??
        b.quotedDepositAmount ??
        (b.totalAmount * 0.3);

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
          'Hợp đồng thuê xe',
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
                    'HỢP ĐỒNG ĐIỆN TỬ',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF2563EB),
                      letterSpacing: 0.5,
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Hợp đồng thuê xe',
                    style: TextStyle(
                      fontSize: 24,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Kiểm tra thông tin và điều khoản trước khi xác nhận hợp đồng.',
                    style: TextStyle(
                      fontSize: 13,
                      color: Color(0xFF64748B),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // ── Info Bar (Mã hợp đồng, Đơn thuê, Quay lại) ─────
                  Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      crossAxisAlignment: CrossAxisAlignment.center,
                      children: [
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'MÃ HỢP ĐỒNG',
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: Color(0xFF64748B),
                              ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              c?.contractNumber ?? 'Chưa tạo',
                              style: const TextStyle(
                                fontSize: 15,
                                fontWeight: FontWeight.w700,
                                color: Color(0xFF0F172A),
                              ),
                            ),
                            Text(
                              'Đơn thuê #${b.bookingId}',
                              style: const TextStyle(
                                fontSize: 12,
                                color: Color(0xFF64748B),
                              ),
                            ),
                          ],
                        ),
                        TextButton.icon(
                          onPressed: () => Navigator.pop(context, true),
                          icon: const Icon(Icons.arrow_back, size: 14),
                          label: const Text(
                            'Quay lại chi tiết đơn',
                            style: TextStyle(fontSize: 12),
                          ),
                          style: TextButton.styleFrom(
                            foregroundColor: const Color(0xFF2563EB),
                          ),
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

                  // ── Văn bản Hợp đồng (Paper Preview) ───────────────
                  Container(
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                      boxShadow: [
                        BoxShadow(
                          color: Colors.black.withValues(alpha: 0.03),
                          blurRadius: 10,
                          offset: const Offset(0, 4),
                        ),
                      ],
                    ),
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // Header của văn bản
                        const Text(
                          'HỢP ĐỒNG THUÊ XE',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF2563EB),
                            letterSpacing: 0.5,
                          ),
                        ),
                        const SizedBox(height: 4),
                        const Text(
                          'DriveX — Hợp đồng thuê xe du lịch',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                            color: Color(0xFF0F172A),
                          ),
                        ),
                        const SizedBox(height: 2),
                        const Text(
                          'Bản xem trước giao diện — không phải tệp PDF hay chữ ký số.',
                          style: TextStyle(
                            fontSize: 11.5,
                            color: Color(0xFF64748B),
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                        const SizedBox(height: 12),
                        Container(
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: const Color(0xFFF1F5F9),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Text(
                                'Mã: ${c?.contractNumber ?? "Chưa lập"}',
                                style: const TextStyle(
                                    fontSize: 12, fontWeight: FontWeight.w600),
                              ),
                              Row(
                                children: [
                                  const Text('Trạng thái: ',
                                      style: TextStyle(
                                          fontSize: 12,
                                          color: Color(0xFF64748B))),
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                        horizontal: 8, vertical: 2),
                                    decoration: BoxDecoration(
                                      color: (isSigned
                                              ? const Color(0xFF16A34A)
                                              : const Color(0xFF2563EB))
                                          .withValues(alpha: 0.12),
                                      borderRadius: BorderRadius.circular(6),
                                    ),
                                    child: Text(
                                      isSigned
                                          ? 'Đã ký'
                                          : (isIssued ? 'Chờ ký' : 'Chưa có'),
                                      style: TextStyle(
                                        fontSize: 11,
                                        fontWeight: FontWeight.w700,
                                        color: isSigned
                                            ? const Color(0xFF16A34A)
                                            : const Color(0xFF2563EB),
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                        const Divider(height: 32, color: Color(0xFFE2E8F0)),

                        // 1. THÔNG TIN KHÁCH HÀNG
                        _sectionTitle('1. THÔNG TIN KHÁCH HÀNG'),
                        _specRow('Họ tên', b.customerName),
                        const SizedBox(height: 16),

                        // 2. THÔNG TIN THUÊ XE
                        _sectionTitle('2. THÔNG TIN THUÊ XE'),
                        _specRow('Mã đơn', '#${b.bookingId}'),
                        _specRow(
                            'Hình thức thuê',
                            Formatters.rentalModeLabel(
                                c?.rentalMode ?? b.rentalMode)),
                        _specRow('Thời lượng', '$days ngày'),
                        const SizedBox(height: 16),

                        // 3. THÔNG TIN PHƯƠNG TIỆN
                        _sectionTitle('3. THÔNG TIN PHƯƠNG TIỆN'),
                        _specRow('Phương tiện', vehicleMeta),
                        const SizedBox(height: 16),

                        // 4. THỜI GIAN VÀ ĐỊA ĐIỂM
                        _sectionTitle('4. THỜI GIAN VÀ ĐỊA ĐIỂM'),
                        _specRow('Ngày nhận', Formatters.dt(b.startDate)),
                        _specRow('Ngày trả', Formatters.dt(b.endDate)),
                        _specRow('Địa điểm nhận', b.pickupAddress),
                        _specRow('Địa điểm trả', b.dropoffAddress),
                        const SizedBox(height: 16),

                        // 5. CHI PHÍ THUÊ
                        _sectionTitle('5. CHI PHÍ THUÊ'),
                        _specRow('Tổng tiền thuê',
                            Formatters.vnd(c?.totalAmount ?? b.totalAmount),
                            isHighlight: true),
                        const SizedBox(height: 16),

                        // 6. TIỀN CỌC
                        _sectionTitle('6. TIỀN CỌC'),
                        _specRow('Tiền cọc', Formatters.vnd(depositAmount),
                            isHighlight: true),
                        const SizedBox(height: 16),

                        // 7. QUY ĐỊNH THUÊ XE
                        _sectionTitle('7. QUY ĐỊNH THUÊ XE'),
                        _termBullet('Khách hàng sử dụng xe đúng mục đích thuê.'),
                        _termBullet(
                            'Thời gian nhận và trả xe được thực hiện theo thông tin đơn thuê.'),
                        _termBullet(
                            'Các khoản phát sinh được xử lý theo chính sách của hệ thống.'),
                        _termBullet(
                            'Khách hàng có trách nhiệm kiểm tra tình trạng xe khi nhận và trả xe.'),
                        const SizedBox(height: 4),
                        const Text(
                          'Nội dung điều khoản là minh họa giao diện.',
                          style: TextStyle(
                            fontSize: 11,
                            color: Color(0xFF94A3B8),
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                        const SizedBox(height: 20),

                        // 8. XÁC NHẬN HỢP ĐỒNG
                        _sectionTitle('8. XÁC NHẬN HỢP ĐỒNG'),
                        if (isSigned) ...[
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: const Color(0xFFF0FDF4),
                              borderRadius: BorderRadius.circular(10),
                              border:
                                  Border.all(color: const Color(0xFFBBF7D0)),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Row(
                                  children: [
                                    Icon(Icons.check_circle_rounded,
                                        size: 16, color: Color(0xFF16A34A)),
                                    SizedBox(width: 6),
                                    Text(
                                      'Đã xác nhận điện tử',
                                      style: TextStyle(
                                        fontSize: 12,
                                        fontWeight: FontWeight.w700,
                                        color: Color(0xFF16A34A),
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  'Bên thuê: ${c?.customerName ?? b.customerName}',
                                  style: const TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.w600,
                                    color: Color(0xFF0F172A),
                                  ),
                                ),
                                if (c?.signedAt != null)
                                  Text(
                                    'Thời điểm ký: ${Formatters.dt(c!.signedAt!)}',
                                    style: const TextStyle(
                                      fontSize: 12,
                                      color: Color(0xFF64748B),
                                    ),
                                  ),
                                const SizedBox(height: 4),
                                const Text(
                                  'Đây là chữ ký điện tử mô phỏng của hệ thống — chưa tạo chữ ký số.',
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: Color(0xFF94A3B8),
                                    fontStyle: FontStyle.italic,
                                  ),
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
                              border:
                                  Border.all(color: const Color(0xFFE2E8F0)),
                            ),
                            child: const Text(
                              'Chưa ký — khách hàng cần xác nhận nội dung hợp đồng.',
                              style: TextStyle(
                                fontSize: 13,
                                color: Color(0xFF64748B),
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),

                  // ── Action Card (Ký hợp đồng & Thanh toán cọc) ──────
                  Container(
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                      boxShadow: [
                        BoxShadow(
                          color: Colors.black.withValues(alpha: 0.03),
                          blurRadius: 10,
                          offset: const Offset(0, 4),
                        ),
                      ],
                    ),
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text(
                          'Hợp đồng',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF0F172A),
                          ),
                        ),
                        const SizedBox(height: 12),
                        _summaryRow(
                            'Trạng thái',
                            isSigned
                                ? 'Đã ký'
                                : (isIssued ? 'Chờ ký' : 'Chưa lập')),
                        _summaryRow('Đơn thuê', '#${b.bookingId}'),
                        _summaryRow('Giá trị hợp đồng',
                            Formatters.vnd(c?.totalAmount ?? b.totalAmount),
                            isBold: true),
                        _summaryRow('Tiền cọc', Formatters.vnd(depositAmount),
                            isBold: true),
                        const Divider(height: 24, color: Color(0xFFE2E8F0)),

                        // Checkbox đồng ý
                        if (!isSigned) ...[
                          GestureDetector(
                            onTap: () => setState(() => _agreed = !_agreed),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                SizedBox(
                                  width: 22,
                                  height: 22,
                                  child: Checkbox(
                                    value: _agreed,
                                    activeColor: const Color(0xFF2563EB),
                                    shape: RoundedRectangleBorder(
                                      borderRadius: BorderRadius.circular(4),
                                    ),
                                    onChanged: (v) =>
                                        setState(() => _agreed = v ?? false),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                const Expanded(
                                  child: Text(
                                    'Tôi đã đọc và đồng ý với nội dung hợp đồng thuê xe.',
                                    style: TextStyle(
                                      fontSize: 13,
                                      color: Color(0xFF374151),
                                      height: 1.4,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 16),
                        ],

                        // Nút Ký hợp đồng hoặc Lập hợp đồng
                        if (!hasContract) ...[
                          FilledButton.icon(
                            onPressed: _actionBusy ? null : _issueContract,
                            icon: const Icon(Icons.note_add_outlined, size: 18),
                            label: const Text('Lập hợp đồng thuê xe'),
                            style: FilledButton.styleFrom(
                              backgroundColor: const Color(0xFF2563EB),
                              padding: const EdgeInsets.symmetric(vertical: 14),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10),
                              ),
                            ),
                          ),
                          const SizedBox(height: 10),
                        ] else if (canSign) ...[
                          FilledButton.icon(
                            onPressed: _actionBusy ? null : _signContract,
                            icon: const Icon(Icons.draw_outlined, size: 18),
                            label: _actionBusy
                                ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                      color: Colors.white,
                                    ),
                                  )
                                : const Text('Ký hợp đồng'),
                            style: FilledButton.styleFrom(
                              backgroundColor: const Color(0xFF2563EB),
                              padding: const EdgeInsets.symmetric(vertical: 14),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10),
                              ),
                            ),
                          ),
                          const SizedBox(height: 10),
                        ],

                        // Nút Thanh toán tiền cọc
                        FilledButton.icon(
                          onPressed: _goToDepositPayment,
                          icon: const Icon(Icons.payment, size: 18),
                          label: const Text('Thanh toán tiền cọc'),
                          style: FilledButton.styleFrom(
                            backgroundColor: const Color(0xFF16A34A),
                            padding: const EdgeInsets.symmetric(vertical: 14),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                          ),
                        ),
                        const SizedBox(height: 10),

                        // Nút Quay lại
                        OutlinedButton(
                          onPressed: () => Navigator.pop(context, true),
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: 14),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                            side: const BorderSide(color: Color(0xFFCBD5E1)),
                          ),
                          child: const Text(
                            'Quay lại đơn',
                            style: TextStyle(
                              color: Color(0xFF475569),
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 30),
                ],
              ),
            ),
    );
  }

  Widget _sectionTitle(String text) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text(
          text,
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w700,
            color: Color(0xFF0F172A),
          ),
        ),
      );

  Widget _specRow(String label, String value, {bool isHighlight = false}) =>
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: const TextStyle(
                fontSize: 13,
                color: Color(0xFF64748B),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                value,
                textAlign: TextAlign.right,
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: isHighlight ? FontWeight.w700 : FontWeight.w500,
                  color: isHighlight
                      ? const Color(0xFF2563EB)
                      : const Color(0xFF0F172A),
                ),
              ),
            ),
          ],
        ),
      );

  Widget _termBullet(String text) => Padding(
        padding: const EdgeInsets.only(bottom: 4),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('• ',
                style: TextStyle(
                    fontSize: 14,
                    color: Color(0xFF2563EB),
                    fontWeight: FontWeight.bold)),
            Expanded(
              child: Text(
                text,
                style: const TextStyle(
                  fontSize: 12.5,
                  color: Color(0xFF334155),
                  height: 1.4,
                ),
              ),
            ),
          ],
        ),
      );

  Widget _summaryRow(String label, String value, {bool isBold = false}) =>
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              label,
              style: const TextStyle(
                fontSize: 13,
                color: Color(0xFF64748B),
              ),
            ),
            Text(
              value,
              style: TextStyle(
                fontSize: 14,
                fontWeight: isBold ? FontWeight.w700 : FontWeight.w500,
                color: isBold
                    ? const Color(0xFF0F172A)
                    : const Color(0xFF334155),
              ),
            ),
          ],
        ),
      );
}

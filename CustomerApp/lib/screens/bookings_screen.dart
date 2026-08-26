import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../models/models.dart';
import '../services/api_service.dart';

class BookingsScreen extends StatefulWidget {
  final ApiService api;
  const BookingsScreen({super.key, required this.api});

  @override
  State<BookingsScreen> createState() => _BookingsScreenState();
}

class _BookingsScreenState extends State<BookingsScreen> {
  List<Booking> _bookings = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _bookings = await widget.api.getBookings();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    }
    if (mounted) setState(() => _loading = false);
  }

  void _openDetail(Booking b) {
    final fmt = DateFormat('dd/MM/yyyy HH:mm');
    final money = NumberFormat('#,###', 'vi_VN');
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) {
        final a = b.assignment;
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Center(
                  child: Container(
                    width: 40,
                    height: 4,
                    decoration: BoxDecoration(
                      color: const Color(0xFFCBD5E1),
                      borderRadius: BorderRadius.circular(4),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                Text('#${b.bookingId} · ${b.vehicleTypeName}',
                    style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
                const SizedBox(height: 6),
                Chip(label: Text(b.status)),
                const SizedBox(height: 12),
                _row('Tuyến', '${b.pickupAddress} → ${b.dropoffAddress}'),
                _row('Bắt đầu', fmt.format(b.startDate.toLocal())),
                _row('Kết thúc', fmt.format(b.endDate.toLocal())),
                _row('Cước phí', '${money.format(b.totalAmount)} VND'),
                if (b.notes != null && b.notes!.isNotEmpty) _row('Ghi chú', b.notes!),
                const Divider(height: 28),
                const Text('Tài xế phân công', style: TextStyle(fontWeight: FontWeight.w800)),
                const SizedBox(height: 8),
                if (a == null)
                  const Text('Chưa phân công — đợi điều phối xác nhận.',
                      style: TextStyle(color: Color(0xFF64748B)))
                else ...[
                  _row('Họ tên', a.driverName),
                  _row('Số điện thoại', (a.driverPhone == null || a.driverPhone!.isEmpty) ? '—' : a.driverPhone!),
                  _row('Biển số xe', a.licensePlate),
                  _row('Trạng thái', a.status),
                ],
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(label, style: const TextStyle(color: Color(0xFF64748B), fontWeight: FontWeight.w600)),
          ),
          Expanded(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w600))),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final fmt = DateFormat('dd/MM/yyyy HH:mm');
    final money = NumberFormat('#,###', 'vi_VN');
    return Scaffold(
      backgroundColor: const Color(0xFFF0F4FF),
      appBar: AppBar(
        title: const Text('Đơn của tôi'),
        backgroundColor: const Color(0xFF1E3A8A),
        foregroundColor: Colors.white,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: _error != null
                  ? ListView(children: [
                      const SizedBox(height: 80),
                      Center(child: Text(_error!)),
                      Center(child: TextButton(onPressed: _load, child: const Text('Thử lại'))),
                    ])
                  : _bookings.isEmpty
                      ? ListView(children: const [
                          SizedBox(height: 120),
                          Center(child: Text('Chưa có đơn đặt xe')),
                        ])
                      : ListView.builder(
                          padding: const EdgeInsets.all(16),
                          itemCount: _bookings.length,
                          itemBuilder: (context, i) {
                            final b = _bookings[i];
                            final driverLine = b.assignment == null
                                ? 'Tài xế: chưa phân công'
                                : 'Tài xế: ${b.assignment!.driverName} · ${b.assignment!.licensePlate}';
                            return Card(
                              margin: const EdgeInsets.only(bottom: 10),
                              elevation: 0,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(14),
                                side: const BorderSide(color: Color(0xFFE2E8F0)),
                              ),
                              child: ListTile(
                                onTap: () => _openDetail(b),
                                title: Text('#${b.bookingId} - ${b.vehicleTypeName}',
                                    style: const TextStyle(fontWeight: FontWeight.bold)),
                                subtitle: Text(
                                  '${b.pickupAddress} → ${b.dropoffAddress}\n${fmt.format(b.startDate.toLocal())}\n$driverLine',
                                ),
                                isThreeLine: true,
                                trailing: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  crossAxisAlignment: CrossAxisAlignment.end,
                                  children: [
                                    Text('${money.format(b.totalAmount)} VND',
                                        style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700)),
                                    const SizedBox(height: 4),
                                    Chip(
                                      label: Text(b.status, style: const TextStyle(fontSize: 10)),
                                      padding: EdgeInsets.zero,
                                      visualDensity: VisualDensity.compact,
                                    ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
            ),
    );
  }
}

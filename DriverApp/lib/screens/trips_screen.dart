import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import 'login_screen.dart';

class TripsScreen extends StatefulWidget {
  final ApiService api;
  final String userName;

  const TripsScreen({super.key, required this.api, required this.userName});

  @override
  State<TripsScreen> createState() => _TripsScreenState();
}

class _TripsScreenState extends State<TripsScreen> {
  List<DriverBooking> _trips = [];
  bool _loading = true;
  String _driverStatus = 'Available';

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      _trips = await widget.api.getMyTrips();
    } catch (_) {}
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _setStatus(String status) async {
    try {
      await widget.api.updateStatus(status);
      setState(() => _driverStatus = status);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Trạng thái: $status')));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    }
  }

  Future<void> _action(Future<void> Function() fn) async {
    try {
      await fn();
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    }
  }

  Future<void> _logout() async {
    await widget.api.authService.logout();
    if (!mounted) return;
    Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const LoginScreen()));
  }

  @override
  Widget build(BuildContext context) {
    final fmt = DateFormat('dd/MM/yyyy HH:mm');
    return Scaffold(
      appBar: AppBar(
        title: Text('Xin chào, ${widget.userName}'),
        actions: [IconButton(icon: const Icon(Icons.logout), onPressed: _logout)],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                const Text('Trạng thái: '),
                ChoiceChip(label: const Text('Sẵn sàng'), selected: _driverStatus == 'Available', onSelected: (_) => _setStatus('Available')),
                const SizedBox(width: 8),
                ChoiceChip(label: const Text('Bận'), selected: _driverStatus == 'Busy', onSelected: (_) => _setStatus('Busy')),
                const SizedBox(width: 8),
                ChoiceChip(label: const Text('Offline'), selected: _driverStatus == 'Offline', onSelected: (_) => _setStatus('Offline')),
              ],
            ),
          ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : RefreshIndicator(
                    onRefresh: _load,
                    child: _trips.isEmpty
                        ? ListView(children: const [SizedBox(height: 120), Center(child: Text('Chưa có chuyến nào'))])
                        : ListView.builder(
                            itemCount: _trips.length,
                            itemBuilder: (context, i) {
                              final t = _trips[i];
                              final aid = t.assignmentId;
                              return Card(
                                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                                child: Padding(
                                  padding: const EdgeInsets.all(12),
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('#${t.bookingId} - ${t.vehicleTypeName}', style: const TextStyle(fontWeight: FontWeight.bold)),
                                      Text('Khách: ${t.customerName}'),
                                      Text('${t.pickupAddress} → ${t.dropoffAddress}'),
                                      Text(fmt.format(t.startDate)),
                                      Text('Trạng thái: ${t.status}'),
                                      if (t.assignment != null) Text('Xe: ${t.assignment!.licensePlate}'),
                                      if (aid != null) ...[
                                        const SizedBox(height: 8),
                                        Wrap(spacing: 8, children: [
                                          if (t.status == 'Assigned')
                                            FilledButton(onPressed: () => _action(() => widget.api.acceptTrip(aid)), child: const Text('Nhận chuyến')),
                                          if (t.status == 'Assigned' || t.status == 'InProgress')
                                            FilledButton(onPressed: () => _action(() => widget.api.startTrip(aid)), child: const Text('Bắt đầu')),
                                          if (t.status == 'InProgress')
                                            FilledButton(onPressed: () => _action(() => widget.api.completeTrip(aid)), child: const Text('Hoàn thành')),
                                        ]),
                                      ],
                                    ],
                                  ),
                                ),
                              );
                            },
                          ),
                  ),
          ),
        ],
      ),
    );
  }
}

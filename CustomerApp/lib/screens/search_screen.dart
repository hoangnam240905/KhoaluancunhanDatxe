import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../utils/formatters.dart';
import 'create_booking_screen.dart';
import 'vehicle_detail_screen.dart';

class SearchScreen extends StatefulWidget {
  final ApiService api;

  const SearchScreen({super.key, required this.api});

  @override
  State<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends State<SearchScreen> {
  final _query = TextEditingController();
  List<VehicleType> _types = [];
  List<VehicleType> _filtered = [];
  bool _loading = true;
  String? _error;
  String _mode = 'WithDriver';
  int? _seats;
  double? _maxPrice;

  @override
  void initState() {
    super.initState();
    _load();
    _query.addListener(_filter);
  }

  @override
  void dispose() {
    _query.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      _types = await widget.api.getVehicleTypes();
      _filter();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    }
    if (mounted) setState(() => _loading = false);
  }

  void _filter() {
    final q = _query.text.trim().toLowerCase();
    setState(() {
      _filtered = _types.where((type) {
        final matchesText =
            q.isEmpty ||
            type.typeName.toLowerCase().contains(q) ||
            (type.description ?? '').toLowerCase().contains(q);
        final matchesSeats = _seats == null || type.seatCapacity >= _seats!;
        final matchesPrice =
            _maxPrice == null || type.pricePerDay <= _maxPrice!;
        return matchesText && matchesSeats && matchesPrice;
      }).toList();
    });
  }

  Future<void> _openBooking(VehicleType type) async {
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => CreateBookingScreen(
          api: widget.api,
          vehicleTypes: _types,
          vehicleType: type,
          initialRentalMode: _mode,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Tìm xe')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
          ? Center(child: Text(_error!))
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  TextField(
                    controller: _query,
                    decoration: const InputDecoration(
                      prefixIcon: Icon(Icons.search),
                      hintText: 'Tìm theo tên loại xe...',
                    ),
                  ),
                  const SizedBox(height: 12),
                  SegmentedButton<String>(
                    segments: const [
                      ButtonSegment(
                        value: 'WithDriver',
                        label: Text('Có tài xế'),
                      ),
                      ButtonSegment(value: 'SelfDrive', label: Text('Tự lái')),
                    ],
                    selected: {_mode},
                    onSelectionChanged: (value) =>
                        setState(() => _mode = value.first),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<int?>(
                    initialValue: _seats,
                    decoration: const InputDecoration(
                      labelText: 'Số chỗ tối thiểu',
                    ),
                    items: const [
                      DropdownMenuItem(value: null, child: Text('Tất cả')),
                      DropdownMenuItem(value: 4, child: Text('Từ 4 chỗ')),
                      DropdownMenuItem(value: 7, child: Text('Từ 7 chỗ')),
                      DropdownMenuItem(value: 16, child: Text('Từ 16 chỗ')),
                    ],
                    onChanged: (value) {
                      _seats = value;
                      _filter();
                    },
                  ),
                  const SizedBox(height: 16),
                  Text(
                    'Có ${_filtered.length} loại xe',
                    style: const TextStyle(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: 8),
                  ..._filtered.map(
                    (type) => Card(
                      margin: const EdgeInsets.only(bottom: 10),
                      child: ListTile(
                        leading: const CircleAvatar(
                          child: Icon(Icons.directions_car),
                        ),
                        title: Text(
                          type.typeName,
                          style: const TextStyle(fontWeight: FontWeight.w800),
                        ),
                        subtitle: Text(
                          '${type.seatCapacity} chỗ · ${Formatters.vnd(type.pricePerDay)}/ngày',
                        ),
                        onTap: () => Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => VehicleDetailScreen(
                              api: widget.api,
                              vehicleType: type,
                              allTypes: _types,
                              isLoggedIn: true,
                              initialRentalMode: _mode,
                            ),
                          ),
                        ),
                        trailing: FilledButton(
                          onPressed: () => _openBooking(type),
                          child: const Text('Đặt'),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
    );
  }
}

import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';

class CreateBookingScreen extends StatefulWidget {
  final ApiService api;
  final VehicleType vehicleType;
  final String? pickup;
  final String? dropoff;
  final double? distanceKm;

  const CreateBookingScreen({
    super.key,
    required this.api,
    required this.vehicleType,
    this.pickup,
    this.dropoff,
    this.distanceKm,
  });

  @override
  State<CreateBookingScreen> createState() => _CreateBookingScreenState();
}

class _CreateBookingScreenState extends State<CreateBookingScreen> {
  late final TextEditingController _pickup;
  late final TextEditingController _dropoff;
  late final TextEditingController _distance;
  final _notes = TextEditingController();
  final DateTime _start = DateTime.now().add(const Duration(days: 1));
  final DateTime _end = DateTime.now().add(const Duration(days: 1, hours: 8));
  bool _loading = false;

  @override
  void initState() {
    super.initState();
    _pickup = TextEditingController(text: widget.pickup ?? 'TP.HCM');
    _dropoff = TextEditingController(text: widget.dropoff ?? 'Vung Tau');
    _distance = TextEditingController(text: (widget.distanceKm ?? 120).toStringAsFixed(0));
  }

  @override
  void dispose() {
    _pickup.dispose();
    _dropoff.dispose();
    _distance.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _loading = true);
    try {
      await widget.api.createBooking(
        vehicleTypeId: widget.vehicleType.typeId,
        pickupAddress: _pickup.text.trim(),
        dropoffAddress: _dropoff.text.trim(),
        startDate: _start,
        endDate: _end,
        estimatedDistance: double.tryParse(_distance.text),
        notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Dat xe thanh cong!')));
      Navigator.pop(context);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  InputDecoration _dec(String label) => InputDecoration(
        labelText: label,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
        filled: true,
        fillColor: Colors.white,
      );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF0F4FF),
      appBar: AppBar(
        title: Text('Dat ${widget.vehicleType.typeName}'),
        backgroundColor: const Color(0xFF1E3A8A),
        foregroundColor: Colors.white,
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            elevation: 0,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16), side: const BorderSide(color: Color(0xFFE2E8F0))),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  TextField(controller: _pickup, decoration: _dec('Diem don')),
                  const SizedBox(height: 12),
                  TextField(controller: _dropoff, decoration: _dec('Diem tra')),
                  const SizedBox(height: 12),
                  TextField(controller: _distance, keyboardType: TextInputType.number, decoration: _dec('Km uoc tinh')),
                  const SizedBox(height: 12),
                  TextField(controller: _notes, maxLines: 2, decoration: _dec('Ghi chu')),
                  const SizedBox(height: 20),
                  FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: const Color(0xFF2563EB),
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                    onPressed: _loading ? null : _submit,
                    child: _loading
                        ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : const Text('Gui yeu cau dat xe'),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

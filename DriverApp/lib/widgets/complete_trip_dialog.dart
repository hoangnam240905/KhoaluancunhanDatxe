import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../utils/complete_trip_fields.dart';

typedef CompleteTripSubmit =
    Future<String> Function({
      required double odometerKm,
      required double fuelLevel,
      String? exteriorCondition,
      String? technicalCondition,
      String? notes,
    });

/// Owns TextEditingControllers for the complete-trip form so they are
/// disposed in [State.dispose] after the route unmounts — not by the caller
/// while the dialog is still in the tree.
class CompleteTripDialog extends StatefulWidget {
  const CompleteTripDialog({
    super.key,
    required this.onSubmit,
    this.title = 'Hoàn thành chuyến',
    this.description =
        'Nhập số km, mức nhiên liệu và tình trạng xe khi kết thúc chuyến.',
    this.submitLabel = 'Hoàn thành',
    this.odometerLabel = 'Số km (bắt buộc)',
    this.fuelLabel = 'Nhiên liệu % (bắt buộc)',
  });

  final String title;
  final String description;
  final String submitLabel;
  final String odometerLabel;
  final String fuelLabel;

  final CompleteTripSubmit onSubmit;

  @override
  State<CompleteTripDialog> createState() => _CompleteTripDialogState();
}

class _CompleteTripDialogState extends State<CompleteTripDialog> {
  final _odo = TextEditingController();
  final _fuel = TextEditingController();
  final _exterior = TextEditingController();
  final _technical = TextEditingController();
  final _notes = TextEditingController();
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _odo.dispose();
    _fuel.dispose();
    _exterior.dispose();
    _technical.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_submitting) return;
    final parsed = CompleteTripFields.parse(_odo.text, _fuel.text);
    if (parsed.error != null) {
      setState(() => _error = parsed.error);
      return;
    }
    final fields = parsed.fields!;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final message = await widget.onSubmit(
        odometerKm: fields.odometerKm,
        fuelLevel: fields.fuelLevel,
        exteriorCondition: _exterior.text,
        technicalCondition: _technical.text,
        notes: _notes.text,
      );
      if (!mounted) return;
      Navigator.of(context).pop(message);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !_submitting,
      child: AlertDialog(
        title: Text(widget.title),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(widget.description, style: TextStyle(fontSize: 13)),
              const SizedBox(height: 8),
              Text(
                'Thời điểm do máy chủ ghi UTC. Ứng dụng không tính giá chốt.',
                style: TextStyle(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                  fontSize: 12,
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                key: const Key('complete-odometer'),
                controller: _odo,
                enabled: !_submitting,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: InputDecoration(labelText: widget.odometerLabel),
              ),
              TextField(
                key: const Key('complete-fuel'),
                controller: _fuel,
                enabled: !_submitting,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: InputDecoration(labelText: widget.fuelLabel),
              ),
              TextField(
                key: const Key('complete-exterior'),
                controller: _exterior,
                enabled: !_submitting,
                maxLength: 100,
                decoration: const InputDecoration(
                  labelText: 'Tình trạng ngoại thất',
                ),
              ),
              TextField(
                key: const Key('complete-technical'),
                controller: _technical,
                enabled: !_submitting,
                maxLength: 100,
                decoration: const InputDecoration(
                  labelText: 'Tình trạng kỹ thuật',
                ),
              ),
              TextField(
                key: const Key('complete-notes'),
                controller: _notes,
                enabled: !_submitting,
                maxLength: 500,
                maxLines: 2,
                decoration: const InputDecoration(labelText: 'Ghi chú (notes)'),
              ),
              if (_error != null) ...[
                const SizedBox(height: 8),
                Text(_error!, style: const TextStyle(color: AppColors.danger)),
              ],
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: _submitting ? null : () => Navigator.of(context).pop(),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: _submitting ? null : _submit,
            child: _submitting
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : Text(widget.submitLabel),
          ),
        ],
      ),
    );
  }
}

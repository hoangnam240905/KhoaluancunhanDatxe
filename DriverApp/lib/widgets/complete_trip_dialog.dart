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
        'Ghi nhận dữ liệu vận hành khi trả xe. Số km và nhiên liệu là bắt buộc.',
    this.submitLabel = 'Hoàn thành',
    this.initialOdometerKm,
  });

  final CompleteTripSubmit onSubmit;
  final String title;
  final String description;
  final String submitLabel;
  /// When provided, the vehicle's assigned current odometer is displayed as
  /// read-only. This is used for the start inspection only.
  final double? initialOdometerKm;

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
  void initState() {
    super.initState();
    final km = widget.initialOdometerKm;
    if (km != null) _odo.text = km.toStringAsFixed(0);
  }

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
    final palette = AppPalette.of(context);
    return PopScope(
      canPop: !_submitting,
      child: AlertDialog(
        title: Text(widget.title),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                widget.description,
                style: const TextStyle(fontSize: 13),
              ),
              const SizedBox(height: 8),
              Text(
                'Thời điểm do máy chủ ghi UTC. Ứng dụng không tính giá chốt.',
                style: TextStyle(color: palette.muted, fontSize: 12),
              ),
              const SizedBox(height: 12),
              TextField(
                key: const Key('complete-odometer'),
                controller: _odo,
                readOnly: widget.initialOdometerKm != null,
                enabled: !_submitting,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: InputDecoration(
                  labelText: widget.initialOdometerKm != null
                      ? 'Số km hiện tại của xe'
                      : 'Số km (bắt buộc)',
                  helperText: widget.initialOdometerKm != null
                      ? 'Tự động lấy từ hồ sơ xe được phân công'
                      : null,
                ),
              ),
              TextField(
                key: const Key('complete-fuel'),
                controller: _fuel,
                enabled: !_submitting,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: const InputDecoration(
                  labelText: 'Nhiên liệu % (bắt buộc)',
                ),
              ),
              TextField(
                key: const Key('complete-exterior'),
                controller: _exterior,
                enabled: !_submitting,
                maxLength: 100,
                decoration: InputDecoration(
                  labelText: widget.initialOdometerKm != null
                      ? 'Khác biệt ngoại thất (nếu có)'
                      : 'Tình trạng ngoại thất',
                ),
              ),
              TextField(
                key: const Key('complete-technical'),
                controller: _technical,
                enabled: !_submitting,
                maxLength: 100,
                decoration: InputDecoration(
                  labelText: widget.initialOdometerKm != null
                      ? 'Khác biệt kỹ thuật (nếu có)'
                      : 'Tình trạng kỹ thuật',
                ),
              ),
              TextField(
                key: const Key('complete-notes'),
                controller: _notes,
                enabled: !_submitting,
                maxLength: 500,
                maxLines: 2,
                decoration: InputDecoration(
                  labelText: widget.initialOdometerKm != null
                      ? 'Ghi chú bàn giao (nếu có)'
                      : 'Ghi chú',
                ),
              ),
              if (_error != null) ...[
                const SizedBox(height: 8),
                Text(
                  _error!,
                  style: TextStyle(color: palette.danger),
                ),
              ],
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: _submitting
                ? null
                : () => Navigator.of(context).pop(),
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

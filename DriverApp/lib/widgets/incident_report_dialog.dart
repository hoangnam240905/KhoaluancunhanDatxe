import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

typedef IncidentReportSubmit =
    Future<String> Function({
      required String incidentType,
      required String description,
    });

class IncidentReportDialog extends StatefulWidget {
  const IncidentReportDialog({super.key, required this.onSubmit});

  final IncidentReportSubmit onSubmit;

  @override
  State<IncidentReportDialog> createState() => _IncidentReportDialogState();
}

class _IncidentReportDialogState extends State<IncidentReportDialog> {
  static const _types = {
    'Accident': 'Tai nạn',
    'VehicleIssue': 'Sự cố xe',
    'CustomerIssue': 'Sự cố khách',
    'Other': 'Khác',
  };

  final _description = TextEditingController();
  String _type = 'VehicleIssue';
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _description.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_submitting) return;
    if (_description.text.trim().isEmpty) {
      setState(() => _error = 'Nhập mô tả sự cố.');
      return;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final message = await widget.onSubmit(
        incidentType: _type,
        description: _description.text,
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
        title: const Text('Báo cáo sự cố'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              RadioGroup<String>(
                groupValue: _type,
                onChanged: (v) {
                  if (_submitting) return;
                  setState(() => _type = v ?? _type);
                },
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    ..._types.entries.map(
                      (e) => RadioListTile<String>(
                        dense: true,
                        title: Text(e.value),
                        value: e.key,
                      ),
                    ),
                  ],
                ),
              ),
              TextField(
                controller: _description,
                enabled: !_submitting,
                maxLines: 3,
                maxLength: 500,
                decoration: const InputDecoration(labelText: 'Mô tả'),
              ),
              if (_error != null)
                Text(
                  _error!,
                  style: const TextStyle(color: AppColors.danger),
                ),
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
            child: const Text('Gửi'),
          ),
        ],
      ),
    );
  }
}

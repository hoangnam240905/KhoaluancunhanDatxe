import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Asks what does not match (required, max 500 characters); returns the note or null.
class DisputeDialog extends StatefulWidget {
  const DisputeDialog({super.key});

  @override
  State<DisputeDialog> createState() => _DisputeDialogState();
}

class _DisputeDialogState extends State<DisputeDialog> {
  final _note = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Báo sai lệch'),
    content: Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Ghi rõ điểm xe thật khác biên bản điều phối. Chuyến chưa bắt đầu cho tới khi điều phối sửa xong.',
          style: TextStyle(fontSize: 13),
        ),
        const SizedBox(height: 10),
        TextField(
          key: const Key('dispute-note'),
          controller: _note,
          maxLength: 500,
          maxLines: 3,
          autofocus: true,
          decoration: InputDecoration(
            labelText: 'Điểm sai lệch (bắt buộc)',
            hintText: 'Ví dụ: cửa sau bị trầy, biên bản ghi ngoại thất tốt',
            errorText: _error,
          ),
        ),
      ],
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.of(context).pop(),
        child: const Text('Hủy'),
      ),
      FilledButton(
        style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
        onPressed: () {
          final text = _note.text.trim();
          if (text.isEmpty) {
            setState(() => _error = 'Vui lòng ghi rõ điểm sai lệch.');
            return;
          }
          Navigator.of(context).pop(text);
        },
        child: const Text('Gửi báo sai lệch'),
      ),
    ],
  );
}

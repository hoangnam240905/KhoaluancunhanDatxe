import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/app_widgets.dart';

class LicenseUploadScreen extends StatefulWidget {
  final ApiService api;

  const LicenseUploadScreen({super.key, required this.api});

  @override
  State<LicenseUploadScreen> createState() => _LicenseUploadScreenState();
}

class _LicenseUploadScreenState extends State<LicenseUploadScreen> {
  static const _types = {
    'LicenseFront': 'Mặt trước bằng lái',
    'LicenseBack': 'Mặt sau bằng lái',
  };

  final _picker = ImagePicker();
  List<UserDocument> _documents = [];
  bool _loading = true;
  String? _uploading;
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
      _documents = await widget.api.getMyDocuments();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    }
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _pick(String type, ImageSource source) async {
    try {
      final file = await _picker.pickImage(
        source: source,
        maxWidth: 2000,
        imageQuality: 85,
      );
      if (file == null) return;
      setState(() => _uploading = type);
      await widget.api.uploadDocument(type, await file.readAsBytes(), file.name);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Đã gửi ${_types[type]!.toLowerCase()}.')),
        );
      }
      await _load();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
        );
      }
    } finally {
      if (mounted) setState(() => _uploading = null);
    }
  }

  UserDocument? _document(String type) {
    for (final item in _documents.reversed) {
      if (item.documentType == type) return item;
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Ảnh bằng lái xe')),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải giấy tờ...'))
          : _error != null
              ? AppErrorState(message: _error!, onRetry: _load)
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      const Text(
                        'Gửi đủ hai mặt bằng lái để được xét duyệt khi đặt xe tự lái.',
                        style: TextStyle(color: AppColors.muted),
                      ),
                      const SizedBox(height: 16),
                      for (final entry in _types.entries) ...[
                        _documentCard(entry.key, entry.value),
                        const SizedBox(height: 12),
                      ],
                    ],
                  ),
                ),
    );
  }

  Widget _documentCard(String type, String label) {
    final document = _document(type);
    final busy = _uploading == type;
    final status = document?.status == 'Accepted'
        ? 'Đã duyệt'
        : document?.status == 'Rejected'
            ? 'Bị từ chối'
            : document == null
                ? 'Chưa gửi'
                : 'Chờ duyệt';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.badge_outlined, color: AppColors.primary),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(label, style: const TextStyle(fontWeight: FontWeight.w800)),
                ),
                Text(status, style: const TextStyle(color: AppColors.muted)),
              ],
            ),
            if (document?.rejectReason != null) ...[
              const SizedBox(height: 8),
              Text('Lý do: ${document!.rejectReason}', style: const TextStyle(color: AppColors.danger)),
            ],
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: busy ? null : () => _pick(type, ImageSource.camera),
                    icon: const Icon(Icons.camera_alt_outlined),
                    label: const Text('Chụp ảnh'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: busy ? null : () => _pick(type, ImageSource.gallery),
                    icon: const Icon(Icons.photo_library_outlined),
                    label: const Text('Thư viện'),
                  ),
                ),
              ],
            ),
            if (busy) const LinearProgressIndicator(),
          ],
        ),
      ),
    );
  }
}

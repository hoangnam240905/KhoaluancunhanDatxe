import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/app_widgets.dart';

/// "Bằng lái xe" for self-drive rentals (plan1.md Phần E + F): declare the licence, send both sides, see the
/// approval and which vehicle types the class allows. Approved once — later self-drive bookings need nothing more.
class LicenseScreen extends StatefulWidget {
  const LicenseScreen({super.key, required this.api});

  final ApiService api;

  @override
  State<LicenseScreen> createState() => _LicenseScreenState();
}

class _LicenseScreenState extends State<LicenseScreen> {
  static const sides = <String, String>{
    'LicenseFront': 'Bằng lái — mặt trước',
    'LicenseBack': 'Bằng lái — mặt sau',
  };
  static final _day = DateFormat('dd/MM/yyyy');

  final _number = TextEditingController();
  final _picker = ImagePicker();
  CustomerLicense? _data;
  List<LicenseClassOption> _classes = [];
  Map<String, String> _imageHeaders = {};
  String? _licenseClass;
  DateTime? _issueDate;
  DateTime? _expiry;
  bool _loading = true;
  bool _saving = false;
  String? _uploading;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _number.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        widget.api.getMyLicense(),
        widget.api.getLicenseClasses(),
        widget.api.authHeaders(),
      ]);
      if (!mounted) return;
      final data = results[0] as CustomerLicense;
      setState(() {
        _data = data;
        // Any car licence works for self-drive (old B1 included); the new B1 is a motorcycle licence.
        _classes = (results[1] as List<LicenseClassOption>)
            .where((c) => c.maxSeats > 0)
            .toList();
        _imageHeaders = results[2] as Map<String, String>;
        _number.text = data.licenseNumber ?? '';
        _licenseClass = data.licenseClass;
        _issueDate = data.licenseIssueDate;
        _expiry = data.licenseExpiry;
      });
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _toast(String message) => ScaffoldMessenger.of(
    context,
  ).showSnackBar(SnackBar(content: Text(message)));

  Future<void> _save() async {
    if (_number.text.trim().isEmpty ||
        _licenseClass == null ||
        _expiry == null) {
      _toast('Vui lòng nhập số bằng, chọn hạng bằng và ngày hết hạn.');
      return;
    }
    setState(() => _saving = true);
    try {
      final data = await widget.api.updateMyLicense(
        licenseNumber: _number.text.trim(),
        licenseClass: _licenseClass!,
        licenseIssueDate: _issueDate,
        licenseExpiry: _expiry!,
      );
      if (!mounted) return;
      setState(() => _data = data);
      _toast('Đã lưu thông tin bằng lái. Gửi đủ ảnh 2 mặt để điều phối duyệt.');
    } catch (e) {
      if (mounted) _toast(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
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
      await widget.api.uploadDocument(
        type,
        await file.readAsBytes(),
        file.name,
      );
      if (!mounted) return;
      _toast('Đã gửi ảnh ${sides[type]!.toLowerCase()}.');
      await _load();
    } catch (e) {
      if (mounted) _toast(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _uploading = null);
    }
  }

  Future<void> _pickDate({required bool expiry}) async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: (expiry ? _expiry : _issueDate) ?? now,
      firstDate: expiry ? now : DateTime(now.year - 50),
      lastDate: expiry ? DateTime(now.year + 30) : now,
      helpText: expiry ? 'Ngày hết hạn' : 'Ngày cấp',
    );
    if (picked != null) {
      setState(() => expiry ? _expiry = picked : _issueDate = picked);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Bằng lái xe (thuê tự lái)')),
    body: _loading
        ? const Center(child: AppLoading(message: 'Đang tải...'))
        : _error != null
        ? AppErrorState(message: _error!, onRetry: _load)
        : RefreshIndicator(
            onRefresh: _load,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _statusCard(),
                const SizedBox(height: 12),
                _formCard(),
                const SizedBox(height: 12),
                _photosCard(),
              ],
            ),
          ),
  );

  Widget _statusCard() {
    final d = _data!;
    final (label, color) = switch (d.licenseStatus) {
      'Approved' => ('Đã duyệt', AppColors.success),
      'Rejected' => ('Chưa được chấp nhận', AppColors.danger),
      'Pending' => ('Chờ điều phối duyệt', AppColors.warning),
      _ => ('Chưa khai', AppColors.muted),
    };
    final allowed = d.vehicleTypes
        .where((t) => t.canDrive)
        .map((t) => t.typeName)
        .toList();
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text(
                  'TRẠNG THÁI',
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12),
                ),
              ),
              Text(
                label,
                style: TextStyle(color: color, fontWeight: FontWeight.w800),
              ),
            ],
          ),
          const SizedBox(height: 6),
          if (d.licenseStatus == 'Rejected' && d.reviewNote != null)
            Text(
              'Lý do: ${d.reviewNote}. Vui lòng gửi lại ảnh bằng lái hoặc liên hệ điều phối.',
              style: const TextStyle(color: AppColors.danger),
            ),
          if (d.licenseStatus == 'Approved')
            const Text('Lúc nhận xe, vui lòng mang bằng lái gốc để xác minh.'),
          if (d.licenseStatus == 'None')
            const Text(
              'Chỉ cần khi thuê xe tự lái. Khai bằng lái và gửi ảnh 2 mặt để được duyệt.',
            ),
          if (d.isDeclared)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                'Loại xe được tự lái: ${allowed.isEmpty ? 'chưa có' : allowed.join(', ')}',
              ),
            ),
          if (d.licenseExpired || d.licenseExpiringSoon)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                d.licenseExpired
                    ? 'Bằng lái đã hết hạn — cập nhật bằng mới để thuê xe tự lái.'
                    : 'Bằng lái sắp hết hạn (${_day.format(d.licenseExpiry!)}).',
                style: const TextStyle(
                  color: AppColors.danger,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _formCard() => AppCard(
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'THÔNG TIN BẰNG LÁI',
          style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12),
        ),
        TextField(
          key: const Key('customer-license-number'),
          controller: _number,
          maxLength: 30,
          textCapitalization: TextCapitalization.characters,
          decoration: const InputDecoration(labelText: 'Số bằng lái'),
        ),
        DropdownButtonFormField<String>(
          initialValue: _classes.any((c) => c.code == _licenseClass)
              ? _licenseClass
              : null,
          decoration: const InputDecoration(labelText: 'Hạng bằng'),
          items: _classes
              .map((c) => DropdownMenuItem(value: c.code, child: Text(c.label)))
              .toList(),
          onChanged: (v) => setState(() => _licenseClass = v),
        ),
        const SizedBox(height: 4),
        const Text(
          'Xe 4–9 chỗ cần bằng B trở lên (B1, B2 cũ còn hạn được chấp nhận); xe 16 chỗ cần D1 trở lên.',
          style: TextStyle(fontSize: 12, color: AppColors.muted),
        ),
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Ngày cấp'),
          trailing: Text(
            _issueDate == null ? 'Chọn ngày' : _day.format(_issueDate!),
          ),
          onTap: () => _pickDate(expiry: false),
        ),
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Ngày hết hạn'),
          trailing: Text(_expiry == null ? 'Chọn ngày' : _day.format(_expiry!)),
          onTap: () => _pickDate(expiry: true),
        ),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: _saving ? null : _save,
            child: _saving
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Lưu thông tin bằng lái'),
          ),
        ),
      ],
    ),
  );

  Widget _photosCard() {
    final cameraOk = _picker.supportsImageSource(ImageSource.camera);
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'ẢNH BẰNG LÁI',
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12),
          ),
          const SizedBox(height: 4),
          const Text(
            'Chụp rõ nét, đủ 4 góc, không lóa (JPG/PNG, tối đa 5 MB).',
            style: TextStyle(fontSize: 12, color: AppColors.muted),
          ),
          const SizedBox(height: 10),
          ...sides.entries.map((side) {
            final doc = _data!.documentOf(side.key);
            final (statusLabel, statusColor) = switch (doc?.status) {
              'Approved' => ('Đã duyệt', AppColors.success),
              'Rejected' => ('Bị từ chối', AppColors.danger),
              'Pending' => ('Chờ duyệt', AppColors.warning),
              _ => ('Chưa gửi', AppColors.muted),
            };
            return Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          side.value,
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                      ),
                      Text(
                        statusLabel,
                        style: TextStyle(
                          color: statusColor,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: AspectRatio(
                      aspectRatio: 16 / 10,
                      child: doc == null
                          ? Container(
                              color: Theme.of(
                                context,
                              ).colorScheme.surfaceContainerHighest,
                              alignment: Alignment.center,
                              child: const Icon(Icons.credit_card, size: 40),
                            )
                          : Image.network(
                              widget.api.documentFileUrl(doc.userDocumentId),
                              headers: _imageHeaders,
                              fit: BoxFit.cover,
                              errorBuilder: (_, _, _) => const Center(
                                child: Text('Không tải được ảnh'),
                              ),
                            ),
                    ),
                  ),
                  if (doc?.status == 'Rejected' && doc?.rejectReason != null)
                    Text(
                      'Lý do: ${doc!.rejectReason}',
                      style: const TextStyle(color: AppColors.danger),
                    ),
                  const SizedBox(height: 6),
                  if (_uploading == side.key)
                    const LinearProgressIndicator()
                  else
                    Row(
                      children: [
                        if (cameraOk)
                          Expanded(
                            child: OutlinedButton.icon(
                              onPressed: _uploading != null
                                  ? null
                                  : () => _pick(side.key, ImageSource.camera),
                              icon: const Icon(Icons.photo_camera_outlined),
                              label: const Text('Chụp ảnh'),
                            ),
                          ),
                        if (cameraOk) const SizedBox(width: 8),
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: _uploading != null
                                ? null
                                : () => _pick(side.key, ImageSource.gallery),
                            icon: const Icon(Icons.photo_library_outlined),
                            label: Text(doc == null ? 'Chọn ảnh' : 'Gửi lại'),
                          ),
                        ),
                      ],
                    ),
                ],
              ),
            );
          }),
        ],
      ),
    );
  }
}

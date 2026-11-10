import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';

/// "Bằng lái & giấy tờ" (plan1.md Phần D + F): the driver declares the licence, photographs both sides of
/// the licence and the ID card, and follows the two review steps (photos accepted → original checked at
/// the office → verified). Only verified drivers can be assigned trips.
class VerificationScreen extends StatefulWidget {
  const VerificationScreen({super.key, required this.api});

  final ApiService api;

  @override
  State<VerificationScreen> createState() => _VerificationScreenState();
}

class _VerificationScreenState extends State<VerificationScreen> {
  static const sides = <String, String>{
    'LicenseFront': 'Bằng lái — mặt trước',
    'LicenseBack': 'Bằng lái — mặt sau',
    'IdCardFront': 'CCCD — mặt trước',
    'IdCardBack': 'CCCD — mặt sau',
  };

  final _number = TextEditingController();
  final _idNumber = TextEditingController();
  final _picker = ImagePicker();
  DriverVerification? _data;
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
    _idNumber.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        widget.api.getMyVerification(),
        widget.api.getLicenseClasses(),
        widget.api.authHeaders(),
      ]);
      if (!mounted) return;
      final data = results[0] as DriverVerification;
      setState(() {
        _data = data;
        _classes = (results[1] as List<LicenseClassOption>)
            .where((c) => c.allowedForDrivers)
            .toList();
        _imageHeaders = results[2] as Map<String, String>;
        _number.text = data.licenseNumber ?? '';
        _idNumber.text = data.idNumber ?? '';
        _licenseClass = data.licenseClass;
        _issueDate = data.licenseIssueDate;
        _expiry = data.licenseExpiry;
      });
    } catch (e) {
      if (mounted) setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _toast(String message) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));

  Future<void> _saveLicense() async {
    if (_number.text.trim().isEmpty || _licenseClass == null || _expiry == null) {
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
        idNumber: _idNumber.text,
      );
      if (!mounted) return;
      setState(() => _data = data);
      _toast('Đã lưu thông tin bằng lái.');
    } catch (e) {
      if (mounted) _toast(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _pick(String type, ImageSource source) async {
    try {
      final file = await _picker.pickImage(source: source, maxWidth: 2000, imageQuality: 85);
      if (file == null) return;
      setState(() => _uploading = type);
      final bytes = await file.readAsBytes();
      await widget.api.uploadDocument(type, bytes, file.name);
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
      firstDate: expiry ? now : DateTime(now.year - 40),
      lastDate: expiry ? DateTime(now.year + 30) : now,
      helpText: expiry ? 'Ngày hết hạn' : 'Ngày cấp',
    );
    if (picked == null) return;
    setState(() => expiry ? _expiry = picked : _issueDate = picked);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Bằng lái & giấy tờ')),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải hồ sơ...'))
          : _error != null
          ? AppErrorState(message: _error!, onRetry: _load)
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  _statusCard(),
                  const SizedBox(height: 12),
                  _licenseCard(),
                  const SizedBox(height: 12),
                  _photosCard(),
                ],
              ),
            ),
    );
  }

  Widget _statusCard() {
    final d = _data!;
    final (label, color) = switch (d.verificationStatus) {
      'Verified' => ('Đã xác minh', AppColors.success),
      'Rejected' => ('Xác minh không thành công', AppColors.danger),
      _ => ('Chờ xác minh', AppColors.warning),
    };
    final step = switch ((d.verificationStatus, d.photoStatus)) {
      ('Verified', _) => 'Bạn đã có thể nhận chuyến.',
      ('Rejected', _) => 'Lý do: ${d.verificationNote ?? '—'}. Vui lòng cập nhật lại thông tin bằng lái.',
      (_, 'Accepted') => 'Ảnh đã được chấp nhận. Vui lòng lên văn phòng để đối chiếu bằng gốc (mang bằng lái và CCCD).',
      (_, 'Pending') => 'Đã gửi đủ ảnh, đang chờ điều phối duyệt.',
      (_, 'Rejected') => 'Có ảnh bị từ chối — xem lý do bên dưới và gửi lại.',
      _ => 'Nhập thông tin bằng lái và gửi đủ 4 ảnh để được xác minh.',
    };
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text('TRẠNG THÁI XÁC MINH', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12)),
              ),
              StatusBadge(label: label, color: color),
            ],
          ),
          const SizedBox(height: 8),
          Text(step),
          if (d.verifiedByName != null && d.verificationStatus != 'Pending')
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                'Điều phối: ${d.verifiedByName}',
                style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant),
              ),
            ),
          if (d.licenseExpired || d.licenseExpiringSoon)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(
                d.licenseExpired
                    ? 'Bằng lái đã hết hạn — bạn không được phân công cho tới khi cập nhật bằng mới.'
                    : 'Bằng lái sắp hết hạn (${Formatters.date(d.licenseExpiry)}). Vui lòng cập nhật bằng mới.',
                style: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700),
              ),
            ),
        ],
      ),
    );
  }

  Widget _licenseCard() {
    final dateStyle = Theme.of(context).textTheme.bodyLarge;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('THÔNG TIN BẰNG LÁI', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12)),
          const SizedBox(height: 8),
          TextField(
            key: const Key('license-number'),
            controller: _number,
            textCapitalization: TextCapitalization.characters,
            maxLength: 30,
            decoration: const InputDecoration(labelText: 'Số bằng lái'),
          ),
          DropdownButtonFormField<String>(
            key: const Key('license-class'),
            initialValue: _classes.any((c) => c.code == _licenseClass) ? _licenseClass : null,
            decoration: const InputDecoration(labelText: 'Hạng bằng (Luật 36/2024)'),
            items: _classes
                .map((c) => DropdownMenuItem(value: c.code, child: Text(c.label)))
                .toList(),
            onChanged: (v) => setState(() => _licenseClass = v),
          ),
          const SizedBox(height: 4),
          Text(
            'Xe 4–9 chỗ cần hạng B trở lên (hoặc B2 cũ); xe 16 chỗ cần D1 trở lên.',
            style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
          ),
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Ngày cấp'),
            trailing: Text(_issueDate == null ? 'Chọn ngày' : Formatters.date(_issueDate), style: dateStyle),
            onTap: () => _pickDate(expiry: false),
          ),
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Ngày hết hạn'),
            trailing: Text(_expiry == null ? 'Chọn ngày' : Formatters.date(_expiry), style: dateStyle),
            onTap: () => _pickDate(expiry: true),
          ),
          TextField(
            key: const Key('id-number'),
            controller: _idNumber,
            keyboardType: TextInputType.number,
            maxLength: 12,
            decoration: const InputDecoration(labelText: 'Số CCCD (12 số)'),
          ),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: _saving ? null : _saveLicense,
              child: _saving
                  ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Lưu thông tin bằng lái'),
            ),
          ),
          if (_data!.isVerified)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                'Đổi thông tin bằng lái sẽ cần điều phối xác minh lại.',
                style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
              ),
            ),
        ],
      ),
    );
  }

  Widget _photosCard() {
    final cameraOk = _picker.supportsImageSource(ImageSource.camera);
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('ẢNH GIẤY TỜ', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12)),
          const SizedBox(height: 4),
          Text(
            'Chụp rõ nét, đủ 4 góc, không lóa (JPG/PNG, tối đa 5 MB). Ảnh không thay việc đối chiếu bằng gốc.',
            style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 10),
          ...sides.entries.map((side) {
            final doc = _data!.documentOf(side.key);
            final busy = _uploading == side.key;
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
                      Expanded(child: Text(side.value, style: const TextStyle(fontWeight: FontWeight.w700))),
                      StatusBadge(label: statusLabel, color: statusColor),
                    ],
                  ),
                  const SizedBox(height: 6),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: AspectRatio(
                      aspectRatio: 16 / 10,
                      child: doc == null
                          ? Container(
                              color: Theme.of(context).colorScheme.surfaceContainerHighest,
                              alignment: Alignment.center,
                              child: const Icon(Icons.badge_outlined, size: 40),
                            )
                          : Image.network(
                              widget.api.documentFileUrl(doc.userDocumentId),
                              headers: _imageHeaders,
                              fit: BoxFit.cover,
                              errorBuilder: (_, _, _) => const Center(child: Text('Không tải được ảnh')),
                            ),
                    ),
                  ),
                  if (doc?.status == 'Rejected' && doc?.rejectReason != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text('Lý do: ${doc!.rejectReason}', style: const TextStyle(color: AppColors.danger)),
                    ),
                  const SizedBox(height: 6),
                  if (busy)
                    const LinearProgressIndicator()
                  else
                    Row(
                      children: [
                        if (cameraOk)
                          Expanded(
                            child: OutlinedButton.icon(
                              onPressed: _uploading != null ? null : () => _pick(side.key, ImageSource.camera),
                              icon: const Icon(Icons.photo_camera_outlined),
                              label: const Text('Chụp ảnh'),
                            ),
                          ),
                        if (cameraOk) const SizedBox(width: 8),
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: _uploading != null ? null : () => _pick(side.key, ImageSource.gallery),
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

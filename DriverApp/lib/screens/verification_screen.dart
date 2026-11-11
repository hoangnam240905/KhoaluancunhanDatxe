import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';

import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';

/// "Bằng lái & giấy tờ" (plan1.md Phần D + F, Phase 9): the driver may declare several licences (B2, D1…).
/// Each licence has its two photos and its own review (photos accepted → original checked at the office →
/// verified); the ID card is sent once. A trip can be assigned when one verified licence covers its vehicle type.
class VerificationScreen extends StatefulWidget {
  const VerificationScreen({super.key, required this.api});

  final ApiService api;

  @override
  State<VerificationScreen> createState() => _VerificationScreenState();
}

class _VerificationScreenState extends State<VerificationScreen> {
  static const licenceSides = <String, String>{
    'LicenseFront': 'Mặt trước',
    'LicenseBack': 'Mặt sau',
  };
  static const idSides = <String, String>{
    'IdCardFront': 'CCCD — mặt trước',
    'IdCardBack': 'CCCD — mặt sau',
  };

  final _picker = ImagePicker();
  DriverVerification? _data;
  List<LicenseClassOption> _classes = [];
  Map<String, String> _imageHeaders = {};
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
      final results = await Future.wait([
        widget.api.getMyVerification(),
        widget.api.getLicenseClasses(),
        widget.api.authHeaders(),
      ]);
      if (!mounted) return;
      setState(() {
        _data = results[0] as DriverVerification;
        _classes = (results[1] as List<LicenseClassOption>)
            .where((c) => c.allowedForDrivers)
            .toList();
        _imageHeaders = results[2] as Map<String, String>;
      });
    } catch (e) {
      if (mounted) setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _toast(String message) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));

  Future<void> _openForm([PersonLicense? licence]) async {
    final saved = await showModalBottomSheet<DriverVerification>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _LicenceForm(
        api: widget.api,
        classes: _classes,
        licence: licence,
        idNumber: _data?.idNumber,
      ),
    );
    if (saved == null || !mounted) return;
    setState(() => _data = saved);
    _toast(licence == null
        ? 'Đã thêm bằng lái. Gửi ảnh 2 mặt của bằng này để được xác minh.'
        : 'Đã lưu thông tin bằng lái.');
  }

  Future<void> _pick(String type, ImageSource source, {PersonLicense? licence}) async {
    try {
      final file = await _picker.pickImage(source: source, maxWidth: 2000, imageQuality: 85);
      if (file == null) return;
      setState(() => _uploading = '${licence?.licenseId ?? 0}-$type');
      final bytes = await file.readAsBytes();
      await widget.api.uploadDocument(type, bytes, file.name, licenseId: licence?.licenseId);
      if (!mounted) return;
      _toast('Đã gửi ảnh.');
      await _load();
    } catch (e) {
      if (mounted) _toast(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _uploading = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Bằng lái & giấy tờ')),
      floatingActionButton: _loading || _error != null
          ? null
          : FloatingActionButton.extended(
              key: const Key('add-licence'),
              onPressed: () => _openForm(),
              icon: const Icon(Icons.add),
              label: Text(_data!.licences.isEmpty ? 'Khai bằng lái' : 'Thêm bằng lái'),
            ),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải hồ sơ...'))
          : _error != null
          ? AppErrorState(message: _error!, onRetry: _load)
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
                children: [
                  _statusCard(),
                  for (final l in _data!.licences) ...[
                    const SizedBox(height: 12),
                    _licenceCard(l),
                  ],
                  const SizedBox(height: 12),
                  _idCard(),
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
    final verified = d.licences.where((l) => l.status == 'Approved').map((l) => l.classLabel).toList();
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
          Text(d.licences.isEmpty
              ? 'Khai bằng lái, gửi ảnh 2 mặt của bằng và CCCD để được xác minh.'
              : verified.isEmpty
              ? 'Chưa có bằng nào được xác minh — chưa nhận được chuyến.'
              : 'Bằng đã xác minh: ${verified.join(', ')}. Bạn được nhận các loại xe những bằng này cho phép.'),
          const SizedBox(height: 6),
          Text(
            'Có thể khai nhiều bằng (ví dụ B2 và D1); mỗi bằng xác minh riêng, sửa một bằng không ảnh hưởng bằng khác.',
            style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }

  String _stepOf(PersonLicense l) => switch ((l.status, l.photoStatus)) {
    ('Approved', _) => 'Đã xác minh${l.reviewedByName == null ? '' : ' (điều phối ${l.reviewedByName})'}.',
    ('Rejected', _) => 'Lý do: ${l.reviewNote ?? '—'}. Vui lòng sửa thông tin hoặc gửi lại ảnh.',
    (_, 'Accepted') => 'Ảnh đã được chấp nhận. Lên văn phòng đối chiếu bằng gốc (mang bằng lái và CCCD).',
    (_, 'Pending') => 'Đã gửi đủ ảnh, đang chờ điều phối duyệt.',
    (_, 'Rejected') => 'Có ảnh bị từ chối — xem lý do và gửi lại.',
    _ => 'Gửi ảnh 2 mặt của bằng này (và CCCD nếu chưa gửi).',
  };

  Widget _licenceCard(PersonLicense l) {
    final (label, color) = switch (l.status) {
      'Approved' => ('Đã xác minh', AppColors.success),
      'Rejected' => ('Không thành công', AppColors.danger),
      _ => ('Chờ xác minh', AppColors.warning),
    };
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text('Bằng ${l.classLabel}', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
              ),
              StatusBadge(label: label, color: color),
            ],
          ),
          const SizedBox(height: 4),
          Text('Số ${l.number} · hết hạn ${Formatters.date(l.expiry)}'),
          const SizedBox(height: 4),
          Text(_stepOf(l)),
          if (l.expired || l.expiringSoon)
            Text(
              l.expired ? 'Bằng này đã hết hạn — không dùng để nhận chuyến được.' : 'Bằng này sắp hết hạn.',
              style: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700),
            ),
          const SizedBox(height: 8),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final side in licenceSides.entries) ...[
                Expanded(child: _photo(side.key, side.value, l.documentOf(side.key), licence: l)),
                if (side.key == 'LicenseFront') const SizedBox(width: 8),
              ],
            ],
          ),
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: () => _openForm(l),
              icon: const Icon(Icons.edit_outlined, size: 18),
              label: const Text('Sửa thông tin'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _idCard() {
    final d = _data!;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('CĂN CƯỚC CÔNG DÂN', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12)),
          const SizedBox(height: 4),
          Text('Số CCCD: ${d.idNumber ?? 'chưa khai (nhập khi khai / sửa bằng lái)'}'),
          const SizedBox(height: 8),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final side in idSides.entries) ...[
                Expanded(child: _photo(side.key, side.value, d.documentOf(side.key))),
                if (side.key == 'IdCardFront') const SizedBox(width: 8),
              ],
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Chụp rõ nét, đủ 4 góc, không lóa (JPG/PNG, tối đa 5 MB). Ảnh không thay việc đối chiếu bằng gốc.',
            style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }

  Widget _photo(String type, String title, UserDocument? doc, {PersonLicense? licence}) {
    final cameraOk = _picker.supportsImageSource(ImageSource.camera);
    final busy = _uploading == '${licence?.licenseId ?? 0}-$type';
    final (statusLabel, statusColor) = switch (doc?.status) {
      'Approved' => ('Đã duyệt', AppColors.success),
      'Rejected' => ('Bị từ chối', AppColors.danger),
      'Pending' => ('Chờ duyệt', AppColors.warning),
      _ => ('Chưa gửi', AppColors.muted),
    };
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('$title · $statusLabel', style: TextStyle(fontSize: 12, color: statusColor, fontWeight: FontWeight.w700)),
        const SizedBox(height: 4),
        ClipRRect(
          borderRadius: BorderRadius.circular(10),
          child: AspectRatio(
            aspectRatio: 16 / 10,
            child: doc == null
                ? Container(
                    color: Theme.of(context).colorScheme.surfaceContainerHighest,
                    alignment: Alignment.center,
                    child: const Icon(Icons.badge_outlined, size: 32),
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
          Text('Lý do: ${doc!.rejectReason}', style: const TextStyle(color: AppColors.danger, fontSize: 12)),
        if (busy)
          const Padding(padding: EdgeInsets.only(top: 8), child: LinearProgressIndicator())
        else
          Row(
            children: [
              if (cameraOk)
                IconButton(
                  tooltip: 'Chụp ảnh',
                  onPressed: _uploading != null ? null : () => _pick(type, ImageSource.camera, licence: licence),
                  icon: const Icon(Icons.photo_camera_outlined),
                ),
              Expanded(
                child: TextButton(
                  onPressed: _uploading != null ? null : () => _pick(type, ImageSource.gallery, licence: licence),
                  child: Text(doc == null ? 'Chọn ảnh' : 'Gửi lại'),
                ),
              ),
            ],
          ),
      ],
    );
  }
}

/// Add a licence (licence == null) or correct one; only that licence is verified again.
class _LicenceForm extends StatefulWidget {
  const _LicenceForm({required this.api, required this.classes, this.licence, this.idNumber});

  final ApiService api;
  final List<LicenseClassOption> classes;
  final PersonLicense? licence;
  final String? idNumber;

  @override
  State<_LicenceForm> createState() => _LicenceFormState();
}

class _LicenceFormState extends State<_LicenceForm> {
  late final _number = TextEditingController(text: widget.licence?.number ?? '');
  late final _idNumber = TextEditingController(text: widget.idNumber ?? '');
  late String? _licenseClass = widget.licence?.licenseClass;
  late DateTime? _issueDate = widget.licence?.issueDate;
  late DateTime? _expiry = widget.licence?.expiry;
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _number.dispose();
    _idNumber.dispose();
    super.dispose();
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

  Future<void> _save() async {
    if (_number.text.trim().isEmpty || _licenseClass == null || _expiry == null) {
      setState(() => _error = 'Vui lòng nhập số bằng, chọn hạng bằng và ngày hết hạn.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final data = await widget.api.saveMyLicence(
        licenseId: widget.licence?.licenseId,
        licenseNumber: _number.text.trim(),
        licenseClass: _licenseClass!,
        licenseIssueDate: _issueDate,
        licenseExpiry: _expiry!,
        idNumber: _idNumber.text,
      );
      if (mounted) Navigator.of(context).pop(data);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final dateStyle = Theme.of(context).textTheme.bodyLarge;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 16, 16, 16 + MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              widget.licence == null ? 'THÊM BẰNG LÁI' : 'SỬA BẰNG ${widget.licence!.classLabel.toUpperCase()}',
              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12),
            ),
            TextField(
              key: const Key('license-number'),
              controller: _number,
              maxLength: 12,
              keyboardType: TextInputType.number,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(labelText: 'Số bằng lái', helperText: 'Đúng 12 chữ số, in ở mục "Số / No." trên bằng'),
            ),
            DropdownButtonFormField<String>(
              key: const Key('license-class'),
              initialValue: widget.classes.any((c) => c.code == _licenseClass) ? _licenseClass : null,
              decoration: const InputDecoration(labelText: 'Hạng bằng (Luật 36/2024)'),
              items: widget.classes
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
            if (widget.licence?.status == 'Approved')
              Padding(
                padding: const EdgeInsets.only(bottom: 6),
                child: Text(
                  'Đổi thông tin bằng này sẽ cần điều phối xác minh lại bằng này (các bằng khác không đổi).',
                  style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
                ),
              ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Text(_error!, style: const TextStyle(color: AppColors.danger)),
              ),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: _saving ? null : _save,
                child: _saving
                    ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : Text(widget.licence == null ? 'Thêm bằng lái' : 'Lưu bằng lái'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

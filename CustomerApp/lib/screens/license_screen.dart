import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/app_widgets.dart';

/// "Bằng lái xe" for self-drive rentals (plan1.md Phần E + F, Phase 9): a customer may declare several
/// licences (B, D1…), each with its two photos and its own approval. A vehicle type may be rented self-drive
/// when any approved licence covers it and is valid until the return date.
class LicenseScreen extends StatefulWidget {
  const LicenseScreen({super.key, required this.api});

  final ApiService api;

  @override
  State<LicenseScreen> createState() => _LicenseScreenState();
}

class _LicenseScreenState extends State<LicenseScreen> {
  static const sides = <String, String>{
    'LicenseFront': 'Mặt trước',
    'LicenseBack': 'Mặt sau',
  };
  static final _day = DateFormat('dd/MM/yyyy');

  final _picker = ImagePicker();
  CustomerLicense? _data;
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
        widget.api.getMyLicense(),
        widget.api.getLicenseClasses(),
        widget.api.authHeaders(),
      ]);
      if (!mounted) return;
      setState(() {
        _data = results[0] as CustomerLicense;
        // Any car licence works for self-drive (old B1 included); the new B1 is a motorcycle licence.
        _classes = (results[1] as List<LicenseClassOption>)
            .where((c) => c.maxSeats > 0)
            .toList();
        _imageHeaders = results[2] as Map<String, String>;
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

  Future<void> _openForm([PersonLicense? licence]) async {
    final saved = await showModalBottomSheet<CustomerLicense>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _LicenceForm(
        api: widget.api,
        classes: _classes,
        licence: licence,
      ),
    );
    if (saved == null || !mounted) return;
    setState(() => _data = saved);
    _toast(
      licence == null
          ? 'Đã thêm bằng lái. Gửi đủ ảnh 2 mặt của bằng này để điều phối duyệt.'
          : 'Đã lưu bằng lái. Gửi đủ ảnh 2 mặt để điều phối duyệt.',
    );
  }

  Future<void> _pick(PersonLicense licence, String type, ImageSource source) async {
    try {
      final file = await _picker.pickImage(
        source: source,
        maxWidth: 2000,
        imageQuality: 85,
      );
      if (file == null) return;
      setState(() => _uploading = '${licence.licenseId}-$type');
      await widget.api.uploadDocument(
        type,
        await file.readAsBytes(),
        file.name,
        licenseId: licence.licenseId,
      );
      if (!mounted) return;
      _toast('Đã gửi ảnh ${sides[type]!.toLowerCase()} của bằng ${licence.classLabel}.');
      await _load();
    } catch (e) {
      if (mounted) _toast(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _uploading = null);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Bằng lái xe (thuê tự lái)')),
    floatingActionButton: _loading || _error != null
        ? null
        : FloatingActionButton.extended(
            key: const Key('add-licence'),
            onPressed: () => _openForm(),
            icon: const Icon(Icons.add),
            label: Text(_data!.licences.isEmpty ? 'Khai bằng lái' : 'Thêm bằng lái'),
          ),
    body: _loading
        ? const Center(child: AppLoading(message: 'Đang tải...'))
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
              ],
            ),
          ),
  );

  static (String, Color) _statusOf(String status) => switch (status) {
    'Approved' => ('Đã duyệt', AppColors.success),
    'Rejected' => ('Chưa được chấp nhận', AppColors.danger),
    'Pending' => ('Chờ điều phối duyệt', AppColors.warning),
    _ => ('Chưa khai', AppColors.muted),
  };

  Widget _statusCard() {
    final d = _data!;
    final allowed = d.vehicleTypes
        .where((t) => t.canDrive)
        .map((t) => t.typeName)
        .toList();
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'LOẠI XE ĐƯỢC TỰ LÁI',
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12),
          ),
          const SizedBox(height: 6),
          if (d.licences.isEmpty)
            const Text(
              'Chỉ cần khi thuê xe tự lái. Khai bằng lái và gửi ảnh 2 mặt để được duyệt.',
            )
          else
            Text(allowed.isEmpty ? 'Chưa có' : allowed.join(', ')),
          const SizedBox(height: 6),
          const Text(
            'Có thể khai nhiều bằng (ví dụ B và D1). Mỗi bằng được duyệt riêng; chỉ cần một bằng đã duyệt, đủ hạng và còn hạn tới ngày trả xe là thuê được loại xe đó. Lúc nhận xe mang bằng gốc để đối chiếu.',
            style: TextStyle(fontSize: 12, color: AppColors.muted),
          ),
        ],
      ),
    );
  }

  Widget _licenceCard(PersonLicense l) {
    final (label, color) = _statusOf(l.status);
    final cameraOk = _picker.supportsImageSource(ImageSource.camera);
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Bằng ${l.classLabel}',
                  style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
                ),
              ),
              Text(label, style: TextStyle(color: color, fontWeight: FontWeight.w800)),
            ],
          ),
          const SizedBox(height: 4),
          Text('Số ${l.number}'
              '${l.expiry == null ? '' : ' · hết hạn ${_day.format(l.expiry!)}'}'),
          if (l.status == 'Rejected' && l.reviewNote != null)
            Text(
              'Lý do: ${l.reviewNote}. Vui lòng gửi lại ảnh hoặc sửa thông tin.',
              style: const TextStyle(color: AppColors.danger),
            ),
          if (l.expired || l.expiringSoon)
            Text(
              l.expired ? 'Bằng này đã hết hạn.' : 'Bằng này sắp hết hạn.',
              style: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700),
            ),
          const SizedBox(height: 8),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final side in sides.entries) ...[
                Expanded(child: _photo(l, side.key, side.value, cameraOk)),
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

  Widget _photo(PersonLicense l, String type, String title, bool cameraOk) {
    final doc = l.documentOf(type);
    final (statusLabel, statusColor) = switch (doc?.status) {
      'Approved' => ('Đã duyệt', AppColors.success),
      'Rejected' => ('Bị từ chối', AppColors.danger),
      'Pending' => ('Chờ duyệt', AppColors.warning),
      _ => ('Chưa gửi', AppColors.muted),
    };
    final busy = _uploading == '${l.licenseId}-$type';
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
                    child: const Icon(Icons.credit_card, size: 32),
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
                  onPressed: _uploading != null ? null : () => _pick(l, type, ImageSource.camera),
                  icon: const Icon(Icons.photo_camera_outlined),
                ),
              Expanded(
                child: TextButton(
                  onPressed: _uploading != null ? null : () => _pick(l, type, ImageSource.gallery),
                  child: Text(doc == null ? 'Chọn ảnh' : 'Gửi lại'),
                ),
              ),
            ],
          ),
      ],
    );
  }
}

/// Add a licence (licence == null) or correct one; only that licence goes back for approval.
class _LicenceForm extends StatefulWidget {
  const _LicenceForm({required this.api, required this.classes, this.licence});

  final ApiService api;
  final List<LicenseClassOption> classes;
  final PersonLicense? licence;

  @override
  State<_LicenceForm> createState() => _LicenceFormState();
}

class _LicenceFormState extends State<_LicenceForm> {
  static final _day = DateFormat('dd/MM/yyyy');
  late final _number = TextEditingController(text: widget.licence?.number ?? '');
  late String? _licenseClass = widget.licence?.licenseClass;
  late DateTime? _issueDate = widget.licence?.issueDate;
  late DateTime? _expiry = widget.licence?.expiry;
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _number.dispose();
    super.dispose();
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
      );
      if (mounted) Navigator.of(context).pop(data);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => Padding(
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
            key: const Key('customer-license-number'),
            controller: _number,
            maxLength: 12,
            keyboardType: TextInputType.number,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: const InputDecoration(
              labelText: 'Số bằng lái',
              helperText: 'Đúng 12 chữ số, in ở mục "Số / No." trên bằng',
            ),
          ),
          DropdownButtonFormField<String>(
            initialValue: widget.classes.any((c) => c.code == _licenseClass) ? _licenseClass : null,
            decoration: const InputDecoration(labelText: 'Hạng bằng'),
            items: widget.classes
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
            trailing: Text(_issueDate == null ? 'Chọn ngày' : _day.format(_issueDate!)),
            onTap: () => _pickDate(expiry: false),
          ),
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Ngày hết hạn'),
            trailing: Text(_expiry == null ? 'Chọn ngày' : _day.format(_expiry!)),
            onTap: () => _pickDate(expiry: true),
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

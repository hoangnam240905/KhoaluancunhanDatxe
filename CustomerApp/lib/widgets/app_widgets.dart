import 'package:flutter/material.dart';
import '../config/api_config.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';

class AppCard extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry padding;
  final VoidCallback? onTap;

  const AppCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final card = Container(
      width: double.infinity,
      padding: padding,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 16,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: child,
    );
    if (onTap == null) return card;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(18),
        child: card,
      ),
    );
  }
}

class AppLoading extends StatelessWidget {
  final String? message;
  const AppLoading({super.key, this.message});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const CircularProgressIndicator(),
          if (message != null) ...[
            const SizedBox(height: 12),
            Text(message!, style: const TextStyle(color: AppColors.muted)),
          ],
        ],
      ),
    );
  }
}

class AppErrorState extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;
  const AppErrorState({
    super.key,
    required this.message,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.wifi_off_rounded, size: 40, color: AppColors.muted),
          const SizedBox(height: 12),
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 16),
          FilledButton(onPressed: onRetry, child: const Text('Thử lại')),
        ],
      ),
    );
  }
}

class AppEmptyState extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final String? actionLabel;
  final VoidCallback? onAction;

  const AppEmptyState({
    super.key,
    required this.icon,
    required this.title,
    required this.subtitle,
    this.actionLabel,
    this.onAction,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 48, color: AppColors.primary),
          const SizedBox(height: 12),
          Text(
            title,
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 6),
          Text(
            subtitle,
            textAlign: TextAlign.center,
            style: const TextStyle(color: AppColors.muted),
          ),
          if (actionLabel != null && onAction != null) ...[
            const SizedBox(height: 16),
            FilledButton(onPressed: onAction, child: Text(actionLabel!)),
          ],
        ],
      ),
    );
  }
}

class StatusChip extends StatelessWidget {
  final String status;
  const StatusChip({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final color = Formatters.statusColor(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        Formatters.statusLabel(status),
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.w700,
          fontSize: 12,
        ),
      ),
    );
  }
}

class VehicleImage extends StatelessWidget {
  final String? imageUrl;
  final String? typeName;
  final int? seatCapacity;
  final double height;
  final BorderRadius? borderRadius;

  const VehicleImage({
    super.key,
    this.imageUrl,
    this.typeName,
    this.seatCapacity,
    this.height = 120,
    this.borderRadius,
  });

  static String assetFor(String? typeName, int? seats) {
    final n = (typeName ?? '').toLowerCase();
    if (n.contains('limousine') || n.contains('luxury') || n.contains('limo')) {
      return 'assets/images/home/limousine.jpg';
    }
    if (n.contains('suv') || (seats != null && seats >= 6 && seats <= 8)) {
      return 'assets/images/home/suv.jpg';
    }
    if (n.contains('van') || n.contains('mpv') || n.contains('16') || (seats != null && seats >= 12)) {
      return 'assets/images/home/van.jpg';
    }
    if (n.contains('sedan') || (seats != null && seats <= 5)) {
      return 'assets/images/home/sedan.jpg';
    }
    return 'assets/images/home/sedan.jpg';
  }

  @override
  Widget build(BuildContext context) {
    final fallbackAsset = assetFor(typeName, seatCapacity);
    final rad = borderRadius ?? BorderRadius.circular(14);

    Widget imageWidget;
    if (imageUrl != null && imageUrl!.trim().isNotEmpty) {
      final trimmed = imageUrl!.trim();
      if (trimmed.startsWith('assets/')) {
        imageWidget = Image.asset(trimmed, fit: BoxFit.cover);
      } else {
        final resolved = ApiConfig.resolveImageUrl(trimmed);
        if (resolved != null) {
          imageWidget = Image.network(
            resolved,
            fit: BoxFit.cover,
            errorBuilder: (_, _, _) => Image.asset(
              fallbackAsset,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => _placeholder(),
            ),
          );
        } else {
          imageWidget = Image.asset(
            fallbackAsset,
            fit: BoxFit.cover,
            errorBuilder: (_, _, _) => _placeholder(),
          );
        }
      }
    } else {
      imageWidget = Image.asset(
        fallbackAsset,
        fit: BoxFit.cover,
        errorBuilder: (_, _, _) => _placeholder(),
      );
    }

    return ClipRRect(
      borderRadius: rad,
      child: SizedBox(
        height: height,
        width: double.infinity,
        child: imageWidget,
      ),
    );
  }

  Widget _placeholder() {
    return Container(
      color: const Color(0xFFDBEAFE),
      child: const Center(
        child: Icon(
          Icons.directions_car_filled,
          size: 42,
          color: AppColors.primary,
        ),
      ),
    );
  }
}

class RentalModeToggle extends StatelessWidget {
  final String value;
  final ValueChanged<String> onChanged;

  const RentalModeToggle({
    super.key,
    required this.value,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _modeCard(
            selected: value == 'WithDriver',
            title: 'Có tài xế',
            subtitle: 'Tài xế được điều phối cho chuyến đi',
            icon: Icons.airline_seat_recline_extra,
            onTap: () => onChanged('WithDriver'),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _modeCard(
            selected: value == 'SelfDrive',
            title: 'Tự lái',
            subtitle: 'Nhận xe và tự sử dụng',
            icon: Icons.vpn_key_outlined,
            onTap: () => onChanged('SelfDrive'),
          ),
        ),
      ],
    );
  }

  Widget _modeCard({
    required bool selected,
    required String title,
    required String subtitle,
    required IconData icon,
    required VoidCallback onTap,
  }) {
    return Material(
      color: selected ? const Color(0xFFEFF6FF) : Colors.white,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: selected ? AppColors.primary : AppColors.border,
              width: selected ? 1.8 : 1,
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, color: selected ? AppColors.primary : AppColors.muted),
              const SizedBox(height: 8),
              Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
              const SizedBox(height: 4),
              Text(
                subtitle,
                style: const TextStyle(fontSize: 12, color: AppColors.muted),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Thanh tiến trình đơn hàng 5 bước mô phỏng theo BookingDetailUi bên PortalWeb
class BookingTimelineWidget extends StatelessWidget {
  final String status;
  final bool isCancelled;

  const BookingTimelineWidget({
    super.key,
    required this.status,
    this.isCancelled = false,
  });

  static const _steps = [
    {'key': 'Pending', 'label': 'Chờ duyệt'},
    {'key': 'Confirmed', 'label': 'Đã duyệt'},
    {'key': 'Assigned', 'label': 'Đã gán'},
    {'key': 'InProgress', 'label': 'Đang đi'},
    {'key': 'Completed', 'label': 'Hoàn thành'},
  ];

  int _currentIndex() {
    switch (status) {
      case 'Pending':
        return 0;
      case 'Confirmed':
        return 1;
      case 'Assigned':
        return 2;
      case 'InProgress':
        return 3;
      case 'Completed':
        return 4;
      default:
        return -1;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (isCancelled || status == 'Cancelled') {
      return Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.dangerSoft,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.danger.withValues(alpha: 0.3)),
        ),
        child: const Row(
          children: [
            Icon(Icons.cancel_outlined, color: AppColors.danger, size: 32),
            SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Đơn thuê đã bị hủy',
                    style: TextStyle(
                      color: AppColors.danger,
                      fontWeight: FontWeight.w800,
                      fontSize: 15,
                    ),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'Chuyến đi này không còn hiệu lực.',
                    style: TextStyle(color: AppColors.text, fontSize: 13),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    }

    final cur = _currentIndex();

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.timeline, color: AppColors.primary, size: 20),
              SizedBox(width: 8),
              Text(
                'Tiến trình đơn thuê',
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Row(
            children: List.generate(_steps.length * 2 - 1, (i) {
              if (i.isOdd) {
                final stepIndex = i ~/ 2;
                final isDone = cur > stepIndex;
                return Expanded(
                  child: Container(
                    height: 3,
                    color: isDone ? AppColors.primary : AppColors.border,
                  ),
                );
              }
              final stepIndex = i ~/ 2;
              final isDone = cur > stepIndex;
              final isCurrent = cur == stepIndex;
              Color circleColor = AppColors.border;
              Widget iconWidget = Container();

              if (isDone) {
                circleColor = AppColors.primary;
                iconWidget = const Icon(Icons.check, size: 14, color: Colors.white);
              } else if (isCurrent) {
                circleColor = AppColors.primary;
                iconWidget = Container(
                  width: 8,
                  height: 8,
                  decoration: const BoxDecoration(
                    color: Colors.white,
                    shape: BoxShape.circle,
                  ),
                );
              }

              return Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 24,
                    height: 24,
                    decoration: BoxDecoration(
                      color: circleColor,
                      shape: BoxShape.circle,
                    ),
                    child: Center(child: iconWidget),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    _steps[stepIndex]['label']!,
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: isCurrent ? FontWeight.w800 : FontWeight.w500,
                      color: isCurrent
                          ? AppColors.primary
                          : (isDone ? AppColors.text : AppColors.muted),
                    ),
                  ),
                ],
              );
            }),
          ),
        ],
      ),
    );
  }
}

/// Hộp tài chính nổi bật: Tổng tiền, Tiền cọc, Còn lại
class FinancialSummaryBox extends StatelessWidget {
  final num totalAmount;
  final num? depositAmount;
  final num? remainingAmount;
  final String? depositStatus;

  const FinancialSummaryBox({
    super.key,
    required this.totalAmount,
    this.depositAmount,
    this.remainingAmount,
    this.depositStatus,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFFF8FAFC), Color(0xFFEFF6FF)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFBFDBFE)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.account_balance_wallet_outlined, color: AppColors.primary, size: 20),
              SizedBox(width: 8),
              Text(
                'Tóm tắt chi phí',
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Tổng chi phí chuyến đi', style: TextStyle(color: AppColors.muted, fontSize: 13)),
              Text(
                Formatters.vnd(totalAmount),
                style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: AppColors.text),
              ),
            ],
          ),
          if (depositAmount != null) ...[
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    const Text('Tiền cọc quy định', style: TextStyle(color: AppColors.muted, fontSize: 13)),
                    if (depositStatus != null) ...[
                      const SizedBox(width: 6),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                        decoration: BoxDecoration(
                          color: (depositStatus == 'Paid' ? AppColors.success : AppColors.warning).withValues(alpha: 0.15),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: Text(
                          Formatters.paymentStatusLabel(depositStatus!),
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight: FontWeight.w700,
                            color: depositStatus == 'Paid' ? AppColors.success : AppColors.warning,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
                Text(
                  Formatters.vnd(depositAmount!),
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: AppColors.primary),
                ),
              ],
            ),
          ],
          if (remainingAmount != null) ...[
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Divider(height: 1, color: Color(0xFFE2E8F0)),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'Số tiền còn lại khi trả xe',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
                ),
                Text(
                  Formatters.vnd(remainingAmount!),
                  style: const TextStyle(
                    fontWeight: FontWeight.w900,
                    fontSize: 16,
                    color: AppColors.primaryDark,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// Chip thông số xe nhỏ gọn
class VehicleSpecChip extends StatelessWidget {
  final IconData icon;
  final String label;

  const VehicleSpecChip({super.key, required this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: const Color(0xFFF1F5F9),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: AppColors.muted),
          const SizedBox(width: 4),
          Text(
            label,
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.text),
          ),
        ],
      ),
    );
  }
}

/// Tiêu đề phân khu có icon và mô tả
class SectionHeader extends StatelessWidget {
  final IconData icon;
  final String title;
  final String? subtitle;
  final Widget? trailing;

  const SectionHeader({
    super.key,
    required this.icon,
    required this.title,
    this.subtitle,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          padding: const EdgeInsets.all(8),
          decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: 0.1),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Icon(icon, size: 18, color: AppColors.primary),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800)),
              if (subtitle != null) ...[
                const SizedBox(height: 2),
                Text(subtitle!, style: const TextStyle(fontSize: 12, color: AppColors.muted)),
              ],
            ],
          ),
        ),
        ?trailing,
      ],
    );
  }
}


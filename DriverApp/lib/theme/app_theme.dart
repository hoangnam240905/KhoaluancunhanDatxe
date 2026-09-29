import 'package:flutter/material.dart';

class AppPalette extends ThemeExtension<AppPalette> {
  const AppPalette({
    required this.background,
    required this.surface,
    required this.card,
    required this.primary,
    required this.text,
    required this.muted,
    required this.border,
    required this.success,
    required this.danger,
    required this.warning,
  });

  final Color background;
  final Color surface;
  final Color card;
  final Color primary;
  final Color text;
  final Color muted;
  final Color border;
  final Color success;
  final Color danger;
  final Color warning;

  static AppPalette of(BuildContext context) =>
      Theme.of(context).extension<AppPalette>()!;

  @override
  AppPalette copyWith({
    Color? background,
    Color? surface,
    Color? card,
    Color? primary,
    Color? text,
    Color? muted,
    Color? border,
    Color? success,
    Color? danger,
    Color? warning,
  }) =>
      AppPalette(
        background: background ?? this.background,
        surface: surface ?? this.surface,
        card: card ?? this.card,
        primary: primary ?? this.primary,
        text: text ?? this.text,
        muted: muted ?? this.muted,
        border: border ?? this.border,
        success: success ?? this.success,
        danger: danger ?? this.danger,
        warning: warning ?? this.warning,
      );

  @override
  AppPalette lerp(covariant AppPalette? other, double t) {
    if (other == null) return this;
    return AppPalette(
      background: Color.lerp(background, other.background, t)!,
      surface: Color.lerp(surface, other.surface, t)!,
      card: Color.lerp(card, other.card, t)!,
      primary: Color.lerp(primary, other.primary, t)!,
      text: Color.lerp(text, other.text, t)!,
      muted: Color.lerp(muted, other.muted, t)!,
      border: Color.lerp(border, other.border, t)!,
      success: Color.lerp(success, other.success, t)!,
      danger: Color.lerp(danger, other.danger, t)!,
      warning: Color.lerp(warning, other.warning, t)!,
    );
  }
}

class AppTheme {
  static const _seed = Color(0xFF2563EB);

  static ThemeData get light => _build(Brightness.light);
  static ThemeData get dark => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    final scheme = ColorScheme.fromSeed(
      seedColor: _seed,
      brightness: brightness,
    ).copyWith(
      surface: isDark ? const Color(0xFF0B1220) : const Color(0xFFF5F7FA),
      onSurface: isDark ? const Color(0xFFE8EEF8) : const Color(0xFF182230),
      error: isDark ? const Color(0xFFFF8A80) : const Color(0xFFB42318),
    );
    final palette = AppPalette(
      background: scheme.surface,
      surface: isDark ? const Color(0xFF121A2B) : const Color(0xFFFFFFFF),
      card: isDark ? const Color(0xFF121A2B) : const Color(0xFFFFFFFF),
      primary: scheme.primary,
      text: scheme.onSurface,
      muted: scheme.onSurfaceVariant,
      border: scheme.outlineVariant,
      success: isDark ? const Color(0xFF6BD6A0) : const Color(0xFF16794B),
      danger: scheme.error,
      warning: isDark ? const Color(0xFFFFCA72) : const Color(0xFF965B00),
    );

    return ThemeData(
      brightness: brightness,
      colorScheme: scheme,
      extensions: [palette],
      useMaterial3: true,
      scaffoldBackgroundColor: palette.background,
      appBarTheme: AppBarTheme(
        backgroundColor: palette.surface,
        foregroundColor: palette.text,
        elevation: 0,
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: palette.surface,
        indicatorColor: palette.primary.withValues(alpha: 0.16),
        labelTextStyle: WidgetStateProperty.resolveWith((states) {
          final selected = states.contains(WidgetState.selected);
          return TextStyle(
            fontSize: 12,
            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
            color: selected ? palette.primary : palette.muted,
          );
        }),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: palette.surface,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: palette.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: palette.primary, width: 1.6),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: scheme.primary,
          foregroundColor: scheme.onPrimary,
          minimumSize: const Size.fromHeight(50),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
          textStyle: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
        ),
      ),
      cardTheme: CardThemeData(
        color: palette.card,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(color: palette.border),
        ),
      ),
    );
  }
}

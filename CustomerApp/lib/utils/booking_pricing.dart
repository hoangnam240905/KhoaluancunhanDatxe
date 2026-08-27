import 'dart:math' as math;

/// Offline/UX fallback for rental-day display only.
/// Mode-specific totals come from GET /api/bookings/quote (PricingService).
/// Do not send totals to POST /api/bookings.
class BookingPricing {
  static int rentalDays(DateTime start, DateTime end) {
    final totalDays =
        end.difference(start).inMicroseconds / Duration.microsecondsPerDay;
    return math.max(1, totalDays.ceil());
  }

  static double estimate({
    required double pricePerDay,
    required double pricePerKm,
    required DateTime start,
    required DateTime end,
    double? estimatedDistance,
  }) {
    final days = rentalDays(start, end);
    return pricePerDay * days + (estimatedDistance ?? 0) * pricePerKm;
  }
}

class CompleteTripFields {
  const CompleteTripFields({
    required this.odometerKm,
    required this.fuelLevel,
  });

  final double odometerKm;
  final double fuelLevel;

  static const odometerRequired = 'Vui lòng nhập số km hợp lệ.';
  static const odometerInvalid = 'Số km không hợp lệ.';
  static const fuelRequired = 'Vui lòng nhập mức nhiên liệu từ 0 đến 100.';
  static const fuelInvalid = 'Nhiên liệu phải từ 0 đến 100.';

  static ({CompleteTripFields? fields, String? error}) parse(
    String odoRaw,
    String fuelRaw,
  ) {
    final odo = odoRaw.trim().replaceAll(',', '.');
    if (odo.isEmpty) {
      return (fields: null, error: odometerRequired);
    }
    final odometerKm = double.tryParse(odo);
    if (odometerKm == null || odometerKm < 0) {
      return (fields: null, error: odometerInvalid);
    }
    final fuel = fuelRaw.trim().replaceAll(',', '.');
    if (fuel.isEmpty) {
      return (fields: null, error: fuelRequired);
    }
    final fuelLevel = double.tryParse(fuel);
    if (fuelLevel == null || fuelLevel < 0 || fuelLevel > 100) {
      return (fields: null, error: fuelInvalid);
    }
    return (
      fields: CompleteTripFields(
        odometerKm: odometerKm,
        fuelLevel: fuelLevel,
      ),
      error: null,
    );
  }
}

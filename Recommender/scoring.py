"""Locked weighted scoring. Do not invent new weights.

Score = 0.5 * avgRating + 0.3 * bookingCountNormalized + 0.2 * availabilityBonus
estimatedDistance is accepted by the API and is not part of the score.
Customer booking history is annotated only; it does not change Score.
Maintenance due is a hard constraint (isMaintenanceBlocked), not a score term.
"""

from decimal import Decimal, ROUND_HALF_UP

from models import RecommendItem, RecommendRequest, TypeSnapshot, VehicleSnapshot

WEIGHT_RATING = Decimal("0.5")
WEIGHT_BOOKING_COUNT = Decimal("0.3")
WEIGHT_AVAILABILITY = Decimal("0.2")

UNAVAILABLE_STATUSES = frozenset({"Inactive", "Maintenance", "Rented"})


class RecommendError(ValueError):
    pass


def round_away(value: Decimal, places: int) -> Decimal:
    quant = Decimal("1").scaleb(-places)
    return value.quantize(quant, rounding=ROUND_HALF_UP)


def validate(request: RecommendRequest) -> None:
    if request.end_date <= request.start_date:
        raise RecommendError("Thời gian kết thúc phải sau thời gian bắt đầu.")
    if request.seats is not None and request.seats < 1:
        raise RecommendError("Số chỗ không hợp lệ.")
    if request.price_max is not None and request.price_max < 0:
        raise RecommendError("Giá tối đa không hợp lệ.")
    if request.estimated_distance is not None and request.estimated_distance < 0:
        raise RecommendError("Km dự kiến không được âm.")


def is_vehicle_usable(vehicle: VehicleSnapshot) -> bool:
    if vehicle.is_maintenance_blocked:
        return False
    if vehicle.status in UNAVAILABLE_STATUSES:
        return False
    return vehicle.is_calendar_available


def type_passes_hard_filters(item: TypeSnapshot, request: RecommendRequest) -> bool:
    if not item.is_active:
        return False
    if request.seats is not None and item.seat_capacity < request.seats:
        return False
    if request.price_max is not None and item.price_per_day > request.price_max:
        return False
    return True


def recommend(request: RecommendRequest) -> list[RecommendItem]:
    validate(request)

    types = [t for t in request.types if type_passes_hard_filters(t, request)]
    if not types:
        return []

    vehicles_by_type: dict[int, list[VehicleSnapshot]] = {}
    for vehicle in request.vehicles:
        vehicles_by_type.setdefault(vehicle.vehicle_type_id, []).append(vehicle)

    max_count = max((t.booking_count for t in types), default=0)
    preferred = set(request.customer_completed_type_ids)
    scored: list[RecommendItem] = []

    for item in types:
        available_ids = [
            v.vehicle_id
            for v in vehicles_by_type.get(item.vehicle_type_id, [])
            if is_vehicle_usable(v)
        ]
        if not available_ids:
            continue

        normalized = (
            Decimal(0)
            if max_count == 0
            else Decimal(item.booking_count) / Decimal(max_count)
        )
        score = (
            WEIGHT_RATING * item.avg_rating
            + WEIGHT_BOOKING_COUNT * normalized
            + WEIGHT_AVAILABILITY * Decimal(1)
        )
        reasons = ["avgRating", "bookingCount", "availability"]
        if item.vehicle_type_id in preferred:
            reasons.append("customerHistory")

        scored.append(
            RecommendItem(
                vehicle_type_id=item.vehicle_type_id,
                type_name=item.type_name,
                score=round_away(score, 4),
                ranking=0,
                avg_rating=round_away(item.avg_rating, 2),
                price_per_day=item.price_per_day,
                available_count=len(available_ids),
                available_vehicle_ids=sorted(available_ids),
                reasons=reasons,
            )
        )

    scored.sort(key=lambda x: (-x.score, x.vehicle_type_id))
    for index, item in enumerate(scored, start=1):
        item.ranking = index
    return scored

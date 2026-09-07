from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path
import sys

from fastapi.testclient import TestClient

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app import app
from models import RecommendRequest, TypeSnapshot, VehicleSnapshot
from scoring import recommend

client = TestClient(app)

START = datetime(2026, 11, 1, 8, 0, 0, tzinfo=timezone.utc)
END = datetime(2026, 11, 3, 18, 0, 0, tzinfo=timezone.utc)


def type_snap(
    type_id: int,
    name: str,
    seats: int,
    price: str,
    rating: str = "0",
    bookings: int = 0,
    active: bool = True,
) -> TypeSnapshot:
    return TypeSnapshot(
        vehicleTypeId=type_id,
        typeName=name,
        seatCapacity=seats,
        pricePerDay=Decimal(price),
        isActive=active,
        avgRating=Decimal(rating),
        bookingCount=bookings,
    )


def vehicle_snap(
    vehicle_id: int,
    type_id: int,
    status: str = "Available",
    calendar: bool = True,
    maintenance_blocked: bool = False,
) -> VehicleSnapshot:
    return VehicleSnapshot(
        vehicleId=vehicle_id,
        vehicleTypeId=type_id,
        status=status,
        isCalendarAvailable=calendar,
        isMaintenanceBlocked=maintenance_blocked,
    )


def base_request(**kwargs) -> RecommendRequest:
    payload = dict(
        startDate=START,
        endDate=END,
        seats=None,
        priceMax=None,
        estimatedDistance=None,
        types=[
            type_snap(1, "Sedan", 4, "800000", "4.00", 4),
            type_snap(2, "SUV", 7, "1200000", "0", 1),
            type_snap(3, "Van", 16, "2500000", "0", 0),
        ],
        vehicles=[
            vehicle_snap(1, 1),
            vehicle_snap(2, 1),
            vehicle_snap(3, 2),
            vehicle_snap(5, 3),
        ],
        customerCompletedTypeIds=[],
    )
    payload.update(kwargs)
    return RecommendRequest.model_validate(payload)


def test_valid_recommendation_request():
    items = recommend(base_request())
    assert [i.vehicle_type_id for i in items] == [1, 2, 3]
    assert items[0].ranking == 1
    assert items[0].available_vehicle_ids == [1, 2]


def test_hard_filter_inactive_type():
    request = base_request(
        types=[
            type_snap(1, "Sedan", 4, "800000", "4", 4, active=False),
            type_snap(2, "SUV", 7, "1200000", "0", 1),
        ],
        vehicles=[vehicle_snap(1, 1), vehicle_snap(3, 2)],
    )
    items = recommend(request)
    assert [i.vehicle_type_id for i in items] == [2]


def test_unavailable_vehicle_excluded():
    request = base_request(
        types=[type_snap(1, "Sedan", 4, "800000", "0", 0)],
        vehicles=[vehicle_snap(1, 1, calendar=False)],
    )
    assert recommend(request) == []


def test_maintenance_vehicle_excluded():
    request = base_request(
        types=[type_snap(4, "Limo", 9, "3500000", "0", 0)],
        vehicles=[vehicle_snap(6, 4, status="Maintenance", calendar=True)],
    )
    assert recommend(request) == []


def test_maintenance_due_flag_excluded():
    request = base_request(
        types=[type_snap(1, "Sedan", 4, "800000", "0", 0)],
        vehicles=[
            vehicle_snap(1, 1, maintenance_blocked=True),
            vehicle_snap(2, 1, calendar=True),
        ],
    )
    items = recommend(request)
    assert len(items) == 1
    assert items[0].available_vehicle_ids == [2]


def test_insufficient_seats():
    items = recommend(base_request(seats=10))
    assert all(i.vehicle_type_id != 1 for i in items)
    assert any(i.vehicle_type_id == 3 for i in items)


def test_price_max():
    items = recommend(base_request(priceMax=Decimal("900000")))
    assert [i.vehicle_type_id for i in items] == [1]


def test_ranking_and_score():
    items = recommend(base_request())
    sedan = next(i for i in items if i.vehicle_type_id == 1)
    expected = (
        Decimal("0.5") * Decimal("4")
        + Decimal("0.3") * (Decimal(4) / Decimal(4))
        + Decimal("0.2")
    ).quantize(Decimal("0.0001"))
    assert sedan.score == expected
    assert items[0].score >= items[1].score
    assert [i.ranking for i in items] == list(range(1, len(items) + 1))


def test_empty_result():
    request = base_request(
        types=[type_snap(1, "Sedan", 4, "800000")],
        vehicles=[vehicle_snap(1, 1, status="Rented")],
    )
    assert recommend(request) == []


def test_http_valid_and_validation_error():
    payload = base_request().model_dump(by_alias=True, mode="json")
    ok = client.post("/recommend", json=payload)
    assert ok.status_code == 200
    body = ok.json()
    assert "items" in body
    assert body["items"][0]["vehicleTypeId"] == 1

    bad = client.post(
        "/recommend",
        json={**payload, "endDate": payload["startDate"]},
    )
    assert bad.status_code == 400
    assert "message" in bad.json()


def test_health():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"


def test_customer_history_does_not_change_score():
    without = recommend(base_request())
    with_history = recommend(base_request(customerCompletedTypeIds=[2, 2]))
    by_id_without = {i.vehicle_type_id: i.score for i in without}
    by_id_with = {i.vehicle_type_id: i.score for i in with_history}
    assert by_id_without == by_id_with
    suv = next(i for i in with_history if i.vehicle_type_id == 2)
    assert "customerHistory" in suv.reasons

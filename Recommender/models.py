from decimal import Decimal
from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field


class TypeSnapshot(BaseModel):
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    vehicle_type_id: int = Field(alias="vehicleTypeId")
    type_name: str = Field(alias="typeName")
    seat_capacity: int = Field(alias="seatCapacity")
    price_per_day: Decimal = Field(alias="pricePerDay")
    is_active: bool = Field(alias="isActive")
    avg_rating: Decimal = Field(alias="avgRating")
    booking_count: int = Field(alias="bookingCount")


class VehicleSnapshot(BaseModel):
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    vehicle_id: int = Field(alias="vehicleId")
    vehicle_type_id: int = Field(alias="vehicleTypeId")
    status: str
    is_calendar_available: bool = Field(alias="isCalendarAvailable")
    is_maintenance_blocked: bool = Field(default=False, alias="isMaintenanceBlocked")


class RecommendRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    start_date: datetime = Field(alias="startDate")
    end_date: datetime = Field(alias="endDate")
    seats: int | None = None
    price_max: Decimal | None = Field(default=None, alias="priceMax")
    estimated_distance: Decimal | None = Field(default=None, alias="estimatedDistance")
    types: list[TypeSnapshot]
    vehicles: list[VehicleSnapshot]
    customer_completed_type_ids: list[int] = Field(
        default_factory=list, alias="customerCompletedTypeIds"
    )


class RecommendItem(BaseModel):
    model_config = ConfigDict(populate_by_name=True, serialize_by_alias=True)

    vehicle_type_id: int = Field(alias="vehicleTypeId")
    type_name: str = Field(alias="typeName")
    score: Decimal
    ranking: int
    avg_rating: Decimal = Field(alias="avgRating")
    price_per_day: Decimal = Field(alias="pricePerDay")
    available_count: int = Field(alias="availableCount")
    available_vehicle_ids: list[int] = Field(alias="availableVehicleIds")
    reasons: list[str]


class RecommendResponse(BaseModel):
    items: list[RecommendItem]

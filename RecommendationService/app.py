# ============================================================
# Recommendation Service — FastAPI wrapper around V7 engine
# ============================================================

from __future__ import annotations

from contextlib import asynccontextmanager
from typing import Any

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field, field_validator

from recommender import CarRecommendationEngine

engine: CarRecommendationEngine | None = None


@asynccontextmanager
async def lifespan(_: FastAPI):
    global engine
    engine = CarRecommendationEngine()
    yield


app = FastAPI(
    title="Car Rental Recommendation Service",
    description="V7 ML-Assisted Content-Based Recommendation (Step 1 — standalone)",
    version="1.0.0",
    lifespan=lifespan,
)


class HistoryItem(BaseModel):
    car_id: str | None = None
    seats: int | None = Field(default=None, ge=1)
    weight: float = Field(..., ge=0)


class RecommendRequest(BaseModel):
    age: int = Field(..., ge=16, le=100)
    kids: int = Field(..., ge=0)
    family_size: int = Field(..., ge=1)
    income_bracket: str = Field(..., min_length=1)
    city_type: str = Field(..., min_length=1)
    commute_km_day: float = Field(..., ge=0)
    charging_access: int = Field(..., ge=0, le=1)
    annual_km: float = Field(..., ge=0)
    usage_primary: str = Field(..., min_length=1)
    budget_monthly: float = Field(..., ge=0)
    preferences_text: str = Field(..., min_length=1)
    top_k: int = Field(default=5, ge=1, le=50)
    # Optional rental history (extension). Omitted / weight 0 -> original V7 ranking.
    history: list[HistoryItem] = Field(default_factory=list, max_length=100)
    history_weight: float = Field(default=0.0, ge=0, le=1)

    @field_validator("income_bracket", "city_type", "usage_primary", "preferences_text")
    @classmethod
    def strip_nonempty(cls, value: str) -> str:
        cleaned = value.strip()
        if not cleaned:
            raise ValueError("must not be blank")
        return cleaned


class RecommendResponse(BaseModel):
    success: bool
    predicted_segment: str
    recommendations: list[dict[str, Any]]
    train_count: int | None = None


@app.get("/health")
def health() -> dict[str, str]:
    if engine is None or not engine.is_ready:
        raise HTTPException(status_code=503, detail="Engine not ready")
    return {"status": "ok"}


@app.post("/recommend", response_model=RecommendResponse)
def recommend(body: RecommendRequest) -> RecommendResponse:
    if engine is None or not engine.is_ready:
        raise HTTPException(status_code=503, detail="Engine not ready")

    user = body.model_dump()
    top_k = user.pop("top_k")
    history = user.pop("history")
    history_weight = user.pop("history_weight")

    try:
        result = engine.recommend(user, top_k=top_k, history=history, history_weight=history_weight)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:  # noqa: BLE001 — surface as 400, do not crash process
        raise HTTPException(status_code=400, detail=f"Recommendation failed: {exc}") from exc

    return RecommendResponse(
        success=True,
        predicted_segment=result["predicted_segment"],
        recommendations=result["recommendations"],
        train_count=result["train_count"],
    )

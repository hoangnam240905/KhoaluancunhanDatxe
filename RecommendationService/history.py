# ============================================================
# Rental-history boost (extension on top of V7, not part of V7)
# ============================================================
# Final (with history) = (1 - w) * V7 Final Score + w * History Affinity
#
# - The Random Forest is NOT retrained: the synthetic dataset has no rental history.
# - w = 0 (default) or empty history -> exactly the original V7 ranking (cold-start safe).
# - History Affinity of a car = weighted share of the customer's past rentals that
#   match it: same segment when the past car_id is in the catalog, otherwise same seats.
# ============================================================

from __future__ import annotations

from collections import defaultdict
from typing import Any, Iterable, Mapping

import numpy as np


def history_affinity(
    car_segments: Iterable[str],
    car_seats: Iterable[int],
    history: Iterable[Mapping[str, Any]],
    segment_of_car_id: Mapping[str, str],
) -> np.ndarray:
    segments = list(car_segments)
    seats = list(car_seats)
    segment_weight: dict[str, float] = defaultdict(float)
    seats_weight: dict[int, float] = defaultdict(float)
    total = 0.0

    for item in history:
        weight = float(item.get("weight") or 0)
        if weight <= 0:
            continue
        total += weight
        segment = segment_of_car_id.get(str(item.get("car_id") or ""))
        if segment:
            segment_weight[segment] += weight
        elif item.get("seats"):
            seats_weight[int(item["seats"])] += weight

    if total <= 0:
        return np.zeros(len(segments))

    return np.array(
        [(segment_weight[seg] + seats_weight[int(seat)]) / total for seg, seat in zip(segments, seats)]
    )


def blend(v7_scores: np.ndarray, affinity: np.ndarray, weight: float) -> np.ndarray:
    if weight <= 0 or not np.any(affinity):
        return v7_scores
    return (1 - weight) * v7_scores + weight * affinity

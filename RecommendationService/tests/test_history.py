"""Unit tests for the rental-history boost (no dataset download needed).

Run from RecommendationService/:  py -m pytest tests -q
"""

import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from history import blend, history_affinity  # noqa: E402

SEGMENTS = ["Sedan", "SUV", "Mini", "Sedan"]
SEATS = [5, 7, 4, 5]
CATALOG = {"car_1": "Sedan", "car_2": "SUV"}


def test_empty_history_gives_zero_affinity_and_pure_v7():
    v7 = np.array([0.9, 0.5, 0.4, 0.3])
    affinity = history_affinity(SEGMENTS, SEATS, [], CATALOG)
    assert np.array_equal(affinity, np.zeros(4))
    assert np.array_equal(blend(v7, affinity, 0.2), v7)


def test_weight_zero_keeps_v7_even_with_history():
    v7 = np.array([0.9, 0.5, 0.4, 0.3])
    affinity = history_affinity(SEGMENTS, SEATS, [{"car_id": "car_2", "weight": 1}], CATALOG)
    assert np.array_equal(blend(v7, affinity, 0.0), v7)


def test_segment_of_rented_catalog_car_is_boosted():
    affinity = history_affinity(SEGMENTS, SEATS, [{"car_id": "car_2", "weight": 1}], CATALOG)
    assert affinity.tolist() == [0, 1, 0, 0]  # only the SUV matches


def test_unknown_car_falls_back_to_seats():
    affinity = history_affinity(SEGMENTS, SEATS, [{"car_id": None, "seats": 5, "weight": 1}], CATALOG)
    assert affinity.tolist() == [1, 0, 0, 1]  # both 5-seaters


def test_weights_are_shared_proportionally():
    history = [{"car_id": "car_1", "weight": 3}, {"car_id": "car_2", "weight": 1}]
    affinity = history_affinity(SEGMENTS, SEATS, history, CATALOG)
    assert np.allclose(affinity, [0.75, 0.25, 0, 0.75])


def test_history_can_reorder_ranking():
    v7 = np.array([0.60, 0.55, 0.10, 0.20])  # Sedan first under pure V7
    affinity = history_affinity(SEGMENTS, SEATS, [{"car_id": "car_2", "weight": 1}], CATALOG)
    final = blend(v7, affinity, 0.2)
    assert int(np.argmax(final)) == 1  # SUV moves to the top


def test_zero_or_negative_weights_are_ignored():
    affinity = history_affinity(SEGMENTS, SEATS, [{"car_id": "car_2", "weight": 0}], CATALOG)
    assert not np.any(affinity)

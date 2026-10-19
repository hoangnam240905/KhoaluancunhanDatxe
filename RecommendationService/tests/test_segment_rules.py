"""Unit tests for the segment rules (no dataset download needed).

Run from RecommendationService/:  py -m pytest tests -q
"""

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from segment_rules import rule_segment, rule_segments  # noqa: E402


@pytest.mark.parametrize(
    ("kids", "family", "usage", "expected"),
    [
        (2, 5, "trips", "7-Seater"),
        (1, 3, "work", "Crossover"),
        (0, 6, "trips", "Crossover"),  # V7 ignores a big party without kids
        (0, 2, "work", "Sedan"),
        (0, 2, "city", "Supermini"),
        (0, 2, "family", "Crossover"),
    ],
)
def test_v7_rules_are_the_original_ones(kids, family, usage, expected):
    assert rule_segment(kids, family, usage) == expected
    assert rule_segment(kids, family, usage, "v7") == expected


@pytest.mark.parametrize(
    ("family", "usage", "expected"),
    [
        (5, "city", "7-Seater"),  # party size beats trip type
        (6, "trips", "7-Seater"),
        (4, "trips", "SUV"),
        (2, "Work", "Sedan"),
        (1, "city", "Supermini"),
        (4, "family", "Crossover"),
    ],
)
def test_travel_rules_use_party_size_then_trip_type(family, usage, expected):
    assert rule_segment(0, family, usage, "travel") == expected


def test_vectorised_matches_scalar():
    kids, family, usage = [0, 2, 0], [6, 3, 2], ["city", "work", "trips"]
    for rules in ("v7", "travel"):
        got = rule_segments(kids, family, usage, rules).tolist()
        assert got == [rule_segment(k, f, u, rules) for k, f, u in zip(kids, family, usage)]


def test_unknown_rules_rejected():
    with pytest.raises(ValueError):
        rule_segment(0, 2, "city", "buyer")

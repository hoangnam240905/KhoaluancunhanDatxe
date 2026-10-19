"""
Target segment for the Structured part of V7 (extension, plan1.md 4.3).

- "v7"     : original car-buyer rules (kids > 0 ...) — default, keeps V7 unchanged.
- "travel" : rules for a rental trip: party size first, then trip type.
"""

from __future__ import annotations

import numpy as np

SEGMENT_RULES = ("v7", "travel")


def rule_segments(kids, family_size, usage, rules: str = "v7") -> np.ndarray:
    """Vectorised: one target segment per user (arrays or scalars)."""
    if rules not in SEGMENT_RULES:
        raise ValueError(f"segment_rules must be one of {SEGMENT_RULES}")

    kids = np.atleast_1d(np.asarray(kids, dtype=int))
    family = np.atleast_1d(np.asarray(family_size, dtype=int))
    usage = np.char.lower(np.atleast_1d(np.asarray(usage, dtype=str)))

    if rules == "travel":
        conditions = [family >= 5, usage == "trips", usage == "work", usage == "city"]
        choices = ["7-Seater", "SUV", "Sedan", "Supermini"]
    else:
        conditions = [(kids > 0) & (family >= 5), kids > 0, usage == "work", usage == "city"]
        choices = ["7-Seater", "Crossover", "Sedan", "Supermini"]
    return np.select(conditions, choices, default="Crossover")


def rule_segment(kids: int, family_size: int, usage: str, rules: str = "v7") -> str:
    return str(rule_segments(kids, family_size, usage, rules)[0])

"""Synthetic football event generation."""
from __future__ import annotations

import random
from dataclasses import dataclass

TEAMS = ("Home", "Away")
EVENT_TYPES = ("pass", "tackle", "shot", "goal", "foul", "corner")


@dataclass(frozen=True)
class Event:
    id: int
    minute: int
    team: str
    type: str
    player: int
    x: float  # 0-100 along pitch, 100 = attacked goal line
    y: float  # 0-100 across pitch, 50 = centre


def generate_match(seed: int = 0, n_events: int = 300) -> list[Event]:
    """Deterministically generate a synthetic match for the given seed."""
    rng = random.Random(seed)
    strength = {"Home": rng.uniform(0.8, 1.2), "Away": rng.uniform(0.8, 1.2)}
    minutes = sorted(rng.randint(0, 89) for _ in range(n_events))
    events = []
    for i, minute in enumerate(minutes):
        team = rng.choices(TEAMS, weights=[strength[t] for t in TEAMS])[0]
        etype = rng.choices(EVENT_TYPES, weights=[50, 15, 12, 0, 8, 5])[0]
        x = rng.uniform(0, 100)
        if etype == "shot":
            x = rng.uniform(65, 99)
        y = min(100.0, max(0.0, rng.gauss(50, 20)))
        if etype == "shot" and _shot_xg(x, y) > rng.random():
            etype = "goal"
        events.append(Event(i, minute, team, etype, rng.randint(1, 11),
                            round(x, 1), round(y, 1)))
    return events


def _shot_xg(x: float, y: float) -> float:
    from .agents import shot_xg
    return shot_xg(x, y)

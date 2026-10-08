"""Explainable analysis agents.

Each agent reads events and emits Insights that carry a human-readable
explanation and the ids of the events used as evidence.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field
from typing import Callable, Iterable

from .events import Event, TEAMS


@dataclass
class Insight:
    agent: str
    headline: str
    explanation: str
    evidence: list[int] = field(default_factory=list)


def shot_xg(x: float, y: float) -> float:
    """Simple expected-goals model: closer and more central is better."""
    dist = math.hypot(100 - x, (y - 50) * 0.68)
    return max(0.01, min(0.95, 0.5 * math.exp(-dist / 12)))


def shot_quality_agent(events: Iterable[Event]) -> list[Insight]:
    xg = {t: 0.0 for t in TEAMS}
    goals = {t: 0 for t in TEAMS}
    shots = {t: [] for t in TEAMS}
    for e in events:
        if e.type in ("shot", "goal"):
            xg[e.team] += shot_xg(e.x, e.y)
            shots[e.team].append(e.id)
            goals[e.team] += e.type == "goal"
    out = []
    for t in TEAMS:
        diff = goals[t] - xg[t]
        verdict = ("overperformed" if diff > 0.5 else
                   "underperformed" if diff < -0.5 else "performed in line with")
        out.append(Insight(
            "shot_quality",
            f"{t} {verdict} chance quality",
            f"{t} scored {goals[t]} from {len(shots[t])} shots with {xg[t]:.2f} "
            f"expected goals (xG); xG is based on shot distance and angle.",
            shots[t]))
    return out


def momentum_agent(events: Iterable[Event], window: int = 15) -> list[Insight]:
    weights = {"pass": 1, "shot": 4, "goal": 8, "corner": 3, "tackle": 1, "foul": -1}
    events = list(events)
    best = None
    for start in range(0, 90 - window + 1, 5):
        win = [e for e in events if start <= e.minute < start + window]
        score = {t: sum(weights[e.type] for e in win if e.team == t) for t in TEAMS}
        gap = abs(score["Home"] - score["Away"])
        if best is None or gap > best[0]:
            leader = max(TEAMS, key=score.get)
            best = (gap, start, leader, score, [e.id for e in win if e.team == leader])
    if best is None or best[0] == 0:
        return []
    gap, start, leader, score, ev = best
    return [Insight(
        "momentum",
        f"{leader} dominated minutes {start}-{start + window}",
        f"Weighted activity (shots, corners, passes) was {score[leader]} vs "
        f"{score[next(t for t in TEAMS if t != leader)]}, the largest gap in any "
        f"{window}-minute window.", ev)]


def discipline_agent(events: Iterable[Event]) -> list[Insight]:
    fouls = {t: [] for t in TEAMS}
    for e in events:
        if e.type == "foul":
            fouls[e.team].append(e.id)
    worst = max(TEAMS, key=lambda t: len(fouls[t]))
    if len(fouls["Home"]) == len(fouls["Away"]):
        return []
    return [Insight(
        "discipline", f"{worst} committed more fouls",
        f"{worst} had {len(fouls[worst])} fouls vs "
        f"{len(fouls[next(t for t in TEAMS if t != worst)])}.", fouls[worst])]


AGENTS: list[Callable[[Iterable[Event]], list[Insight]]] = [
    shot_quality_agent, momentum_agent, discipline_agent]


def analyze(events: Iterable[Event]) -> list[Insight]:
    events = list(events)
    return [i for agent in AGENTS for i in agent(events)]

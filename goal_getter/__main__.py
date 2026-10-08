import argparse

from . import analyze, generate_match


def main() -> None:
    p = argparse.ArgumentParser(description="Explainable match intelligence")
    p.add_argument("--seed", type=int, default=0)
    a = p.parse_args()
    events = generate_match(a.seed)
    goals = {t: sum(e.type == "goal" and e.team == t for e in events)
             for t in ("Home", "Away")}
    print(f"Match (seed {a.seed}): Home {goals['Home']} - {goals['Away']} Away\n")
    for i in analyze(events):
        print(f"[{i.agent}] {i.headline}\n  why: {i.explanation}\n"
              f"  evidence: {len(i.evidence)} events (ids {i.evidence[:5]}...)\n")


if __name__ == "__main__":
    main()

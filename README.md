# goal-getter
Ingesting and analyzing live football match events

## Explainable match intelligence
Generates synthetic match events (`goal_getter/events.py`) and runs analysis
agents (`goal_getter/agents.py`: shot quality/xG, momentum, discipline). Every
insight includes a plain-language explanation and the evidence event ids.

    python -m goal_getter --seed 2
    python -m unittest discover -s tests

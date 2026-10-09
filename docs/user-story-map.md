# GoalGetter MVP: User Story Map

GoalGetter turns synthetic football events into explainable match intelligence, real-time narratives and automated recaps. Football stories do not start and end with the scoreline, so no story below only reports a score or a raw count.

Items labelled **Assumption** are unvalidated and need a check before they are relied on.

## Contents

1. [Personas](#1-personas)
2. [Framing](#2-framing)
3. [Story map](#3-story-map)
4. [MVP stories](#4-mvp-stories)
5. [Brief coverage](#5-brief-coverage)
6. [Judging coverage](#6-judging-coverage)
7. [Out of scope](#7-out-of-scope)
8. [Open questions](#8-open-questions)

## 1. Personas

All personas are **Assumptions** to be validated.

| Persona | Role | Primary need |
|---|---|---|
| Casual Camila | Casual fan who follows one club | Understand what just happened and why it matters, in her language |
| Analytical Andre | Data-hungry analyst | Pass and shot quality, control vs. chaos, and the evidence behind every claim |
| Producer Priya (secondary) | Overlay consumer in a studio or stream | Timed, machine-readable overlays she can render next to the live feed |

**Segment:** Football viewers who watch a live (synthetic) match on a second screen, a stream or a studio feed. Both modes use one pipeline.

**First release:** Camila and Andre as two modes of one experience. Priya is served through the overlay feed.

## 2. Framing

**Problem.** Camila and Andre follow a match but only get the scoreline and raw counts. They cannot see why a moment mattered, and they cannot tailor it to their club, player, mode or language. Producers have no machine-readable insight to render beside the play.

**Jobs to be done (Assumption)**

| Type | Job |
|---|---|
| Functional | Follow the match, understand key moments, track my player or club |
| Social | Sound informed with friends or on air |
| Emotional | Feel the match was made for me, and not miss the turning point |

**Epic hypothesis.** If we turn synthetic match events into explainable, personalized intelligence, rendered as timed overlays and a recap, then casual fans and analysts will each get a distinct experience from one pipeline.

**Validation measures (to be defined):**

- Event-to-overlay latency is under 2 s.
- Every recap turning point traces to source events.
- Viewers rate the two modes as meaningfully different.

**Narrative.** Follow a live match, understand why its key moments matter, and relive it through my own mode, club, player and language.

## 3. Story map

Release lines:

- **R1 (MVP):** a thin end-to-end slice that covers both Render and Personalize. It is built as an English walking skeleton first, then Spanish is added.
- **R2:** depth.
- **R3:** reach.

| Release | 1. Tune in and tailor | 2. Follow live | 3. Understand why | 4. Follow my player and club | 5. Relive the match |
|---|---|---|---|---|---|
| **R1 (MVP)** | Synthetic match stream; Casual or Analyst mode; club and player; English, then Spanish | Player tags; pass distance and accuracy; shot speed; timed overlay under 2 s | Key-moment detection; why-it-matters line; evidence link; control vs. chaos | Player-focus feed; club-filtered narrative | Full-time recap with turning points, in English then Spanish |
| **R2** | Scenario library; mid-match mode switch; single-metric view | Speed and distance thresholds; pass difficulty; ball speed; overlay contract hardening | Milestones; tactical pressure shifts; game rhythm | Player performance context; threshold alerts | Halftime summary; recap shaped by mode and focus; broadcast-ready export |
| **R3** | Saved profiles | Auto-eventing from video; producer controls | Cross-match context | Multi-player comparison | Social highlights |

### Activity 1: Tune in and tailor my view

| Step | Task | Rel. | Stage |
|---|---|---|---|
| 1.1 Start the match | Start a match with fixture metadata (teams, lineups, kickoff) | R1 | Ingest |
| | Play events back at live pace with football-realistic coordinates and timestamps | R1 | Ingest |
| | Pick a scenario (comeback, red card, dominant but losing) | R2 | Ingest |
| 1.2 Pick my mode | Choose Casual or Analyst mode | R1 | Personalize |
| | Switch mode mid-match without losing context | R2 | Personalize |
| 1.3 Pick what I follow | Use English (walking skeleton default) | R1 | Personalize |
| | Switch to Spanish | R1 | Personalize |
| | Choose a favorite club and player | R1 | Personalize |
| | Choose a single metric to follow | R2 | Personalize |
| | Save the profile across matches | R3 | Personalize |

### Activity 2: Follow the match live

| Step | Task | Rel. | Stage |
|---|---|---|---|
| 2.1 See who is involved | Tag the player on every event | R1 | Ingest / Interpret |
| | Show a speed or distance indicator when a threshold is reached | R2 | Interpret |
| 2.2 See pass and shot quality | Show pass distance and accuracy | R1 | Interpret |
| | Show shot speed | R1 | Interpret |
| | Show pass difficulty rating and ball speed | R2 | Interpret |
| | Auto-event from video | R3 | Ingest |
| 2.3 See it on screen | Emit timed, machine-readable overlay messages within 2 s | R1 | Render |
| | Harden the overlay contract for the rendering partner | R2 | Render |
| | Producer controls (approve or suppress an overlay) | R3 | Render |

### Activity 3: Understand why moments matter

| Step | Task | Rel. | Stage |
|---|---|---|---|
| 3.1 Spot the key moment | Detect goals, big chances, turnovers and pressure spikes | R1 | Interpret |
| | Detect player and team milestones | R2 | Interpret |
| 3.2 Read the why | Add a why-it-matters line to each key moment | R1 | Explain |
| | Link each explanation to its source events | R1 | Explain |
| 3.3 Sense the match's shape | Show a control vs. chaos indicator | R1 | Explain |
| | Show tactical pressure shifts and game rhythm patterns | R2 | Explain |

### Activity 4: Follow my player and club

| Step | Task | Rel. | Stage |
|---|---|---|---|
| 4.1 Focus on my player | Show a feed of that player's moments | R1 | Personalize |
| | Show performance context against the match baseline | R2 | Explain |
| | Send threshold alerts for that player | R2 | Personalize |
| 4.2 Follow my club | Show a club-filtered narrative | R1 | Personalize |
| 4.3 Compare | Compare two players | R3 | Personalize |

### Activity 5: Relive the match

| Step | Task | Rel. | Stage |
|---|---|---|---|
| 5.1 Get the recap | Generate a full-time recap with turning points and reasons | R1 | Explain / Render |
| | Generate a halftime summary | R2 | Render |
| 5.2 Get it my way | Deliver the recap in English, then Spanish | R1 | Personalize |
| | Shape the recap by my mode and focus | R2 | Personalize |
| 5.3 Reuse | Export a broadcast-ready script and JSON | R2 | Render |
| | Create social highlight cards | R3 | Render |

## 4. MVP stories

Each story has one When and one Then. Delivery order inside R1 is: walking skeleton in English (US-01 to US-10, US-12, with US-11 English), then Spanish (US-11 Spanish).

### User Story US-01: Watch a football-realistic match unfold live

- **Pipeline stage:** Ingest | **Capability:** Data innovation
- **As** Casual Camila, **I want** a match to play out from realistic synthetic events **so that** the insights react to real football patterns.
- **Scenario:** Live synthetic match
- **Given** a fixture with lineups and metadata, and events with IDs, timestamps and pitch coordinates (passes, shots, tackles, possession changes, pressure)
- **When** I start the match
- **Then** events arrive in match-clock order at live pace.

### User Story US-02: See who is on the ball

- **Pipeline stage:** Ingest, Interpret | **Capability:** Player identification
- **As** Casual Camila, **I want** each event tagged with the player's name **so that** I know who did what.
- **Scenario:** Player tag
- **Given** an incoming event with a player ID
- **When** the event is processed
- **Then** it shows the player's name and team.

### User Story US-03: Judge a pass by distance and accuracy

- **Pipeline stage:** Interpret | **Capability:** Pass quality
- **As** Analytical Andre, **I want** pass distance and running accuracy **so that** I can judge a player's passing.
- **Scenario:** Pass quality
- **Given** completed and failed passes with start and end coordinates
- **When** a pass event arrives
- **Then** its distance (m) and the passer's accuracy (%) update on screen.

### User Story US-04: See how hard the shot was struck

- **Pipeline stage:** Interpret | **Capability:** Speed metrics
- **As** Casual Camila, **I want** shot speed on each shot **so that** I feel how big the chance was.
- **Scenario:** Shot speed
- **Given** a shot event with ball-speed data
- **When** the shot is processed
- **Then** the speed (km/h) is shown with the shot.

### User Story US-05: Know why a moment matters

- **Pipeline stage:** Explain | **Capability:** Narrative generation, Explainability
- **As** Casual Camila, **I want** a short reason beside each key moment **so that** I understand it beyond "a shot happened".
- **Scenario:** Why it matters
- **Given** a detected key moment (for example a big chance after a turnover and a pressure spike)
- **When** it is detected
- **Then** a one-sentence explanation of its cause and impact appears, not only the event type.

### User Story US-06: Check the evidence behind a claim

- **Pipeline stage:** Explain | **Capability:** Explainability
- **As** Analytical Andre, **I want** each explanation linked to its source events **so that** I can trust it.
- **Scenario:** Evidence link
- **Given** an explanation on screen
- **When** I open its evidence
- **Then** I see the event IDs and values that support every claim in it.

### User Story US-07: See control vs. chaos

- **Pipeline stage:** Interpret, Explain | **Capability:** Explainability
- **As** Analytical Andre, **I want** a control-vs-chaos indicator with its reason **so that** I can read the match's shape.
- **Scenario:** Match shape
- **Given** a rolling window of possession, pass and pressure events
- **When** the window updates
- **Then** the indicator shows control, balanced or chaos with its main driver, for example "Team A under high press, 4 turnovers in 3 min".

### User Story US-08: Drop insights onto the live feed

- **Pipeline stage:** Render | **Capability:** Real-time intelligence
- **As** Producer Priya, **I want** timed, machine-readable overlay messages **so that** I can render them next to the match.
- **Scenario:** Overlay feed
- **Given** a running match and a consumer subscribed to the overlay stream
- **When** an insight is produced
- **Then** a schema-valid message with match clock, type, text and language reaches the consumer within 2 s of the source event.

### User Story US-09: Choose my mode

- **Pipeline stage:** Personalize | **Capability:** Personalization
- **As** Casual Camila or Analytical Andre, **I want** to pick Casual or Analyst mode **so that** the same moment fits how I watch.
- **Scenario:** Two modes
- **Given** the same key moment
- **When** I select a mode
- **Then** Casual shows a plain-language story, and Analyst shows the story plus its metrics (pass distance, accuracy, shot speed, indicator).

### User Story US-10: Follow my club and player

- **Pipeline stage:** Personalize | **Capability:** Personalization
- **As** Casual Camila, **I want** to pick a favorite club and player **so that** my feed centers on them.
- **Scenario:** Player focus
- **Given** I selected a club and a player
- **When** an event involves them
- **Then** it is surfaced first in my feed with its explanation.

### User Story US-11: Read it in my language

- **Pipeline stage:** Personalize | **Capability:** Multi-language storytelling
- **As** Casual Camila, **I want** narratives in English or Spanish **so that** the story is clear in my market.
- **Scenario:** Language choice
- **Given** a key moment with an explanation
- **When** I select a language
- **Then** the narrative appears in that language, and player and club names remain correct.

**Delivery sequence:** English is the default in the walking skeleton. Spanish is the next increment in the MVP.

### User Story US-12: Get a recap that is more than the score

- **Pipeline stage:** Explain, Render | **Capability:** Narrative generation
- **As** Casual Camila, **I want** an automatic full-time recap **so that** I can relive the turning points.
- **Scenario:** Full-time recap
- **Given** the match has ended and key moments are stored
- **When** full time is reached
- **Then** a recap in my language lists the turning points with reasons, and each one links to evidence.

**Splitting check:** US-12 has one When and one Then. Recaps shaped by mode and focus are a separate R2 story. US-05 and US-11 need no split.

## 5. Brief coverage

| Pipeline stage / capability | Stories | Release |
|---|---|---|
| Ingest | US-01, US-02 | R1 |
| Interpret | US-02, US-03, US-04, US-07 | R1 |
| Explain | US-05, US-06, US-07 | R1 |
| Render | US-08, US-12 | R1 |
| Personalize | US-09, US-10, US-11 | R1 |
| Player identification | US-02 | R1 (speed and distance thresholds in R2) |
| Pass quality | US-03 | R1 (difficulty rating in R2) |
| Speed metrics | US-04 | R1 (ball speed in R2) |
| Auto-eventing from video | none | Deferred to R3, because the MVP uses synthetic events and needs no video model |
| Narrative generation | US-05, US-12 | R1 (milestones and performance context in R2) |
| Explainability | US-05, US-06, US-07 | R1 (pressure shift and rhythm in R2) |
| Data innovation | US-01 | R1 (scenario library in R2) |
| Multi-language | US-11, US-12 | R1 (English, then Spanish) |

## 6. Judging coverage

| Criterion | Status | Supporting stories and notes |
|---|---|---|
| Stage One: theme fit | Pass | Every story traces to a pipeline stage |
| Stage One: required technology | To be decided | Selected in the technical deep-dive |
| Technological Implementation | Strong | US-01 (synthetic data) and US-08 (schema contract). Tests are in each acceptance criterion. |
| Agentic Design and Innovation | Partial | US-05, US-06 and US-12 need grounded AI. No story defines orchestration yet, so add one in R2. |
| Real-World Impact | Partial | US-08 and US-11 serve studio and global scenarios. Production deployability is not covered by a story. |
| User Experience and Presentation | Partial | US-09 and US-10 give two distinct experiences. The presentation story is not mapped yet. |
| Adherence to Category | Strong | The full pipeline is covered end to end |

## 7. Out of scope

- A third language (decided: English first, then Spanish only).
- Auto-eventing from video (R3).
- Timeline, deadline and demo-video planning (the focus is outcome and value delivery).
- Technology and hero-technology selection (decided in the technical deep-dive).

## 8. Open questions

1. What are the synthetic event fields and the rendering partner's overlay contract? These are not known yet. Until they are, US-01 and US-08 stay provisional.

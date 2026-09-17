# Puzzles of the Forest — Game Design Document

> Generated from the current repository state (code + scene wiring), not from a pre-existing design
> doc — this project didn't have one. Treat it as a snapshot of what is actually implemented as of
> commit `a402ff70`. Where the code suggests something planned-but-unfinished, it's called out
> explicitly instead of silently smoothed over.

## 1. Elevator Pitch

A solo-developed, systems-first hex tile-placement puzzle game. Draw a tile from a fixed deck,
place it next to your existing board, and shape connected regions into animal **habitats** for
score. Habitats are efficiency puzzles: the fewer tiles you spend to satisfy an animal's biome
requirement, the more points per tile you get. A single run lasts until the deck runs dry.

Target: a small, polished **premium indie release on Steam** (~15 PLN), not a live-service or
long-tail idle game — despite the working repo name "Idle Forest," there is no idle/incremental
mechanic in the current build. It's a roguelite-lite puzzle game with perks and quests layered on
top of a deterministic placement puzzle.

## 2. Current Status

- Gameplay-complete alpha: placement, habitats, scoring, deck, reroll, perks, and quests all work
  end-to-end.
- Perks are the acknowledged rough edge — six exist, and they need balance/UX polish, not new
  architecture.
- No meta-progression between runs (no currency carried over, no unlocks, no save file beyond
  `PlayerPrefs.Save()` on quit). Each run starts from a clean slate.
- Scope is intentionally closed: the goal is to finish and polish the existing loop for a Steam
  release, not expand it.

## 3. Core Loop

```
Main Menu → PLAY → Session (30-tile deck + free center tile)
                       │
                       ├─ draw next tile → place / reroll
                       ├─ tile placement → habitat classification (automatic)
                       ├─ habitat formed → score + deck reward + quest progress
                       │        └─ every 5th habitat → perk draft (choose 1 of 3)
                       └─ deck empties → Game Over (score, habitat count, largest habitat)
                                            │
                                            ├─ RESTART → new session
                                            └─ MAIN MENU
```

One session = one 30-tile deck (`GameFlowController.SessionTileCount`). There is no run counter,
no chapter/stage structure, and no persistent player level — "the game" is one arbitrarily
repeatable session type, differentiated only by the random deck order, quest offers, and perk
draft RNG.

## 4. Session Flow, Step by Step

1. **Main Menu**: PLAY / HOW TO PLAY / QUIT.
2. **Session start** (`GameFlowController.StartSession`):
   - Camera resets, board is cleared, HUD and quest HUD become visible.
   - `PerkManager.OnSessionStart()` fires session-start perk hooks (e.g. Seed Bank, Mulligan).
   - Deck is rebuilt (30 tiles, weighted-random biome pool, reshuffled) and habitat-reward hookup
     is (re)configured (+3 tiles per habitat by default).
   - A free starting tile is placed at the grid center — **does not consume a deck card**.
   - Quests reset for the new session.
3. **Turn loop** (no explicit "turns" — it's continuous):
   - HUD shows the next tile to be placed (icon, name, queue length) and current score/habitat
     count.
   - Player places the current tile on any empty cell adjacent to an already-occupied tile
     (`TileAvailabilityService` computes legal cells).
   - Placing a tile draws the next one from the deck queue.
   - Reroll: replaces the *current* (not-yet-placed) tile with a new random draw from the biome
     pool. 3 rerolls per session by default (perks can add more or make rerolls free/charge-based).
4. **Habitat formation** (automatic, after every placement — see §6): if the newly placed tile
   completes a valid habitat region, it's scored, added to the board, and can merge with adjacent
   same-animal habitats.
5. **Rewards on habitat creation**:
   - Score is added (see §7 scoring formula), animated via a "flyout" from habitat to score
     counter.
   - +3 random tiles are appended to the deck queue (configurable; this is the main way a run
     "extends itself" — good play literally buys more turns).
   - Quest progress is checked; completed quests grant their reward immediately.
   - Every 5th habitat created triggers a **perk draft**: 3 perks offered, up to `DraftRerollsPerRun`
     (3) individual-slot rerolls, pick exactly one.
6. **End of session**: triggered purely by the deck emptying (`TileDeck.DeckEmptied`) — there is no
   other loss condition (no timer, no board-full state is treated as a loss in the current code).
   Game Over screen shows final score, habitat count, and largest single habitat (post-merge tile
   count).
7. Player can RESTART (new session immediately) or return to MAIN MENU.

## 5. Controls & Camera

- Mouse: hover to preview placement (see §6.3), click to place / select.
- WASD: camera pan (`CameraWASDController`).
- ESC / pause button: pause menu (`PauseMenuController`), which also freezes `Time.timeScale`.

## 6. Tile & Board Systems

### 6.1 Grid

Hexagonal grid (`TileGrid`, axial coordinates `q,r`). Tiles can only be placed on empty cells
adjacent to at least one occupied cell — the board grows organically outward from the center tile.

### 6.2 Biomes

Five tile biomes: **Meadow, Forested, Bushy, Rocks, Water** (`TileBiome`). Each occupied tile
contributes `+1` to its biome axis of a 5-dimensional **BiomeVector** `(Meadow, Forest, Bush, Rock,
Water)` used for habitat requirement checks.

Each hex tile is subdivided into 12 triangular slots (`TileBiomeRuntime` / `HexTileLayout`) that
get populated with biome-appropriate decoration (trees, bushes, flowers, rocks) by
`BiomeTilePopulator` — this is purely visual/flavor, not gameplay-mechanical.

### 6.3 Deck & Draw

- `TileDeck` holds a `Queue<TileDraw>` built from a weighted pool of `TilePrefabGroup` entries
  (one entry per biome, each with a `weight` controlling draw frequency).
- Deck is fully shuffled and rebuilt at session start to `deckSize` (30).
- Hover preview (`TileNextTileHoverPreview`) shows a ghost of the next tile and highlights which
  habitat(s) it would likely complete, color-coded:
  - **Gray** — no habitat progress
  - **Yellow** — partial progress toward a habitat
  - **Green** — placement would complete a habitat
- Reroll swaps only the head of the queue; the rest of the deck order is untouched.

### 6.4 Session-extension tiles

Two sources add tiles mid-run instead of shrinking the deck monotonically:
- Habitat creation: +3 tiles (configurable, see §4 step 5).
- Quest rewards: `AddDeckCards` (generic) or `AddBiomeTiles` (specific biome, e.g. reward for a
  beaver quest is Water tiles).

This means a session's actual length is not fixed at 30 placements — skilled/lucky play extends
it. There is no hard cap observed in code, so a very efficient run could in principle run
indefinitely; the intended failure mode is "the player runs out of good moves/enthusiasm," not a
hard wall.

## 7. Habitats & Scoring

### 7.1 Animals & requirements

Four playable animals (`HabitatAnimal`), each with a required `BiomeVector` (the region's summed
biome counts must be **≥** every component) and a base point value:

| Animal | Requirement (Meadow, Forest, Bush, Rock, Water) | Base points | Min. tiles to satisfy |
|--------|--------------------------------------------------|-------------|------------------------|
| Bees   | (2, 1, 2, 0, 0)                                   | 300         | 5 |
| Beaver | (0, 2, 1, 0, 2)                                   | 400         | 5 |
| Deer   | (2, 1, 1, 0, 1)                                   | 500         | 5 |
| Bear   | (0, 1, 1, 2, 1)                                   | 600         | 5 |

> **Note — a 5th species exists in intent but not in play.** `HabitatCompatibilityService`'s
> compatibility matrix comment and `docs/KLASY_I_RELACJE.md`'s type table both reference a
> `RockDweller` animal, and the compatibility matrix is visibly built for 5 animals (one row is
> even truncated to 4 columns — a latent bug if `RockDweller` is ever re-added). The `HabitatAnimal`
> enum and `HabitatRequirements` only implement 4. Either this was cut content or an
> intended-but-unshipped 5th animal. Worth a deliberate decision (finish it or remove the dangling
> references) before shipping.

### 7.2 Region discovery (automatic, every placement)

After each tile placement, `BiomeHabitatClassifier`:
1. BFS-collects occupied tiles within `maxGraphStepsFromPlacement` (5) of the just-placed tile.
2. Enumerates every connected subset of up to `maxTilesPerHabitat` (**5**) tiles that includes the
   placed tile.
3. For each candidate region, builds a compatibility-filtered biome vector and checks it against
   each animal's requirement.
4. Validates a "core tile" requirement (`HabitatCoreValidation` — not detailed here, but gates
   whether a region has enough dedicated tiles, likely to stop degenerate 1-tile-does-everything
   claims).
5. Scores every valid candidate as `basePoints / tileCount` and keeps the single best-scoring one
   (ties broken by base points, then fewer tiles, then a deterministic tile-order comparison).
6. Registers the winning region as a new habitat.

**Design implication**: because score = `basePoints / tileCount`, the game rewards *efficient*
habitats — satisfying Bear's requirement in exactly 5 tiles (its minimum) scores much higher than
padding the region. This is the central skill expression of the puzzle.

### 7.3 Compatibility & multi-animal tiles

A single tile can belong to **up to 2 habitats** (`habitatIds: List<int>`, max 2 per
`TileRuntimeStore.Runtime`). `HabitatCompatibilityService` defines a symmetric 0/1 compatibility
matrix between animals — e.g. Deer is compatible with Beaver and Bees but not Bear; Bear is
compatible with nothing except (intended) RockDweller. Incompatible animals can't both draw biome
value from the same tile.

### 7.4 Merging

Adjacent habitats of the **same animal** can merge into one larger habitat
(`TileEvents.HabitatMerged`), which:
- Sums their points plus a **merge connection bonus**
  (`ComputeMergeConnectionBonus = ComputeAwardedPoints(animal, 2) * (habitatsMerged - 1)`).
- Reduces the displayed habitat count by `mergedHabitatCount - 1`.
- Updates "largest habitat" tile count (this is how main quests like "Great Herd — 10+ tiles" are
  reachable despite the single-region cap of 5: quests track the *post-merge* habitat size, not a
  single classification pass).
- Triggers a chain-reaction visual animation (`HabitatChainReactionAnimator`).

### 7.5 Scoring summary

- Per-habitat points: `round(basePoints / tileCount)`.
- Merge bonus: `ComputeAwardedPoints(animal, 2) * (habitatsMerged - 1)` added on top.
- Perks can modify the score via `PerkManager.ModifyHabitatScore` (hook: `HabitatEvaluation`) —
  none of the current 6 perks use this hook, so it's an unused extension point today.
- Final score, habitat count, and largest habitat are the only persisted end-of-run stats (no
  high-score table, no save-across-sessions leaderboard in code).

## 8. Perks

Perks are drafted **during** a run (not a meta-progression system — they reset every session).
Every 5th habitat created (`HabitatsPerDraft = 5`) opens a draft of 3 perks
(`PerkDraftService.OpenDraft`), excluding perks already active. The player gets 3 individual-slot
rerolls per run (`DraftRerollsPerRun`) and must pick exactly one perk per draft. Picked perks stay
active for the rest of the session.

Architecture (`Perks/`): `PerkDefinition` (ScriptableObject identity + icon + hook list) +
`PerkBehavior` (subclass implementing one or more hooks) + `PerkRunState` (session-scoped counters)
+ `PerkCommand`/`PerkEffectExecutor` (perks emit commands like `AddRerolls`, `SpawnTile`,
`AddDeckTiles` rather than mutating world state directly — clean command pattern, easy to add new
perks without touching core systems).

Hooks available: `SessionStart`, `HabitatAssigned`, `RerollCost`, `TilePlaced`,
`HabitatEvaluation`.

### 8.1 The six current perks

| Perk | Hook | Effect |
|------|------|--------|
| **Seed Bank** | SessionStart | +5 tiles added to the deck at session start. |
| **Mulligan** | SessionStart | +2 starting rerolls. |
| **Biodiversity** | HabitatAssigned | If a habitat region spans ≥4 distinct biomes, +1 deck tile. |
| **Patient Forager** | HabitatAssigned, RerollCost | Each habitat created grants 1 free reroll charge; free rerolls consume a charge instead of the reroll counter. |
| **Beaver Dam** | HabitatAssigned (Beaver only) | On a Beaver habitat, spawns a Water tile on an adjacent empty cell — snowballs future Beaver/Deer habitats. |
| **Expansion** | TilePlaced | 30% chance per placement to spawn a same-biome tile on a random empty neighbor — free board growth. |

None currently use `HabitatEvaluation` (direct score modification) — all are economy (more
tiles/rerolls) or world-mutation (free extra tiles on the board) effects. This matches the user's
note that perks are the least-polished system: the design space (direct scoring perks, negative/
risk-reward perks, biome-specific synergy perks) is set up architecturally but only lightly
explored in content.

**Polish opportunities to consider** (not yet acted on, just visible gaps from reading the code):
- No perk currently touches `HabitatEvaluation`, despite the hook and `ApplyPerkScoreModifiers`
  plumbing already existing in `BiomeHabitatClassifier`.
- No perk interacts with animal *compatibility* or multi-animal tiles.
- No "drawback" perks (risk/reward) — all six are strictly positive.
- Perk pool is only 6 entries for a game structured around repeated drafts (a run reaching 25+
  habitats would exhaust the pool and start seeing `PickFromPool` return null / smaller offers).

## 9. Quests

`QuestCatalog` defines two tiers, both reset every session (no cross-run quest state):

- **Main quests** (8): "Build a `<animal>` habitat with 10+ or 15+ tiles" (i.e., post-merge chain
  size), reward = choose 1 of 3 perks immediately (a second, quest-triggered draft path alongside
  the every-5-habitats draft).
- **Side quests** (10): "Create N habitats of `<animal>`" (N = 1, 2, or a cross-animal 2/4 via
  `HabitatAnimal.None`), rewards are small economy boosts — rerolls, deck cards, points, or
  biome-specific tiles (e.g. beaver quest rewards Water tiles, bear quest rewards Rocks tiles or a
  flat +1000 points).

Quests give the run direction beyond "place tiles until deck empties" — they're the main soft
guidance system nudging the player toward specific animals rather than pure score-maximizing
Bear-spam.

## 10. UI/UX Surfaces (implemented)

- **Main Menu**: PLAY, HOW TO PLAY (in-game rules modal), QUIT.
- **Gameplay HUD**: score (animated with impact-shake on gain), habitat count, next-tile card
  (icon/name/queue count), reroll button with remaining count, pause button.
- **Quest HUD**: visible during a session, tracks main + side quest progress (`QuestHudView`).
- **Habitat hover preview**: gray/yellow/green feedback before committing a placement.
- **Score flyout**: visual points travel from the completed habitat to the score counter
  (`HabitatScoreFlyoutPresenter`).
- **Habitat outline & chain-reaction visuals**: `HabitatOutlineVisualizer`,
  `HabitatChainReactionAnimator`, `HabitatGridManager` (color spreading per habitat).
- **Pause menu**: `Time.timeScale` freeze, resume/quit-to-menu.
- **Game Over**: final score, habitat count, largest habitat tile count; RESTART or MAIN MENU.

All UI is built procedurally in code (`GameFlowController`, `GameUI`) with a scene-based override
path (`GameFlowMenuView`, `GameHudView`) preferred when present — i.e., the game can run with
zero manually-authored UI prefabs, which is unusual and worth knowing if you're about to restyle
menus: check whether a `*View` component is wired in the scene before assuming the procedural
fallback is what's live.

## 11. What "finished and polished for Steam" likely means from here

Given the stated goal (ship a small, ~15 PLN, finished puzzle game — not expand scope), the
natural remaining work implied by the current state is:

1. **Perk pass**: balance the 6 perks, likely add a handful more (the draft system will start
   starving after ~6 drafts / 30 habitats with the current pool), consider a `HabitatEvaluation`
   perk or two since the hook exists unused.
2. **Resolve the RockDweller loose end**: either cut every remaining reference (matrix, comments,
   docs) or ship a 5th animal. Leaving it half-wired is a pre-launch cleanup item.
3. **Steam-specific packaging**: store page, achievements (none currently implemented — no
   persistent stats to hook into an achievement system yet), settings menu (no options/settings UI
   observed: no volume, resolution, or key rebinding screens in the code read so far), credits.
4. **Progression/replayability hook for a paid release**: currently every session is mechanically
   identical (same 30-tile deck size, same quest pool, same perk pool) with only RNG variance. For
   a premium one-time-purchase puzzle game this may be fine (think *Threes!* / solitaire-style
   replay value), but it's worth an explicit decision rather than a default.

## 12. Explicit non-goals (per current scope)

- No meta-progression, currency, or unlocks across runs.
- No narrative/story layer.
- No multiplayer.
- Despite the repo/working title "Idle Forest," no idle/incremental mechanics exist or are planned
  in the current build — the shipped identity is the README's title, **Puzzles of the Forest**.

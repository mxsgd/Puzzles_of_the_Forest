# Idle Forest

![Idle Forest gameplay](<docs/gifs/PuzzlesOfTheForest (1).gif>)

`Idle Forest` is a public Unity project built as a portfolio piece and an actively developed game prototype.

In the current build, the player places hex tiles, creates animal habitats, manages a limited deck and rerolls, and gradually shapes a high-scoring run through board decisions and perk choices. The in-game UI currently uses the title `Puzzles of the Forest`, while this repository uses the working project name `Idle Forest`.

## Recruiter Note

This repository is public on purpose.

I want recruiters and hiring teams to be able to review not just the final result, but also how I structure gameplay code, separate systems, iterate on architecture, and document design decisions during development.

If you are reviewing this project as part of my job search, the best places to start are:

- `Assets/Scripts/` for the gameplay and UI code
- `docs/KLASY_I_RELACJE.md` for a structural overview of classes and relationships
- `Assets/Scripts/TileScripts/` for the main board, habitat, and placement systems
- `Assets/Scripts/Perks/` for the run-based perk architecture

## What the Game Is

This is a run-based, single-player tile strategy prototype built around:

- drawing tiles from a limited deck
- placing them on a hex grid
- forming valid animal habitats
- scoring efficiently
- using rerolls carefully
- selecting perks during the run to bend the rules in your favor

The core loop is intentionally compact: place a tile, improve the board, trigger habitats, extend the run, and push for a better score before the deck runs out.

## Current Gameplay Systems

- Hex-grid tile placement
- Deck-based tile draw system
- Limited rerolls
- Habitat detection and scoring
- Hover preview for likely habitat outcomes
- Session flow with start, run, and game over states
- Procedural UI built from code
- Run-based perk draft system with economy, rule, and world-effect perks

## Technical Highlights

- Unity 6 project using C#
- URP and the Unity Input System
- Event-driven gameplay flow around tile state and habitat creation
- Shared habitat evaluation logic between hover preview and final classification
- Procedural UI instead of scene-heavy manual setup
- Data-driven perk definitions with runtime state, command execution, and hook-based behaviors
- Clear separation between board state, placement, deck logic, UI, and progression systems

## Repository Structure

Key areas of the repository:

- `Assets/Scenes/SampleScene.unity`  
  Main playable scene.

- `Assets/Scripts/GameUI.cs` and `Assets/Scripts/UI/`  
  Session UI, menus, and procedural HUD.

- `Assets/Scripts/TileScripts/`  
  Core gameplay: grid, placement, runtime tile state, habitat evaluation, hover preview, deck, and feedback systems.

- `Assets/Scripts/Perks/`  
  Perk definitions, run state, draft logic, command execution, and perk behaviors.

- `docs/KLASY_I_RELACJE.md`  
  Internal architecture notes and class/system map.

## Running the Project

### Requirements

- Unity `6000.2.6f2`

### Open and Play

1. Open the repository in Unity.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press Play in the editor.

## Controls and Player Flow

- Start a new run from the main menu
- Place tiles on available hexes next to occupied ones
- Build habitats by creating valid biome combinations
- Use rerolls when the next tile is a poor fit
- Continue until the deck is exhausted
- Review final score, habitat count, and best habitat size

## Why This Project Matters

This repo is not just a game prototype. It is also a practical example of how I approach:

- gameplay architecture
- maintainable system boundaries
- UI built directly in code
- feature iteration without rewriting the whole project
- design-to-code implementation in Unity

I use it as a place to explore systems that are common in production game work: board state management, scoring logic, player-facing feedback, progression layers, and modular feature design.

## Status

The project is actively evolving. The codebase already contains working gameplay systems and ongoing architecture work for progression features such as perks. Some systems are still prototype-grade and are being refined as the design becomes clearer.

## Notes

- This is a public portfolio repository.
- The project is intended to be readable as well as playable.
- Internal documentation may mix English and Polish because the project has been developed iteratively during active design work.

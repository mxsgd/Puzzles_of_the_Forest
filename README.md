# Puzzles of the Forest
![Puzzles of the Forest gameplay](<docs/gifs/PuzzlesOfTheForest (1).gif>)

`Puzzles of the Forest` is a solo-developed Unity game currently in **alpha**.  
The current build is a playable systems-first version, and the project is moving toward a public **demo** release.

In the current game loop, the player places hex tiles, forms animal habitats, manages a limited deck and rerolls, and tries to stretch each run into the highest score possible.

## About the Project

This repository tracks the active development of the game rather than a polished final release.  
The focus right now is on making the core loop feel good, making the board readable, and building systems that can scale into a larger version of the game.


## Core Player Systems

The current alpha revolves around a few connected systems:

- **Hex tile placement**  
  The player places tiles on valid cells adjacent to already occupied tiles.

- **Deck-driven progression**  
  Every move is shaped by the next tile in the deck rather than free placement from a palette.

- **Reroll economy**  
  The player has limited rerolls and has to decide when to spend them and when to commit to a difficult draw.

- **Habitat building**  
  Different biome combinations can produce different animal habitats with their own scoring logic.

- **Scoring pressure**  
  Good placement is not only about making a habitat, but about making it efficiently.

- **Hover preview and placement feedback**  
  Before committing a tile, the player gets feedback about what the move is likely to create.

- **Run structure**  
  A run starts from a clean state, builds momentum through habitat rewards, and ends when the deck is exhausted.

- **Perk draft system**  
  During the run, the player can choose perks that modify economy, rule evaluation, or board state.

## Systems Built So Far

From the game side, the current project includes:

- tile grid generation and tile queries
- runtime occupancy and board state tracking
- tile placement flow with visual feedback
- deck draw and deck refill logic
- reroll flow and UI
- habitat classification and scoring
- habitat hover evaluation
- session start / gameplay / game over flow
- procedural UI built entirely in code
- perk architecture with:
  - perk definitions
  - runtime perk state
  - draft offers and rerolls
  - hook-based perk behaviors
  - command-driven world and economy effects

## Problems Solved During Development

One of the main goals of this project has been solving real gameplay and architecture problems, not just getting a prototype on screen.

Some of the important issues solved along the way:

- **Keeping hover preview and final habitat result consistent**  
  The preview cannot promise one outcome and then produce another after placement, so shared evaluation logic was introduced between preview and final classification.

- **Separating board systems cleanly**  
  Tile placement, tile runtime state, deck logic, scoring, UI, and habitat detection were split into distinct systems so features could be changed without rewriting everything.

- **Making deck and reroll gameplay readable**  
  The run economy depends on the player understanding the next tile, reroll value, and habitat rewards clearly enough to make deliberate choices.

- **Handling habitat logic without hardcoding every case into one place**  
  Habitat detection and scoring needed to stay data-driven and extensible as the design evolved.

- **Building UI without relying on a heavy manually wired scene setup**  
  A lot of the UI is generated procedurally so iteration is faster and system ownership stays in code.

- **Adding perks without scattering perk exceptions across the whole project**  
  The perk system was designed around a central manager, hook-based behaviors, and command execution so future perks can stay modular.

- **Making world-effect perks safe to expand later**  
  Some perks affect deck economy, some affect evaluation rules, and some affect the world itself, so the architecture had to support all three without turning into a giant special-case switch.

## Art and Asset Authorship

All models, art assets, and visual game assets in this project are **fully my own work**.

- no generative AI was used in the creation of the models
- no generative AI was used in the creation of the assets
- the visual side of the project is authored manually from scratch

## Tech Overview

- Unity `6000.2.6f2`
- C#
- Universal Render Pipeline
- Unity Input System
- event-driven gameplay flow
- procedural UI
- data-driven gameplay architecture

## Repository Structure

Important parts of the repository:

- `Assets/Scenes/SampleScene.unity`  
  Main playable scene.

- `Assets/Scripts/TileScripts/`  
  Core gameplay systems: board, runtime tile state, placement, habitat logic, hover evaluation, deck, and board feedback.

- `Assets/Scripts/UI/` and `Assets/Scripts/GameUI.cs`  
  Menus, HUD, and run flow presentation.

- `Assets/Scripts/Perks/`  
  Perk definitions, draft state, active perk runtime logic, and effect execution.

- `docs/KLASY_I_RELACJE.md`  
  Internal documentation for classes and system relationships.

## Running the Project

### Requirements

- Unity `6000.2.6f2`

### Open and Play

1. Open the repository in Unity.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press Play.

## Current Status

This is an **alpha** version focused on gameplay systems, iteration, and architecture.  
It is already playable, but still actively changing as the game moves toward a stronger and more representative demo build.

## License

All contents of this repository are **All Rights Reserved** unless explicit
written permission is granted by the author.

Source code, documentation, and project content may not be copied, modified,
redistributed, used in other projects, or used commercially without explicit
written permission.

Game assets are additionally covered by `ASSETS_LICENSE.txt`. All graphics,
audio, animations, models, UI elements, characters, levels, names, logos, and
narrative content remain the exclusive property of Maks Szygenda.

## Notes

- This repository is public.
- Development documentation may mix English and Polish.
- The codebase is being shaped alongside ongoing gameplay iteration.

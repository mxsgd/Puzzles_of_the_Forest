# Idle Forest — classes in the game

English translation of `docs/KLASY_I_RELACJE.md`. Keep both in sync when the architecture changes,
or drop the Polish original if it's no longer needed.

Documentation of all C# types in `Assets/Scripts/` (40 files, ~55 public types).
Relationships covered: **inheritance**, **nesting**, **SerializeField references**, **events**
(`TileEvents`).

---

## File and type tree

```
Assets/Scripts/
├── GameManager.cs                    → GameManager
├── GameUI.cs                         → GameUI
├── CameraWASDController.cs             → CameraWASDController
├── HoldProgressIndicator.cs          → HoldProgressIndicator
│
├── UI/
│   ├── PauseMenuController.cs        → PauseMenuController
│   └── UISpriteFactory.cs            → UISpriteFactory (static)
│
└── TileScripts/
    ├── BiomeVector.cs                  → HabitatAnimal (enum)
    │                                   → BiomeVector (struct)
    ├── TileBiome.cs                    → TileBiome (enum)
    │                                   → TileContentTags (static)
    │                                   → TileBiomeRules (static)
    ├── HexTileLayout.cs              → HexTileLayout (static)
    ├── TileGrid.cs                     → TileGrid
    │                                   └── TileGrid.Tile (nested)
    ├── TileRuntimeStore.cs           → TileRuntimeStore
    │                                   ├── Runtime (nested)
    │                                   └── HabitatRecord (nested)
    ├── TileEvents.cs                 → HabitatAssignmentData (struct)
    │                                   → TileEvents (static)
    ├── HabitatRequirements.cs        → HabitatRequirements (static)
    ├── HabitatCompatibilityService.cs→ HabitatCompatibilityService (static)
    ├── HabitatHoverEvaluator.cs      → HabitatHoverScratch
    │                                   → HabitatHoverPreviewKind (enum)
    │                                   → HabitatHoverResult (struct)
    │                                   → HabitatHoverEvaluator (static)
    ├── TileDeck.cs                   → TilePrefabGroup
    │                                   → TileDraw
    │                                   → TileDeck
    ├── TilePlacementService.cs       → TilePlacementService
    │                                   └── BiomeParticleEntry (private nested)
    ├── TileAvailabilityService.cs    → TileAvailabilityService
    ├── TileQueryService.cs           → TileQueryService
    ├── TileSelectionModel.cs         → TileSelectionModel
    ├── TileClickSelector.cs          → TileClickSelector
    ├── TileAvailabilityVisualizer.cs → TileAvailabilityVisualizer
    │                                   └── PlacementFeedbackKind (private enum)
    ├── TileNextTileHoverPreview.cs   → TileNextTileHoverPreview
    │                                   └── HabitatIconEntry (struct)
    ├── BiomeHabitatClassifier.cs     → BiomeHabitatClassifier
    ├── BiomeTilePopulator.cs         → BiomeTilePopulator
    │                                   ├── PrefabEntry (nested)
    │                                   ├── ContentPrefab (nested)
    │                                   ├── BiomeContent (nested)
    │                                   └── PlacementRequest (private struct)
    ├── TileBiomeRuntime.cs           → TileBiomeRuntime
    ├── TileTriangleSlot.cs           → TileTriangleSlot
    ├── TileObject.cs                 → TileObject
    ├── TilePositionTint.cs           → TilePositionTint
    ├── BiomeDecorationTintProfile.cs → BiomeDecorationTintProfile
    │                                   └── TagTintRule (nested)
    ├── BiomeDecorationTintReceiver.cs→ BiomeDecorationTintReceiver
    ├── PerObjectColor.cs             → PerObjectColor
    ├── HabitatTintProfile.cs         → HabitatTintProfile
    │                                   └── HabitatTintEntry (nested struct)
    ├── HabitatGridManager.cs         → HabitatGridManager
    ├── HabitatSource.cs              → HabitatSource (serializable data)
    ├── HabitatTile.cs                → HabitatTile (serializable data)
    ├── HabitatOutlineVisualizer.cs   → HabitatOutlineVisualizer
    ├── HabitatChainReactionAnimator.cs→ HabitatChainReactionAnimator
    ├── HabitatGridDebugSpawner.cs    → HabitatGridDebugSpawner
    └── TileDeckUI.cs                 → TileDeckUI
```

> **Not covered by this diagram**: the `Perks/` and `Quests/` folders (added after this document
> was originally written) — perk drafting, perk behaviors, and the quest catalog/manager. See
> `docs/GDD.md` §8–9 for those systems.

---

## Type legend

| Symbol | Meaning |
|--------|-----------|
| `MB` | `MonoBehaviour` — a component on a scene object / prefab |
| `SO` | `ScriptableObject` — a project asset |
| `static` | Static class — logic with no instance |
| `data` | Struct / data class (Inspector, deck, grid) |
| `event` | Global event hub |

---

## Unity inheritance

```
MonoBehaviour
├── GameManager
├── GameUI
├── CameraWASDController
├── HoldProgressIndicator
├── PauseMenuController
├── TileGrid
├── TileRuntimeStore
├── TilePlacementService
├── TileAvailabilityService
├── TileQueryService
├── TileSelectionModel
├── TileClickSelector
├── TileAvailabilityVisualizer
├── TileNextTileHoverPreview
├── TileDeck
├── TileDeckUI
├── BiomeHabitatClassifier
├── BiomeTilePopulator
├── TileBiomeRuntime          ← on the instance of a placed tile
├── TileObject                ← optionally on a tile
├── TilePositionTint          ← on the tile prefab
├── BiomeDecorationTintReceiver ← on decorations (trees, bushes…)
├── PerObjectColor
├── HabitatGridManager
├── HabitatOutlineVisualizer
├── HabitatChainReactionAnimator
└── HabitatGridDebugSpawner

ScriptableObject
├── HabitatTintProfile
└── BiomeDecorationTintProfile
```

No inheritance between the project's own classes — everything either extends a Unity type or is
plain C#.

---

## Main diagram — all classes

One view of the whole `Assets/Scripts/`. Each arrow = a separate connection.
Line colors (see `linkStyle` at the bottom of the diagram):

| Color | Type | Meaning |
|-------|-----|-----------|
| **Blue** | `ref` | `[SerializeField]` / Inspector reference |
| **Orange** | `emit` / `sub` | `TileEvents` events (dashed line) |
| **Purple** | `uses` | call into a `static` class |
| **Green** | `creates` | Instantiate / component on a prefab / `AddComponent` |
| **Gray** | `data` | type held in a field, payload, list in the Inspector |

> Scroll the diagram sideways — it's wide. In Markdown preview, zoom with Ctrl + scroll.

```mermaid
%%{init: {'flowchart': {'curve': 'basis', 'nodeSpacing': 28, 'rankSpacing': 48}}}%%
flowchart LR

    subgraph LEG["Legend"]
        direction TB
        Lref["🔵 ref"]
        Levt["🟠 emit / sub"]
        Luse["🟣 uses"]
        Lcre["🟢 creates"]
        Ldat["⚪ data"]
    end

    subgraph ENUM["Enums"]
        TBio["TileBiome"]
        HAn["HabitatAnimal"]
        HPrev["HabitatHoverPreviewKind"]
    end

    subgraph DATA["Data / struct"]
        BVec["BiomeVector"]
        TDraw["TileDraw"]
        TPGroup["TilePrefabGroup"]
        TSlot["TileTriangleSlot"]
        HAssign["HabitatAssignmentData"]
        HResult["HabitatHoverResult"]
        HScratch["HabitatHoverScratch"]
        HSrc["HabitatSource"]
        HTile["HabitatTile"]
        TCell["TileGrid.Tile"]
    end

    subgraph STATIC["Static"]
        TE_SC["TileEvents.TileStateChanged"]
        TE_HA["TileEvents.HabitatAssigned"]
        HR["HabitatRequirements"]
        HCS["HabitatCompatibilityService"]
        HHE["HabitatHoverEvaluator"]
        HTL["HexTileLayout"]
        TBRules["TileBiomeRules"]
        TTags["TileContentTags"]
        USF["UISpriteFactory"]
    end

    subgraph SO["ScriptableObject"]
        HTP["HabitatTintProfile"]
        BDTP["BiomeDecorationTintProfile"]
    end

    subgraph CORE["Scene core"]
        GM["GameManager"]
        TG["TileGrid"]
        TRS["TileRuntimeStore"]
        TPS["TilePlacementService"]
        TD["TileDeck"]
    end

    subgraph SVC["Grid services"]
        TQS["TileQueryService"]
        TAS["TileAvailabilityService"]
        TSM["TileSelectionModel"]
        TCS["TileClickSelector"]
    end

    subgraph GAME["Gameplay"]
        TAV["TileAvailabilityVisualizer"]
        TNHP["TileNextTileHoverPreview"]
        BHC["BiomeHabitatClassifier"]
        BTP["BiomeTilePopulator"]
    end

    subgraph VIS["Habitat — visuals"]
        HOV["HabitatOutlineVisualizer"]
        HGM["HabitatGridManager"]
        HCRA["HabitatChainReactionAnimator"]
        HGDS["HabitatGridDebugSpawner"]
    end

    subgraph UI["UI"]
        GUI["GameUI"]
        PMC["PauseMenuController"]
        TDU["TileDeckUI"]
    end

    subgraph PREFAB["Tile prefab"]
        TBR["TileBiomeRuntime"]
        TPT["TilePositionTint"]
        BDR["BiomeDecorationTintReceiver"]
        TObj["TileObject"]
    end

    subgraph SOLO["No grid coupling"]
        CAM["CameraWASDController"]
        HOLD["HoldProgressIndicator"]
        POC["PerObjectColor"]
    end

    GM --> TG
    GM --> TRS
    GM --> TPS
    GM --> TD
    TPS --> TRS
    TPS --> TD
    TPS --> BTP
    TQS --> TG
    TQS --> TRS
    TAS --> TG
    TAS --> TRS
    TCS --> TQS
    TCS --> TSM
    TAV --> TG
    TAV --> TRS
    TAV --> TPS
    TAV --> TD
    TAV --> TAS
    TAV --> TQS
    TAV --> TSM
    TAV --> BHC
    TAV --> TNHP
    TNHP --> TG
    TNHP --> TRS
    TNHP --> TD
    TNHP --> TAS
    TNHP --> TQS
    TNHP --> BTP
    TNHP --> BHC
    BHC --> TRS
    HOV --> TG
    HOV --> TRS
    HGM --> TG
    HGM --> TRS
    HGM --> HTP
    HCRA --> HGM
    HCRA --> TRS
    HCRA --> HTP
    HGDS --> HGM
    HGDS --> TG
    GUI --> TD
    TDU --> TD
    BDR --> TPT
    BDR --> BDTP
    TObj --> TG
    BTP --> BDTP

    TRS -.-> TE_SC
    TRS -.-> TE_HA
    TE_SC -.-> BHC
    TE_SC -.-> TNHP
    TE_SC -.-> HOV
    TE_HA -.-> GUI
    TE_HA -.-> TD
    TE_HA -.-> HGM
    TE_HA -.-> HOV
    TE_HA -.-> HCRA

    BHC ==> HR
    BHC ==> HCS
    HHE ==> HR
    HHE ==> HCS
    TAV ==> HHE
    TNHP ==> HHE
    BTP ==> TBRules
    BTP ==> HTL
    TBRules ==> TTags
    TD ==> TBRules
    GUI ==> USF
    PMC ==> USF
    TRS ==> HR

    TPS --> TBR
    BTP --> TBR
    BTP --> BDR
    GUI --> PMC
    TPT --> TBR

    TD --> TDraw
    TD --> TPGroup
    TRS --> TDraw
    TRS --> TCell
    TE_HA --> HAssign
    HR --> HAn
    HR --> BVec
    HCS --> HAn
    HHE --> HAn
    HHE --> TRS
    TBR --> TSlot
    TBR --> HTL
    TBR --> TBio
    BDR --> TBio
    TPT --> TBio
    HGM --> HSrc
    HGM --> HTile
    HHE --> HScratch
    HHE --> HResult
    HHE --> HPrev
    BTP --> TBio

    linkStyle 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45 stroke:#1565C0,stroke-width:2px
    linkStyle 46,47,48,49,50,51,52,53,54,55 stroke:#EF6C00,stroke-width:2px
    linkStyle 56,57,58,59,60,61,62,63,64,65,66,67,68 stroke:#7B1FA2,stroke-width:2px
    linkStyle 69,70,71,72,73 stroke:#2E7D32,stroke-width:2px
    linkStyle 74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94 stroke:#78909C,stroke-width:1.5px
```

**Isolated** (nodes with no arrows in the diagram): `CameraWASDController`, `HoldProgressIndicator`,
`PerObjectColor`.

**Nested** (not a separate node): `TileRuntimeStore.Runtime`, `TileRuntimeStore.HabitatRecord`,
types inside `BiomeTilePopulator` — described in the table at the end of the document.

---

## Grid and runtime model

```
TileGrid
  └── Tile { i, j, q, r, worldPos, grid }
        │
        ▼
TileRuntimeStore.Runtime (per Tile)
  ├── occupied, available
  ├── occupantInstance, templatePrefab
  ├── tileDraw: TileDraw
  ├── biome: TileBiome
  ├── biomeRuntime: TileBiomeRuntime
  └── habitatIds: List<int>  (max 2)

TileRuntimeStore.HabitatRecord
  ├── Id, Animal: HabitatAnimal
  └── Tiles: List<TileGrid.Tile>
```

**Grid-based services** (all hold `TileGrid` + `TileRuntimeStore`):

| Class | Role |
|-------|-------|
| `TileQueryService` | Raycast → `Tile` |
| `TileAvailabilityService` | which neighboring cells are available |
| `TilePlacementService` | Instantiate / ghost → `MarkOccupied` |

---

## Biome and decoration layer

```
TileBiome (enum)
  └── TileBiomeRules.GetAllowedTags()
        └── BiomeTilePopulator.Populate()
              └── TileBiomeRuntime (12 × TileTriangleSlot)
                    └── HexTileLayout (triangle geometry)

Tile prefab:
  TilePositionTint ──► BiomeDecorationTintReceiver (decorations)
  TileBiomeRuntime
```

| SO asset | Usage |
|----------|--------|
| `BiomeDecorationTintProfile` | Color rules by content tag (`Tree`, `Bush`…) |
| `HabitatTintProfile` | Animal colors on tiles (`HabitatGridManager`) |

---

## Habitat logic (pure C#)

| Type | Responsibility |
|-----|------------------|
| `HabitatAnimal` | Deer, Beaver, Bear, Bees, RockDweller |
| `BiomeVector` | R⁵ biome vector; `FromTileBiome`, `Satisfies`, `DeficitSumToward` |
| `HabitatRequirements` | Requirement vectors, base points, scoring |
| `HabitatCompatibilityService` | 5×5 animal-compatibility matrix for sharing one tile |
| `HabitatHoverEvaluator` | Gray / Yellow / Green preview before placement |
| `BiomeHabitatClassifier` | Region classification following `TileStateChanged` |

`HabitatSource`, `HabitatTile` — configuration data for `HabitatGridManager` (influence sources /
tint).

> Note: `HabitatAnimal` lists a `RockDweller` value here and the compatibility matrix is shaped for
> 5 animals, but as of this translation `HabitatRequirements` only implements 4 (Deer, Beaver, Bear,
> Bees) — see `docs/GDD.md` §7.1 for the discrepancy.

---

## UI and input

| Class | Dependencies |
|-------|-------|
| `GameUI` | `TileDeck`, `TileEvents`, `UISpriteFactory`, creates `PauseMenuController` |
| `PauseMenuController` | `UISpriteFactory`, `Time.timeScale` |
| `TileDeckUI` | `TileDeck` (older deck UI) |
| `TileClickSelector` | `TileQueryService`, `TileSelectionModel` |
| `TileAvailabilityVisualizer` | full placement + hover + classifier chain |
| `TileNextTileHoverPreview` | ghost of the next tile + habitat icons |
| `HoldProgressIndicator` | progress bar (Image) |
| `CameraWASDController` | camera movement |

---

## Components on a placed tile instance (prefab)

Typical set after a tile is placed:

```
GameObject (occupant)
├── TileBiomeRuntime
├── TilePositionTint
├── TileObject (optional)
└── children with BiomeDecorationTintReceiver
```

`TileObject.AssignTile(TileGrid, TileGrid.Tile)` — binds the instance to its logical grid position.

---

## Table of all types

### MonoBehaviour (scene / prefab)

| Class | File | Main role |
|-------|------|-------------|
| `GameManager` | `GameManager.cs` | Start: center tile |
| `GameUI` | `GameUI.cs` | Score, next tile, reroll, pause |
| `CameraWASDController` | `CameraWASDController.cs` | Camera control |
| `HoldProgressIndicator` | `HoldProgressIndicator.cs` | Hold-progress UI indicator |
| `PauseMenuController` | `UI/PauseMenuController.cs` | Pause menu (ESC) |
| `TileGrid` | `TileGrid.cs` | Hexagonal grid |
| `TileRuntimeStore` | `TileRuntimeStore.cs` | Tile and habitat state |
| `TilePlacementService` | `TilePlacementService.cs` | Placing a tile |
| `TileAvailabilityService` | `TileAvailabilityService.cs` | Available cells |
| `TileQueryService` | `TileQueryService.cs` | Picking |
| `TileSelectionModel` | `TileSelectionModel.cs` | Selected tile |
| `TileClickSelector` | `TileClickSelector.cs` | Click → selection |
| `TileAvailabilityVisualizer` | `TileAvailabilityVisualizer.cs` | Markers + placement + sound |
| `TileNextTileHoverPreview` | `TileNextTileHoverPreview.cs` | Ghost + habitat preview |
| `TileDeck` | `TileDeck.cs` | Deck, draw, reroll |
| `TileDeckUI` | `TileDeckUI.cs` | Tile list in UI |
| `BiomeHabitatClassifier` | `BiomeHabitatClassifier.cs` | Habitat detection |
| `BiomeTilePopulator` | `BiomeTilePopulator.cs` | Trees, bushes, flowers in slots |
| `TileBiomeRuntime` | `TileBiomeRuntime.cs` | 12 triangles on a tile |
| `TileObject` | `TileObject.cs` | Reference to `TileGrid.Tile` |
| `TilePositionTint` | `TilePositionTint.cs` | Ground color gradient |
| `BiomeDecorationTintReceiver` | `BiomeDecorationTintReceiver.cs` | Decoration tint |
| `PerObjectColor` | `PerObjectColor.cs` | Per-renderer color |
| `HabitatGridManager` | `HabitatGridManager.cs` | Habitat color spreading |
| `HabitatOutlineVisualizer` | `HabitatOutlineVisualizer.cs` | Region outlines |
| `HabitatChainReactionAnimator` | `HabitatChainReactionAnimator.cs` | Chain-reaction animation after a habitat |
| `HabitatGridDebugSpawner` | `HabitatGridDebugSpawner.cs` | Debug: H/J sources |

### ScriptableObject

| Class | File |
|-------|------|
| `HabitatTintProfile` | `HabitatTintProfile.cs` |
| `BiomeDecorationTintProfile` | `BiomeDecorationTintProfile.cs` |

### Static classes

| Class | File |
|-------|------|
| `TileEvents` | `TileEvents.cs` |
| `HabitatRequirements` | `HabitatRequirements.cs` |
| `HabitatCompatibilityService` | `HabitatCompatibilityService.cs` |
| `HabitatHoverEvaluator` | `HabitatHoverEvaluator.cs` |
| `HexTileLayout` | `HexTileLayout.cs` |
| `TileContentTags` | `TileBiome.cs` |
| `TileBiomeRules` | `TileBiome.cs` |
| `UISpriteFactory` | `UI/UISpriteFactory.cs` |

### Enums

| Enum | Values (abridged) |
|------|-------------------|
| `TileBiome` | None, Forested, Meadow, Rocks, Bushy, Water |
| `HabitatAnimal` | None, Deer, Beaver, Bear, Bees, RockDweller |
| `HabitatHoverPreviewKind` | Gray, Yellow, Green |

### Structs and data classes

| Type | File |
|-----|------|
| `BiomeVector` | `BiomeVector.cs` |
| `HabitatAssignmentData` | `TileEvents.cs` |
| `HabitatHoverResult` | `HabitatHoverEvaluator.cs` |
| `TilePrefabGroup` | `TileDeck.cs` |
| `TileDraw` | `TileDeck.cs` |
| `TileTriangleSlot` | `TileTriangleSlot.cs` |
| `HabitatSource` | `HabitatSource.cs` |
| `HabitatTile` | `HabitatTile.cs` |
| `HabitatHoverScratch` | `HabitatHoverEvaluator.cs` |
| `TileGrid.Tile` | `TileGrid.cs` |
| `TileRuntimeStore.Runtime` | `TileRuntimeStore.cs` |
| `TileRuntimeStore.HabitatRecord` | `TileRuntimeStore.cs` |

### Nested (helper) types

| Type | Parent |
|-----|--------|
| `BiomeTilePopulator.PrefabEntry` | `BiomeTilePopulator` |
| `BiomeTilePopulator.ContentPrefab` | `BiomeTilePopulator` |
| `BiomeTilePopulator.BiomeContent` | `BiomeTilePopulator` |
| `BiomeDecorationTintProfile.TagTintRule` | `BiomeDecorationTintProfile` |
| `HabitatTintProfile.HabitatTintEntry` | `HabitatTintProfile` |
| `TileNextTileHoverPreview.HabitatIconEntry` | `TileNextTileHoverPreview` |
| `TilePlacementService.BiomeParticleEntry` | `TilePlacementService` (private) |

---

## SerializeField dependency matrix (main)

Row **uses →** column:

|  | TileGrid | TileRuntimeStore | TileDeck | TilePlacement | BiomePopulator | Classifier | AvailabilitySvc | QuerySvc | Selection | HabitatGrid |
|--|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| GameManager | ✓ | ✓ | ✓ | ✓ | | | | | | |
| TilePlacementService | | ✓ | ✓ | | ✓ | | | | | |
| TileAvailabilityVisualizer | ✓ | ✓ | ✓ | ✓ | | ✓ | ✓ | ✓ | ✓ | |
| TileNextTileHoverPreview | ✓ | ✓ | ✓ | | ✓ | ✓ | ✓ | ✓ | | |
| BiomeHabitatClassifier | | ✓ | | | | | | | | |
| HabitatGridManager | ✓ | ✓ | | | | | | | | |
| HabitatOutlineVisualizer | ✓ | ✓ | | | | | | | | |
| HabitatChainReactionAnimator | | ✓ | | | | | | | | ✓ |
| GameUI | | | ✓ | | | | | | | |

---

*Generated from `Assets/Scripts/` — Idle Forest. Translated from `docs/KLASY_I_RELACJE.md`.*

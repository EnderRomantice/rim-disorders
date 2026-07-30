# Rim Mental Disorders

[Chinese version](README_CN.md)

## Design goals

The mod separates mental disorders into five layers: disease definitions, cause records, acquisition rules, symptom mechanics, and external compatibility. Its central design is that disorders emerge from pawn experiences while the trauma and acquisition infrastructure remains reusable by other mods.

Comorbidity is currently disabled. A pawn may have at most one disorder from this mod, preventing multiple behavior-altering conditions from competing for control of mental breaks, work, and combat.

## System architecture

### Disease layer

Disorders are defined as `HediffDef` records. Runtime behavior is implemented by `Hediff_MentalDisorder` and its mechanic extensions. Four severity tiers—mild, moderate, severe, and extreme—consistently determine baseline social prejudice and minimum psylink level.

Stat modifiers, mood memories, and behavior mechanics are kept separate. XML contains static data, while C# handles stateful behavior, target selection, and event reactions.

### Etiology and trauma layer

`MentalCauseDef` describes an accumulative and optionally recoverable cause. A hidden `Hediff_MentalEtiology` on each pawn stores amount, event count, source, timing, and recovery progress.

High-frequency events do not accumulate once per hit without limits. Combat, brain injury, long-range gunfire, and psychic attacks use incident windows and per-incident caps. Relationship rupture is keyed by the other pawn, so conflict with the same person during one in-game hour is one event.

Sustained high mood gradually establishes stability and removes recoverable trauma. Causes such as age or irreversible brain damage may opt out of high-mood recovery.

### Disease acquisition layer

`DiseaseAcquisitionExtension` connects disorders to etiological conditions. An acquisition recipe can express:

- conditions that must all be met;
- conditions where any one is sufficient;
- alternative groups of paths;
- cause amount, event count, and temporal ordering;
- acquisition chance and cooldown.

The initial disorder chance, severity weights, and per-disorder weights within each severity are configurable. Acquired disorders use the same severity and disease weighting model.

### Presentation and UI layer

The pawn inspection panel contains a Trauma tab with separate Trauma Records and Possible Disorders pages. Records are grouped by cause and source. Risk entries expose acquisition progress and can be filtered by severity.

Tooltips provide a short clinical summary. The health details page provides structured effects, dynamic state, and etiology. Internal framework terminology is not exposed in player-facing text.

### Compatibility layer

RimTalk compatibility calls its public context-injection API through reflection and creates no hard assembly dependency. When RimTalk is active, disorders, current phases, dynamic targets, and social prejudice enter dialogue context. Without RimTalk, the compatibility layer remains inactive.

Anomaly is optional. Logic related to mind-numb serum activates only when the corresponding content exists.

## Extension API

Other mods can define trauma types using `TraumaDef`, the public alias of `MentalCauseDef`, and attach `DiseaseAcquisitionExtension` to any `HediffDef`.

`TraumaAPI` exposes:

- `Add`
- `Reduce`
- `GetSeverity`
- `GetRecords`
- `GetDiseaseRisks`
- trauma change and recovery events

An extension can add causes and acquisition recipes using XML alone. It only needs the API when gameplay code must actively record trauma.

## Data flow

```text
Game event or long-term state
        ↓
Cause sampling and incident deduplication
        ↓
Hediff_MentalEtiology
        ↓
DiseaseAcquisitionExtension evaluation
        ↓
Severity weight → disorder weight
        ↓
Hediff_MentalDisorder
        ↓
Stats, mood, behavior, episodes, and compatibility context
```

## Repository layout

```text
1.6/Defs/                         XML definitions
1.6/Languages/                    localization
1.6/Patches/                      XML patches and acquisition recipes
1.6/Assemblies/                   loadable assembly
Source/MoreMentalDisorders/       C# source
About/                            metadata and preview
work/                             validation helpers
```

The runtime target is RimWorld 1.6 with Harmony and Royalty.

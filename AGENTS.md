# AGENTS.md — Contributor Guide

This repository contains a Unity 2D/3D project (2022.3 LTS) for a roguelike deckbuilder with map traversal, events, shop, battle, and a lightweight SQLite-backed save system. This guide helps both human contributors and automation/AI agents make safe, consistent changes.

## Project Overview
- Engine: Unity `2022.3.62f1` (LTS)
- Language: C# (.NET 4.x Equivalent, Unity runtime)
- Persistence: SQLite via `DatabaseManager` and POCO models in `Assets/Scripts/DB/SaveData.cs`
- DI/Services: Simple service locator `ServiceRegistry`
- Content: ScriptableObjects (e.g., Events) under `Assets/Resources`
- Scenes: Map, Event, Battle, Shop, Main Menu

Key systems and entry points:
- `Assets/Scripts/DB/GameInitializer.cs`: Boots DB, registers services (`IDatabase`, `IWalletService`, `IEventManager`).
- `Assets/Scripts/Event/*`: Event data DTOs, `EventManager`, and event scene bootstrap.
- `Assets/Scripts/맵/*`: Map traversal, node definitions, and scene routing.
- `Assets/Scripts/Shop/*`: Shop overlay and persistence.

## Repository Layout
- `Assets/Scripts/DB`: Save models (tables), DB manager/facade, bootstrap.
- `Assets/Scripts/Event`: Event data (SO + DTOs), manager, UI bootstrap.
- `Assets/Scripts/ITEM`, `Shop`, `Companion`, etc.: Feature domains.
- `Assets/Resources/Events`: Event ScriptableObjects (e.g., `GoldenIdolEvent.asset`).
- `Assets/Scenes`: Unity scenes (Map, Event, Battle, etc.).

## Getting Started
1. Install Unity Hub and Unity Editor `2022.3.62f1`.
2. Open the `Mycard` project folder in Unity Hub.
3. First open may trigger package resolution; let it complete.
4. Play mode entry points:
   - Map: `Assets/Scenes/Map Scene.unity`
   - Event: `Assets/Scenes/Event.unity` (normally loaded from Map)
   - Battle: `Assets/Scenes/Battle_android.unity`

Notes:
- DB file is created at `Application.persistentDataPath` on first run.
- `GameInitializer` registers services at startup; avoid creating duplicate bootstraps in scenes.

## Development Workflow
- Branching: Use feature branches off `main`. Example: `feature/events-add-card-effect`.
- Commits: Prefer Conventional Commits (e.g., `feat: add AddCard effect to events`).
- PRs: Keep focused, with clear description, testing notes, and screenshots for UI changes.
- Code style: Follow existing C# style in repo. Use explicit access modifiers; avoid one-letter vars; prefer early returns; keep methods short and cohesive.
- Null/Errors: Fail fast with clear `Debug.LogError/Warning` messages; avoid throwing in runtime gameplay unless caught.

## Unity & Assets Guidelines
- Scenes: Do not hardcode scene names in multiple places; expose via serialized fields when possible. `MapTraversalController` routes to scenes centrally for Map interactions.
- ScriptableObjects: Place game content under `Assets/Resources/...` only when runtime `Resources.Load` is required (events do). Keep asset names stable and aligned with IDs used in code.
- Meta files: Always commit `.meta` files. Do not rename or move assets without updating references.
- Prefabs: Keep prefabs self-contained; avoid circular dependencies. Prefer additive setup via serialized fields.

## Services & Data Access
- Use `ServiceRegistry` for accessing cross-cutting services:
  - `IDatabase` via `DatabaseFacade` for DB operations
  - `IWalletService` for gold updates and UI broadcasting
  - `IEventManager` for event session lifecycle
- Favor interface-first design. If you add new systems, define an interface and register an implementation in `GameInitializer`.
- DB writes: Prefer narrow, safe updaters (e.g., `UpdateRunGold`, `UpdateRunHp`) rather than bulk overwrites unless you intend to replace full run state. Keep operations idempotent where possible.

## Event System
- Data types live in `Assets/Scripts/Event/EventData.cs`:
  - `EventScriptableObject` (authoring)
  - DTOs (`EventSessionDTO`, `EventChoiceDTO`, `EventEffectDTO`) for save/load
- Manager: `EventManager` handles session load/create, applies effects (`HpDelta`, `GoldDelta`), writes `MapNodeState.EventResolutionJson`, and clears active session on resolve.
- UI: `EventSceneBootstrap` binds text/buttons to current session via `IEventManager`.
- Content authoring:
  1. Create a ScriptableObject via Create menu: `Events/New Event`.
  2. Set `eventId` to match the asset name and any references used in code/prefabs.
  3. Author `choices` and `effects` (`type`, `amount`, `refId`).
  4. Save under `Assets/Resources/Events/<EventId>.asset`.
- Extending effects: Add new `type` handling in `EventManager.ApplyChoice`. Keep side-effects localized (e.g., use `IWalletService` for currency, a future `IDeckService` for deck changes).

## Database & Save Data
- Models: See `Assets/Scripts/DB/SaveData.cs` for tables (e.g., `CurrentRun`, `MapNodeState`, `ActiveEventSession`, `ActiveShopSession`).
- Connection: Managed by `DatabaseManager`; uses WAL mode where available. Do not open new SQLite connections elsewhere.
- Schema changes: Avoid breaking changes. If unavoidable, bump and manage migration in an explicit step (a migration utility is not yet included).
- Access: Use `IDatabase` methods for save/load. Avoid direct filesystem I/O.

## Testing & Validation
- Editor tests live under `Assets/Scripts/Tests/Editor`. Use Unity Test Runner for playmode/editmode tests.
- Add targeted tests when changing core logic (e.g., new event effects, wallet updates).
- Manual checks:
  - Map → Event node → Event scene loads → choice applies → returns to Map
  - Gold/HP updates reflect in UI via `IWalletService.OnGoldChanged` and DB persistence

## Performance & Safety
- Keep per-frame allocations low; avoid LINQ in hot Update loops. Use pooling if needed.
- `Resources.Load`: Cache results if accessed frequently.
- DB operations: Batch within transactions where multiple rows are updated (`DatabaseManager` provides helpers).

## Adding New Features
1. Define interface and data contracts.
2. Implement service with minimal Unity dependencies (testable).
3. Register in `GameInitializer`.
4. Integrate with UI via serialized fields and events.
5. Persist minimal necessary state via `IDatabase`.

## AI/Automation Agent Guidelines
- Scope changes narrowly and follow the existing architecture (ServiceRegistry, IDatabase facade, ScriptableObjects in Resources when needed).
- Prefer small, reviewable patches tied to a single concern.
- Do not introduce new global singletons; leverage `ServiceRegistry`.
- Do not alter unrelated scenes/prefabs. If required, explain the rationale.
- Avoid adding license headers or changing file headers.
- Maintain file structure and naming conventions. Do not rename public APIs without justification.
- When adding effects or save fields, update both the runtime logic and DTO/serialization paths.

## Git Operations Policy (Human‑Managed)
- All Git repository operations (commit, push, branch creation/switching, history rewrites) are managed by humans (GitHub Desktop). Agents must not perform them.
- Prohibited (non‑exhaustive): `git commit`, `git push`, `git branch`, `git checkout`/`git switch`, `git reset`, `git revert`, `git rebase`, `git tag`, `git merge`, `git stash apply/pop`, or any command that changes repo state.
- Default stance: Do not run Git commands at all. Read‑only commands (e.g., `git status`, `git log`, `git diff`) are allowed only if explicitly requested by a human.
- Focus the agent role on code analysis/edits (e.g., via `apply_patch`) and leave repository management entirely to the user.

## Unity Serialization & Renames
- Use `[FormerlySerializedAs]` when renaming serialized fields on `MonoBehaviour`/`ScriptableObject` to prevent data loss.
- Avoid destructive type changes for serialized fields; prefer adding new fields and migrating data explicitly.
- Use `[SerializeReference]` only when required; document polymorphic graphs and ensure asset compatibility.

Example (safe rename)
```csharp
using UnityEngine;
using UnityEngine.Serialization;

public class WalletView : MonoBehaviour
{
    // Before: public int gold;
    [FormerlySerializedAs("gold")]
    [SerializeField] private int currentGold;
}
```

Checklist
- Before: identify assets that reference the field (search GUID/usages).
- Apply `[FormerlySerializedAs]`; keep type compatible.
- After: open key scenes and verify values persist; check Console for serialization warnings.
- Tests: load an asset created before rename and assert values are intact.

## Event Effects Extension Checklist
- When adding a new `EventEffectDTO.type`, update: DTO definition/parsing, `EventManager.ApplyChoice`, persistence paths, and any UI copy impacted.
- Route side‑effects through services (`IWalletService`, future `IDeckService`) and keep effects localized.
- On unknown types, log `Debug.LogError("[EventManager] Unknown effect: <type>")` and fail fast (no silent no‑ops).
- Maintain `eventId == asset name`; add guards and logs if a mismatch is detected at load time.

Example (ApplyChoice extension)
```csharp
// inside EventManager.ApplyChoice(...)
switch (effect.Type)
{
    case EventEffectType.HpDelta:
        // existing
        break;
    case EventEffectType.GoldDelta:
        // existing via IWalletService
        break;
    case EventEffectType.AddCard: // NEW
        _deckService.AddCard(effect.RefId);
        _database.AppendEventLog(sessionId, effect);
        break;
    default:
        Debug.LogError($"[EventManager] Unknown effect type: {effect.Type}");
        return; // fail fast
}
```

Checklist
- DTO: add new enum/string value; ensure JSON parse/serialize updated.
- Manager: implement branch in `ApplyChoice` and persist minimal state.
- Services: call through appropriate service (wallet/deck/etc.).
- UI/Copy: confirm button/description text reflects the new effect.
- Tests: unit test effect application; run Map→Event manual flow.

## Database Versioning & Test Isolation
- Maintain a `schema_version` (or equivalent) and bump on breaking schema changes.
- Implement idempotent, transaction‑wrapped migrations using `DatabaseManager` helpers where possible.
- Do not perform DB I/O in hot gameplay loops; batch writes and avoid per‑frame queries.
- Tests must use a temporary DB path (not `Application.persistentDataPath`), and clean up after execution.

Example (basic migration pattern)
```sql
-- schema_version table
CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL);
INSERT INTO schema_version(version) SELECT 1 WHERE NOT EXISTS(SELECT 1 FROM schema_version);

-- migrating 1 -> 2
BEGIN TRANSACTION;
  -- ALTER TABLE / CREATE INDEX ...
  UPDATE schema_version SET version = 2;
COMMIT;
```

Test isolation sketch
```csharp
// Pseudocode: configure DatabaseManager for tests
var tmp = System.IO.Path.GetTempFileName();
var db = new DatabaseManager();
db.Open(tmp);
// run tests...
db.Close();
System.IO.File.Delete(tmp);
```

## Resources & Caching
- Cache `Resources.Load` results; do not call `Resources.Load(All)` per frame.
- Keep asset paths stable; if moving/renaming, update references and verify Map → Event flows.
- Only place assets in `Assets/Resources/...` when runtime loading is required.

Example (simple cache)
```csharp
static class EventCatalog
{
    private static readonly Dictionary<string, EventScriptableObject> cache = new();

    public static EventScriptableObject Get(string eventId)
    {
        if (cache.TryGetValue(eventId, out var so)) return so;
        so = Resources.Load<EventScriptableObject>($"Events/{eventId}");
        if (so == null) Debug.LogError($"[Events] Missing SO: {eventId}");
        else cache[eventId] = so;
        return so;
    }
}
```

Checklist
- Cache key: stable and matches asset name/ID.
- Verify first access loads; subsequent calls hit cache.
- Avoid cache in edit-time asset import code.

## Editor‑Only Code
- Place editor scripts under `Assets/Editor` and/or guard them with `#if UNITY_EDITOR`.
- Do not include editor‑only assemblies in runtime builds.

Example
```csharp
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(MapTraversalController))]
public class MapTraversalControllerEditor : Editor { /* ... */ }
#endif
```

Checklist
- Editor code is under `Assets/Editor` or wrapped with `#if UNITY_EDITOR`.
- Runtime assemblies have no references to UnityEditor.* APIs.

## Performance (Hot Paths)
- Avoid allocations in `Update/LateUpdate/FixedUpdate`; avoid LINQ/boxing; reuse collections; pool objects created at runtime.
- Avoid reflection/dynamic invocation in hot paths; prefer direct calls or precomputed delegates.
- Profile changes with Unity Profiler; fix regressions before merging.

Example (reuse list instead of LINQ)
```csharp
private readonly List<Card> _tmp = new(32);
void Tick()
{
    _tmp.Clear();
    foreach (var c in _hand) if (c.Cost == 0) _tmp.Add(c);
    // use _tmp ...
}
```

Checklist
- No `new` allocations or LINQ in per-frame loops.
- Object pooling for transient FX/projectiles.
- Profiled frame time and GC allocs unchanged or improved.

## Scene & Build Settings
- Do not modify `ProjectSettings/*` or `EditorBuildSettings.asset` without explicit request and verification.
- Keep scene navigation centralized (e.g., `MapTraversalController`); avoid new hard‑coded scene names and duplicate bootstraps.

Example (centralized routing)
```csharp
public class SceneRouter : MonoBehaviour
{
    [SerializeField] private string eventSceneName = "Event";
    public void GoToEvent() => UnityEngine.SceneManagement.SceneManager.LoadScene(eventSceneName);
}
```

Checklist
- Scene names exposed via serialized fields (not sprinkled constants).
- One bootstrap path per flow; no duplicate initializers in scenes.

## Local Tooling (BMAD)
- Flatten snapshots (`flattened-codebase.xml`, `flatten-*.xml`) and Node artifacts are local developer assets; do not commit.
- Prefer local ignore via `.git/info/exclude` to keep team workflows unaffected.

Checklist
- VS Code tasks run npm scripts; outputs are ignored locally.
- `.git/info/exclude` includes: `.vscode/`, `tools/`, `package*.json`, `node_modules/`, `flatten*.xml`.
- No BMAD artifacts in PRs; optional usage only.

## Coding Conventions (C#)
- Names: `PascalCase` for types/methods/properties, `camelCase` for locals/fields (private fields may use `_camelCase`).
- Access: Mark everything `private` by default; widen only as needed.
- Null safety: Guard inputs; early-return on invalid state. Log with actionable context.
- Serialization: Keep DTOs `[Serializable]` and Unity-serializable fields public or `[SerializeField]`.
- Logging: Use `Debug.Log`, `Debug.LogWarning`, `Debug.LogError` with clear prefixes (e.g., `[EventManager]`).

## PR Checklist
- Code compiles in Unity 2022.3.
- No stray scene/prefab reference breaks; open key scenes to validate.
- New content assets have `.meta` files and live in correct folders.
- Save/Load paths updated when DTOs change.
- Tests added or updated for core logic where practical.
- Changelog entry or PR description explains user impact.

## Support & Questions
- If you’re unsure which service to extend, search for usages in `Assets/Scripts` (`ServiceRegistry`, `IDatabase`, `IEventManager`, `IWalletService`).
- Keep changes incremental and ask for feedback early when altering persistence or scene flow.


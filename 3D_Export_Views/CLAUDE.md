# 3D Export Views — Developer Guide

## Project Overview

Revit add-in that batch-creates 3D isometric views from selected floor/ceiling plans. Uses a modeless WPF window with ExternalEvent handlers for Revit API interaction.

## Architecture

```
Cmd_3DExportViews (IExternalCommand)
  └─ Shows modeless ExportViewsWindow (singleton)
       ├─ "Create"  → ExternalEvent → CreateViewsHandler → ViewCreator
       ├─ "Refresh"  → ExternalEvent → RefreshHandler → re-queries plans & templates
       └─ Click result → ExternalEvent → ActivateViewHandler
```

- **Modeless window** stays open; Revit remains interactive
- **All Revit API calls** go through `IExternalEventHandler` implementations (Revit is single-threaded for API)
- **Single transaction** wraps all view creation per batch
- **Progress updates** use `Dispatcher.PushFrame()` to pump WPF render queue between views (Revit handler blocks the UI thread)

## Key Files

| File | Purpose |
|------|---------|
| `Cmd_3DExportViews.cs` | Command entry point. Creates/shows singleton window, sets up ExternalEvents. |
| `Views/ExportViewsWindow.xaml(.cs)` | Modeless WPF UI — DataGrid with checkbox selection, search filter, discipline, template, progress bar, results display. |
| `Logic/ViewCreator.cs` | Core logic — creates 3D views, section boxes, applies templates. Static methods, transaction-aware. |
| `Handlers/CreateViewsHandler.cs` | `IExternalEventHandler` — bridges UI "Create" action to ViewCreator. Reports per-view progress. |
| `Handlers/RefreshHandler.cs` | `IExternalEventHandler` — re-queries plans and templates from the active document. |
| `Handlers/ActivateViewHandler.cs` | `IExternalEventHandler` — activates a view when user double-clicks a result entry. |
| `Common/GlobalUsing.cs` | Global using statements for the project. |
| `Common/ButtonDataClass.cs` | Ribbon button creation helper with icon support. |

## UI Features

- **DataGrid with checkbox column** — toggle-all via header click (operates on visible/filtered rows only)
- **Search filter** — case-insensitive substring match on plan name
- **Live selection count** — "X of Y selected" label
- **Shift+click range selection** — within visible rows
- **Multi-select toggle** — click checkbox when multiple rows are highlighted
- **Refresh button** (U+21BB ↻) — re-queries plans and templates from the document
- **Determinate progress bar** — updates per-view via dispatcher pump, with "N / Total" counter text
- **Result counts** — "(X created, Y failed)" in the Results group header
- **Double-click result** — activates the created view in Revit

## Revit API Patterns

- **View creation:** `View3D.CreateIsometric(doc, viewFamilyTypeId)`
- **Section box:** `view3d.SetSectionBox(boundingBox)` — XY from `ViewPlan.CropBox` (transform-aware), Z from `ViewPlan.GetViewRange()`
- **View template:** `view3d.ViewTemplateId = templateId` — applied BEFORE section box to prevent template override
- **Orientation:** Copied from project's default `{3D}` view via `GetOrientation()`
- **Naming:** `"3D - {DISCIPLINE} - {LevelName}"` with unique collision handling
- **Template filtering:** Only `View3D` templates are collected (`.OfClass(typeof(View3D)).Where(v => v.IsTemplate)`), ensuring compatibility with created 3D views

## Error Handling

- Per-view try-catch in `ViewCreator.CreateViews` — one failure doesn't abort the batch
- Top-level try-catch in all `IExternalEventHandler.Execute` methods — unhandled exceptions are logged and the UI is notified with fallback results (prevents Revit crash)
- Section box Z inversion guard — swaps bottom/top if view range is misconfigured
- Null `GenLevel` guard — ceiling plans can have null GenLevel, falls back to elevation 0.0
- Shift-click bounds check — validates `_lastClickedIndex` is within visible item count

## Debug Logging

All files use `Debug.WriteLine()` with prefixed tags for tracing in the Visual Studio Output window:
- `[Cmd_3DExportViews]` — command startup, plan/template counts
- `[ExportViewsWindow]` — UI events (create, refresh, toggle-all, activate, close)
- `[CreateViewsHandler]` — handler entry/exit, exception fallback
- `[RefreshHandler]` — handler entry/exit, counts
- `[ActivateViewHandler]` — view activation, null/invalid guards
- `[ViewCreator]` — per-view creation, section box dimensions, name collisions, orientation source, warnings

## Unicode Characters Used

| Character | Code Point | Usage |
|-----------|-----------|-------|
| ✓ | U+2713 (Check Mark) | DataGrid "✓ All" header, success prefix in results |
| ✗ | U+2717 (Ballot X) | Failure prefix in results |
| ↻ | U+21BB (Clockwise Open Circle Arrow) | Refresh button icon |

## Build

- Multi-version: Revit 2023–2026
- R23–R24: .NET Framework 4.8 | R25–R26: .NET 8.0
- Post-build copies to `%AppData%\Autodesk\REVIT\Addins\{version}\_3D_Export_Views`
- Dev target: R2026

## Conventions

- Transaction ownership lives in `ViewCreator`, not in handlers
- ExternalEvent shared state is set on UI thread before `Raise()`
- Window uses `Hide()` on close for singleton reuse
- Error handling per-view (one failure doesn't abort the batch)
- All handlers wrapped in try-catch to prevent Revit crashes

## Naming Rules

- **Always use descriptive variable names.** No single-letter or abbreviated variables.
- This applies everywhere: locals, loop variables, LINQ lambdas, event handler parameters.
- LINQ lambdas must use the full type name: `viewPlan => viewPlan.Name`, never `v => v.Name`.
- Event parameters: use `sender` / `args`, never `s` / `e` / `ev`.
- Loop counters: use meaningful names like `rangeIndex`, never bare `i`.
- Examples: `transaction` not `t`, `template` not `t`, `planItem` not `pi`, `viewResult` not `r`.

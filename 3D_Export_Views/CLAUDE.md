# 3D Export Views — Developer Guide

## Project Overview

Revit add-in that batch-creates 3D isometric views from selected floor/ceiling plans. Uses a modeless WPF window with ExternalEvent handlers for Revit API interaction.

## Architecture

```
Cmd_3DExportViews (IExternalCommand)
  └─ Shows modeless ExportViewsWindow (singleton)
       ├─ "Create" → ExternalEvent → CreateViewsHandler → ViewCreator
       └─ Click result → ExternalEvent → ActivateViewHandler
```

- **Modeless window** stays open; Revit remains interactive
- **All Revit API calls** go through `IExternalEventHandler` implementations (Revit is single-threaded for API)
- **Single transaction** wraps all view creation per batch

## Key Files

| File | Purpose |
|------|---------|
| `Cmd_3DExportViews.cs` | Command entry point. Creates/shows singleton window, sets up ExternalEvents. |
| `Views/ExportViewsWindow.xaml(.cs)` | Modeless WPF UI — view selection, discipline, template, results display. |
| `Logic/ViewCreator.cs` | Core logic — creates 3D views, section boxes, applies templates. Static methods, transaction-aware. |
| `Handlers/CreateViewsHandler.cs` | `IExternalEventHandler` — bridges UI "Create" action to ViewCreator in Revit API context. |
| `Handlers/ActivateViewHandler.cs` | `IExternalEventHandler` — activates a view when user clicks a result entry. |
| `Common/GlobalUsing.cs` | Global using statements for the project. |
| `Common/ButtonDataClass.cs` | Ribbon button creation helper with icon support. |

## Revit API Patterns

- **View creation:** `View3D.CreateIsometric(doc, viewFamilyTypeId, orientation)`
- **Section box:** `view3d.SetSectionBox(boundingBox)` — XY from `ViewPlan.CropBox` (transform-aware), Z from `ViewPlan.GetViewRange()`
- **View template:** `view3d.ViewTemplateId = templateId` — applied BEFORE section box to prevent template override
- **Orientation:** Copied from project's default `{3D}` view via `GetOrientation()`
- **Naming:** `"3D - {DISCIPLINE} - {LevelName}"` with unique collision handling

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

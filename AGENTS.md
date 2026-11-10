# Agent rules — rir.atalaya.network

Fork of Rhino.Inside.Revit (RiR) used by Atalaya. Follow [`CONVENTIONS.md`](CONVENTIONS.md) and [`BUILDSOURCE.md`](BUILDSOURCE.md).

## Scope

- Prefer small, RiR-shaped Grasshopper components under `src/RhinoInside.Revit.GH/`.
- Match existing patterns: `ZuiComponent` / transactional bases, `ParamDefinition`, `External.DB.Extensions`, singular Revit terminology.
- Do not invent a parallel plugin, installer, or component library outside this tree unless explicitly asked.

## Build & deploy (this machine)

- Configurations look like `Debug-2026-R9` (Revit year + Rhino major). Revit **2026.5+** APIs need `net10.0-windows` (see `src/RevitAPI.targets`).
- Debug builds deploy to `%APPDATA%\Autodesk\Revit\Addins\<year>\` (per-user, no admin).
- Machine install lives under `C:\ProgramData\Autodesk\Revit\Addins\<year>\` and **requires admin** to rename/overwrite.
- **Never leave both** `ProgramData\...\RhinoInside.Revit.addin` and `AppData\...\RhinoInside.Revit.addin` enabled: same `AddInId`, unpredictable load. Keep stock ProgramData active until ready to switch; then disable ProgramData once (admin) and use AppData for iteration.
- Helper: [`script/disable-programdata-rir-addin.bat`](script/disable-programdata-rir-addin.bat) (elevated, one-time).
- After swapping add-ins, restart Revit. Do not expect a live session to pick up a new `.gha` without reload/restart.

## Family file component

- `New Family File` (`Components/Family/FamilyFile.cs`) is disk-only: template → `.rfa` → close. No geometry, no `LoadFamily`.
- Persist `TrackedPath` on the component. Name/folder changes **rename/move**; existing target path is **adopted**. Template is used only on create.
- Prefer tracking over brute recreate/overwrite of family content.

## Safety

- Do not commit secrets, personal absolute paths, or local AppData/ProgramData binaries.
- Do not force-push `1.x` / `main`. Commit only when asked; push when asked.
- Do not start Revit/Rhino installers or cloud batch jobs unless requested.

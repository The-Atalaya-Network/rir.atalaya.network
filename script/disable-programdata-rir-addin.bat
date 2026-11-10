@echo off
echo Disabling ProgramData Rhino.Inside.Revit add-ins (one-time)...
for %%Y in (2025 2026) do (
  if exist "C:\ProgramData\Autodesk\Revit\Addins\%%Y\RhinoInside.Revit.addin" (
    move /Y "C:\ProgramData\Autodesk\Revit\Addins\%%Y\RhinoInside.Revit.addin" "C:\ProgramData\Autodesk\Revit\Addins\%%Y\RhinoInside.Revit.addin.disabled"
    echo Disabled %%Y
  ) else (
    echo Already disabled or missing %%Y
  )
)
echo.
echo AppData debug build will load on next Revit start:
echo   %APPDATA%\Autodesk\Revit\Addins\2026\RhinoInside.Revit.addin
pause

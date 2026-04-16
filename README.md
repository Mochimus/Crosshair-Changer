# Data Center Crosshair

MelonLoader **0.7.x** **IL2CPP** mod for **Data Center** (Steam). Adjusts the HUD reticle and the bandwidth / pointer readout under the crosshair.

**Author:** Mochimus  
**Version:** 0.1.0

## Features

- Crosshair dot: size and color (`DataCenterCrosshair.txt` in the game install folder).
- Pointer label (e.g. `0 / 10 Gbps`): font size, color, optional dark panel behind text, vertical/horizontal lift.
- Label position uses a Harmony prefix on `RectTransform.anchoredPosition` so lifts apply reliably under Il2Cpp.

## Build

Requires a local Data Center install with MelonLoader (paths in `Directory.Build.props`).

```bash
dotnet build Crosshair.csproj -c Release
```

Optional copy to game `Mods` folder:

```bash
dotnet build Crosshair.csproj -c Release -p:CopyToGameMods=true
```

Output: `bin/Release/DataCenterCrosshair.dll` → place in `Data Center\Mods\`.

## Configure

Edit **`DataCenterCrosshair.txt`** next to the game executable. The mod creates a commented template on first run. See comments in that file for keys (`size`, `color`, `pointerLabelLiftY`, etc.).

## Requirements

- .NET 6 SDK
- MelonLoader net6 + Il2CppInterop assemblies from the game’s `MelonLoader` folder (referenced via `Directory.Build.props`)

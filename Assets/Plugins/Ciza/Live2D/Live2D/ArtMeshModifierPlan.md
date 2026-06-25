# ArtMesh Modifier Plan

## Background

Current tests show that a single global Linear/Gamma compensation formula cannot fit every Live2D part.

Opaque ArtMeshes usually do not need special compensation. The real mismatch appears in specific semi-transparent parts such as glow, light beams, haze, and other gamma-authored effects. However, other semi-transparent assets, such as clouds, can already look correct and become worse when the same formula is applied globally.

Conclusion: stop treating this as a global shader formula problem. Treat it as an art-directed per-ArtMesh modifier setting.

## New Direction

For each Live2D model folder, allow an ArtMesh modifier text file in the parent folder of the generated prefab / model import files. The file must use the same base name as the Live2D prefab, with `.ArtMeshModifier.txt` appended.

Example folder:

```text
Models/
  SUM05_Overdrive01.ArtMeshModifier.txt
  SUM05_Overdrive01/
    SUM05_Overdrive01.model3.json
    SUM05_Overdrive01.prefab
  Rice.ArtMeshModifier.txt
  Rice/
    Rice.model3.json
    Rice.prefab
```

Example file content:

```csv
; ArtMeshName,TintMultiplier
; Only list ArtMeshes that need a modifier.
ArtMesh15,FFFFFF255
ArtMeshLight01,FFFFFF128
ArtMeshGlowBody,FFD0D0255
```

The importer reads this file from the model folder's parent folder during model import / prefab generation and applies the listed value only to the matching ArtMesh renderer on that prefab. On the first import, if the file does not exist yet, the importer should create a minimal template file automatically. If the file already exists, the importer must preserve it and never overwrite or append art-authored values.

When an existing `.ArtMeshModifier.txt` file changes, the asset pipeline should reimport the matching `.model3.json` automatically so the generated prefab receives the latest values.

## Value Meaning

Use one unified name for this value:

```text
TintMultiplier
```

The text file format is:

```text
ArtMeshName,TintMultiplier
```

`TintMultiplier` means "this ArtMesh has an author-specified RGB tint and alpha multiplier." It is intentionally not named after Gamma or Linear. The current use case is to tune specific semi-transparent ArtMeshes, but the API should stay simple: it controls this ArtMesh's final tint and opacity multiplier.

`TintMultiplier` should be stored on `CubismRenderer` and written to the renderer's material properties at runtime. It should not be treated as a global material value.

The value format is 6-digit hex RGB followed by an alpha byte value:

```text
RRGGBBAlpha
```

The RGB section is copied directly from Unity's hex color field, which usually does not include alpha. The alpha section is a decimal integer in `0..255` appended after the 6 RGB hex digits, matching the usual byte-style color value range.

```text
FFFFFF255 = original/default color and opacity
FFFFFF128 = keep RGB, reduce opacity to about 50%
FFD0D0255 = tint toward light red, keep opacity
8080FF255 = reduce red/green, keep blue and opacity
```

The default value is `FFFFFF255`. A value of `FFFFFF255` means normal/default tint for this ArtMesh. Only ArtMeshes listed in `{PrefabName}.ArtMeshModifier.txt` should receive a non-default author modifier. ArtMeshes not listed in the file remain at `FFFFFF255`.

Important naming note: `TintMultiplier` is an RGB tint plus alpha multiplier for the ArtMesh, not a Gamma/Linear correction power and not a fixed replacement color. If future shader formulas need another curve or compensation value, use a different property name instead of changing this meaning.

Suggested C# naming:

```csharp
[SerializeField]
private Color _tintMultiplier = Color.white;

public Color TintMultiplier
{
    get { return _tintMultiplier; }
    set { _tintMultiplier = value; }
}
```

Suggested shader property naming:

```hlsl
cubism_TintMultiplier("Tint Multiplier", Color) = (1, 1, 1, 1)
```

Suggested shader variable constant:

```csharp
public const string TintMultiplier = "cubism_TintMultiplier";
```

`TintMultiplier` should be applied through `MaterialPropertyBlock`, following the existing `MultiplyColor`, `ScreenColor`, and `ModelOpacity` pattern. Avoid writing directly to the shared `Material`, because that can affect every ArtMesh using the same material.

Suggested runtime flow:

```text
Importer reads ../{PrefabName}.ArtMeshModifier.txt from the model folder's parent folder
Importer writes CubismRenderer.TintMultiplier on matching ArtMeshes
CubismRenderer.TryInitialize()
CubismRenderer.TryInitializeTintMultiplier()
CubismRenderer.ApplyTintMultiplier()
MaterialPropertyBlock.SetColor(cubism_TintMultiplier, TintMultiplier)
```

Do not put this in `TryInitializeMesh()`. Mesh initialization can return early when the mesh already exists, while `TintMultiplier` must be applied every renderer initialization.

## Importer Behavior

1. Build the modifier file path from the prefab base name: `{ParentFolderOfModelFolder}/{PrefabName}.ArtMeshModifier.txt`.
2. Find that file in the parent folder of the model prefab / `.model3.json` folder.
3. If the file does not exist on first import, create a minimal template file automatically. The template should contain comments and format examples only; do not list every ArtMesh.
4. If the file already exists, read it without overwriting it and without appending newly discovered ArtMeshes.
5. Parse each non-empty, non-comment line as `ArtMeshName,TintMultiplier`. Comment lines start with `; `.
6. Match `ArtMeshName` against the generated ArtMesh `GameObject.name`. In practice this should align with the ArtMesh name.
7. Write `TintMultiplier` only to the matched `CubismRenderer`.
8. Leave all unmatched ArtMeshes at the default value.
9. Log warnings for malformed lines, malformed RGB hex values, malformed alpha byte values, or missing ArtMesh names, but do not fail the import.

## Why This Direction

- Keeps clouds, smoke, and already-correct transparent parts untouched.
- Allows glow/light/haze parts to be tuned individually.
- Lets artists adjust color and opacity from the same reviewable file.
- Avoids endless global shader formula tuning.
- Makes the correction visible and reviewable beside the model folder.
- Fits the importer workflow: the prefab can be regenerated and still receive the same art-directed modifiers.

## Open Questions

- Should unmatched modifier entries warn every import, or only when verbose logging is enabled?
- Should `TintMultiplier` be shown in the normal `CubismRenderer` inspector, or hidden unless debug/advanced mode is enabled?
- Should old `.ArtMeshOverrides.txt` / `AlphaOverride` files be migrated automatically, or treated as obsolete?

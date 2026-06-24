# ArtMesh AlphaOverride Plan

## Background

Current tests show that a single global Linear/Gamma compensation formula cannot fit every Live2D part.

Opaque ArtMeshes usually do not need special alpha compensation. The real mismatch appears in specific semi-transparent parts such as glow, light beams, haze, and other gamma-authored effects. However, other semi-transparent assets, such as clouds, can already look correct and become worse when the same formula is applied globally.

Conclusion: stop treating this as a global shader formula problem. Treat it as an art-directed per-ArtMesh override setting.

## New Direction

For each Live2D model folder, allow an ArtMesh override text file in the parent folder of the generated prefab / model import files. The file must use the same base name as the Live2D prefab, with `.ArtMeshOverrides.txt` appended.

Example folder:

```text
Models/
  SUM05_Overdrive01.ArtMeshOverrides.txt
  SUM05_Overdrive01/
    SUM05_Overdrive01.model3.json
    SUM05_Overdrive01.prefab
  MaskWoman.ArtMeshOverrides.txt
  MaskWoman/
    MaskWoman.model3.json
    MaskWoman.prefab
```

Example file content:

```csv
; ArtMeshName,AlphaOverride
; Only list ArtMeshes that need an override.
ArtMesh,0.5
ArtMeshLight01,0.8
ArtMeshGlowBody,1.0
```

The importer reads this file from the model folder's parent folder during model import / prefab generation and applies the listed value only to the matching ArtMesh renderer on that prefab. On the first import, if the file does not exist yet, the importer should create a minimal template file automatically. If the file already exists, the importer must preserve it and never overwrite or append art-authored values.

When an existing `.ArtMeshOverrides.txt` file changes, the asset pipeline should reimport the matching `.model3.json` automatically so the generated prefab receives the latest values.

## Value Meaning

Use one unified name for this value:

```text
AlphaOverride
```

The text file format is:

```text
ArtMeshName,AlphaOverride
```

`AlphaOverride` is intentionally not named after Gamma or Linear. The field means "this ArtMesh has an author-specified opacity multiplier." The current use case is to tune specific semi-transparent ArtMeshes, but the API should stay simple: it controls this ArtMesh's opacity.

`AlphaOverride` should be stored on `CubismRenderer` and written to the renderer's material properties at runtime. It should not be treated as a global material value.

The valid range is `0..1`.

```text
0 = fully transparent
1 = original/default opacity
```

The default value is `1`. A value of `1` means normal/default opacity for this ArtMesh. Only ArtMeshes listed in `{PrefabName}.ArtMeshOverrides.txt` should receive a non-default author override. ArtMeshes not listed in the file remain at `1`.

Important naming note: `AlphaOverride` is an opacity multiplier for the ArtMesh, not a Gamma/Linear correction power. If future shader formulas need another curve or compensation value, use a different property name instead of changing this meaning.

Suggested C# naming:

```csharp
[SerializeField]
private float _alphaOverride = 1f;

public float AlphaOverride
{
    get { return _alphaOverride; }
    set { _alphaOverride = Mathf.Clamp01(value); }
}
```

Suggested shader property naming:

```hlsl
cubism_AlphaOverride("Alpha Override", Range(0, 1)) = 1
```

Suggested shader variable constant:

```csharp
public const string AlphaOverride = "cubism_AlphaOverride";
```

`AlphaOverride` should be applied through `MaterialPropertyBlock`, following the existing `MultiplyColor`, `ScreenColor`, and `ModelOpacity` pattern. Avoid writing directly to the shared `Material`, because that can affect every ArtMesh using the same material.

Suggested runtime flow:

```text
Importer reads ../{PrefabName}.ArtMeshOverrides.txt from the model folder's parent folder
Importer writes CubismRenderer.AlphaOverride on matching ArtMeshes
CubismRenderer.TryInitialize()
CubismRenderer.TryInitializeAlphaOverride()
CubismRenderer.ApplyAlphaOverride()
MaterialPropertyBlock.SetFloat(cubism_AlphaOverride, AlphaOverride)
```

Do not put this in `TryInitializeMesh()`. Mesh initialization can return early when the mesh already exists, while `AlphaOverride` must be applied every renderer initialization.

## Importer Behavior

1. Build the override file path from the prefab base name: `{ParentFolderOfModelFolder}/{PrefabName}.ArtMeshOverrides.txt`.
2. Find that file in the parent folder of the model prefab / `.model3.json` folder.
3. If the file does not exist on first import, create a minimal template file automatically. The template should contain comments and format examples only; do not list every ArtMesh.
4. If the file already exists, read it without overwriting it and without appending newly discovered ArtMeshes.
5. Parse each non-empty, non-comment line as `ArtMeshName,AlphaOverride`. Comment lines start with `; `.
6. Match `ArtMeshName` against the generated ArtMesh `GameObject.name`. In practice this should align with the ArtMesh name.
7. Write `AlphaOverride` only to the matched `CubismRenderer`.
8. Leave all unmatched ArtMeshes at the default value.
9. Log warnings for malformed lines or missing ArtMesh names, but do not fail the import.

## Why This Direction

- Keeps clouds, smoke, and already-correct transparent parts untouched.
- Allows glow/light/haze parts to be tuned individually.
- Avoids endless global shader formula tuning.
- Makes the correction visible and reviewable beside the model folder.
- Fits the importer workflow: the prefab can be regenerated and still receive the same art-directed overrides.

## Open Questions

- Should unmatched override entries warn every import, or only when verbose logging is enabled?
- Should `AlphaOverride` be shown in the normal `CubismRenderer` inspector, or hidden unless debug/advanced mode is enabled?

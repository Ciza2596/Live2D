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
; ArtMeshName,TintMultiplier,IsUseGammaInLinear
; Only list ArtMeshes that need a modifier.
; Format: RRGGBBAlpha, for example FFFFFF255 or FFFFFF128.
; Example:
; ArtMesh, FFFFFF255, true
```

The importer reads this file from the model folder's parent folder during model import / prefab generation and applies the listed value only to the matching ArtMesh renderer on that prefab. On the first import, if the file does not exist yet, the importer should create a minimal template file automatically. If the file already exists, the importer must preserve it and never overwrite or append art-authored values.

When an existing `.ArtMeshModifier.txt` file changes, the asset pipeline should reimport the matching `.model3.json` automatically so the generated prefab receives the latest values.

If `.ArtMeshModifier.txt` changes while the Editor is in Play Mode, queue the matching `.model3.json` reimport and run it after Play Mode exits. Do not force prefab regeneration during Play Mode.

## Value Meaning

Use one unified name for this value:

```text
TintMultiplier
```

The text file format is:

```text
ArtMeshName,TintMultiplier,IsUseGammaInLinear
```

`TintMultiplier` means "this ArtMesh has an author-specified RGB tint and alpha multiplier." It is intentionally not named after Gamma or Linear. The current use case is to tune specific semi-transparent ArtMeshes, but the API should stay simple: it controls this ArtMesh's final tint and opacity multiplier.

`TintMultiplier` should be stored on `CubismRenderer` and written to the renderer's material properties at runtime. It should not be treated as a global material value.

`IsUseGammaInLinear` means "this ArtMesh should use the Gamma-authored alpha correction path while the project is running in Linear color space." It is a per-ArtMesh boolean flag. It should default to `false`, so existing models and ArtMeshes that are not listed in the modifier file keep the normal Unlit rendering path.

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

[SerializeField]
private bool _isUseGammaInLinear;

public Color TintMultiplier
{
    get { return _tintMultiplier; }
    set { _tintMultiplier = value; }
}

public bool IsUseGammaInLinear
{
    get { return _isUseGammaInLinear; }
    set { _isUseGammaInLinear = value; }
}
```

Suggested shader property naming:

```hlsl
cubism_TintMultiplier("Tint Multiplier", Color) = (1, 1, 1, 1)
[PerRendererData] cubism_IsUseGammaInLinear("Is Use Gamma In Linear", Float) = 0
```

Suggested shader variable constant:

```csharp
public const string TintMultiplier = "cubism_TintMultiplier";
public const string IsUseGammaInLinear = "cubism_IsUseGammaInLinear";
```

`TintMultiplier` and `IsUseGammaInLinear` should be applied through `MaterialPropertyBlock`, following the existing `MultiplyColor`, `ScreenColor`, and `ModelOpacity` pattern. Avoid writing directly to the shared `Material`, because that can affect every ArtMesh using the same material.

`IsUseGammaInLinear` should only be added to the normal `Live2D Cubism/Unlit` shader path. Do not add this feature to `UnlitMasked`, `UnlitMaskedInverted`, or other mask shader variants for now. Masked ArtMeshes should keep their current rendering behavior even if a modifier file contains `IsUseGammaInLinear=true`.

The alpha correction power should not be exposed as a field. Use a fixed power of `0.7`, equivalent to multiplying the corrected RGB by `pow(saturate(alpha), 0.7)`.

Suggested Unlit shader behavior:

```hlsl
if (cubism_IsUseGammaInLinear > 0.5)
{
    textureColor.rgb = CubismLinearToGamma(textureColor.rgb);
    multiplyColor.rgb = CubismLinearToGamma(multiplyColor.rgb);
    screenColor.rgb = CubismLinearToGamma(screenColor.rgb);
}

textureColor.rgb *= multiplyColor.rgb;
textureColor.rgb = (textureColor.rgb + screenColor.rgb) - (textureColor.rgb * screenColor.rgb);

fixed4 OUT = textureColor * vertexColor;

if (cubism_IsUseGammaInLinear > 0.5)
{
    OUT.rgb *= pow(saturate(OUT.a), 0.7);
    OUT.rgb = CubismGammaToLinear(OUT.rgb);
}
else
{
    OUT.rgb *= OUT.a;
}
```

Suggested runtime flow:

```text
Importer reads ../{PrefabName}.ArtMeshModifier.txt from the model folder's parent folder
Importer writes CubismRenderer.TintMultiplier and CubismRenderer.IsUseGammaInLinear on matching ArtMeshes
CubismRenderer.TryInitialize()
CubismRenderer.TryInitializeTintMultiplier()
CubismRenderer.TryInitializeIsUseGammaInLinear()
CubismRenderer.ApplyTintMultiplier()
CubismRenderer.ApplyIsUseGammaInLinear()
MaterialPropertyBlock.SetColor(cubism_TintMultiplier, TintMultiplier)
MaterialPropertyBlock.SetFloat(cubism_IsUseGammaInLinear, ShouldApplyGammaInLinear() ? 1f : 0f)
```

Do not put this in `TryInitializeMesh()`. Mesh initialization can return early when the mesh already exists, while `TintMultiplier` and `IsUseGammaInLinear` must be applied every renderer initialization.

## Importer Behavior

1. Build the modifier file path from the prefab base name: `{ParentFolderOfModelFolder}/{PrefabName}.ArtMeshModifier.txt`.
2. Find that file in the parent folder of the model prefab / `.model3.json` folder.
3. If the file does not exist on first import, create a minimal template file automatically. The template should contain comments and format examples only; do not list every ArtMesh.
4. If the file already exists, read it without overwriting it and without appending newly discovered ArtMeshes.
5. Parse each non-empty, non-comment line as `ArtMeshName,TintMultiplier,IsUseGammaInLinear`. Comment lines start with `; `.
6. Match `ArtMeshName` against the generated ArtMesh `GameObject.name`. In practice this should align with the ArtMesh name.
7. For backward compatibility, accept old two-column lines as `ArtMeshName,TintMultiplier` and treat `IsUseGammaInLinear` as `false`.
8. Parse `IsUseGammaInLinear` as a boolean value. Valid values are `true` and `false`.
9. Write `TintMultiplier` and `IsUseGammaInLinear` only to the matched `CubismRenderer`.
10. Leave all unmatched ArtMeshes at the default values: `TintMultiplier = FFFFFF255` and `IsUseGammaInLinear = false`.
11. Log warnings for malformed lines, malformed RGB hex values, malformed alpha byte values, malformed boolean values, or missing ArtMesh names, but do not fail the import.

## Inspector Placement

`TintMultiplier` and `IsUseGammaInLinear` are custom Ciza-added fields, not part of the original Live2D Cubism renderer field group. Show them at the bottom of `CubismRendererInspector`, separated from the original fields by five `EditorGUILayout.Space()` calls and a bold `Modifier` header.

Future Ciza-added fields should follow the same rule: keep the original Live2D fields in their existing order, then place new custom fields at the bottom after the same five-space separation and `Modifier` header.

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

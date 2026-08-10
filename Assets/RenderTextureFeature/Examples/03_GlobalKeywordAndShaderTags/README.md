# 03 - Global Shader Keyword and Shader Tags

This example demonstrates two advanced output settings together without an
override material:

- `Shader Tags` selects a custom shader pass whose ShaderLab `LightMode` is
  `RTFKeywordCapture`.
- `Global Shader Keywords` enables `_RTF_KEYWORD_CAPTURE_ON` immediately before
  the renderer list is drawn and disables it immediately afterward.

Open `Example.unity` and enter Play mode. The scene camera already uses
`GlobalKeywordAndShaderTagsExampleRenderer`. The capsule remains blue in the
normal camera render and receives a green debug overlay from the captured mask.

## How the shader is structured

`KeywordAndShaderTagObject.shader` contains two passes:

```text
UniversalForward    -> draws the blue capsule into the normal camera
RTFKeywordCapture   -> draws into _KeywordAndShaderTagExample only
```

The capture pass declares:

```hlsl
#pragma multi_compile _ _RTF_KEYWORD_CAPTURE_ON
```

It writes white only while `_RTF_KEYWORD_CAPTURE_ON` is enabled. The configured
keyword actions are:

```text
Before Render: Enable
After Render:  Disable
```

The after action is explicit state assignment, not automatic restoration. This
example knows the desired state after the pass is disabled.

The feature's validation message about global-state modification is expected in
this example. It is a performance/scheduling warning, not a missing-reference or
rendering error.

## Why use Shader Tags?

Use a custom `LightMode` tag when an object's own shader contains a dedicated
capture pass—for example object IDs, selection masks, simplified normals, or
effect-specific data. It lets the feature select that pass without replacing the
object's material.

`Shader Tags` matches ShaderLab `LightMode`, not the pass `Name` and not the
shader asset name. Here the required value is exactly `RTFKeywordCapture`.

## Why use a global keyword?

Use a global keyword only when the renderer-list shaders genuinely need a shared
variant state for this draw. A typical case is using the same capture pass in
several contexts while switching a project-wide capture behavior.

Global keyword changes reduce Render Graph scheduling freedom and can affect
other passes if their final state is not set deliberately. Prefer material
properties or local shader keywords when the state belongs to one material.

## Things to try

- Change `Shader Tags[0]` to another string: the custom pass no longer matches,
  so the capsule disappears from the captured texture.
- Set `Before Render Mode` to `None`: the pass still matches, but its shader
  writes black because the keyword variant is not enabled.
- Change `After Render Mode` to `None`: the keyword remains enabled and can leak
  into later passes, which is why the example explicitly disables it.

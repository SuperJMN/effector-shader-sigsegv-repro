# Effector 0.5.0 — Runtime Shader SIGSEGV Reproduction

Minimal reproduction for a native crash (SIGSEGV, exit code 139) when applying
any Effector **runtime SkSL shader effect** (`ISkiaShaderEffectFactory`) to an
Avalonia control on Linux with an NVIDIA GPU.

## Observed behavior

- **Filter-based effects** (`ISkiaEffectFactory` → `SKImageFilter`) work fine.
- **Runtime shader effects** (`ISkiaShaderEffectFactory` → SkSL) cause SIGSEGV.
- The crash occurs **after** the managed rendering code completes successfully
  (Effector trace shows `end:overlay-done` and `end:snapshot-ok` before the
  process is killed by signal 11).
- Setting `EFFECTOR_ENABLE_DIRECT_RUNTIME_SHADERS=false` (CPU fallback) does
  **not** prevent the crash.

## Environment

| Component        | Version                          |
| ---------------- | -------------------------------- |
| OS               | Ubuntu 24.04 (Linux x86_64)      |
| GPU              | NVIDIA GeForce GTX 1650          |
| NVIDIA driver    | 590.48.01                        |
| .NET SDK         | 10.0.201                         |
| Avalonia         | 12.0.0                           |
| Effector         | 0.5.0                            |
| SkiaSharp        | 3.x (via Avalonia 12)            |

## Steps to reproduce

```bash
dotnet run
```

The app opens a window with a blue panel.  After 2 seconds it applies a trivial
runtime SkSL shader (`RedTintShader`).  On affected systems the process crashes
immediately with exit code 139 (SIGSEGV).

## Effector trace output

With `EFFECTOR_SHADER_TRACE_PATH=/tmp/trace.log`, the trace shows:

```
begin:patched → begin:create-capture-context → begin:capture-context-created
→ begin:pushed → initial-transform → adjust-transform (×4)
→ end:patched → end:popped → end:flush-canvas → end:flush-surface
→ end:snapshot → end:snapshot-ok → end:base-reset → end:base-draw-image
→ end:overlay → end:overlay-done
```

…then the process dies.  The managed rendering path completes, but something in
the subsequent GPU compositing / deferred-resource cycle triggers a native fault.

## Diagnostic notes

- A `BlurEffect` (built-in Avalonia, no Effector) on the same panel works fine.
- A `SpriteTintEffect` (Effector **filter**-based, `SKImageFilter`) works fine.
- Only Effector **shader**-based effects crash.
- The crash is consistent across multiple shader sources (complex multi-layer
  shader and trivial single-line red tint).

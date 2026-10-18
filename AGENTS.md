# AGENTS.md — MEFrpLauncherX (PML 2)

Avalonia 12 / .NET 10 desktop launcher for ME Frp. Windows + Linux + macOS.
Solution: `MEFrpLauncherX.sln` (11 projects). ~358 `.cs` / 91 `.axaml` files, all in `MEFrpLauncherX/` and `MEFrpLauncherX.Core/`.

## Build & run

- `dotnet build MEFrpLauncherX.sln` — main app is `MEFrpLauncherX/MEFrpLauncherX.csproj`, library is `MEFrpLauncherX.Core/`.
- `dotnet run --project MEFrpLauncherX/MEFrpLauncherX.csproj`
- **A running PML 2 instance breaks the build**: the final copy step fails with `MSB3021`/`MSB3027` and ~10 errors *even though compilation succeeded*. The app is single-instance (`Global\tech.rycb.pml2` mutex in `Program.Main`); stop the running process / debugger first. Expect ~415 warnings on a clean build — warnings are not signal here.
- **Two projects are NOT in the main `.sln`**: `MEFrpLauncherX.CrashDisplayer` (own `.sln`) and `RYCB.PML2.Splash` (own `.slnx`). They are the easiest to forget — run `dotnet run --project tools/P1Verify` to compile all three at once and get a pass/fail exit code.
- `FluentAvalonia.MarkdownRender` is a **git submodule**; clone with `--recursive`. Its csproj is one directory deeper than it used to be: `FluentAvalonia.MarkdownRender\FluentAvalonia.MarkdownRender\FluentAvalonia.MarkdownRender.csproj`. Code changes there are mirrored by hand to `MarkdownRenderX.Avalonia` (same repo, independent copy) — and committed in the submodule, not here.
- Central Package Management: every `PackageReference` in the repo (including `tools/*` and the two out-of-sln projects) must be **versionless**; versions live only in root `Directory.Packages.props`. `SkiaSharp` and its 4 `NativeAssets.*` entries must stay aligned or restore fails with `NU1605`.

## There is no unit test framework

No xunit/nunit/mstest anywhere. Verification is done with standalone probes in `tools/` (`ExcludeFromSolution`, not built by the sln):

| Probe | Command | Notes |
|---|---|---|
| `P1Verify` | `dotnet run --project tools/P1Verify` | compiles sln + CrashDisplayer + Splash |
| `XamlSmoke` | `dotnet run --project tools/XamlSmoke` | catches XAML that compiles but fails at runtime |
| `P3UiRegression` | `dotnet run --project tools/P3UiRegression` | Avalonia Headless, drives real UI + screenshots |
| `TabTransitionVerify` | `dotnet run --project tools/TabTransitionVerify` | plays tab transitions headless |
| `P4Verify` | `dotnet run --project tools/P4Verify` | validates `TrimmerRoots.xml` entries |
| `TelemetryVerify` | `dotnet run --project tools/TelemetryVerify` | asserts telemetry off-switch semantics |

All of them **reflect over the app's already-built output** (the main project is a self-contained `WinExe` and cannot be `ProjectReference`d — NETSDK1151), so build the main project first. If `bin` is locked by a running app, `TabTransitionVerify` falls back to `obj`; otherwise set `PML2_VERIFY_APPDIR`.

`PML2Test/` is a scratch console app, not tests, and references `SecretLib` via a machine-specific path (`G:\VSProj\SecretLib\...\Obfuscar\SecretLib.dll`) — it will not restore on other machines. `PML2CertCheck/` gets `InternalsVisibleTo` access into Core.

## AOT / trimming constraints (non-Debug builds)

Non-`Debug` configurations define the `AOT` constant, set `PublishAot`/`PublishTrimmed`, `IlcDisableReflection`, and `OptimizationPreference=Size`. Consequences you must respect:

- Every type persisted with `System.Text.Json` must be registered in `MEFrpLauncherX.Core/AppJsonSerializerContexts.cs` (and the app-side `AppJsonSerializerContexts.cs`) or serialization breaks only in the shipped build.
- `MEFrpLauncherX/TrimmerRoots.xml` entries with a wrong assembly/type fullname are **silently ignored** by the linker (no error, no warning) — run `tools/P4Verify` after any Avalonia version change. XAML reflection bindings are why main/Core/Plugin/Fonts assemblies are preserved wholesale.
- Some UI rows are built in code instead of XAML bindings on purpose (AOT reflection-binding risk); don't "clean that up".
- Release publish is slow; `dotnet publish ... -r <rid> --self-contained` is what CI does per RID.

## i18n: resx does not regenerate the Designer

Localized strings live in `MEFrpLauncherX.Core/Languages/Languages{,.en-US,.zh-hant}.resx`. `Languages*.Designer.cs` is produced by `PublicResXFileCodeGenerator`, which **only runs inside VS/Rider** — `dotnet build` never regenerates it, so adding a key by hand leaves `Languages.XXX` and `{x:Static}` uncompilable.

- After editing resx: `pwsh tools/sync-lang-designer.ps1` (adds missing properties in generator order).
- Removing keys: `pwsh tools/remove-lang-keys.ps1 -Keys Text.Foo,Text.Bar`.
- Every new key must be added to **all three** resx files (`zh-hant` has no Designer.cs — it falls back at runtime). `Designer.cs` must stay UTF-8 **with BOM**.

## Architecture notes

- MVVM with ReactiveUI + CommunityToolkit.Mvvm. Views/ViewModels in `MEFrpLauncherX/`, business logic and services in `MEFrpLauncherX.Core/`. Navigation: `ViewModels/MainPageFrameViewModel.NavigateToPage("Manage"|"Create"|"Update"|...)`.
- `docs/26.3-entry-map.md` is the feature→file map ("where do I add X"). Read it before touching a feature area.
- **`AppPaths` (`MEFrpLauncherX.Core/AppPaths.cs`) is the single source of truth for paths** since 26.5.0. Do not use `AppDomain.CurrentDomain.BaseDirectory`. Layout: install root = `PML 2.exe` + `launcher.json` + `vXXX/` (read-only code) + `data/` (Config/Cache/Logs/Run). `PML2_INSTALL_ROOT` / `PML2_DATA_ROOT` override. Runtime downloads (mefrpc, lego) go to `data/Run`, never next to the exe. `PML2.Launcher` and Core share `Shared/VersionResolver.cs` (linked compile) — that file must stay using- and nullable-annotation-free.
- Plugin system: YAML in `Config/Plugins/*.yaml`, ~1 s hot reload. `type: create-proxy-template` (resource plugin) vs default `event`. Fire events with `_ = PluginService.Instance.TriggerAsync("domain.action", data)` (never block); `await` only when the process is about to exit. Full trigger/action/telemetry reference: `docs/插件系统埋点说明.md`.
- Telemetry (Sentry + Cloudflare) must stay fire-and-forget, swallowed-on-error, and gated on `IsTelemetryEnabled`; Debug builds send nothing.

## Toolchain quirks worth knowing

- **IconPacks**: the NuGet packages (`IconPacks.Avalonia.*` 2.0.0) are compiled against Avalonia 11 and render **silently blank** on Avalonia 12. Icons come from the source fork at `third_party/IconPacks.Avalonia/src/` via ProjectReference. The `PackageVersion` entries in `Directory.Packages.props` are rollback-only — do not switch back to them.
- **Rendering settings are split**: `Config/Render.json` (render mode, GPU memory, low-latency) must be readable *before* Avalonia starts; `Config/Settings.json` (incl. `AnimationLevel`) loads later. `PML2_RENDER_DIAG=1` (Debug only) overlays FPS/layout/render timings. On Windows, AngleEgl must be tried before Vulkan or the composited swap-chain modes never register.
- Don't animate `Width`/`Height`/`Margin` — it spikes layout time. Use transforms/opacity.
- `.editorconfig` is currently empty (uncommitted), so no formatter/lint is enforced in-repo. Qodana config (`qodana.yaml`) is all defaults.
- `installer/*.iss` `Source` paths must match CI's Windows publish output `MEFrpLauncherX/bin/Release/net10.0/<publish_sub>/publish`; `tools/stage-layout.sh` produces the versioned layout that ISCC consumes. `launcher.json` is written by the installer on Windows (it knows the pre-upgrade `current`), not by the script.
- GitHub Actions: **job-level `if:` cannot reference `secrets`** — the whole workflow fails to parse. Use job-level `env` + step-level `if: env.X != ''`.

## Version numbers live in two places

`MEFrpLauncherX/MEFrpLauncherX.csproj` (`AssemblyVersion` / `Version` / `ApplicationVersion`, currently `26.5.0`) **and** `MEFrpLauncherX.Core/App.cs` (`App.Version`, currently `26.5.1-preview1`). They are currently out of sync; bump both. Plugin `minCoreVersion` is compared against `App.Version`.

## Proprietary binaries (committed, do not "fix" the references)

`MEFrpLauncherX/RYCB.PML2.MEFrpCaptchaLib.dll` and `MEFrpLauncherX/SecretLib.dll` are obfuscated/closed-source but tracked in git and referenced by `HintPath` from both the app and Core. They are required to build. Do not delete, replace, or convert them to PackageReferences.

## Team conventions (from `.clinerules/`)

- Build **once after finishing all goals**, not after each one; max ~5 builds per task.
- Commit when the task is complete and you've checked CI-safety and UX-friendliness; commit messages must clearly state what changed (no template).
- Local framework sources for reference: Avalonia at `G:\VSProj\Avalonia`, FluentAvalonia at `G:\VSProj\fav3` (see `.clinerules/Local Sources.md`). JetBrains MCP is configured in `.mcp.json`.

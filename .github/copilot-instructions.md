# Copilot Instructions

## Project Details

* Const Visualizer – a Visual Studio extension (VSIX) that shows the value of a `const` above the line of C# (and, when the option is on, Visual Basic) code that uses it; the features are described in #file:'README.md', which is short and partly out of date (a Visual Studio 2019 badge, and a build badge that points at another repository). There is no change log in the repository
* Installs into Visual Studio 2022 from 17.14 up to, but not including, 18.0, on `amd64` and `arm64` (see #file:'src/ConstVisualizer/source.extension.vsixmanifest'), so Visual Studio 2026 is not a target; .NET Framework 4.7.2; `LangVersion` is 12.0 (set in #file:'Directory.Build.props'). The Visual Studio range is written in several places that must change together: the two `InstallationTarget` elements of the manifest (one per architecture), its `CoreEditor` prerequisite, `MinimumVisualStudioVersion` (16.0) in the project file and the README badge
* The solution is #file:'ConstVisualizer.slnx', in the repository root on purpose: Visual Studio reads `.mcp.json` from the folder of the solution (see "MCP Servers"). It uses the XML `.slnx` format. It needs the .NET SDK 9.0.200 or later, or the MSBuild of Visual Studio or Build Tools 17.13 or later (`dotnet sln` edits it). It has two old-style (non-SDK) projects: the extension #file:'src/ConstVisualizer/ConstVisualizer.csproj' and its tests #file:'src/ConstVisualizer.Tests/ConstVisualizer.Tests.csproj'. Every `.cs`, content and resource file is listed in them by hand, so a new file is neither compiled nor packaged until it is added there. Package versions are `PackageReference` items in the same files – there is no central package management, and #file:'nuget.config' maps every package to nuget.org
* Windows only: building needs Visual Studio with the "Visual Studio extension development" workload and the .NET Framework 4.7.2 targeting pack. Use the MSBuild of Visual Studio; `dotnet build` does not build VSIX projects
* Build from the repository root in PowerShell. `msbuild` is usually not on `PATH`, so find it with `vswhere` (add `-version '[17.0,18.0)'` to build with Visual Studio 2022 instead of the latest one; the CI runs on `windows-2022`, which has Visual Studio 2022); `DeployExtension=false` keeps the build from installing the extension into the Experimental instance:

  ```powershell
  $msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
  & $msbuild ConstVisualizer.slnx -restore -p:Configuration=Debug -p:DeployExtension=false -v:m
  ```

* Test: `src/ConstVisualizer.Tests` is an MSTest project with 20 tests of the string helpers in `StringExtensions.cs`. Run it from Test Explorer, or after a build with the `vstest.console.exe` of Visual Studio:

  ```powershell
  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -property installationPath
  & "$vs\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" src\ConstVisualizer.Tests\bin\Debug\ConstVisualizer.Tests.dll
  ```

  Everything else (package load, the constant parser, the adornments, the options page) has no automated tests: run the extension in the Visual Studio Experimental instance (F5 starts `devenv.exe /rootsuffix Exp`, see the start options of the project) and open a solution that uses constants in other files
* CI: GitHub Actions (#file:'.github/workflows/build.yml') builds the solution in `Debug` on `windows-2022` for pushes and pull requests to `main` (changes to root-level `.md` files only are ignored), runs the tests through `josepho0918/vstest-action` and uploads the VSIX as an artifact. A step stamps the run number into the version in the manifest and in `Vsix.Version` of `src/ConstVisualizer/source.extension.cs`, so change versions by hand only for a deliberate release bump. The last steps publish a test badge to a gist of the original author; the upload runs on `main` only and needs a `GIST_TOKEN` secret that a fork does not have

## Solution Structure

**IMPORTANT: Every new source file must be added to #file:'src/ConstVisualizer/ConstVisualizer.csproj'** (or to the test project): `<Compile Include>` for code and `<Content Include>` with `IncludeInVSIX` for files that go into the VSIX. A file that is created outside Visual Studio is not added automatically.

```text
/
├── .claude/                                  – Claude Code settings (the vs-agent-plugins plugin) and helper scripts; `skills` is a local, git-ignored link to `.github/skills`
├── .github/                                  – these instructions, the `vs-extension-expert` agent, skills, prompts and workflows (the CI build among them)
├── art/                                      – screenshots (`screenshot.png` is the one the README shows)
├── src/
│   ├── .editorconfig                         – code style and naming rules for everything below `src`
│   ├── ConstVisualizer/                      – the extension, a single old-style project
│   │   ├── ConstVisualizerPackage.cs         – the `AsyncPackage`: option page, solution events, running document table events, the first parse of the solution
│   │   ├── ConstFinder.cs                    – the shared part of the partial class that collects the `const` declarations through the Roslyn workspace
│   │   ├── ConstFinder.CSharp.cs             – the C# part of it
│   │   ├── ConstFinder.VisualBasic.cs        – the Visual Basic part of it
│   │   ├── ResourceAdornmentManagerFactory.cs – MEF export that creates a `ResourceAdornmentManager` for each text view and defines the adornment layer `ConstCommentLayer`
│   │   ├── ResourceAdornmentManager.cs       – finds the usages of known constants on the lines of a text view and adds a `TextBlock` above the line
│   │   ├── MyLineTransformSourceProvider.cs  – MEF export of the `ILineTransformSourceProvider` that creates the `MyLineTransformSource`
│   │   ├── MyLineTransformSource.cs          – makes room above the lines that have an adornment
│   │   ├── MyRunningDocTableEvents.cs        – reloads the constants of the active document when a document is saved or its window is shown
│   │   ├── OptionsGrid.cs                    – the Tools > Options > Const Visualizer page
│   │   ├── OutputPane.cs, ExceptionHelper.cs – the "Const Visualizer" pane of the Output window and the logging of exceptions
│   │   ├── Constants.cs, StringExtensions.cs, TaskExtensions.cs – helpers (`LogAndForget` is in `TaskExtensions.cs`)
│   │   ├── SponsorDetector.cs, SponsorRequestHelper.cs, AnalyticsConfig.cs – sponsorship message and telemetry settings of the original author (see below)
│   │   ├── filestosign.txt                   – the list for signing the release build; the signing target is not in this repository
│   │   ├── GlobalSuppressions.cs
│   │   ├── source.extension.vsixmanifest / .cs – the manifest and the file generated from it
│   │   ├── Properties/AssemblyInfo.cs        – assembly attributes
│   │   ├── Resources/                        – the icon and the license text that go into the VSIX
│   │   └── ConstVisualizer.csproj
│   └── ConstVisualizer.Tests/                – the MSTest project
├── ConstVisualizer.slnx                      – the solution; it lives in the repository root so that Visual Studio finds `.mcp.json`
├── Directory.Build.props                     – `LangVersion`
├── Directory.Build.targets                   – links `.claude/skills` to `.github/skills`
├── nuget.config, LICENSE, README.md
```

### How It Works

* `ConstVisualizerPackage` is an `AsyncPackage` that loads in the background when a C# or Visual Basic project is loaded. `InitializeAsync` switches to the UI thread, writes the name and version to the Output pane, stores `Instance`, reads the font size and the color of strings from the Fonts and Colors settings, subscribes to `SolutionEvents.OnAfterOpenSolution` and `OnAfterCloseSolution`, advises the running document table, parses the solution when the C# or Visual Basic option is on (awaited on the UI thread, yielding between steps), subscribes to `VSColorTheme.ThemeChanged` to read the settings again, shows the sponsorship message and sends the usage telemetry.
* `ConstFinder` walks the syntax trees of every project of the `VisualStudioWorkspace` (in dependency order) and keeps the `const` fields and locals in `KnownConsts` as `(Key, Qualification, Value, Source)`. `GetConstsFromSyntaxRoot` skips files that end in `.designer.cs`, `.designer.vb`, `.g.cs` or `.g.i.cs`, and `TrackConstsInDocumentAsync` (the reload path) skips paths that contain `.g.` or `.Designer.`. `MyRunningDocTableEvents` calls `ReloadConstsAsync` when a document is saved or its window is shown: once the solution has been parsed (`HasParsedSolution`) that re-reads the active document only, and only when its language is CSharp or Basic; before that it runs the full parse. `TryParseSolutionAsync` sets `HasParsedSolution` to `true` even when the parse throws, so a failed parse is not retried, and `KnownConsts`, `parsingInProgress` and `HasParsedSolution` are static and unsynchronized.
* Every document text view (role `Document`) of the content types `CSharp` and `Basic` gets a `ResourceAdornmentManager` (one per view, kept in the view's `Properties`): `ResourceAdornmentManagerFactory` creates it in `TextViewCreated`, and `MyLineTransformSourceProvider` creates it too when it is the first to run. The manager handles `LayoutChanged`: on the first layout it visits all lines of the view, afterwards the new or reformatted ones. A line that contains ` const ` (a definition) is skipped first. Otherwise the manager looks for whole-word occurrences of the known constant names (`GetAllWholeWordIndexesAsync`; a name that is followed by `(` is never a match), works out the qualifier before the name, asks `ConstFinder.GetDisplayText` for the value (the same file first, other files only when the usage has a qualifier such as `Class.NAME`) and adds a `TextBlock` to the adornment layer, positioned at the usage.
* The matches are visited from right to left, and several rules end the loop with `break`: no value found, ` =` after the name, a name next to a `"`, `].` before it, ` {` before it, whitespace before and `.` after, and a line that starts with `//` (trailing, block and Visual Basic comments are not recognized). A `break` also skips the matches to the left of the rejected one, so check how a new rule interacts with the others.
* `MyLineTransformSource` adds the space above a line that has an adornment: the default top space, plus the top and bottom padding options scaled by `Constants.TextBlockSizeToFontScaleFactor`, plus the editor font size. The height of the `TextBlock` is the font size times the same factor plus the unscaled padding; keep the two calculations consistent when you change one.
* `OptionsGrid` (a `DialogPage` registered with `ProvideOptionPage`) holds the top and bottom padding, the switches for C# (on by default) and Visual Basic (off), and advanced logging.
* `OutputPane.Instance` creates the pane on first use and its constructor requires the UI thread, so the first access must happen there. `ExceptionHelper.Log` writes an exception to the pane and requires the UI thread; in `DEBUG` builds it first calls `Debugger.Break()`, as `ConstFinder.SafeGetActiveDocumentAsync` does for an exception that it says is common, so an attached debugger stops there.

### Parts of the Original Author

The publisher identity in the manifest, the sponsorship message (`SponsorDetector`, `SponsorRequestHelper`), the telemetry (`AnalyticsConfig.TelemetryConnectionString` is empty in the repository, and `TrackBasicUsageAnalytics` sends data only in non-`DEBUG` builds when it is set) and the signing of the release build (`filestosign.txt`) belong to Matt Lacey. Do not change or remove them unless the task asks for it. `SponsorDetector.IsSponsorAsync` says in a comment that the released extension is built with a different implementation.

## Visual Studio Extension Rules

* Threading: use Visual Studio COM objects (`DTE`, `IVs*` services) and WPF elements on the UI thread only. Switch with `await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken)` (`this.JoinableTaskFactory` inside the package) before you create or call such an object. Never block with `.Result` or `.Wait()`. The two callbacks of `MyRunningDocTableEvents` that call `ThreadHelper.JoinableTaskFactory.Run` are the existing exception, because `IVsRunningDocTableEvents` is synchronous – do not add more. Keep the work per line in `LayoutChangedHandler` small: it runs for every layout of every text view. The VSTHRD analyzers come with `Microsoft.VisualStudio.SDK`: fix what they report, and where a suppression is unavoidable use a narrow `#pragma warning disable` / `restore` pair, as the existing code does for `async void` handlers
* Errors: never swallow a caught exception – log it with `ExceptionHelper.Log(exc)` on the UI thread (it writes to the Output pane), or write to the pane with `OutputPane.Instance.WriteAsync`, or hand a faulted task to `TaskExtensions.LogAndForget`, which writes to the activity log. Never let an exception escape from an event handler into Visual Studio: `LayoutChangedHandler` is `async void`, catches only `InvalidOperationException` around `CreateVisualsAsync` and relies on the catch-all inside that method. `async void` is for event handlers only. One existing `catch` only calls `Debug.WriteLine` on purpose (`ConstFinder.SafeGetActiveDocumentAsync`, which says why in a comment); a new one needs the same justification
* Theming: the adornment takes its font size from the Fonts and Colors setting of the text editor and its color from the setting for strings (`ResourceAdornmentManager.TextSize` and `TextForegroundColor`, refreshed on `VSColorTheme.ThemeChanged`); never hard-code colors or fonts. The gray and the size 10 are only the values used until the settings are read
* Editor extensibility: the exports are MEF parts of the extension's own assembly, declared as the `MefComponent` asset in the manifest. The adornment layer name `ConstCommentLayer` is written twice, in the `[Name]` of the layer definition and in `view.GetAdornmentLayer(...)`, and the `[ContentType]` and `[TextViewRole]` attributes of `ResourceAdornmentManagerFactory` and `MyLineTransformSourceProvider` must stay equal; a mismatch compiles and fails or silently does nothing at run time
* Generated files: `src/ConstVisualizer/source.extension.cs` carries an `<auto-generated>` header and is made from the manifest by the `VsixManifestGenerator` custom tool set in the project file, which needs the VSIX Synchronizer extension. MSBuild and agents cannot run it, so edit the file by hand and keep it in sync with `source.extension.vsixmanifest` (they are in sync now). `Vsix.Version` is a compile-time constant that feeds `AssemblyVersion`, `AssemblyFileVersion` and `InstalledProductRegistration`: it must stay a `const string` of the form `a.b.c.d`
* Failures that appear only at run time: a missing manifest asset (such as the `MefComponent` one), a mismatched layer name or content type, or a missing `<Content>` item compile fine and break the extension later. Check such changes in the Experimental instance
* .NET Framework 4.7.2: language features that need no runtime support work (file-scoped namespaces, pattern matching, switch expressions, target-typed `new`, `is not null`); `record` and `init` need an `IsExternalInit` polyfill type, which the project does not have. The framework has no `Index`/`Range` or default interface members. `Span<T>`, `Memory<T>` and `IAsyncEnumerable<T>` compile only because the packages that the project restores bring `System.Memory` and `Microsoft.Bcl.AsyncInterfaces`; do not add a polyfill package for something that already compiles, and check that an API exists in .NET Framework 4.7.2 before using it
* Package versions: Visual Studio loads its own copy of some libraries; the VSIX contains no Visual Studio SDK assemblies and no `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll` or `Microsoft.VisualStudio.LanguageServices.dll`, so the extension runs against the Roslyn of the Visual Studio that hosts it, and a Roslyn API that is newer than the oldest supported Visual Studio (17.14) fails at run time. `Microsoft.VisualStudio.SDK` is 17.14.40265, the same as the minimum Visual Studio in the manifest, and is referenced with `ExcludeAssets="runtime"`; keep the two aligned, and check the build warnings and run the extension before you raise a version

## Libraries in Use

* [Visual Studio SDK](https://learn.microsoft.com/visualstudio/extensibility/) (`Microsoft.VisualStudio.SDK` 17.14.40265) – the shell, text editor and threading APIs, `EnvDTE`, the interop assemblies and the VSTHRD analyzers
* `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.VisualBasic` and `Microsoft.VisualStudio.LanguageServices` 4.14.0 – the Roslyn workspace that `ConstFinder` reads
* `Microsoft.ApplicationInsights` – the usage telemetry of the release build
* `MessagePack`, `Nerdbank.Streams`, `Microsoft.IO.Redist`, `System.Collections.Immutable` and `System.Text.Json` – dependencies that the project pins explicitly
* `WarnAboutTODOs` – turns each `TODO` comment into a build warning
* MSTest with `MSTest.Analyzers` (4.0.1) in the test project

## Agents and Skills

* Use the `vs-extension-expert` agent (#file:'.github/agents/vs-extension-expert.agent.md') for work on the extension code.
* Skills live in `.github/skills`; `Directory.Build.targets` links the folder to the git-ignored `.claude/skills` for Claude Code. They are copied from [`vs-agent-plugins`](https://github.com/madskristensen/vs-agent-plugins) by Mads Kristensen, see #file:'.github/skills/README.md': `handling-async-threading`, `handling-extension-errors`, `adding-editor-adornments`, `listening-text-view-events`, `adding-options-settings` and `handling-solution-events`. When a task matches the domain of a skill, load it and follow it before you write code.
* `.claude/settings.json` also enables the whole catalogue (40 skills) as the Claude Code plugin `vs-extensibility-skills`; if Claude Code has not offered it, install it with `claude plugin install vs-extensibility-skills@vs-agent-plugins --scope project`.
* The skills describe three approaches: VisualStudio.Extensibility (out of process), Community.VisualStudio.Toolkit and VSSDK. This extension is in-process VSSDK (`AsyncPackage`) without the Toolkit, so follow the VSSDK parts and ignore the others.

## MCP Servers

Agents discover servers from #file:'.mcp.json'; these are only hints on when to prefer one.

* Use the `GitHub` server for code search in dependency repositories. In Claude Code it authenticates through `.claude/scripts/github-mcp-headers.ps1`, which reuses the GitHub token stored by Git Credential Manager, because the server does not support the OAuth dynamic client registration Claude Code needs.
* Use the `Microsoft Learn` server for official Microsoft documentation, the Visual Studio extensibility documentation included. Visual Studio bundles this server itself, so `.mcp.json` lists it for Claude Code only.
* Visual Studio reads `.mcp.json` from the folder of the solution, and Claude Code reads it from the root of the repository. The solution is in the repository root so that one file serves both; keep it there.
* When the solution is open in Visual Studio and an MCP server that exposes the IDE (`vs`) is connected, prefer its tools to the shell: build with it instead of resolving MSBuild, read the diagnostics from it instead of parsing the build output, and navigate with its symbol tools instead of text search.
* When adding or changing a server, keep `servers` (Visual Studio / VS Code / GitHub Copilot) and `mcpServers` (Claude Code) in sync and sorted alphabetically by key, except for the servers that Visual Studio already bundles (Microsoft Learn, NuGet): those go only in `mcpServers`.

## Definition of Done

Before reporting a task as finished:

1. Follow `src/.editorconfig` and the style of the surrounding code.
2. Build the solution and make sure that it succeeds with no errors; do not claim a successful build that you did not run.
3. Compare the warnings with the build before your change: the change must add none. The build already reports warnings (the NuGet audit warnings `NU1902` and `NU1903` for `MessagePack`, and `MSTEST0001` and `MSTEST0037` in the tests), so compare instead of expecting zero.
4. Run the tests when the change touches code that they cover, and add a test for logic that does not depend on Visual Studio. The `catch` blocks of the string helpers call `ExceptionHelper.Log` after a switch to the UI thread, which needs Visual Studio (and breaks into an attached debugger in `DEBUG`), so a test must not make a helper throw: `GetAllWholeWordIndexesAsync`, for example, throws for a match at index 0. Say what you could not verify without the Experimental instance instead of claiming that it works: the constant parser, the adornments, the options page and the package load are not covered by tests.
5. Update `README.md` when a user-visible feature changes.

## C# Coding Guidelines

* Follow #file:'src/.editorconfig' and match the file that you edit; do not reformat code that you do not change. The `.editorconfig` asks for CRLF line endings and a width of 4 for indents and tabs in `.cs` and `.vb` files but does not set `indent_style`: the files use tabs, except `SponsorDetector.cs` and the generated `source.extension.cs` (4 spaces). Most files start with a UTF-8 byte order mark, which you keep. The repository stores LF line endings (there is no `.gitattributes`); a Windows checkout with `core.autocrlf=true` shows CRLF.
* New files start with the copyright header block of the existing files (`// <copyright file="Name.cs" company="Matt Lacey">`); `AnalyticsConfig.cs` and the generated `source.extension.cs` have none.
* Qualify instance members with `this.`, as the existing code does. Put `using` directives above the namespace (`System` first in most files) and remove unused ones. Several files carry `using Task = System.Threading.Tasks.Task;` from the package template; leave it where it is.
* Types live in the `ConstVisualizer` namespace (`ConstVisualizer.Tests` for the tests) in a block-scoped `namespace` declaration. Do not convert existing files.
* Name async methods with the `Async` suffix. Only the package methods take a `CancellationToken`; pass one on in new async code when the caller has one. The tests are the exception to the suffix: `VSTHRD200` is suppressed for them in their `GlobalSuppressions.cs`.
* XML documentation: StyleCop rules `SA1611` and `SA1615` are suppressed in `GlobalSuppressions.cs`, `SA1600` is silent in the `.editorconfig` and `SA0001` is in `<NoWarn>`, so documenting every member is not required, but document new public types and non-obvious members. These StyleCop rule IDs come from an earlier setup: the project file lists no StyleCop package.

## Naming Conventions

* Use PascalCase for types, methods and public members, and camelCase without a prefix for private fields (`layer`, `view`, `fileName`, `parsingInProgress`); the `.editorconfig` asks for PascalCase types and members and an `I` prefix for interfaces. The public field `editorAdornmentLayer` of the layer definition keeps the name from the editor template.
* Name the files after the type they hold; the line transform classes are named `MyLineTransformSource` and `MyLineTransformSourceProvider`, and `ResourceAdornmentManager` is the name of the adornment manager although this extension shows constants.

## General

* Make only high-confidence suggestions when reviewing code changes.
* Write code with good maintainability practices, including comments on why certain design decisions were made.
* Handle edge cases and write clear exception handling.
* Never duplicate code unless explicitly allowed.
* All comments, documentation, README files, and markdown files must be written in **English only**.
* Use an en dash (`–`) as a punctuation dash, never an em dash (`—`), and do not use emojis in text, comments or messages.
* In markdown, write each paragraph on one line instead of hard-wrapping it.
* Scripts and one-off automation: PowerShell 7 (`pwsh`, `.ps1`) or F# Interactive (`dotnet fsi`, `.fsx`); do not use Python.
* The default branch is `main`, and pull requests go against it.
* Commit messages follow the history of this repository: one short sentence in the imperative, with no prefix (`Add additional analytics data`). Many messages are lower-case (`update test packages`), and releases are plain version numbers (`v2.3.3`).

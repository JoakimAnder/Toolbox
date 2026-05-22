# Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold the JoakimAnder.Toolbox repository into a buildable, packable .NET 10 solution with a source-generator-bundled NuGet, placeholder content only, ready for feature sub-projects to begin.

**Architecture:** Five-project solution (main lib, source generator, two test projects, examples) under `src/` + `tests/` + `examples/`. Build configuration centralized in repo-root + per-tier `Directory.Build.props`. Source generator built as `netstandard2.0`, referenced by main lib as an analyzer, and its DLL repacked into the main NuGet at `analyzers/dotnet/cs/`. Versioning via MinVer (git-tag-driven). Full NuGet polish (SourceLink, symbols, deterministic, AOT-compatible).

**Tech Stack:** .NET 10, C# latest, MSBuild, MinVer, Microsoft.SourceLink.GitHub, Microsoft.CodeAnalysis.CSharp (Roslyn 4.x), xUnit, Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit.

**Spec:** [docs/superpowers/specs/2026-05-22-foundation-design.md](../specs/2026-05-22-foundation-design.md)

---

## Task ordering and dependencies

```
Task 1 (configs) ──┬──> Task 3 (sln + main lib) ──> Task 5 (wire source gen) ──> Task 6 (tests) ──> Task 7 (examples) ──> Task 8 (final verification)
                   │                            ^
                   │                            │
                   └──> Task 4 (source gen project)
Task 2 (docs) ─────────────────────────────────────────────────────────────────────────────────────────────────────────^
```

Tasks 1 and 2 are independent. Task 4 (source gen project) can start as soon as Task 3 lays down the solution file. Tasks 5+ are sequential. Task 8 depends on everything.

---

### Task 1: Repo-wide build configuration

**Goal:** Lay down the centralized build/tooling configuration that every project in the repo will inherit (`Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitattributes`, `.gitignore` additions).

**Files:**
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `global.json`
- Create: `.editorconfig`
- Create: `.gitattributes`
- Modify: `.gitignore` (append editor folders)

**Acceptance Criteria:**
- [ ] All six files exist with the exact content shown below.
- [ ] `dotnet --version` reports an SDK whose major.minor is 10.0 (resolved via `global.json` + `rollForward: latestFeature`).
- [ ] Existing `.gitignore` content preserved; new entries appended.

**Verify:** `ls Directory.Build.props Directory.Packages.props global.json .editorconfig .gitattributes && dotnet --version` → all files listed, SDK version starts with `10.0.`.

**Steps:**

- [ ] **Step 1: Create `Directory.Build.props` at repo root**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisMode>Recommended</AnalysisMode>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create `Directory.Packages.props` at repo root**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MinVer" Version="6.0.0" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="8.0.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit" Version="1.1.2" />
  </ItemGroup>
</Project>
```

**Note for implementer:** Before committing, run `dotnet list package --outdated` after Task 6 (when test projects reference packages) to confirm these versions are still current. Bump in `Directory.Packages.props` if newer stable releases exist. Do NOT bump versions in individual csproj files — central management means there are no versions in csprojs.

- [ ] **Step 3: Create `global.json` at repo root**

First, find the installed SDK to set a sensible floor:

```bash
dotnet --list-sdks
```

Pick the lowest 10.0.x version listed (e.g. `10.0.100`). Write:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

Replace `10.0.100` with the actual lowest 10.0.x version `dotnet --list-sdks` reported. If only one 10.0.x SDK is installed, use that.

- [ ] **Step 4: Create `.editorconfig` at repo root**

```ini
root = true

[*]
indent_style = space
trim_trailing_whitespace = true
insert_final_newline = true
charset = utf-8
end_of_line = lf

[*.{cs,csx}]
indent_size = 4

[*.{csproj,xml,props,targets,config,nuspec}]
indent_size = 2

[*.{json,yml,yaml,md}]
indent_size = 2

[*.cs]
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_namespace_declarations = file_scoped:warning
csharp_prefer_braces = true:warning

dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning

dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

dotnet_naming_rule.interfaces_start_with_i.symbols                = interface_symbols
dotnet_naming_rule.interfaces_start_with_i.style                  = i_prefix_style
dotnet_naming_rule.interfaces_start_with_i.severity               = warning
dotnet_naming_symbols.interface_symbols.applicable_kinds          = interface
dotnet_naming_style.i_prefix_style.required_prefix                = I
dotnet_naming_style.i_prefix_style.capitalization                 = pascal_case
```

- [ ] **Step 5: Create `.gitattributes` at repo root**

```
* text=auto eol=lf
*.{cmd,bat} text eol=crlf
*.{png,jpg,jpeg,gif,ico,snk,dll,exe,nupkg,snupkg} binary
```

- [ ] **Step 6: Append to `.gitignore`**

Append these lines to the END of the existing `.gitignore` (do not overwrite):

```

# Editors / IDEs
.vs/
.vscode/
.idea/
*.user
*.suo
```

(Note the leading blank line — separates the new section from the existing content.)

- [ ] **Step 7: Verify**

```bash
ls Directory.Build.props Directory.Packages.props global.json .editorconfig .gitattributes
dotnet --version
```

Expected: all five filenames listed, SDK version starts with `10.0.`. If `dotnet --version` fails with "specified SDK was not found," the `global.json` version is too high; lower it to a version that `dotnet --list-sdks` shows.

- [ ] **Step 8: Commit**

```bash
git add Directory.Build.props Directory.Packages.props global.json .editorconfig .gitattributes .gitignore
git commit -m "chore: add repo-wide build, package, and editor configuration"
```

---

### Task 2: Root documentation files

**Goal:** Write the user-facing root docs (`README.md`, `CHANGELOG.md`, `AI_DISCLOSURE.md`). README must exist before the main lib's `dotnet pack` runs (it's packed into the NuGet via `<None Include="../../README.md">`).

**Files:**
- Modify: `README.md` (replace existing minimal version)
- Create: `CHANGELOG.md`
- Create: `AI_DISCLOSURE.md`

**Acceptance Criteria:**
- [ ] `README.md` contains the full content below (existing minimal version is overwritten).
- [ ] `CHANGELOG.md` exists with the Keep a Changelog template.
- [ ] `AI_DISCLOSURE.md` exists with the disclosure prose.

**Verify:** `head -1 README.md CHANGELOG.md AI_DISCLOSURE.md` → first line of each file is the expected H1.

**Steps:**

- [ ] **Step 1: Replace `README.md`**

```markdown
# JoakimAnder.Toolbox

A collection of helper classes and utilities I've grown tired of copy-pasting between projects.

[![NuGet](https://img.shields.io/nuget/v/JoakimAnder.Toolbox.svg)](https://www.nuget.org/packages/JoakimAnder.Toolbox/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Installation

```sh
dotnet add package JoakimAnder.Toolbox
```

Requires .NET 10 or later.

## What's in the box

Nothing yet — this is the initial scaffolding. See [docs/ROADMAP.md](docs/ROADMAP.md)
for the planned features (Result, ParallelFanout, DI source generator).

## Project structure

- `src/JoakimAnder.Toolbox` — the library
- `src/JoakimAnder.Toolbox.SourceGenerators` — source generators, packed into the main NuGet
- `tests/` — xUnit test projects
- `examples/` — runnable usage examples

## Building locally

```sh
dotnet build
dotnet test
```

## Versioning

Versions are derived from git tags via [MinVer](https://github.com/adamralph/minver).
Tag a commit `v1.2.3` to release `1.2.3` to NuGet.

## License

MIT — see [LICENSE](LICENSE).

## AI usage

See [AI_DISCLOSURE.md](AI_DISCLOSURE.md).
```

- [ ] **Step 2: Create `CHANGELOG.md`**

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Repository scaffolding: solution layout, build configuration, documentation.
```

- [ ] **Step 3: Create `AI_DISCLOSURE.md`**

```markdown
# AI usage disclosure

This project is developed with the assistance of AI tools, primarily
[Claude Code](https://www.anthropic.com/claude-code).

AI assistance is used for:
- Drafting and refactoring code
- Generating tests
- Writing documentation
- Designing APIs and project structure

All committed code is read and reviewed by me before merging. I take full
responsibility for the correctness, security, and quality of everything in
this repository.

If you find a bug, a security issue, or a piece of code that doesn't make
sense — open an issue. "An AI wrote it" is not an excuse, and the goal is
that you can't tell the difference.
```

- [ ] **Step 4: Verify**

```bash
head -1 README.md CHANGELOG.md AI_DISCLOSURE.md
```

Expected output:
```
==> README.md <==
# JoakimAnder.Toolbox
==> CHANGELOG.md <==
# Changelog
==> AI_DISCLOSURE.md <==
# AI usage disclosure
```

- [ ] **Step 5: Commit**

```bash
git add README.md CHANGELOG.md AI_DISCLOSURE.md
git commit -m "docs: add README, CHANGELOG, AI disclosure"
```

---

### Task 3: Solution file and main library project

**Goal:** Create the solution file, the `src/` tier overrides, and the main library project with a placeholder file. Source generator is NOT wired yet — that's Task 5.

**Files:**
- Create: `JoakimAnder.Toolbox.sln`
- Create: `src/Directory.Build.props`
- Create: `src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj`
- Create: `src/JoakimAnder.Toolbox/Placeholder.cs`

**Acceptance Criteria:**
- [ ] Solution file references the main lib project.
- [ ] `dotnet build src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -c Release` exits 0 with zero warnings.
- [ ] `dotnet pack src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -c Release -o ./artifacts` produces a `.nupkg` and `.snupkg`.

**Verify:** `dotnet build -c Release` from repo root succeeds; `dotnet pack src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -c Release -o ./artifacts && ls artifacts/*.nupkg artifacts/*.snupkg` shows both files.

**Steps:**

- [ ] **Step 1: Create solution file**

```bash
dotnet new sln -n JoakimAnder.Toolbox
```

This creates `JoakimAnder.Toolbox.sln` at the repo root.

- [ ] **Step 2: Create `src/Directory.Build.props`**

```bash
mkdir -p src
```

Then create `src/Directory.Build.props`:

```xml
<Project>
  <Import Project="../Directory.Build.props" />
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <IsAotCompatible>true</IsAotCompatible>
    <Authors>Joakim Andersson</Authors>
    <Company>Joakim Andersson</Company>
    <Description>A collection of helper classes and utilities.</Description>
    <PackageProjectUrl>https://github.com/JoakimAnder/Toolbox</PackageProjectUrl>
    <RepositoryUrl>https://github.com/JoakimAnder/Toolbox.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <MinVerTagPrefix>v</MinVerTagPrefix>
  </PropertyGroup>
  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />
    <PackageReference Include="MinVer" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create main library project**

```bash
mkdir -p src/JoakimAnder.Toolbox
```

Create `src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

This is intentionally minimal — every property is inherited from `src/Directory.Build.props` and `Directory.Build.props`. Source generator wiring is added in Task 5.

- [ ] **Step 4: Create `src/JoakimAnder.Toolbox/Placeholder.cs`**

```csharp
namespace JoakimAnder.Toolbox;

internal static class Placeholder
{
    // Placeholder until the first feature lands.
}
```

The class is `internal static` (not exposed in the public API surface) and named without underscore (CA1707 compliance).

- [ ] **Step 5: Add project to solution**

```bash
dotnet sln add src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build -c Release
```

Expected: exits 0, output contains `Build succeeded`, zero warnings, zero errors. If warnings appear (likely CA1812 "Avoid uninstantiated internal classes" on `Placeholder` does NOT fire on static classes — but if any other rule does), stop and address before continuing.

- [ ] **Step 7: Pack and verify**

```bash
mkdir -p artifacts
dotnet pack src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -c Release -o ./artifacts
ls artifacts/
```

Expected: two files matching `JoakimAnder.Toolbox.<version>.nupkg` and `JoakimAnder.Toolbox.<version>.snupkg`. Version will be a MinVer pre-release like `0.0.0-alpha.0.N` (no git tags exist yet — this is expected).

- [ ] **Step 8: Clean up artifacts and commit**

```bash
rm -rf artifacts
git add JoakimAnder.Toolbox.sln src/Directory.Build.props src/JoakimAnder.Toolbox
git commit -m "feat: scaffold solution and main library project"
```

The `artifacts/` directory is ignored by `.gitignore` (matches `artifacts/`).

---

### Task 4: Source generator project

**Goal:** Create the source generator project as a standalone `netstandard2.0` Roslyn component. No wiring to main lib yet — that's Task 5. The project builds independently with just a placeholder file.

**Files:**
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/JoakimAnder.Toolbox.SourceGenerators.csproj`
- Create: `src/JoakimAnder.Toolbox.SourceGenerators/Placeholder.cs`

**Acceptance Criteria:**
- [ ] Source generator project builds standalone (`dotnet build src/JoakimAnder.Toolbox.SourceGenerators/...`).
- [ ] Target framework is `netstandard2.0` (not net10.0 — generators must target netstandard2.0 because Roslyn loads them in a .NET Framework-compatible host).
- [ ] Project is added to the solution file.
- [ ] `IsPackable=false` so this project doesn't produce its own NuGet.

**Verify:** `dotnet build src/JoakimAnder.Toolbox.SourceGenerators/JoakimAnder.Toolbox.SourceGenerators.csproj -c Release` succeeds with zero warnings; no `.nupkg` is produced.

**Steps:**

- [ ] **Step 1: Create the project directory**

```bash
mkdir -p src/JoakimAnder.Toolbox.SourceGenerators
```

- [ ] **Step 2: Create `src/JoakimAnder.Toolbox.SourceGenerators/JoakimAnder.Toolbox.SourceGenerators.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsRoslynComponent>true</IsRoslynComponent>
    <IsAotCompatible>false</IsAotCompatible>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Why each property:
- `TargetFramework=netstandard2.0` — required by Roslyn (generators run inside the compiler, which runs on .NET Framework even when the consumer's app targets .NET 10).
- `IsPackable=false` — overrides `src/Directory.Build.props`. The generator DLL ships inside the main lib's NuGet, not as its own package.
- `IsRoslynComponent=true` — tells the SDK this is an analyzer/generator project, enables tooling like "debug this generator" in Visual Studio.
- `IsAotCompatible=false` — overrides `src/Directory.Build.props`. AOT-compatibility is meaningless for generators (they run in the compiler, not the consumer's app).
- `EnforceExtendedAnalyzerRules=true` — enables analyzer rules that catch common generator authoring mistakes (e.g., capturing semantic models that won't be cached).
- `PrivateAssets="all"` on the Roslyn package — the Roslyn assemblies must not flow downstream to consumers of the main lib.

- [ ] **Step 3: Create `src/JoakimAnder.Toolbox.SourceGenerators/Placeholder.cs`**

```csharp
namespace JoakimAnder.Toolbox.SourceGenerators;

internal static class Placeholder
{
    // Placeholder until the DI source generator lands.
}
```

- [ ] **Step 4: Add to solution**

```bash
dotnet sln add src/JoakimAnder.Toolbox.SourceGenerators/JoakimAnder.Toolbox.SourceGenerators.csproj
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/JoakimAnder.Toolbox.SourceGenerators/JoakimAnder.Toolbox.SourceGenerators.csproj -c Release
ls src/JoakimAnder.Toolbox.SourceGenerators/bin/Release/netstandard2.0/
```

Expected: build succeeds, zero warnings. Output directory contains `JoakimAnder.Toolbox.SourceGenerators.dll`. NO `.nupkg` file anywhere (because `IsPackable=false`).

- [ ] **Step 6: Verify full-solution build still works**

```bash
dotnet build -c Release
```

Expected: both projects build, zero warnings.

- [ ] **Step 7: Commit**

```bash
git add src/JoakimAnder.Toolbox.SourceGenerators JoakimAnder.Toolbox.sln
git commit -m "feat: scaffold source generator project"
```

---

### Task 5: Wire source generator into main library and pack

**Goal:** Modify the main library csproj to reference the source generator as an analyzer (so consumers' compilers run it) AND repack the generator's DLL into the main NuGet at `analyzers/dotnet/cs/`.

**Files:**
- Modify: `src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj`

**Acceptance Criteria:**
- [ ] Main lib's csproj declares the source generator project as an analyzer-only reference (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`).
- [ ] Building the main lib also builds the source generator (dependency chain works).
- [ ] `dotnet pack src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj` produces a `.nupkg` containing `analyzers/dotnet/cs/JoakimAnder.Toolbox.SourceGenerators.dll`.
- [ ] The packaged `.nuspec` does NOT list `JoakimAnder.Toolbox.SourceGenerators` as a dependency (the analyzer is bundled, not depended on).

**Verify:**

```bash
dotnet pack src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -c Release -o ./artifacts
unzip -l artifacts/JoakimAnder.Toolbox.*.nupkg | grep -E '(analyzers/dotnet/cs|README\.md|\.nuspec)'
```

Expected output (paths must include these three lines, version number varies):
```
... analyzers/dotnet/cs/JoakimAnder.Toolbox.SourceGenerators.dll
... README.md
... JoakimAnder.Toolbox.nuspec
```

**Steps:**

- [ ] **Step 1: Update `src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj`**

Replace the contents of the file with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <!-- Reference the generator as an analyzer (compile-time only).
         OutputItemType="Analyzer" tells Roslyn to load it at compile.
         ReferenceOutputAssembly="false" prevents the gen DLL from being
         linked into the runtime lib output. -->
    <ProjectReference Include="..\JoakimAnder.Toolbox.SourceGenerators\JoakimAnder.Toolbox.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false"
                      PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <!-- Pack the generator's built DLL into the analyzer slot of THIS NuGet.
         The path uses $(Configuration) so Debug and Release packs both work. -->
    <None Include="..\JoakimAnder.Toolbox.SourceGenerators\bin\$(Configuration)\netstandard2.0\JoakimAnder.Toolbox.SourceGenerators.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Build the full solution**

```bash
dotnet build -c Release
```

Expected: both projects compile, zero warnings. The build order should now be: source generator first, then main lib.

- [ ] **Step 3: Pack the main lib**

```bash
rm -rf artifacts
dotnet pack src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -c Release -o ./artifacts
```

Expected: a `JoakimAnder.Toolbox.<version>.nupkg` and `JoakimAnder.Toolbox.<version>.snupkg` in `./artifacts/`.

- [ ] **Step 4: Inspect the .nupkg for the analyzer DLL**

```bash
unzip -l artifacts/JoakimAnder.Toolbox.*.nupkg
```

Look for the lines containing:
- `analyzers/dotnet/cs/JoakimAnder.Toolbox.SourceGenerators.dll`
- `README.md`
- `lib/net10.0/JoakimAnder.Toolbox.dll`
- `JoakimAnder.Toolbox.nuspec`

If `analyzers/dotnet/cs/JoakimAnder.Toolbox.SourceGenerators.dll` is missing, the `<None Include>` block is incorrect. Most common cause: the generator wasn't built in Release config before the pack ran. The pack target depends on the build target, which should chain through `<ProjectReference>`, so this should be automatic — but if not, run `dotnet build -c Release` first, then `dotnet pack -c Release --no-build`.

- [ ] **Step 5: Inspect the .nuspec to confirm no generator dependency**

```bash
unzip -p artifacts/JoakimAnder.Toolbox.*.nupkg JoakimAnder.Toolbox.nuspec
```

Look for the `<dependencies>` element. It should contain:
- No `<dependency id="JoakimAnder.Toolbox.SourceGenerators" ... />` entry.
- A `<group targetFramework="net10.0">` group (which may be empty since the lib has no runtime deps yet).

If the generator IS listed as a dependency, then `PrivateAssets="all"` is missing or misspelled on the `<ProjectReference>`. Fix and re-pack.

- [ ] **Step 6: Clean and commit**

```bash
rm -rf artifacts
git add src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj
git commit -m "feat: wire source generator into main library NuGet"
```

---

### Task 6: Test projects with smoke tests

**Goal:** Create the `tests/` tier overrides and both test projects with one passing xUnit smoke test each. `dotnet test` runs both and passes.

**Files:**
- Create: `tests/Directory.Build.props`
- Create: `tests/JoakimAnder.Toolbox.Tests/JoakimAnder.Toolbox.Tests.csproj`
- Create: `tests/JoakimAnder.Toolbox.Tests/SmokeTests.cs`
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/JoakimAnder.Toolbox.SourceGenerators.Tests.csproj`
- Create: `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/SmokeTests.cs`

**Acceptance Criteria:**
- [ ] Both test projects added to solution.
- [ ] `dotnet test -c Release` runs both projects.
- [ ] Both smoke tests pass.
- [ ] Test projects are not packable (no `.nupkg` produced).

**Verify:** `dotnet test -c Release` exits 0 with "Total tests: 2. Passed: 2. Failed: 0." (or equivalent — exact wording depends on xUnit/VS-runner version).

**Steps:**

- [ ] **Step 1: Create `tests/Directory.Build.props`**

```bash
mkdir -p tests
```

Create `tests/Directory.Build.props`:

```xml
<Project>
  <Import Project="../Directory.Build.props" />
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create main lib's test project**

```bash
mkdir -p tests/JoakimAnder.Toolbox.Tests
```

Create `tests/JoakimAnder.Toolbox.Tests/JoakimAnder.Toolbox.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\JoakimAnder.Toolbox\JoakimAnder.Toolbox.csproj" />
  </ItemGroup>
</Project>
```

Create `tests/JoakimAnder.Toolbox.Tests/SmokeTests.cs`:

```csharp
using Xunit;

namespace JoakimAnder.Toolbox.Tests;

public class SmokeTests
{
    [Fact]
    public void Build_pipeline_runs() => Assert.True(true);
}
```

- [ ] **Step 3: Create source generator's test project**

```bash
mkdir -p tests/JoakimAnder.Toolbox.SourceGenerators.Tests
```

Create `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/JoakimAnder.Toolbox.SourceGenerators.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\JoakimAnder.Toolbox.SourceGenerators\JoakimAnder.Toolbox.SourceGenerators.csproj" />
  </ItemGroup>
</Project>
```

Note: this test project targets `net10.0` (inherited from repo-root `Directory.Build.props`), unlike the source generator itself which targets `netstandard2.0`. Testing generators with the `Microsoft.CodeAnalysis.*.Testing.*` packages requires running on a modern .NET runtime.

Create `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/SmokeTests.cs`:

```csharp
using Xunit;

namespace JoakimAnder.Toolbox.SourceGenerators.Tests;

public class SmokeTests
{
    [Fact]
    public void Build_pipeline_runs() => Assert.True(true);
}
```

- [ ] **Step 4: Add both projects to solution**

```bash
dotnet sln add tests/JoakimAnder.Toolbox.Tests/JoakimAnder.Toolbox.Tests.csproj
dotnet sln add tests/JoakimAnder.Toolbox.SourceGenerators.Tests/JoakimAnder.Toolbox.SourceGenerators.Tests.csproj
```

- [ ] **Step 5: Run tests**

```bash
dotnet test -c Release
```

Expected: build succeeds, both test projects' smoke tests run, total of 2 tests, both pass, exit code 0. Output similar to:
```
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, ...
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, ...
```

- [ ] **Step 6: Commit**

```bash
git add tests JoakimAnder.Toolbox.sln
git commit -m "test: add smoke tests for main lib and source generators"
```

---

### Task 7: Examples project

**Goal:** Create the `examples/` tier overrides and a runnable console example project.

**Files:**
- Create: `examples/Directory.Build.props`
- Create: `examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj`
- Create: `examples/JoakimAnder.Toolbox.Examples/Program.cs`

**Acceptance Criteria:**
- [ ] Examples project added to solution.
- [ ] `dotnet run --project examples/JoakimAnder.Toolbox.Examples` prints the expected message.
- [ ] Examples project is not packable.

**Verify:** `dotnet run --project examples/JoakimAnder.Toolbox.Examples -c Release` prints `Toolbox examples — see individual files.` and exits 0.

**Steps:**

- [ ] **Step 1: Create `examples/Directory.Build.props`**

```bash
mkdir -p examples
```

Create `examples/Directory.Build.props`:

```xml
<Project>
  <Import Project="../Directory.Build.props" />
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create the examples project**

```bash
mkdir -p examples/JoakimAnder.Toolbox.Examples
```

Create `examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\JoakimAnder.Toolbox\JoakimAnder.Toolbox.csproj" />
  </ItemGroup>
</Project>
```

Create `examples/JoakimAnder.Toolbox.Examples/Program.cs`:

```csharp
Console.WriteLine("Toolbox examples — see individual files.");
```

(One-line top-level statement program. The em-dash is U+2014.)

- [ ] **Step 3: Add to solution**

```bash
dotnet sln add examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj
```

- [ ] **Step 4: Run the example**

```bash
dotnet run --project examples/JoakimAnder.Toolbox.Examples -c Release
```

Expected output:
```
Toolbox examples — see individual files.
```

Exit code 0.

- [ ] **Step 5: Commit**

```bash
git add examples JoakimAnder.Toolbox.sln
git commit -m "feat: add runnable examples project"
```

---

### Task 8: Final verification per completion criteria

**Goal:** Run the complete acceptance checklist from the spec and confirm the Foundation sub-project is genuinely done.

**Files:** None modified — this is verification only.

**Acceptance Criteria:**
- [ ] `dotnet restore` exits 0.
- [ ] `dotnet build -c Release` exits 0, zero warnings, zero errors across all 5 projects.
- [ ] `dotnet test -c Release` runs both test projects, all tests pass.
- [ ] `dotnet pack -c Release src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -o ./artifacts` produces `.nupkg` + `.snupkg`.
- [ ] `.nupkg` contains: `analyzers/dotnet/cs/JoakimAnder.Toolbox.SourceGenerators.dll`, `README.md`, `lib/net10.0/JoakimAnder.Toolbox.dll`.
- [ ] `.nuspec` inside `.nupkg` contains: `<repository type="git" url="https://github.com/JoakimAnder/Toolbox.git" ... />`, `<license type="expression">MIT</license>`, `<readme>README.md</readme>`.
- [ ] Working tree clean (`git status` shows no uncommitted changes).

**Verify:** All commands below exit 0 and produce the expected output.

**Steps:**

- [ ] **Step 1: Clean state**

```bash
rm -rf artifacts
git status
```

Expected: working tree clean.

- [ ] **Step 2: Restore**

```bash
dotnet restore
```

Expected: exit 0. "Restore complete" or similar.

- [ ] **Step 3: Build**

```bash
dotnet build -c Release
```

Expected: exit 0. Output includes `Build succeeded.` and `0 Warning(s)` and `0 Error(s)`. If any warnings appear, do NOT proceed — fix them first.

- [ ] **Step 4: Test**

```bash
dotnet test -c Release
```

Expected: exit 0. Two test projects run, two smoke tests pass.

- [ ] **Step 5: Pack**

```bash
mkdir -p artifacts
dotnet pack src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -c Release -o ./artifacts
ls artifacts/
```

Expected: `JoakimAnder.Toolbox.<version>.nupkg` and `JoakimAnder.Toolbox.<version>.snupkg` (version is a MinVer pre-release like `0.0.0-alpha.0.N`).

- [ ] **Step 6: Inspect .nupkg contents**

```bash
unzip -l artifacts/JoakimAnder.Toolbox.*.nupkg
```

Confirm all of these paths are present (substring match acceptable):
- `analyzers/dotnet/cs/JoakimAnder.Toolbox.SourceGenerators.dll`
- `lib/net10.0/JoakimAnder.Toolbox.dll`
- `lib/net10.0/JoakimAnder.Toolbox.xml` (if XML docs are produced — may or may not be present depending on SDK defaults)
- `README.md`
- `JoakimAnder.Toolbox.nuspec`

- [ ] **Step 7: Inspect .nuspec metadata**

```bash
unzip -p artifacts/JoakimAnder.Toolbox.*.nupkg JoakimAnder.Toolbox.nuspec
```

Verify the XML contains all of these (values may differ slightly in formatting):
- `<id>JoakimAnder.Toolbox</id>`
- `<authors>Joakim Andersson</authors>`
- `<license type="expression">MIT</license>`
- `<readme>README.md</readme>`
- `<projectUrl>https://github.com/JoakimAnder/Toolbox</projectUrl>`
- `<repository type="git" url="https://github.com/JoakimAnder/Toolbox.git" ... />`

If any are missing, trace back to `src/Directory.Build.props` — the property setting the missing element wasn't picked up. (Possible cause: mistyped property name, or the override in a child csproj turned packaging off.)

- [ ] **Step 8: Inspect .snupkg has matching symbols**

```bash
unzip -l artifacts/JoakimAnder.Toolbox.*.snupkg
```

Confirm `lib/net10.0/JoakimAnder.Toolbox.pdb` is present.

- [ ] **Step 9: Clean up and final commit**

```bash
rm -rf artifacts
git status
```

Expected: working tree clean (artifacts dir already excluded by `.gitignore`).

No commit needed for this task — it's verification only. If any step failed, fix the issue in the relevant earlier task's files and re-run from Step 2.

- [ ] **Step 10: Update CHANGELOG**

Move the placeholder bullet under `## [Unreleased]` into a stronger form. Edit `CHANGELOG.md` and replace the "Added" section under `## [Unreleased]` with:

```markdown
### Added

- Repository scaffolding: solution layout, build configuration, documentation.
- Main library project (`JoakimAnder.Toolbox`) targeting .NET 10.
- Source generator project (`JoakimAnder.Toolbox.SourceGenerators`) bundled into the main NuGet at `analyzers/dotnet/cs/`.
- xUnit smoke tests for main library and source generators.
- Runnable examples project.
- MinVer-driven versioning, SourceLink, symbol package (.snupkg), AOT compatibility.
```

Commit:

```bash
git add CHANGELOG.md
git commit -m "docs: expand changelog with foundation scope"
```

---

## Self-review notes

- All file paths use forward slashes (cross-platform consistent).
- Each task is independently committable and produces a testable outcome.
- No tasks marked `userGate: true` — the user did not request any verification gates in the brief.
- Spec coverage check: every section of the spec maps to at least one task:
  - Solution layout → Tasks 3, 4, 6, 7
  - Build configuration → Task 1, 3, 6, 7
  - Project files → Tasks 3, 4, 5, 6, 7
  - Placeholder content → Tasks 3, 4, 6, 7
  - Documentation files → Task 2
  - Tooling files → Task 1
  - Completion criteria → Task 8
- No placeholder TODOs in plan. No "Add appropriate X" hand-waves.
- Type/name consistency: `Placeholder` (no underscore) used everywhere; `JoakimAnder.Toolbox` package/namespace used everywhere.

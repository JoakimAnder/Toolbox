# Foundation — Design Spec

**Date:** 2026-05-22
**Sub-project:** Foundation (see [docs/ROADMAP.md](../../ROADMAP.md))
**Status:** Approved, ready for implementation planning.

## Goal

Scaffold the Toolbox repository so subsequent sub-projects (CI/CD, Result, ParallelFanout, DI source generator) can begin without re-litigating structural decisions.

The Foundation produces a buildable, packable solution with zero features — placeholder content only. The package is publish-ready in shape (correct metadata, SourceLink, symbols, source-generator wiring) but is not actually published until the CI/CD sub-project is complete.

## Scope

In scope:
- Solution + project layout
- Centralized build/package configuration
- Source-generator-into-main-package wiring
- Root-level documentation (README, CHANGELOG, AI disclosure)
- Editor/tooling files (`.editorconfig`, `.gitattributes`, `.gitignore` additions)
- Placeholder content to make every project compile and tests pass

Out of scope (deferred to other sub-projects):
- Any actual feature code (Result, ParallelFanout, DI attributes/generator)
- CI workflows, NuGet publishing automation
- A published package

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Target framework | `net10.0` only | Modern, single-target simpler than multi-targeting |
| Package/namespace prefix | `JoakimAnder.Toolbox` | Unique on NuGet, distinctive |
| Solution layout | Flat: `src/` + `tests/` + `examples/` | Conventional, easy to navigate |
| Source generator test project | Separate (`*.SourceGenerators.Tests`) | Different testing libraries and concerns |
| Test framework | xUnit | De-facto standard, best source-gen testing support |
| Test assertions | xUnit built-in only | No extra dependency, avoids FluentAssertions licensing question |
| Code style | Modern (nullable, implicit usings, latest lang), warnings stay warnings | Catch issues without failing builds on style |
| NuGet polish | Full (deterministic, SourceLink, symbols, README, metadata, AOT-compatible) | Professional consumer experience |
| Package icon | None | User chose to skip |
| Versioning | MinVer (git-tag-driven) | Lightweight, matches "publish on tag" CI model |
| Changelog format | Keep a Changelog | Human-readable, low overhead |
| AI disclosure | `AI_DISCLOSURE.md`, simple prose | Honest disclaimer, low maintenance |
| Source generator packaging | Bundled into main package | Attributes + generator are inseparable |
| SDK version pin | `global.json` present | Source-generator Roslyn coupling makes it materially useful |
| AOT compatibility | `IsAotCompatible=true` on src projects | DI source generator is AOT-friendly by design; analyzers catch unsafe code at build |

## Solution layout

```
Toolbox/
├── JoakimAnder.Toolbox.sln
├── Directory.Build.props                 (repo-wide build properties)
├── Directory.Packages.props              (Central Package Management)
├── global.json                           (SDK version pin)
├── .editorconfig
├── .gitattributes
├── .gitignore                            (existing; minor additions)
├── LICENSE                               (existing, MIT)
├── README.md                             (rewritten)
├── CHANGELOG.md
├── AI_DISCLOSURE.md
├── .github/
│   └── workflows/                        (empty — populated in CI/CD sub-project)
├── docs/
│   ├── ROADMAP.md                        (existing)
│   └── superpowers/specs/                (design docs)
├── src/
│   ├── Directory.Build.props             (publishable-project overrides)
│   ├── JoakimAnder.Toolbox/
│   │   ├── JoakimAnder.Toolbox.csproj
│   │   └── Placeholder.cs
│   └── JoakimAnder.Toolbox.SourceGenerators/
│       ├── JoakimAnder.Toolbox.SourceGenerators.csproj
│       └── Placeholder.cs
├── tests/
│   ├── Directory.Build.props             (non-packable overrides)
│   ├── JoakimAnder.Toolbox.Tests/
│   │   ├── JoakimAnder.Toolbox.Tests.csproj
│   │   └── SmokeTests.cs
│   └── JoakimAnder.Toolbox.SourceGenerators.Tests/
│       ├── JoakimAnder.Toolbox.SourceGenerators.Tests.csproj
│       └── SmokeTests.cs
└── examples/
    ├── Directory.Build.props             (non-packable overrides)
    └── JoakimAnder.Toolbox.Examples/
        ├── JoakimAnder.Toolbox.Examples.csproj
        └── Program.cs
```

## Build configuration

### `Directory.Build.props` (repo root)

Applies to every project.

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

### `src/Directory.Build.props`

Adds packaging metadata for publishable projects.

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

Note: The source-generator project overrides `IsPackable` and `IsAotCompatible` back to `false` in its own csproj (see below).

### `tests/Directory.Build.props` and `examples/Directory.Build.props`

```xml
<Project>
  <Import Project="../Directory.Build.props" />
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
  </PropertyGroup>
</Project>
```

### `Directory.Packages.props` (repo root)

Central Package Management — every package version is declared here once.

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

Versions shown are the design-time choice. The implementer may bump to the latest stable at implementation time; any bump must be reflected here, not in individual csproj files.

### `global.json`

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

The implementer should set `version` to the lowest 10.0.x SDK they have available, so `rollForward: latestFeature` keeps the floor stable while allowing newer feature-band SDKs locally and in CI.

## Project files

### `src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\JoakimAnder.Toolbox.SourceGenerators\JoakimAnder.Toolbox.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false"
                      PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <None Include="..\JoakimAnder.Toolbox.SourceGenerators\bin\$(Configuration)\netstandard2.0\JoakimAnder.Toolbox.SourceGenerators.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>
</Project>
```

The `<None>` block packs the built source-generator DLL into the main NuGet's `analyzers/dotnet/cs/` path so consumers' compilers pick it up.

### `src/JoakimAnder.Toolbox.SourceGenerators/JoakimAnder.Toolbox.SourceGenerators.csproj`

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

`netstandard2.0` is required by Roslyn (it loads generators in a host that runs on .NET Framework). Overrides the repo-wide `net10.0`.

### `tests/JoakimAnder.Toolbox.Tests/JoakimAnder.Toolbox.Tests.csproj`

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

### `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/JoakimAnder.Toolbox.SourceGenerators.Tests.csproj`

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

### `examples/JoakimAnder.Toolbox.Examples/JoakimAnder.Toolbox.Examples.csproj`

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

## Placeholder content

The Foundation does not ship features — only the minimal content needed for every project to compile and for the test pipeline to run.

- `src/JoakimAnder.Toolbox/Placeholder.cs`
  ```csharp
  namespace JoakimAnder.Toolbox;

  internal static class Placeholder
  {
      // Placeholder until the first feature lands.
  }
  ```
- `src/JoakimAnder.Toolbox.SourceGenerators/Placeholder.cs` — analogous, namespace `JoakimAnder.Toolbox.SourceGenerators`.
- `tests/JoakimAnder.Toolbox.Tests/SmokeTests.cs`
  ```csharp
  using Xunit;

  namespace JoakimAnder.Toolbox.Tests;

  public class SmokeTests
  {
      [Fact]
      public void Build_pipeline_runs() => Assert.True(true);
  }
  ```
- `tests/JoakimAnder.Toolbox.SourceGenerators.Tests/SmokeTests.cs` — analogous, namespace `JoakimAnder.Toolbox.SourceGenerators.Tests`.
- `examples/JoakimAnder.Toolbox.Examples/Program.cs`
  ```csharp
  Console.WriteLine("Toolbox examples — see individual files.");
  ```

Each `Placeholder.cs` is deleted as soon as the project gets real content in a later sub-project.

## Documentation files

### `README.md` (replaces existing)

```markdown
# JoakimAnder.Toolbox

A collection of helper classes and utilities I've grown tired of copy-pasting between projects.

[![NuGet](https://img.shields.io/nuget/v/JoakimAnder.Toolbox.svg)](https://www.nuget.org/packages/JoakimAnder.Toolbox/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Installation

\`\`\`sh
dotnet add package JoakimAnder.Toolbox
\`\`\`

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

\`\`\`sh
dotnet build
dotnet test
\`\`\`

## Versioning

Versions are derived from git tags via [MinVer](https://github.com/adamralph/minver).
Tag a commit `v1.2.3` to release `1.2.3` to NuGet.

## License

MIT — see [LICENSE](LICENSE).

## AI usage

See [AI_DISCLOSURE.md](AI_DISCLOSURE.md).
```

Build-status badge is intentionally omitted; it is added when the CI/CD sub-project lands.

### `CHANGELOG.md`

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Repository scaffolding: solution layout, build configuration, documentation.
```

### `AI_DISCLOSURE.md`

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

## Tooling files

### `.editorconfig`

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

### `.gitattributes`

```
* text=auto eol=lf
*.{cmd,bat} text eol=crlf
*.{png,jpg,jpeg,gif,ico,snk,dll,exe,nupkg,snupkg} binary
```

### `.gitignore` additions

Append to the existing `.gitignore`:

```
# Editors / IDEs
.vs/
.vscode/
.idea/
*.user
*.suo
```

## Completion criteria

The Foundation is complete when all of the following are true at the repo root:

1. `dotnet restore` exits zero.
2. `dotnet build -c Release` exits zero with **zero errors and zero warnings** across all five projects.
3. `dotnet test` runs both test projects and passes (smoke tests).
4. `dotnet pack -c Release src/JoakimAnder.Toolbox/JoakimAnder.Toolbox.csproj -o ./artifacts` produces:
   - `artifacts/JoakimAnder.Toolbox.<version>.nupkg`
   - `artifacts/JoakimAnder.Toolbox.<version>.snupkg`

   With no git tags present, MinVer derives a pre-release version of the form `0.0.0-alpha.0.N` where `N` is the number of commits since the empty history. This is expected — the first real version comes from tagging `v0.1.0` in the CI/CD sub-project.
5. Inspecting the `.nupkg` (it is a zip):
   - `analyzers/dotnet/cs/JoakimAnder.Toolbox.SourceGenerators.dll` is present.
   - `README.md` is present at the package root.
   - `JoakimAnder.Toolbox.nuspec` contains `<repository type="git" url="https://github.com/JoakimAnder/Toolbox.git" .../>`, `<licenseExpression>MIT</licenseExpression>`, `<readme>README.md</readme>`.
6. All scaffolded files (every file listed in [Solution layout](#solution-layout)) are committed.

## Non-goals

- Publishing to NuGet — that is the CI/CD sub-project.
- Any feature implementation.
- Optimizing the source generator (it has no source-generation logic yet).
- Multi-targeting — explicitly chose single target `net10.0`.

## Open questions

None at design time. The implementation plan resolves the following at execution time:
- Latest stable versions of the pinned packages.
- Lowest 10.0.x SDK currently installed for `global.json`.

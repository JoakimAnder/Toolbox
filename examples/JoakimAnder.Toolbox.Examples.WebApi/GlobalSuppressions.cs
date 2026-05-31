// This file is used by Code Analysis to maintain SuppressMessage attributes
// that are applied to this project.
// Project-level suppressions either have no target or are given a specific
// target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// CA1716: "Shared" is a VB.NET reserved keyword, but this is a personal C# example project
// not intended for multi-language consumption.
[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Example project using 'Shared' as a conventional folder/namespace name; not consumed from VB.NET.",
    Scope = "namespace",
    Target = "~N:JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain")]

[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Example project using 'Shared' as a conventional folder/namespace name; not consumed from VB.NET.",
    Scope = "namespace",
    Target = "~N:JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints")]

namespace System.Runtime.CompilerServices;

// Enables `init` accessors and records on netstandard2.0 — the runtime doesn't ship this marker
// type there, so the compiler needs to find one with this exact name and namespace.
#pragma warning disable S2094 // Remove this empty class — a compiler marker type has to be empty.
internal static class IsExternalInit
{
}
#pragma warning restore S2094

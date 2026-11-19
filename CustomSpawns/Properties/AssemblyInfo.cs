using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CustomSpawns.Tests")]
// Moq proxies an internal interface through this assembly, so a test double of one needs it too.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

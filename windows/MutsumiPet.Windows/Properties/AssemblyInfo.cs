using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

// Single source of truth for the executable's version metadata: both build paths
// compile this file, and both csproj files set GenerateAssemblyInfo=false so the
// SDK does not emit a competing set of attributes.

[assembly: AssemblyTitle("若叶睦桌宠")]
[assembly: AssemblyProduct("MutsumiPet")]
[assembly: AssemblyDescription("透明桌宠 · 若叶睦")]
[assembly: AssemblyCopyright("MIT-licensed source. Artwork excluded - see ASSET_NOTICE.md.")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: NeutralResourcesLanguage("zh-Hans")]
[assembly: ComVisible(false)]

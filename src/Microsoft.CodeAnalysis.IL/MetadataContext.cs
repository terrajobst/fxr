using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis.IL
{
    public sealed class MetadataContext
    {
        private MetadataContext(Compilation compilation,
                                ImmutableArray<MetadataReference> assemblies,
                                ImmutableArray<MetadataReference> dependencies)
        {
            Compilation = compilation;
            Assemblies = assemblies.Select(r => compilation.GetAssemblyOrModuleSymbol(r)).OfType<IAssemblySymbol>().ToImmutableArray();
            Dependencies = dependencies.Select(r => compilation.GetAssemblyOrModuleSymbol(r)).OfType<IAssemblySymbol>().ToImmutableArray();
        }

        public Compilation Compilation { get; }
        public ImmutableArray<IAssemblySymbol> Assemblies { get; }
        public ImmutableArray<IAssemblySymbol> Dependencies { get; }

        public static MetadataContext Create(IEnumerable<string> assemblyPaths)
        {
            return Create(assemblyPaths, Enumerable.Empty<string>());
        }

        public static MetadataContext Create(IEnumerable<string> assemblyPaths,
                                             IEnumerable<string> dependencyPaths)
        {
            var capturedAssemblies = assemblyPaths.Select(p => MetadataReference.CreateFromFile(p));
            var capturedDependencies = dependencyPaths.Select(p => MetadataReference.CreateFromFile(p));
            return Create(capturedAssemblies, capturedDependencies);
        }

        public static MetadataContext Create(IEnumerable<MetadataReference> assemblies)
        {
            return Create(assemblies, Enumerable.Empty<MetadataReference>());
        }

        public static MetadataContext Create(IEnumerable<MetadataReference> assemblies,
                                             IEnumerable<MetadataReference> dependencies)
        {
            var capturedAssemblies = assemblies.ToImmutableArray();
            var capturedDependencies = dependencies.ToImmutableArray();
            var allReferences = capturedAssemblies.AddRange(capturedDependencies);
            var compilation = CSharpCompilation.Create("dummy", references: allReferences);
            return new MetadataContext(compilation, capturedAssemblies, capturedDependencies);
        }
    }
}

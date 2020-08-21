using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Platforms;
using Microsoft.Csv;

using Mono.Options;

namespace fxr
{
    internal sealed class PlatformCompatCommand : ToolCommand
    {
        private string _outputPath;
        private bool _includeImplicit;
        private readonly List<string> _paths = new List<string>();

        public override string Name => "platform-compat";

        public override string Description => "Produces a table that lists OS limitations for .NET APIs, harvested from [SupportedOSPlatform] and [UnsupportedOSPlatform].";

        public override void AddOptions(OptionSet options)
        {
            options
                .Add("o|out=", "Specifies the {path} to the output file", v => _outputPath = v)
                .Add("i|implicit", "Specifies wether APIs nested under platform-specific APIs should be included", v => _includeImplicit = true)
                .Add("<>", v => _paths.Add(v))
            ;
        }

        public override void Execute()
        {
            var files = new SortedSet<string>();
            var hasErrors = false;

            foreach (var path in _paths)
            {
                if (Directory.Exists(path))
                {
                    files.UnionWith(Directory.GetFiles(path, "*.dll"));
                }
                else if (File.Exists(path))
                {
                    files.Add(path);
                }
                else
                {
                    Console.Error.WriteLine($"error '{path}' is neither a file nor a directory'");
                    hasErrors = true;
                }
            }

            if (hasErrors)
                return;

            if (files.Count == 0)
            {
                Console.Error.WriteLine("error: no files found");
                return;
            }

            if (string.IsNullOrEmpty(_outputPath) && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: since you don't have Excel, you need to specify an output path");
                return;
            }

            var processor = new Processor(files, _includeImplicit);
            var document = processor.Process();

            if (string.IsNullOrEmpty(_outputPath))
                document.ViewInExcel();
            else
                document.Save(_outputPath);
        }

        private sealed class Processor
        {
            private readonly MetadataContext _context;
            private readonly ImmutableArray<string> _platforms;
            private readonly bool _includeImplicit;
            private readonly CsvDocument _csvDocument;

            private CsvWriter _writer;

            public Processor(IEnumerable<string> files, bool includeImplicit)
            {
                _context = MetadataContext.Create(files);
                _platforms = PlatformSupport.GetPlatforms(_context.Compilation);

                var columns = new[]
                {
                    "Level",
                    "Assembly",
                    "Namespace",
                    "Type",
                    "Member",
                    "Kind",
                    "Implicit"
                }.Concat(_platforms);

                _csvDocument = new CsvDocument(columns);
                _includeImplicit = includeImplicit;
            }

            public CsvDocument Process()
            {
                _writer = _csvDocument.Append();

                foreach (var assembly in _context.Assemblies)
                    ProcessAssembly(assembly);

                return _csvDocument;
            }

            private void ProcessAssembly(IAssemblySymbol assembly)
            {
                Console.WriteLine($"Processing {assembly.Name}...");

                WritePlatformSupport(assembly, "assembly");

                foreach (var type in assembly.GetTypes().Where(t => t.IsVisibleOutsideAssembly()))
                    ProcessType(type);
            }

            private void ProcessType(INamedTypeSymbol type)
            {
                WritePlatformSupport(type, "Type");

                foreach (var member in type.GetMembers().Where(m => m.IsVisibleOutsideAssembly()))
                {
                    if (member is INamedTypeSymbol nestedType)
                    {
                        ProcessType(nestedType);
                    }
                    else
                    {
                        ProcessMember(member);
                    }
                }
            }

            private void ProcessMember(ISymbol member)
            {
                WritePlatformSupport(member, "Member");
            }

            private void WritePlatformSupport(ISymbol symbol, string level)
            {
                var (support, isImplicit) = GetPlatformSupport(symbol);
                if (support == null)
                    return;

                _writer.Write(level);
                _writer.Write(symbol.GetAssemblyName());
                _writer.Write(symbol.GetNamespaceName());
                _writer.Write(symbol.GetTypeName());
                _writer.Write(symbol.GetMemberName());

                if (support.Kind == PlatformSupportKind.AllowList)
                {
                    _writer.Write("platform-specific");
                }
                else if (support.Kind == PlatformSupportKind.DenyList)
                {
                    _writer.Write("platform-restricted");
                }
                else
                {
                    _writer.Write("?");
                }

                _writer.Write(isImplicit ? "Yes" : "No");

                foreach (var platform in _platforms)
                    _writer.Write(support.GeSupportedVersionsString(platform));

                _writer.WriteLine();
            }

            private (PlatformSupport, bool IsImplicit) GetPlatformSupport(ISymbol symbol)
            {
                var isImplicit = false;

                while (symbol != null)
                {
                    var attributes = symbol.GetAttributes();
                    var support = PlatformSupport.Parse(attributes);
                    if (support != null)
                        return (support, isImplicit);

                    if (!_includeImplicit)
                    {
                        symbol = null;
                    }
                    else
                    {
                        symbol = symbol.ContainingSymbol;
                        isImplicit = true;
                    }
                }

                return (null, false);
            }
        }
    }
}

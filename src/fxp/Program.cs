using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Platforms;
using Microsoft.Csv;

using Mono.Options;

internal static class Program
{
    private static void Main(string[] args)
    {
        var outputPath = "";
        var help = false;
        var includeImplicit = false;
        var paths = new List<string>();

        var options = new OptionSet()
            {
                "Produces a table that lists OS limitations for .NET APIs, harvested from",
                "[SupportedOSPlatform] and [UnsupportedOSPlatform].",
                { "o|out=", "Specifies the {path} to the output file", v => outputPath = v },
                { "i|implicit", "Specifies wether APIs nested under platform-specific APIs should be included", v => includeImplicit = true},
                { "h|?|help", null, v => help = true, true },
                { "<>", v => paths.Add(v) }
            };

        try
        {
            var unprocessed = options.Parse(args);

            if (help)
            {
                var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                Console.Error.WriteLine($"usage: {exeName} <path>... [OPTIONS]+");
                options.WriteOptionDescriptions(Console.Error);
                return;
            }

            if (unprocessed.Any())
            {
                foreach (var option in unprocessed)
                    Console.Error.WriteLine($"error: unrecognized argument {option}");

                return;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return;
        }

        var files = new SortedSet<string>();
        var hasErrors = false;

        foreach (var path in paths)
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

        if (string.IsNullOrEmpty(outputPath) && !ExcelExtensions.IsExcelInstalled())
        {
            Console.Error.WriteLine("error: since you don't have Excel, you need to specify an output path");
            return;
        }

        try
        {
            var processor = new Processer(files, includeImplicit);
            var document = processor.Process();

            if (string.IsNullOrEmpty(outputPath))
                document.ViewInExcel();
            else
                document.Save(outputPath);
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            Console.Error.WriteLine(ex.Message);
        }
    }

    private sealed class Processer
    {
        private readonly MetadataContext _context;
        private readonly ImmutableArray<string> _platforms;
        private readonly bool _includeImplicit;
        private readonly CsvDocument _csvDocument;

        private CsvWriter _writer;

        public Processer(IEnumerable<string> files, bool includeImplicit)
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

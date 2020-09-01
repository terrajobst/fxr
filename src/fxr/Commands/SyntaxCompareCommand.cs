using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.IL;
using Microsoft.Csv;

using Mono.Options;

namespace fxr
{
    internal sealed class SyntaxCompareCommand : ToolCommand
    {
        private string? _outputPath;
        private string? _refPath;
        private string? _implPath;

        public override string Name => "syntaxcomp";

        public override string Description => "Produces a table that compares the syntax between ref and impl";

        public override void AddOptions(OptionSet options)
        {
            options
                .Add("o|out=", "Specifies the {path} to the output file", v => _outputPath = v)
                .Add("r|ref=", "Specifies the directory with the reference assemblies", v => _refPath = v)
                .Add("i|impl=", "Specifies the directory with the implementation assemblies", v => _implPath = v)
            ;
        }

        public override void Execute()
        {
            if (_refPath == null)
            {
                Console.Error.WriteLine($"error: must specify a -r");
                return;
            }

            if (_implPath == null)
            {
                Console.Error.WriteLine($"error: must specify a -i");
                return;
            }

            if (!Directory.Exists(_refPath))
            {
                Console.Error.WriteLine($"error '{_refPath}' is not a directory'");
                return;
            }

            if (!Directory.Exists(_implPath))
            {
                Console.Error.WriteLine($"error '{_implPath}' is not a directory'");
                return;
            }

            if (string.IsNullOrEmpty(_outputPath) && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: since you don't have Excel, you need to specify an output path");
                return;
            }

            var document = Run(_refPath, _implPath);

            if (string.IsNullOrEmpty(_outputPath))
                document.ViewInExcel();
            else
                document.Save(_outputPath);
        }

        private class ApiInfo
        {
            public ApiInfo(ISymbol symbol)
            {
                AssemblyName = symbol.GetAssemblyName();
                NamespaceName = symbol.GetNamespaceName();
                TypeName = symbol.GetTypeName();
                MemberName = symbol.GetMemberName();
            }

            public string? AssemblyName { get; set; }
            public string? NamespaceName { get; set; }
            public string? TypeName { get; }
            public string? MemberName { get; }
            public string? RefSyntax { get; set; }
            public string? ImplSyntax { get; set; }
        }

        private static CsvDocument Run(string refPath, string implPath)
        {
            var infoByGuid = new Dictionary<Guid, ApiInfo>();

            foreach (var type in GetTypes(refPath))
            {
                Index(type, s =>
                {
                    var info = new ApiInfo(s);
                    info.RefSyntax = s.GetSyntax();
                    infoByGuid.TryAdd(s.GetGuid(), info);
                });
            }

            foreach (var type in GetTypes(implPath))
            {
                Index(type, s =>
                {
                    if (infoByGuid.TryGetValue(s.GetGuid(), out var info))
                        info.ImplSyntax = s.GetSyntax();
                });
            }

            static IEnumerable<INamedTypeSymbol> GetTypes(string directory)
            {
                var files = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
                var context = MetadataContext.Create(files);
                return context.GetTypes().Where(t => t.IsVisibleOutsideAssembly());
            }

            void Index(INamedTypeSymbol type, Action<ISymbol> recorder)
            {
                recorder(type);

                if (type.TypeKind == TypeKind.Delegate)
                    return;

                foreach (var member in type.GetMembers())
                {
                    if (!member.IsVisibleOutsideAssembly())
                        continue;

                    if (member.IsAccessor())
                        continue;

                    if (member is INamedTypeSymbol t)
                        Index(t, recorder);
                    else
                        recorder(member);
                }
            }

            var document = new CsvDocument("Guid",
                                           "Assembly",
                                           "Namespace",
                                           "Type",
                                           "Member",
                                           "SyntaxRef",
                                           "SyntaxImpl");

            using (var writer = document.Append())
            {
                foreach (var (id, info) in infoByGuid)
                {
                    if (string.Equals(info.RefSyntax, info.ImplSyntax, StringComparison.Ordinal))
                        continue;

                    writer.Write(id.ToString("N"));
                    writer.Write(info.AssemblyName);
                    writer.Write(info.NamespaceName);
                    writer.Write(info.TypeName);
                    writer.Write(info.MemberName);
                    writer.Write(info.RefSyntax);
                    writer.Write(info.ImplSyntax);
                    writer.WriteLine();
                }
            }

            return document;
        }
    }
}

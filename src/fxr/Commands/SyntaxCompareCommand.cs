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
            var refByGuid = new Dictionary<Guid, ISymbol>();

            var refApis = IndexAllApis(refPath);
            var implApis = IndexAllApis(implPath);

            var document = new CsvDocument("Guid",
                                           "Assembly",
                                           "Namespace",
                                           "Type",
                                           "Member",
                                           "SyntaxRef",
                                           "SyntaxImpl");

            using (var writer = document.Append())
            {
                foreach (var (id, refApi) in refApis.OrderBy(kv => kv.Value, ApiComparer.Instance))
                {
                    if (implApis.TryGetValue(id, out var implApi))
                    {
                        var includeNullableAnnotations = refApi.ContainingAssembly.IsNullAnnotated() &&
                                                         implApi.ContainingAssembly.IsNullAnnotated();

                        var assemblyName = refApi.GetAssemblyName();
                        var namespaceName = refApi.GetNamespaceName();
                        var typeName = refApi.GetTypeName();
                        var memberName = refApi.GetMemberName();
                        var refSyntax = refApi.GetSyntax(includeNullableAnnotations: includeNullableAnnotations);
                        var implSyntax = implApi.GetSyntax(includeNullableAnnotations: includeNullableAnnotations);

                        if (string.Equals(refSyntax, implSyntax, StringComparison.Ordinal))
                            continue;

                        writer.Write(id.ToString("N"));
                        writer.Write(assemblyName);
                        writer.Write(namespaceName);
                        writer.Write(typeName);
                        writer.Write(memberName);
                        writer.Write(refSyntax);
                        writer.Write(implSyntax);
                        writer.WriteLine();
                    }
                }
            }

            return document;

            static Dictionary<Guid, ISymbol> IndexAllApis(string directory)
            {
                var result = new Dictionary<Guid, ISymbol>();

                foreach (var api in GetAllApis(directory))
                    result.TryAdd(api.GetGuid(), api);

                return result;
            }

            static IEnumerable<ISymbol> GetAllApis(string directory)
            {
                var files = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
                var context = MetadataContext.Create(files);
                var stack = new Stack<ITypeSymbol>();

                foreach (var type in context.GetTypes().Where(t => t.IsVisibleOutsideAssembly()))
                    stack.Push(type);

                while (stack.Count > 0)
                {
                    var type = stack.Pop();
                    yield return type;

                    foreach (var member in type.GetMembers())
                    {
                        if (!member.IsVisibleOutsideAssembly())
                            continue;

                        if (member.IsAccessor())
                            continue;

                        if (member is INamedTypeSymbol t)
                            stack.Push(t);
                        else
                            yield return member;
                    }
                }
            }
        }
    }
}

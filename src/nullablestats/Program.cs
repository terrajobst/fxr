using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.IL;
using Microsoft.Csv;

using Mono.Options;

namespace nullablestats
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var outputPath = "";
            var help = false;
            var path = "";

            var options = new OptionSet()
            {
                "Shows how many API are nullable annotated",
                { "o|out=", "Specifies the {path} to the output file", v => outputPath = v },
                { "h|?|help", null, v => help = true, true },
                { "<>", v => path = v }
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

            if (!Directory.Exists(path))
            {
                Console.Error.WriteLine($"error '{path}' is not a directory'");
                return;
            }

            if (string.IsNullOrEmpty(outputPath) && !ExcelExtensions.IsExcelInstalled())
            {
                Console.Error.WriteLine("error: since you don't have Excel, you need to specify an output path");
                return;
            }

            try
            {
                var document = Run(path);

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

        private static CsvDocument Run(string path)
        {
            var csvDocument = new CsvDocument("Fx", "Assembly", "Namespace", "Type", "Member", "CanBeAnnotated", "IsAnnotated");

            using (var writer = csvDocument.Append())
            {
                foreach (var fxDirectory in Directory.GetDirectories(path))
                {
                    var files = Directory.GetFiles(fxDirectory, "*.dll", SearchOption.AllDirectories);
                    var context = MetadataContext.Create(files);
                    var walker = new NullableWalker();
                    var fxName = Path.GetFileName(fxDirectory);

                    foreach (var type in context.GetTypes().Where(t => t.IsVisibleOutsideAssembly()))
                        ProcessType(type);

                    void ProcessType(INamedTypeSymbol type)
                    {
                        RecordApi(type);

                        if (type.TypeKind == TypeKind.Delegate)
                            return;

                        foreach (var member in type.GetMembers())
                        {
                            if (!member.IsVisibleOutsideAssembly())
                                continue;

                            if (member.IsAccessor())
                                continue;

                            if (member is IMethodSymbol m)
                                ProcessMethod(m);
                            else if (member is IPropertySymbol p)
                                ProcessProperty(p);
                            else if (member is IFieldSymbol f)
                                ProcessField(f);
                            else if (member is IEventSymbol e)
                                ProcessEvent(e);
                            else if (member is INamedTypeSymbol t)
                                ProcessType(t);
                        }
                    }

                    void ProcessMethod(IMethodSymbol symbol)
                    {
                        walker.WalkMethod(symbol);
                        RecordApi(symbol);
                    }

                    void ProcessField(IFieldSymbol symbol)
                    {
                        walker.WalkField(symbol);
                        RecordApi(symbol);
                    }

                    void ProcessProperty(IPropertySymbol symbol)
                    {
                        walker.WalkProperty(symbol);
                        RecordApi(symbol);
                    }

                    void ProcessEvent(IEventSymbol symbol)
                    {
                        walker.WalkEvent(symbol);
                        RecordApi(symbol);
                    }

                    void RecordApi(ISymbol symbol)
                    {
                        writer.Write(fxName);
                        writer.Write(symbol.GetAssemblyName());
                        writer.Write(symbol.GetNamespaceName());
                        writer.Write(symbol.GetTypeName());
                        writer.Write(symbol.GetMemberName());
                        writer.Write(walker.CanBeAnnotated ? "Yes" : "No");
                        writer.Write(walker.IsAnnotated ? "Yes" : "No");
                        writer.WriteLine();
                        walker.Reset();
                    }
                }
            }

            return csvDocument;
        }
    }

    class NullableWalker : TypeWalker
    {
        public bool CanBeAnnotated { get; private set; }
        public bool IsAnnotated { get; private set; }

        public void Reset()
        {
            CanBeAnnotated = false;
            IsAnnotated = false;
        }

        public override void WalkType(ITypeSymbol symbol)
        {
            if (symbol.SpecialType == SpecialType.System_ValueType ||
                symbol.SpecialType == SpecialType.System_Enum ||
                symbol.SpecialType == SpecialType.System_Delegate ||
                symbol.SpecialType == SpecialType.System_MulticastDelegate)
            {
                return;
            }

            if (symbol.IsReferenceType)
            {
                CanBeAnnotated = true;

                if (symbol.NullableAnnotation == NullableAnnotation.Annotated ||
                    symbol.NullableAnnotation == NullableAnnotation.NotAnnotated)
                    IsAnnotated = true;
            }

            base.WalkType(symbol);
        }
    }

    abstract class TypeWalker
    {
        public virtual void WalkMethod(IMethodSymbol signature)
        {
            WalkType(signature.ReturnType);

            foreach (var parameter in signature.Parameters)
                WalkType(parameter.Type);
        }

        public virtual void WalkField(IFieldSymbol symbol)
        {
            WalkType(symbol.Type);
        }

        public virtual void WalkProperty(IPropertySymbol symbol)
        {
            WalkType(symbol.Type);

            foreach (var parameter in symbol.Parameters)
                WalkType(parameter.Type);
        }

        public virtual void WalkEvent(IEventSymbol symbol)
        {
            WalkType(symbol.Type);
        }

        public virtual void WalkType(ITypeSymbol symbol)
        {
            switch (symbol.TypeKind)
            {
                case TypeKind.Unknown:
                case TypeKind.Error:
                case TypeKind.Module:
                case TypeKind.Submission:
                case TypeKind.TypeParameter:
                    // Ignore
                    break;
                case TypeKind.Array:
                    WalkArray((IArrayTypeSymbol)symbol);
                    break;
                case TypeKind.Class:
                case TypeKind.Delegate:
                case TypeKind.Enum:
                case TypeKind.Interface:
                case TypeKind.Struct:
                case TypeKind.Dynamic:
                    WalkNamedType((INamedTypeSymbol)symbol);
                    break;
                case TypeKind.Pointer:
                    WalkPointer((IPointerTypeSymbol)symbol);
                    break;
                case TypeKind.FunctionPointer:
                    WalkFunctionPointer((IFunctionPointerTypeSymbol)symbol);
                    break;
            }
        }

        protected virtual void WalkNamedType(INamedTypeSymbol symbol)
        {
            foreach (var type in symbol.TypeArguments)
                WalkType(type);

            if (symbol.BaseType != null)
                WalkType(symbol.BaseType);

            //foreach (var type in symbol.Interfaces)
            //    WalkType(type);
        }

        protected virtual void WalkArray(IArrayTypeSymbol symbol)
        {
            WalkType(symbol.ElementType);
        }

        protected virtual void WalkPointer(IPointerTypeSymbol symbol)
        {
            WalkType(symbol.PointedAtType);
        }

        protected virtual void WalkFunctionPointer(IFunctionPointerTypeSymbol symbol)
        {
            WalkMethod(symbol.Signature);
        }
    }
}

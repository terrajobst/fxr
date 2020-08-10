using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.IL
{
    public static class MetadataExtensions
    {
        private static readonly SymbolDisplayFormat _format = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameAndParameters,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
            parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut |
                              SymbolDisplayParameterOptions.IncludeType,
            propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
            localOptions: SymbolDisplayLocalOptions.None,
            kindOptions: SymbolDisplayKindOptions.None,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

        public static IEnumerable<INamedTypeSymbol> GetTypes(this MetadataContext context)
        {
            return context.Assemblies.SelectMany(a => a.GetTypes());
        }

        public static IEnumerable<INamedTypeSymbol> GetTypes(this IAssemblySymbol symbol)
        {
            var stack = new Stack<INamespaceSymbol>();
            stack.Push(symbol.GlobalNamespace);

            while (stack.Count > 0)
            {
                var ns = stack.Pop();
                foreach (var member in ns.GetMembers())
                {
                    if (member is INamespaceSymbol childNs)
                        stack.Push(childNs);
                    else if (member is INamedTypeSymbol type)
                        yield return type;
                }
            }
        }

        public static bool IsAccessor(this ISymbol symbol)
        {
            if (symbol is IMethodSymbol method)
            {
                return method.MethodKind == MethodKind.PropertyGet ||
                       method.MethodKind == MethodKind.PropertySet ||
                       method.MethodKind == MethodKind.EventAdd ||
                       method.MethodKind == MethodKind.EventRemove ||
                       method.MethodKind == MethodKind.EventRaise;
            }

            return false;
        }

        public static bool IsVisibleOutsideAssembly(this ISymbol symbol)
        {
            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.Public:
                    return symbol.ContainingType?.IsVisibleOutsideAssembly() ?? true;
                default:
                case Accessibility.NotApplicable:
                case Accessibility.Private:
                case Accessibility.ProtectedAndInternal:
                case Accessibility.Internal:
                    return false;
            }
        }

        public static string GetAssemblyName(this ISymbol symbol)
        {
            if (symbol == null)
                return null;

            if (symbol is IAssemblySymbol)
                return symbol.Name;
            else
                return symbol.ContainingAssembly.GetAssemblyName();
        }

        public static string GetNamespaceName(this ISymbol symbol)
        {
            if (symbol == null)
                return null;

            if (symbol is INamespaceSymbol ns)
                return ns.ToString();
            else
                return symbol.ContainingNamespace.GetNamespaceName();
        }

        public static string GetTypeName(this ISymbol symbol)
        {
            if (symbol == null)
                return null;

            if (symbol is INamedTypeSymbol type)
            {
                var typeName = type.ToDisplayString(_format);
                return type.ContainingType == null 
                    ? typeName 
                    : type.ContainingType.GetTypeName() + "." + typeName;
            }
            else
            {
                return symbol.ContainingType.GetTypeName();
            }
        }

        public static string GetMemberName(this ISymbol symbol)
        {
            if (symbol == null)
                return null;

            switch (symbol.Kind)
            {
                case SymbolKind.Event:
                case SymbolKind.Field:
                case SymbolKind.Method:
                case SymbolKind.Property:
                    return symbol.ToDisplayString(_format);
                default:
                    return null;
            }
        }
    }
}

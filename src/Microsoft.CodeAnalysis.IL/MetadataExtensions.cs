using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return context.Assemblies.SelectMany(a => a.GetTypes());
        }

        public static IEnumerable<INamedTypeSymbol> GetTypes(this IAssemblySymbol symbol)
        {
            if (symbol is null)
                throw new ArgumentNullException(nameof(symbol));

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

        public static ApiKind GetApiKind(this ISymbol symbol)
        {
            if (symbol is INamespaceSymbol)
                return ApiKind.Namespace;

            if (symbol is INamedTypeSymbol type)
            {
                if (type.TypeKind == TypeKind.Interface)
                    return ApiKind.Interface;
                else if (type.TypeKind == TypeKind.Delegate)
                    return ApiKind.Delegate;
                else if (type.TypeKind == TypeKind.Enum)
                    return ApiKind.Enum;
                else if (type.TypeKind == TypeKind.Struct)
                    return ApiKind.Struct;
                else
                    return ApiKind.Class;
            }

            if (symbol is IMethodSymbol method)
            {
                if (method.MethodKind == MethodKind.Constructor)
                    return ApiKind.Constructor;
                else if (method.MethodKind == MethodKind.Destructor)
                    return ApiKind.Destructor;
                else if (method.MethodKind == MethodKind.UserDefinedOperator || method.MethodKind == MethodKind.Conversion)
                    return ApiKind.Operator;

                return ApiKind.Method;
            }

            if (symbol is IFieldSymbol f)
            {
                if (f.ContainingType.TypeKind == TypeKind.Enum)
                    return ApiKind.EnumItem;

                if (f.IsConst)
                    return ApiKind.Constant;

                return ApiKind.Field;
            }

            if (symbol is IPropertySymbol)
                return ApiKind.Property;

            if (symbol is IEventSymbol)
                return ApiKind.Event;

            throw new Exception($"Unpexected symbol kind {symbol.Kind}");
        }

        public static string GetPublicKeyTokenString(this AssemblyIdentity identity)
        {
            return BitConverter.ToString(identity.PublicKeyToken.ToArray()).Replace("-", "").ToLower();
        }

        public static Guid GetGuid(this ISymbol symbol)
        {
            if (symbol is ITypeParameterSymbol)
                return Guid.Empty;

            if (symbol is INamespaceSymbol ns && ns.IsGlobalNamespace)
                return GetGuid("N:<global>");

            var id = symbol.OriginalDefinition.GetDocumentationCommentId();
            if (id == null)
                return Guid.Empty;

            return GetGuid(id);
        }

        private static Guid GetGuid(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(bytes);
            return new Guid(hashBytes);
        }

        public static string GetSyntax(this ISymbol symbol, bool includeAttributes = false, bool includeNullableAnnotations = false)
        {
            var syntaxWriter = new StringSyntaxWriter();
            var declarationWriter = new CSharpDeclarationWriter
            {
                IncludeAttributes = includeAttributes,
                IncludeNullableAnnotations = includeNullableAnnotations,
                IncludeNonNullAnnotations = includeNullableAnnotations
            };
            declarationWriter.WriteDeclaration(symbol, syntaxWriter);
            return syntaxWriter.ToString();
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
            if (symbol is null)
                throw new ArgumentNullException(nameof(symbol));

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

        public static string? GetAssemblyName(this ISymbol symbol)
        {
            if (symbol == null)
                return null;

            if (symbol is IAssemblySymbol)
                return symbol.Name;
            else
                return symbol.ContainingAssembly.GetAssemblyName();
        }

        public static string? GetNamespaceName(this ISymbol symbol)
        {
            if (symbol == null)
                return null;

            if (symbol is INamespaceSymbol ns)
                return ns.ToString();
            else
                return symbol.ContainingNamespace.GetNamespaceName();
        }

        public static string? GetTypeName(this ISymbol symbol)
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

        public static string? GetMemberName(this ISymbol symbol)
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

        public static bool IsNullAnnotated(this IAssemblySymbol symbol)
        {
            return symbol.GetTypeByMetadataName("System.Runtime.CompilerServices.NullableAttribute") != null;
        }

        public static IEnumerable<T> Ordered<T>(this IEnumerable<T> types)
            where T: ISymbol
        {
            return types.OrderBy(t => t, ApiComparer.Instance);
        }

        public static IEnumerable<AttributeData> Ordered(this IEnumerable<AttributeData> attributes)
        {
            return attributes.OrderBy(a => a.AttributeClass?.Name)
                             .ThenBy(a => a.ConstructorArguments.Length)
                             .ThenBy(a => a.NamedArguments.Length);
        }

        public static IEnumerable<KeyValuePair<string, TypedConstant>> Ordered(this IEnumerable<KeyValuePair<string, TypedConstant>> namedArguments)
        {
            return namedArguments.OrderBy(kv => kv.Key);
        }

        public static bool IsPartOfApi(this AttributeData attribute)
        {
            if (attribute.AttributeClass == null ||
                !attribute.AttributeClass.IsVisibleOutsideAssembly())
                return false;

            if (attribute.AttributeClass.Name == "CompilerGeneratedAttribute")
                return false;

            if (attribute.AttributeClass.Name == "TargetedPatchingOptOutAttribute")
                return false;

            return true;
        }

        public static Accessibility GetApiAccessibility(this ISymbol symbol)
        {
            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.ProtectedOrInternal:
                    return Accessibility.Protected;
                case Accessibility.ProtectedAndInternal:
                case Accessibility.NotApplicable:
                case Accessibility.Private:
                case Accessibility.Protected:
                case Accessibility.Internal:
                case Accessibility.Public:
                default:
                    return symbol.DeclaredAccessibility;
            }
        }

        public static ImmutableArray<AttributeData> GetApiAttributes(this IMethodSymbol method)
        {
            if (method == null)
                return ImmutableArray<AttributeData>.Empty;

            return method.GetAttributes().Where(IsPartOfApi).ToImmutableArray();
        }

        public static ImmutableArray<INamedTypeSymbol> GetApiInterfaces(this INamedTypeSymbol type)
        {
            var interfaces = type.Interfaces.Where(t => t.IsVisibleOutsideAssembly()).ToArray();
            Array.Sort(interfaces, ApiComparer.Instance);
            return interfaces.ToImmutableArray();
        }
    }
}

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL
{
    public sealed class ApiComparer : IComparer<ISymbol>
    {
        public static ApiComparer Instance { get; } = new ApiComparer();

        private ApiComparer()
        {
        }

        public int Compare(ISymbol? x, ISymbol? y)
        {
            if (x == null && y == null)
                return 0;

            if (x == null)
                return -1;

            if (y == null)
                return 1;

            var xKind = x.GetApiKind();
            var yKind = y.GetApiKind();

            var result = xKind.CompareTo(yKind);
            if (result != 0)
                return result;

            if (x is INamedTypeSymbol xNamed && y is INamedTypeSymbol yNamed)
            {
                result = xNamed.Name.CompareTo(yNamed.Name);
                if (result != 0)
                    return result;

                result = xNamed.Arity.CompareTo(yNamed.Arity);
                if (result != 0)
                    return result;
            }

            if (x is IMethodSymbol xMethod && y is IMethodSymbol yMethod)
            {
                result = xMethod.Name.CompareTo(yMethod.Name);
                if (result != 0)
                    return result;

                result = xMethod.TypeParameters.Length.CompareTo(yMethod.TypeParameters.Length);
                if (result != 0)
                    return result;

                result = xMethod.Parameters.Length.CompareTo(yMethod.Parameters.Length);
                if (result != 0)
                    return result;
            }

            return x.ToDisplayString().CompareTo(y.ToDisplayString());
        }
    }
}

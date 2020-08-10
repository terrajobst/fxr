using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.CodeAnalysis.IL.Platforms
{
    public sealed class PlatformSupport
    {
        public PlatformSupport(IEnumerable<Platform> supportedPlatforms,
                               IEnumerable<Platform> unsupportedPlatforms,
                               IEnumerable<Platform> obsoletedPlatforms)
        {
            SupportedPlatforms = supportedPlatforms.OrderBy(x => x).ToImmutableArray();
            UnsupportedPlatforms = unsupportedPlatforms.OrderBy(x => x).ToImmutableArray();
            ObsoletedPlatforms = obsoletedPlatforms.OrderBy(x => x).ToImmutableArray();

            var platformMinimums = SupportedPlatforms.Concat(UnsupportedPlatforms)
                                                     .GroupBy(p => p.Name)
                                                     .Select(g =>  g.Min());

            if (platformMinimums.All(m => SupportedPlatforms.Contains(m)))
                Kind = PlatformSupportKind.AllowList;
            else if (platformMinimums.All(m => UnsupportedPlatforms.Contains(m)))
                Kind = PlatformSupportKind.DenyList;
            else
                Kind = PlatformSupportKind.Malformed;
        }

        public PlatformSupportKind Kind { get; }
        public ImmutableArray<Platform> SupportedPlatforms { get; }
        public ImmutableArray<Platform> UnsupportedPlatforms { get; }
        public ImmutableArray<Platform> ObsoletedPlatforms { get; }

        public IReadOnlyList<(Version From, bool IsSupported)> GetSupportedVersions(string platformName)
        {
            if (Kind == PlatformSupportKind.Malformed)
                return Array.Empty<(Version, bool)>();

            var supported = SupportedPlatforms.Where(p => string.Equals(p.Name, platformName, StringComparison.OrdinalIgnoreCase)).Select(p => p.Version);
            var unsupported = UnsupportedPlatforms.Where(p => string.Equals(p.Name, platformName, StringComparison.OrdinalIgnoreCase)).Select(p => p.Version);
            var interleaved = supported.Select(v => (v, true))
                                       .Concat(unsupported.Select(v => (v, false)))
                                       .Cast<(Version Version, bool IsSupported)>()
                                       .OrderBy(t => t.Version)
                                       .ToList();

            var initalVersionSupported = Kind == PlatformSupportKind.AllowList
                                            ? false
                                            : true;

            var initialVersion = new Version(0, 0, 0, 0);

            if (interleaved.Count == 0 || interleaved[0].Version > initialVersion)
                interleaved.Insert(0, (initialVersion, initalVersionSupported));

            return interleaved;
        }

        public string GeSupportedVersionsString(string platformName)
        {
            var sb = new StringBuilder();
            var ranges = GetSupportedVersions(platformName);

            if (ranges.Count == 1)
            {
                if (ranges[0].IsSupported)
                {
                    return "any";
                }
                else
                {
                    return "none";
                }
            }

            var isOpen = false;

            foreach (var (version, isSupported) in ranges)
            {
                if (isSupported)
                {
                    sb.Append("[");
                    sb.Append(version);
                    sb.Append("-");
                    isOpen = true;
                }
                else
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(version);
                        sb.Append(")");
                        isOpen = false;
                    }
                }
            }

            if (isOpen)
                sb.Append("*]");

            return sb.ToString();
        }

        public static PlatformSupport Parse(ImmutableArray<AttributeData> attributes)
        {
            var supportedPlatforms = (List<Platform>)null;
            var unsupportedPlatforms = (List<Platform>)null;
            var obsoletedPlatforms = (List<Platform>)null;

            foreach (var attribute in attributes)
            {
                if (attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                    attribute.ConstructorArguments[0].Value is string platformName &&
                    attribute.NamedArguments.Length == 0)
                {
                    var name = attribute.AttributeClass.Name;
                    
                    static bool InCorrectNamespace(ISymbol symbol)
                    {
                        return symbol.ContainingNamespace.ToString() == "System.Runtime.Versioning";
                    }

                    switch (name)
                    {
                        case "SupportedOSPlatformAttribute" when InCorrectNamespace(attribute.AttributeClass):
                        {
                            var platform = Platform.Parse(platformName);

                            if (supportedPlatforms == null)
                                supportedPlatforms = new List<Platform>();

                            supportedPlatforms.Add(platform);
                            break;
                        }

                        case "UnsupportedOSPlatformAttribute" when InCorrectNamespace(attribute.AttributeClass):
                        {
                            var platform = Platform.Parse(platformName);

                            if (unsupportedPlatforms == null)
                                unsupportedPlatforms = new List<Platform>();

                            unsupportedPlatforms.Add(platform);
                            break;
                        }

                        case "ObsoletedInOSPlatformAttribute" when InCorrectNamespace(attribute.AttributeClass):
                        {
                            var platform = Platform.Parse(platformName);

                            if (obsoletedPlatforms == null)
                                obsoletedPlatforms = new List<Platform>();

                            obsoletedPlatforms.Add(platform);
                            break;
                        }
                    }
                }
            }

            if (supportedPlatforms == null && unsupportedPlatforms == null && obsoletedPlatforms == null)
                return null;

            return new PlatformSupport((IEnumerable<Platform>) supportedPlatforms ?? Array.Empty<Platform>(),
                                       (IEnumerable<Platform>) unsupportedPlatforms ?? Array.Empty<Platform>(),
                                       (IEnumerable<Platform>) obsoletedPlatforms ?? Array.Empty<Platform>());
        }

        public static ImmutableArray<string> GetPlatforms(Compilation compilation)
        {
            var operatingSystemType = compilation.GetTypeByMetadataName("System.OperatingSystem");
            if (operatingSystemType == null)
                return ImmutableArray<string>.Empty;

            var prefix = "Is";
            var members = operatingSystemType.GetMembers()
                                             .OfType<IMethodSymbol>()
                                             .Where(m => m.Name.StartsWith(prefix) && m.Parameters.Length == 0 && m.ReturnType.SpecialType == SpecialType.System_Boolean)
                                             .Select(m => m.Name.Substring(prefix.Length));
            return members.ToImmutableArray();
        }
    }
}

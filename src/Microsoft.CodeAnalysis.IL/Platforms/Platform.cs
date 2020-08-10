using System;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL.Platforms
{
    public struct Platform : IEquatable<Platform>, IComparable<Platform>
    {
        public Platform(string name, Version version)
        {
            Name = name;
            Version = version;
        }

        public static Platform Parse(string text)
        {
            var versionStart = text.Length;
            while (versionStart > 0)
            {
                var c = text[versionStart - 1];
                if (c == '.' || char.IsDigit(c))
                    versionStart--;
                else
                    break;
            }

            var name = text.Substring(0, versionStart);
            var versionText = text.Substring(versionStart);
            var version = versionText.Length == 0
                    ? new Version(0, 0, 0, 0)
                    : Version.Parse(versionText);
            return new Platform(name, version);
        }

        public override bool Equals(object obj)
        {
            return obj is Platform platform && Equals(platform);
        }

        public bool Equals(Platform other)
        {
            return Name == other.Name &&
                   EqualityComparer<Version>.Default.Equals(Version, other.Version);
        }

        public override int GetHashCode()
        {
            var hashCode = 2112831277;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<Version>.Default.GetHashCode(Version);
            return hashCode;
        }

        public int CompareTo(Platform other)
        {
            var result = Name.CompareTo(other.Name);
            if (result != 0)
                return result;

           return Version.CompareTo(other.Version);
        }

        public string Name { get; }
        public Version Version { get; }

        public static bool operator ==(Platform left, Platform right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Platform left, Platform right)
        {
            return !(left == right);
        }
    }
}

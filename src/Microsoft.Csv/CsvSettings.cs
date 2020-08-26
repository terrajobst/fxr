using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Csv
{
    public struct CsvSettings : IEquatable<CsvSettings>
    {
        public static CsvSettings Default = new CsvSettings(
            encoding: Encoding.UTF8,
            delimiter: ',',
            textQualifier: '"'
        );

        public CsvSettings(Encoding encoding, char delimiter, char textQualifier)
            : this()
        {
            Encoding = encoding;
            Delimiter = delimiter;
            TextQualifier = textQualifier;
        }

        public Encoding Encoding { get; private set; }
        public char Delimiter { get; private set; }
        public char TextQualifier { get; private set; }

        public bool IsValid => Encoding != null;

        public override bool Equals(object? obj)
        {
            return obj is CsvSettings settings && Equals(settings);
        }

        public bool Equals(CsvSettings other)
        {
            return EqualityComparer<Encoding>.Default.Equals(Encoding, other.Encoding) &&
                   Delimiter == other.Delimiter &&
                   TextQualifier == other.TextQualifier &&
                   IsValid == other.IsValid;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Encoding, Delimiter, TextQualifier, IsValid);
        }

        public static bool operator ==(CsvSettings left, CsvSettings right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CsvSettings left, CsvSettings right)
        {
            return !(left == right);
        }
    }
}
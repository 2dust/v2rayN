using System.IO;

namespace v2rayN.Base
{
    static class StringEx
    {
        public static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool IsNullOrWhiteSpace(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        public static bool BeginWithAny(this string s, IEnumerable<char> chars)
        {
            if (s.IsNullOrEmpty()) return false;
            return chars.Contains(s[0]);
        }

        public static bool IsWhiteSpace(this string value)
        {
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c)) continue;

                return false;
            }
            return true;
        }


        public static IEnumerable<string> NonWhiteSpaceLines(this TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IsWhiteSpace()) continue;
                yield return line;
            }
        }

        public static string TrimEx(this string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}

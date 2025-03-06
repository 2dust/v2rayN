namespace ServiceLib.Common
{
    public class SemanticVersion
    {
        private readonly int major;
        private readonly int minor;
        private readonly int patch;
        private readonly string version;

        public SemanticVersion(int major, int minor, int patch)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
            version = $"{major}.{minor}.{patch}";
        }

        public SemanticVersion(string? version)
        {
            try
            {
                if (string.IsNullOrEmpty(version))
                {
                    major = 0;
                    minor = 0;
                    patch = 0;
                    return;
                }
                this.version = version.RemovePrefix('v');

                var parts = this.version.Split('.');
                if (parts.Length == 2)
                {
                    major = int.Parse(parts.First());
                    minor = int.Parse(parts.Last());
                    patch = 0;
                }
                else if (parts.Length is 3 or 4)
                {
                    major = int.Parse(parts[0]);
                    minor = int.Parse(parts[1]);
                    patch = int.Parse(parts[2]);
                }
                else
                {
                    throw new ArgumentException("Invalid version string");
                }
            }
            catch
            {
                major = 0;
                minor = 0;
                patch = 0;
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is SemanticVersion other)
            {
                return major == other.major && minor == other.minor && patch == other.patch;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return major.GetHashCode() ^ minor.GetHashCode() ^ patch.GetHashCode();
        }

        /// <summary>
        /// Use ToVersionString(string? prefix) instead if possible.
        /// </summary>
        /// <returns>major.minor.patch</returns>
        public override string ToString()
        {
            return version;
        }

        public string ToVersionString(string? prefix = null)
        {
            if (prefix == null)
            {
                return version;
            }
            else
            {
                return $"{prefix}{version}";
            }
        }

        public static bool operator ==(SemanticVersion v1, SemanticVersion v2)
        { return v1.Equals(v2); }

        public static bool operator !=(SemanticVersion v1, SemanticVersion v2)
        { return !v1.Equals(v2); }

        public static bool operator >=(SemanticVersion v1, SemanticVersion v2)
        { return v1.GreaterEquals(v2); }

        public static bool operator <=(SemanticVersion v1, SemanticVersion v2)
        { return v1.LessEquals(v2); }

        #region Private

        private bool GreaterEquals(SemanticVersion other)
        {
            if (major < other.major)
            {
                return false;
            }
            else if (major > other.major)
            {
                return true;
            }
            else
            {
                if (minor < other.minor)
                {
                    return false;
                }
                else if (minor > other.minor)
                {
                    return true;
                }
                else
                {
                    if (patch < other.patch)
                    {
                        return false;
                    }
                    else if (patch > other.patch)
                    {
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
        }

        private bool LessEquals(SemanticVersion other)
        {
            if (major < other.major)
            {
                return true;
            }
            else if (major > other.major)
            {
                return false;
            }
            else
            {
                if (minor < other.minor)
                {
                    return true;
                }
                else if (minor > other.minor)
                {
                    return false;
                }
                else
                {
                    if (patch < other.patch)
                    {
                        return true;
                    }
                    else if (patch > other.patch)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
        }

        #endregion Private
    }
}

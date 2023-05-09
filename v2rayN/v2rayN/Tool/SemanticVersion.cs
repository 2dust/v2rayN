using v2rayN.Base;

namespace v2rayN.Tool
{
    public class SemanticVersion
    {
        private int major;
        private int minor;
        private int patch;
        private string version;

        public SemanticVersion(int major, int minor, int patch)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
            this.version = $"{major}.{minor}.{patch}";
        }

        public SemanticVersion(string version)
        {
            this.version = version.RemovePrefix('v');
            try
            {
                string[] parts = this.version.Split('.');
                if (parts.Length == 2)
                {
                    this.major = int.Parse(parts[0]);
                    this.minor = int.Parse(parts[1]);
                    this.patch = 0;
                }
                else if (parts.Length == 3)
                {
                    this.major = int.Parse(parts[0]);
                    this.minor = int.Parse(parts[1]);
                    this.patch = int.Parse(parts[2]);
                }
                else
                {
                    throw new ArgumentException("Invalid version string");
                }
            }
            catch
            {
                this.major = 0;
                this.minor = 0;
                this.patch = 0;
                this.version = "0.0.0";
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is SemanticVersion other)
            {
                return this.major == other.major && this.minor == other.minor && this.patch == other.patch;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return this.major.GetHashCode() ^ this.minor.GetHashCode() ^ this.patch.GetHashCode();
        }

        /// <summary>
        /// Use ToVersionString(string? prefix) instead if possible.
        /// </summary>
        /// <returns>major.minor.patch</returns>
        public override string ToString()
        {
            return this.version;
        }

        public string ToVersionString(string? prefix = null)
        {
            if (prefix == null)
            {
                return this.version;
            }
            else
            {
                return $"{prefix}{this.version}";
            }
        }

        public static bool operator ==(SemanticVersion v1, SemanticVersion v2) { return v1.Equals(v2); }
        public static bool operator !=(SemanticVersion v1, SemanticVersion v2) { return !v1.Equals(v2); }
        public static bool operator >=(SemanticVersion v1, SemanticVersion v2) { return v1.GreaterEquals(v2); }
        public static bool operator <=(SemanticVersion v1, SemanticVersion v2) { return v1.LessEquals(v2); }

        #region Private
        private bool GreaterEquals(SemanticVersion other)
        {
            if (this.major < other.major)
            {
                return false;
            }
            else if (this.major > other.major)
            {
                return true;
            }
            else
            {
                if (this.minor < other.minor)
                {
                    return false;
                }
                else if (this.minor > other.minor)
                {
                    return true;
                }
                else
                {
                    if (this.patch < other.patch)
                    {
                        return false;
                    }
                    else if (this.patch > other.patch)
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
            if (this.major < other.major)
            {
                return true;
            }
            else if (this.major > other.major)
            {
                return false;
            }
            else
            {
                if (this.minor < other.minor)
                {
                    return true;
                }
                else if (this.minor > other.minor)
                {
                    return false;
                }
                else
                {
                    if (this.patch < other.patch)
                    {
                        return true;
                    }
                    else if (this.patch > other.patch)
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

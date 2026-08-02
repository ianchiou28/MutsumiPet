using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MutsumiPet.Support
{
    /// The Windows stand-in for `UserDefaults`: a handful of scalar preferences the
    /// pet remembers between launches. Kept behind an interface so tests can run
    /// against an isolated in-memory store.
    public interface IPetSettings
    {
        string GetString(string key);
        double? GetDouble(string key);
        bool? GetBool(string key);
        void SetString(string key, string value);
        void SetDouble(string key, double value);
        void SetBool(string key, bool value);
    }

    public class InMemoryPetSettings : IPetSettings
    {
        protected readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);

        public string GetString(string key)
        {
            string value;
            return Values.TryGetValue(key, out value) ? value : null;
        }

        public double? GetDouble(string key)
        {
            string raw = GetString(key);
            if (string.IsNullOrEmpty(raw)) return null;
            double parsed;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return parsed;
            return null;
        }

        public bool? GetBool(string key)
        {
            string raw = GetString(key);
            if (string.IsNullOrEmpty(raw)) return null;
            if (raw == "true") return true;
            if (raw == "false") return false;
            return null;
        }

        public void SetString(string key, string value)
        {
            Values[key] = value;
            Persist();
        }

        public void SetDouble(string key, double value)
        {
            SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public void SetBool(string key, bool value)
        {
            SetString(key, value ? "true" : "false");
        }

        protected virtual void Persist()
        {
        }
    }

    /// Reads and writes `%APPDATA%\MutsumiPet\settings.ini`. Any IO failure is
    /// swallowed: a desktop pet that cannot remember its size is still a pet, and
    /// crashing on a locked profile directory would be worse.
    public sealed class FilePetSettings : InMemoryPetSettings
    {
        private readonly string path;
        private bool loading;

        public FilePetSettings() : this(DefaultPath())
        {
        }

        public FilePetSettings(string path)
        {
            this.path = path;
            Load();
        }

        public static string DefaultPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(root, "MutsumiPet"), "settings.ini");
        }

        private void Load()
        {
            loading = true;
            try
            {
                if (File.Exists(path) == false) return;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = line.Substring(0, separator).Trim();
                    if (key.Length == 0) continue;
                    Values[key] = line.Substring(separator + 1).Trim();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                loading = false;
            }
        }

        protected override void Persist()
        {
            if (loading) return;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                {
                    Directory.CreateDirectory(directory);
                }

                var builder = new StringBuilder();
                foreach (KeyValuePair<string, string> entry in Values)
                {
                    builder.Append(entry.Key).Append('=').Append(entry.Value).Append("\r\n");
                }

                string temporary = path + ".tmp";
                File.WriteAllText(temporary, builder.ToString(), Encoding.UTF8);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

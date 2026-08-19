using System;
using System.Collections.Generic;

namespace CarFight.Networking.Runtime
{
    public static class BrowserNetworkLaunchOptions
    {
        public const ushort DefaultSignalingPort = 7770;

        public static string[] Create(string absoluteUrl)
        {
            Uri page = new Uri(absoluteUrl, UriKind.Absolute);
            Dictionary<string, string> query = ParseQuery(page.Query);
            string host = Read(query, "host", page.Host);
            string port = Read(query, "port", DefaultSignalingPort.ToString());
            string runId = Read(query, "run", "browser-review");
            string name = Read(query, "name", "bravo");

            return new[]
            {
                "--client",
                "--host", host,
                "--port", port,
                "--run-id", runId,
                "--name", name,
                "--script", "interactive"
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            string trimmed = query.TrimStart('?');
            if (trimmed.Length == 0)
                return values;

            foreach (string pair in trimmed.Split('&'))
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(parts[0]);
                string value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                values[key] = value;
            }

            return values;
        }

        private static string Read(Dictionary<string, string> values, string key, string fallback)
        {
            return values.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }
    }
}

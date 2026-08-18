using System;

namespace CarFight.Networking.Runtime
{
    public enum NetworkProcessRole
    {
        Server,
        Client
    }

    public readonly struct NetworkLaunchOptions
    {
        public NetworkLaunchOptions(
            NetworkProcessRole role,
            ushort port,
            string runId,
            string host,
            string clientName,
            string script)
        {
            Role = role;
            Port = port;
            RunId = runId;
            Host = host;
            ClientName = clientName;
            Script = script;
        }

        public NetworkProcessRole Role { get; }
        public ushort Port { get; }
        public string RunId { get; }
        public string Host { get; }
        public string ClientName { get; }
        public string Script { get; }

        public static bool HasNetworkRole(string[] arguments)
        {
            return HasFlag(arguments, "--server") || HasFlag(arguments, "--client");
        }

        public static bool TryParse(
            string[] arguments,
            out NetworkLaunchOptions options,
            out string error)
        {
            options = default;
            error = string.Empty;
            bool server = HasFlag(arguments, "--server");
            bool client = HasFlag(arguments, "--client");
            if (server == client)
                return Fail("Specify exactly one of --server or --client.", out error);

            if (!TryValue(arguments, "--port", out string portText) ||
                !ushort.TryParse(portText, out ushort port) ||
                port == 0)
            {
                return Fail("--port must be an integer from 1 through 65535.", out error);
            }

            if (!TryValue(arguments, "--run-id", out string runId) || string.IsNullOrWhiteSpace(runId))
                return Fail("--run-id is required.", out error);

            if (server)
            {
                options = new NetworkLaunchOptions(
                    NetworkProcessRole.Server,
                    port,
                    runId,
                    string.Empty,
                    string.Empty,
                    string.Empty);
                return true;
            }

            if (!TryValue(arguments, "--host", out string host) || string.IsNullOrWhiteSpace(host))
                return Fail("Client --host is required.", out error);
            if (!TryValue(arguments, "--name", out string name) ||
                (name != "alpha" && name != "bravo"))
            {
                return Fail("Client --name must be alpha or bravo.", out error);
            }

            if (!TryValue(arguments, "--script", out string script) || script != "converge")
                return Fail("Checkpoint 2B supports only --script converge.", out error);

            options = new NetworkLaunchOptions(
                NetworkProcessRole.Client,
                port,
                runId,
                host,
                name,
                script);
            return true;
        }

        private static bool HasFlag(string[] arguments, string flag)
        {
            return Array.IndexOf(arguments, flag) >= 0;
        }

        private static bool TryValue(string[] arguments, string flag, out string value)
        {
            int index = Array.IndexOf(arguments, flag);
            if (index < 0 || index + 1 >= arguments.Length)
            {
                value = string.Empty;
                return false;
            }

            value = arguments[index + 1];
            return !value.StartsWith("--", StringComparison.Ordinal);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}

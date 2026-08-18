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
            string script,
            ushort networkDelayMilliseconds = 0,
            string scenario = "baseline")
        {
            Role = role;
            Port = port;
            RunId = runId;
            Host = host;
            ClientName = clientName;
            Script = script;
            NetworkDelayMilliseconds = networkDelayMilliseconds;
            Scenario = scenario;
        }

        public NetworkProcessRole Role { get; }
        public ushort Port { get; }
        public string RunId { get; }
        public string Host { get; }
        public string ClientName { get; }
        public string Script { get; }
        public ushort NetworkDelayMilliseconds { get; }
        public string Scenario { get; }

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
            string scenario = "baseline";
            if (TryValue(arguments, "--scenario", out string scenarioText))
                scenario = scenarioText;
            if (!IsSupportedScenario(scenario))
                return Fail("--scenario is not supported.", out error);

            if (server)
            {
                options = new NetworkLaunchOptions(
                    NetworkProcessRole.Server,
                    port,
                    runId,
                    string.Empty,
                    string.Empty,
                    HasFlag(arguments, "--interactive") ? "interactive" : string.Empty,
                    0,
                    scenario);
                return true;
            }

            if (!TryValue(arguments, "--host", out string host) || string.IsNullOrWhiteSpace(host))
                return Fail("Client --host is required.", out error);
            if (!TryValue(arguments, "--name", out string name) ||
                (name != "alpha" && name != "bravo"))
            {
                return Fail("Client --name must be alpha or bravo.", out error);
            }

            if (!TryValue(arguments, "--script", out string script) ||
                (script != "converge" && script != "interactive"))
            {
                return Fail("Client --script must be converge or interactive.", out error);
            }
            ushort delay = 0;
            if (TryValue(arguments, "--network-delay-ms", out string delayText) &&
                !ushort.TryParse(delayText, out delay))
            {
                return Fail("--network-delay-ms must be a non-negative integer.", out error);
            }

            options = new NetworkLaunchOptions(
                NetworkProcessRole.Client,
                port,
                runId,
                host,
                name,
                script,
                delay,
                scenario);
            return true;
        }

        private static bool HasFlag(string[] arguments, string flag)
        {
            return Array.IndexOf(arguments, flag) >= 0;
        }

        private static bool IsSupportedScenario(string value)
        {
            return value == "baseline" ||
                   value == "latency" ||
                   value == "jitter" ||
                   value == "loss" ||
                   value == "late_join" ||
                   value == "reconnect" ||
                   value == "invalid_authority" ||
                   value == "stall";
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

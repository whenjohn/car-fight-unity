using CarFight.Networking.Runtime;
using NUnit.Framework;

namespace CarFight.Tests.Networking
{
    public sealed class NetworkLaunchOptionsTests
    {
        [Test]
        public void ServerRoleRequiresPortAndRunIdentity()
        {
            bool parsed = NetworkLaunchOptions.TryParse(
                new[] { "CarFight", "--server", "--port", "9900", "--run-id", "gate2b" },
                out NetworkLaunchOptions options,
                out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(options.Role, Is.EqualTo(NetworkProcessRole.Server));
            Assert.That(options.Port, Is.EqualTo(9900));
            Assert.That(options.RunId, Is.EqualTo("gate2b"));
        }

        [Test]
        public void ClientRoleRequiresTheBaselineIdentityAndScript()
        {
            bool parsed = NetworkLaunchOptions.TryParse(
                new[]
                {
                    "CarFight", "--client", "--host", "127.0.0.1", "--port", "9900",
                    "--name", "alpha", "--script", "converge", "--run-id", "gate2b"
                },
                out NetworkLaunchOptions options,
                out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(options.Role, Is.EqualTo(NetworkProcessRole.Client));
            Assert.That(options.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(options.ClientName, Is.EqualTo("alpha"));
            Assert.That(options.Script, Is.EqualTo("converge"));
        }

        [TestCase("--server", "--client")]
        [TestCase("--client", "--name")]
        [TestCase("--client", "--script")]
        public void InvalidOrIncompleteRolesAreRejected(string role, string incompleteFlag)
        {
            string[] arguments = role == "--server"
                ? new[] { "CarFight", role, incompleteFlag, "--port", "9900", "--run-id", "gate2b" }
                : new[]
                {
                    "CarFight", role, "--host", "127.0.0.1", "--port", "9900",
                    incompleteFlag, "invalid", "--run-id", "gate2b"
                };

            Assert.That(
                NetworkLaunchOptions.TryParse(arguments, out _, out string error),
                Is.False,
                error);
        }

        [Test]
        public void SnapshotCadenceIsThirtyHertzAtTheAcceptedPhysicsRate()
        {
            int publishCount = 0;
            for (uint tick = 1; tick <= 120; tick++)
            {
                if (BaselineNetworkScenario.ShouldPublishSnapshot(tick))
                    publishCount++;
            }

            Assert.That(publishCount, Is.EqualTo(30));
            Assert.That(BaselineNetworkScenario.ShouldPublishSnapshot(3), Is.False);
            Assert.That(BaselineNetworkScenario.ShouldPublishSnapshot(4), Is.True);
        }

        [Test]
        public void LifecycleScenarioAndDelayAreParsedForBothRoles()
        {
            Assert.That(
                NetworkLaunchOptions.TryParse(
                    new[]
                    {
                        "CarFight", "--client", "--host", "127.0.0.1", "--port", "9900",
                        "--name", "bravo", "--script", "converge", "--scenario", "stall",
                        "--network-delay-ms", "120", "--run-id", "gate2e"
                    },
                    out NetworkLaunchOptions client,
                    out string clientError),
                Is.True,
                clientError);
            Assert.That(client.Scenario, Is.EqualTo("stall"));
            Assert.That(client.NetworkDelayMilliseconds, Is.EqualTo(120));

            Assert.That(
                NetworkLaunchOptions.TryParse(
                    new[]
                    {
                        "CarFight", "--server", "--port", "9900", "--scenario", "reconnect",
                        "--run-id", "gate2e"
                    },
                    out NetworkLaunchOptions server,
                    out string serverError),
                Is.True,
                serverError);
            Assert.That(server.Scenario, Is.EqualTo("reconnect"));
        }

        [Test]
        public void UnknownLifecycleScenarioIsRejected()
        {
            Assert.That(
                NetworkLaunchOptions.TryParse(
                    new[]
                    {
                        "CarFight", "--server", "--port", "9900", "--scenario", "invented",
                        "--run-id", "gate2e"
                    },
                    out _,
                    out string error),
                Is.False,
                error);
        }
    }
}

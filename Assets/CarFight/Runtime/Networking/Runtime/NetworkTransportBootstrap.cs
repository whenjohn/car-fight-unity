using System;
using System.Collections.Generic;
using System.Reflection;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using WebRtcTransport = FishNet.Transporting.FishyWebRTC.FishyWebRTC;

namespace CarFight.Networking.Runtime
{
    /// <summary>
    /// Selects FishNet transports before NetworkManager initializes. Native clients
    /// retain Tugboat while the server accepts Tugboat and browser WebRTC peers.
    /// </summary>
    public static class NetworkTransportBootstrap
    {
        private static readonly FieldInfo MultipassTransportsField =
            typeof(Multipass).GetField("_transports", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Configure(GameObject root, NetworkProcessRole role)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

#if UNITY_WEBGL && !UNITY_EDITOR
            if (role != NetworkProcessRole.Client)
                throw new InvalidOperationException("WebGL builds can only run as network clients.");
            root.AddComponent<WebRtcTransport>();
#else
            if (role == NetworkProcessRole.Server)
                ConfigureServer(root);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static void ConfigureServer(GameObject root)
        {
            if (MultipassTransportsField == null)
                throw new MissingFieldException(typeof(Multipass).FullName, "_transports");

            Multipass multipass = root.AddComponent<Multipass>();
            Tugboat tugboat = root.AddComponent<Tugboat>();
            WebRtcTransport webRtc = root.AddComponent<WebRtcTransport>();
            MultipassTransportsField.SetValue(
                multipass,
                new List<Transport> { tugboat, webRtc });
            multipass.SetClientTransport(tugboat);
        }
#endif
    }
}

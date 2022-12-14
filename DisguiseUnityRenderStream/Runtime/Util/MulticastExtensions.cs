using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Assertions;

namespace Disguise.RenderStream.Utils
{
    static class MulticastExtensions
    {
        public static void EnableMulticast(this UdpClient udpClient, IPAddress multicastIPAddress, int port, string adapterName = null, int timeoutMs = 5000)
        {
            var (selectedNic, selectedNicAddress) = SelectNetworkInterface(adapterName);
            if (selectedNic == null)
            {
                throw new IOException("There are no available network interfaces that support multicast");
            }

            Assert.IsNotNull(selectedNicAddress);
            var adapterAddress = selectedNicAddress.Address;

            udpClient.Client.SendTimeout = timeoutMs;
            udpClient.Client.ReceiveTimeout = timeoutMs;

            // Ask to send the multicast traffic using the specified adapter
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                adapterAddress.GetAddressBytes());

            // There should be only one hop between the emitter and repeater
            // Food for thought: Should we make this configurable to work on slightly more complex network infrastructures?
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

            // Allow multiple ClusterDisplay applications to bind on the same address and port. Useful for when running
            // multiple nodes locally and unit testing.
            // Food for thought: Does it have a performance cost? Do we want to have it configurable or disabled in some
            //                   cases?
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Bind to receive from the selected adapter on the same port than the port we are sending to (everyone will
            // use the same port).
            udpClient.Client.Bind(new IPEndPoint(adapterAddress, port));

            // Join the multicast group
            udpClient.JoinMulticastGroup(multicastIPAddress);

            // This is normally true by default but this is required to keep things simple (unit tests working, multiple
            // instances on the same computer, ...).  So let's get sure it is on.
            udpClient.MulticastLoopback = true;
        }

        static (NetworkInterface, UnicastIPAddressInformation) SelectNetworkInterface(string adapterName)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface firstUpNic = null;
            UnicastIPAddressInformation firstUpNicIPAddress = null;

            foreach (var nic in nics)
            {
                bool isExplicitNic = !string.IsNullOrEmpty(adapterName) && nic.Name == adapterName;
                bool isUp = nic.OperationalStatus == OperationalStatus.Up;

                if (!isUp)
                {
                    if (isExplicitNic)
                    {
                        Debug.LogError(
                            $"Unable to use explicit interface: \"{nic.Name}\", the interface is down. Attempting to use the next available interface.");
                    }

                    continue;
                }

                var ipProperties = nic.GetIPProperties();
                if (ipProperties == null || !ipProperties.MulticastAddresses.Any())
                {
                    continue;
                }

                if (!nic.SupportsMulticast)
                {
                    continue;
                }

                UnicastIPAddressInformation nicIPAddress = null;
                foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    nicIPAddress = ip;
                    break;
                }

                if (nicIPAddress == null)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(nicIPAddress.Address))
                {
                    // Skip loopback adapter as they cause all sort of problems with multicast...
                    continue;
                }

                firstUpNic ??= nic;
                firstUpNicIPAddress ??= nicIPAddress;
                if (!isExplicitNic)
                {
                    continue;
                }

                Debug.Log($"Selecting explicit interface: \"{nic.Name} with ip {nicIPAddress.Address}\".");
                return (nic, nicIPAddress);
            }

            // If we reach this point then there was no explicit nic selected, use the first up nic as the automatic one
            if (firstUpNic == null)
            {
                Debug.LogError($"There are NO available interfaces to bind cluster display to.");
                return (null, null);
            }

            Debug.Log($"No explicit interface defined, defaulting to interface: \"{firstUpNic.Name}\".");
            return (firstUpNic, firstUpNicIPAddress);
        }
    }
}

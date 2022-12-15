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
        public static void EnableMulticast(this UdpClient udpClient, IPAddress multicastIPAddress, int port, IPAddress adapterAddress, int timeoutMs = 5000)
        {
            udpClient.Client.SendTimeout = timeoutMs;
            udpClient.Client.ReceiveTimeout = timeoutMs;

            // Ask to send the multicast traffic using the specified adapter
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                adapterAddress.GetAddressBytes());

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

        public static (string name, IPAddress address) SelectNetworkInterface(string adapterName)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            NetworkInterface firstUpNic = null;
            UnicastIPAddressInformation firstUpNicIPAddress = null;

            foreach (var nic in nics)
            {
                // Check if NIC supports multicast.
                // Skip loopback adapter as they cause all sort of problems with multicast...
                if (nic.OperationalStatus is not OperationalStatus.Up ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Loopback ||
                    !nic.SupportsMulticast)
                {
                    continue;
                }

                // Check that NIC has multicast addresses
                if (nic.GetIPProperties() is not { } ipProperties || !ipProperties.MulticastAddresses.Any())
                {
                    continue;
                }

                // Check that NIC has unicast addresses
                if (ipProperties.UnicastAddresses.FirstOrDefault(ip =>
                        ip.Address.AddressFamily == AddressFamily.InterNetwork) is not
                    { } unicastAddressInfo)
                {
                    continue;
                }

                // If we have multiple candidate NICs, try to use the one with the highest reported speed.
                if (firstUpNic == null || nic.Speed > firstUpNic.Speed)
                {
                    firstUpNic = nic;
                    firstUpNicIPAddress = unicastAddressInfo;
                }

                if (!string.IsNullOrEmpty(adapterName) && nic.Name == adapterName)
                {
                    Debug.Log($"Selecting explicit interface: \"{nic.Name} with ip {unicastAddressInfo.Address}\".");
                    return (nic.Name, unicastAddressInfo.Address);
                }
            }

            // If we reach this point then there was no explicit nic selected or the explicit nic is unavailable,
            // use the first up nic as the automatic one
            if (firstUpNic == null)
            {
                throw new InvalidOperationException("There are no available interfaces for Cluster Display communication.");
            }

            if (!string.IsNullOrEmpty(adapterName))
            {
                Debug.LogError($"Unable to use explicit interface {adapterName}. Using \"{firstUpNic.Name}\" instead.");
            }
            else
            {
                Debug.Log($"No explicit interface defined, defaulting to interface: \"{firstUpNic.Name}\".");
            }
            return (firstUpNic.Name, firstUpNicIPAddress.Address);
        }
    }
}

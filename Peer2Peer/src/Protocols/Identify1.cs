﻿using Common.Logging;
using Ipfs;
using ProtoBuf;
using Semver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Peer2Peer.Protocols
{
    /// <summary>
    ///   Identifies the peer.
    /// </summary>
    public class Identify1 : IPeerProtocol
    {
        static ILog log = LogManager.GetLogger(typeof(Identify1));

        /// <inheritdoc />
        public string Name { get; } = "ipfs/id";

        /// <inheritdoc />
        public SemVersion Version { get; } = new SemVersion(1, 0);

        /// <inheritdoc />
        public override string ToString()
        {
            return $"/{Name}/{Version}";
        }

        /// <inheritdoc />
        public async Task ProcessRequestAsync(PeerConnection connection, CancellationToken cancel = default(CancellationToken))
        {
            log.Debug("Sending identity to " + connection.RemoteAddress);
            var peer = connection.LocalPeer;
            var res = new Identify
            {
                ProtocolVersion = peer.ProtocolVersion,
                AgentVersion = peer.AgentVersion,
                ListenAddresses = peer?.Addresses
                     .Select(a => a.ToArray())
                     .ToArray(),
                ObservedAddress = connection.RemoteAddress?.ToArray(),
                Protocols = null, // no longer sent
            };
            if (peer.PublicKey != null)
            {
                res.PublicKey = Convert.FromBase64String(peer.PublicKey);
            }
            ProtoBuf.Serializer.SerializeWithLengthPrefix<Identify>(connection.Stream, res, PrefixStyle.Base128);
            await connection.Stream.FlushAsync();
        }

        /// <inheritdoc />
        public Task ProcessResponseAsync(PeerConnection connection, CancellationToken cancel = default(CancellationToken))
        {
            log.Debug("Receiving identity from " + connection.RemoteAddress);
            var info = Serializer.DeserializeWithLengthPrefix<Identify>(connection.Stream, PrefixStyle.Base128);
            Peer remote = connection.RemotePeer;
            if (remote == null)
            {
                remote = new Peer();
                connection.RemotePeer = remote;
            }
            // TODO: remote.Addresses
            remote.AgentVersion = info.AgentVersion;
            remote.ProtocolVersion = info.ProtocolVersion;
            if (info.PublicKey != null)
            {
                remote.PublicKey = Convert.ToBase64String(info.PublicKey);
                if (remote.Id == null)
                {
                    remote.Id = MultiHash.ComputeHash(info.PublicKey);
                }
            }
            if (info.ListenAddresses != null)
            {
                remote.Addresses = info.ListenAddresses
                    .Select(b => new MultiAddress(b))
                    .Union(remote.Addresses)
                    .ToList();
            }

            return Task.CompletedTask;
        }

        [ProtoContract]
        class Identify
        {
            [ProtoMember(5)]
            public string ProtocolVersion;
            [ProtoMember(6)]
            public string AgentVersion;
            [ProtoMember(1)]
            public byte[] PublicKey;
            [ProtoMember(2, IsRequired = true)]
            public byte[][] ListenAddresses;
            [ProtoMember(4)]
            public byte[] ObservedAddress;
            [ProtoMember(3)]
            public string[] Protocols;
        }

    }
}
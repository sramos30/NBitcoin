﻿#if !NOSOCKET
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Socks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Protocol.Connectors
{
	public class DefaultEndpointConnector : IEnpointConnector
	{
		/// <summary>
		/// If it must connect to TOR only (default: false)
		/// </summary>
		public bool AllowOnlyTorEndpoints { get; set; } = false;

		public DefaultEndpointConnector()
		{
		}

		public DefaultEndpointConnector(bool allowOnlyTorEndpoints)
		{
			AllowOnlyTorEndpoints = allowOnlyTorEndpoints;
		}

		public IEnpointConnector Clone()
		{
			return new DefaultEndpointConnector(AllowOnlyTorEndpoints);
		}

		public async Task ConnectSocket(Socket socket, EndPoint endpoint, NodeConnectionParameters nodeConnectionParameters, CancellationToken cancellationToken)
		{
			var isTor = endpoint.IsTor();
			if(AllowOnlyTorEndpoints && !isTor)
				throw new InvalidOperationException($"The Endpoint connector is configured to allow only Tor endpoints and the '{endpoint}' enpoint is not one");
			var socksSettings = nodeConnectionParameters.TemplateBehaviors.Find<SocksSettingsBehavior>();
			bool socks = isTor || socksSettings?.OnlyForOnionHosts is false;
			if (socks && socksSettings?.SocksEndpoint == null)
				throw new InvalidOperationException("SocksSettingsBehavior.SocksEndpoint is not set but the connection is expecting using socks proxy");
			var socketEndpoint = socks ? socksSettings.SocksEndpoint : endpoint;
			if (socketEndpoint is IPEndPoint mappedv4 && mappedv4.Address.IsIPv4MappedToIPv6Ex())
				socketEndpoint = new IPEndPoint(mappedv4.Address.MapToIPv4Ex(), mappedv4.Port);
#if NETCORE
			await socket.ConnectAsync(socketEndpoint).WithCancellation(cancellationToken).ConfigureAwait(false);
#else
			await socket.ConnectAsync(socketEndpoint, cancellationToken).ConfigureAwait(false);
#endif
			if (!socks)
				return;

			await SocksHelper.Handshake(socket, endpoint, cancellationToken);
		}
	}
}
#endif
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Http;
using System.Threading.Tasks;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Hosting;

namespace EventStore.Core {
	// Not everywhere that consumed ClusterVNode needs to know about TStreamId
	public interface IClusterVNode {
		IQueuedHandler MainQueue { get; }
		ISubscriber MainBus { get; }
		IReadIndex ReadIndex { get; }
		QueueStatsManager QueueStatsManager { get; }
		IStartup Startup { get; }
		IAuthenticationProvider AuthenticationProvider { get; }
		AuthorizationGateway AuthorizationGateway { get; }
		IHttpService HttpService { get; }

		Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> InternalClientCertificateValidator { get; }
		Func<X509Certificate2> CertificateSelector { get; }
		bool DisableHttps { get; }

		void Start();
		Task<IClusterVNode> StartAsync(bool waitUntilRead);
		Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
	}
}

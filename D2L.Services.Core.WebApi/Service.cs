﻿using System;
using System.Net;
using System.Web.Http;
using System.Web.Http.Dependencies;
using D2L.Services.Core.Activation;
using D2L.Services.Core.Configuration;
using D2L.Services.Core.WebApi.Auth;
using Microsoft.Owin.Hosting;
using Owin;
using SimpleLogInterface;

namespace D2L.Services.Core.WebApi {
	public sealed class Service : IService {
		private readonly string m_url;
		private readonly ServiceDescriptor m_descriptor;
		private readonly IConfigViewer m_configViewer;
		private readonly ILogProvider m_logProvider;
		private readonly IDependencyResolver m_dependencyResolver;
		private readonly Action<HttpConfiguration> m_startup;

		private IDisposable m_disposeHandle;

		public Service(
			string url,
			ServiceDescriptor descriptor,
			IConfigViewer configViewer,
			ILogProvider logProvider,
			Action<HttpConfiguration> startup,
			Type dependencyLoaderType
		) {
			m_url = url;
			m_descriptor = descriptor;
			m_configViewer = configViewer;
			m_logProvider = logProvider;
			m_startup = startup;

			m_dependencyResolver = DependencyResolverFactory
				.CreateFrom( new CoreDependencyLoader(
					m_configViewer,
					m_logProvider,
					dependencyLoaderType
				)
			);
		}

		ServiceDescriptor IService.Descriptor {
			get { return m_descriptor; }
		}

		void IService.Start() {
			IService @this = this;

			var options = new StartOptions();

			options.Urls.Add( m_url );

			m_disposeHandle = WebApp.Start( options, OwinStartup );
		}

		void IDisposable.Dispose() {
			m_disposeHandle.SafeDispose();
		}

		public void OwinStartup( IAppBuilder builder ) {
			ConfigureHttps();

			var config = new HttpConfiguration {
				DependencyResolver = m_dependencyResolver
			};

			var authConfigurator = m_dependencyResolver
				.Get<IWebApiAuthConfigurator>();

			authConfigurator.Configure( config );

			m_startup( config );

			config.EnsureInitialized();

			builder.UseWebApi( config );
		}

		private static void ConfigureHttps() {
			// Work around shitty defaults in .NET 4.5, but be careful to not enable
			// things that might be "bad" in the future by narrowly looking for the
			// .NET 4.5 default settings.

			// This impacts *outgoing* requests from this service (e.g. HttpClient.)

			const SecurityProtocolType NET_4_5_DEFAULT_SETTING =
				SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;

			if( ServicePointManager.SecurityProtocol == NET_4_5_DEFAULT_SETTING ) {
				ServicePointManager.SecurityProtocol |=
					SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			}
		}
	}
}

﻿using System.Net;

namespace Titanium.Web.Proxy.Models
{
	/// <summary>
	/// A proxy end point client is not aware of 
	/// Usefull when requests are redirected to this proxy end point through port forwarding 
	/// </summary>
	public class TransparentProxyEndPoint : ProxyEndPoint
	{
		/// <summary>
		/// Gets or sets the name of the generic certificate.
		/// Name of the Certificate need to be sent (same as the hostname we want to proxy)
		/// This is valid only when UseServerNameIndication is set to false
		/// </summary>
		public string GenericCertificateName { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TransparentProxyEndPoint"/> class.
		/// </summary>
		/// <param name="ipAddress">The ip address.</param>
		/// <param name="port">The port.</param>
		/// <param name="enableSsl">if set to <c>true</c> [enable SSL].</param>
		public TransparentProxyEndPoint(IPAddress ipAddress, int port, bool enableSsl)
			: base(ipAddress, port, enableSsl)
		{
			GenericCertificateName = "localhost";
		}
	}
}
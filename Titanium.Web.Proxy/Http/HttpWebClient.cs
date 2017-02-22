using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Helpers;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Network;
using Titanium.Web.Proxy.Shared;

namespace Titanium.Web.Proxy.Http
{
	/// <summary>
	/// Used to communicate with the server over HTTP(S)
	/// </summary>
	public class HttpWebClient
	{
		/// <summary>
		/// Connection to server
		/// </summary>
		internal TcpConnection ServerConnection { get; set; }

		/// <summary>
		/// Gets the request identifier.
		/// </summary>
		public Guid RequestId { get; private set; }

		/// <summary>
		/// Gets or sets the connect headers.
		/// </summary>
		public List<HttpHeader> ConnectHeaders { get; set; }

		/// <summary>
		/// Gets or sets the request.
		/// </summary>
		public Request Request { get; set; }

		/// <summary>
		/// Gets or sets the response.
		/// </summary>
		public Response Response { get; set; }

		/// <summary>
		/// PID of the process that is created the current session when client is running in this machine
		/// If client is remote then this will return 
		/// </summary>
		public Lazy<int> ProcessId { get; internal set; }

		/// <summary>
		/// Is Https?
		/// </summary>
		public bool IsHttps => Request.RequestUri.Scheme == Uri.UriSchemeHttps;

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpWebClient"/> class.
		/// </summary>
		internal HttpWebClient()
		{
			RequestId = Guid.NewGuid();

			Request = new Request();
			Response = new Response();
		}

		/// <summary>
		/// Set the tcp connection to server used by this webclient
		/// </summary>
		/// <param name="connection">Instance of <see cref="TcpConnection"/></param>
		internal void SetConnection(TcpConnection connection)
		{
			connection.LastAccess = DateTime.Now;
			ServerConnection = connection;
		}

		/// <summary>
		/// Sends the request.
		/// </summary>
		/// <param name="enable100ContinueBehaviour">if set to <c>true</c> [enable100 continue behaviour].</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal async Task SendRequest(bool enable100ContinueBehaviour,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var stream = ServerConnection.Stream;

			var requestLines = new StringBuilder();

			//prepare the request & headers
			if ((ServerConnection.UpstreamHttpProxy != null && ServerConnection.IsHttps == false) ||
				(ServerConnection.UpstreamHttpsProxy != null && ServerConnection.IsHttps))
			{
				requestLines.Append($"{Request.Method} {Request.RequestUri.AbsoluteUri} HTTP/{Request.HttpVersion.Major}.{Request.HttpVersion.Minor}{ProxyConstants.CoreNewLine}");
			}
			else
			{
				requestLines.Append($"{Request.Method} {Request.RequestUri.PathAndQuery} HTTP/{Request.HttpVersion.Major}.{Request.HttpVersion.Minor}{ProxyConstants.CoreNewLine}");
			}

			//write request headers
			foreach (var headerItem in Request.RequestHeaders)
			{
				var header = headerItem.Value;
				requestLines.Append($"{header.Name}: {header.Value}{ProxyConstants.CoreNewLine}");
			}

			//write non unique request headers
			foreach (var headerItem in Request.NonUniqueRequestHeaders)
			{
				var headers = headerItem.Value;
				foreach (var header in headers)
				{
					requestLines.Append($"{header.Name}: {header.Value}{ProxyConstants.CoreNewLine}");
				}
			}

			requestLines.Append(ProxyConstants.CoreNewLine);

			var request = requestLines.ToString();
			var requestBytes = ProxyConstants.DefaultEncoding.GetBytes(request);

			await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken: cancellationToken);
			await stream.FlushAsync(cancellationToken: cancellationToken);

			if (enable100ContinueBehaviour)
			{
				if (Request.ExpectContinue)
				{
					var httpResult = (await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken));
					var httpResponseHead = HttpResponseHeadParser.Parse(httpResult);

					//find if server is willing for expect continue
					if (httpResponseHead.StatusCode == 100
						&& httpResponseHead.StatusDescription.Equals("continue", StringComparison.InvariantCultureIgnoreCase))
					{
						Request.Is100Continue = true;
						await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken);
					}
					else if (httpResponseHead.StatusCode == 417
							 &&
							 httpResponseHead.StatusDescription.Equals("expectation failed",
								 StringComparison.InvariantCultureIgnoreCase))
					{
						Request.ExpectationFailed = true;
						await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken);
					}
				}
			}
		}

		/// <summary>
		/// Receives the response.
		/// </summary>
		/// <param name="isReplayedRequest">if set to <c>true</c> [is replayed request].</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		internal async Task ReceiveResponse(bool isReplayedRequest = false,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			try
			{
				//return if this is already read
				if (Response.ResponseStatusCode != 0 && !isReplayedRequest)
				{
					return;
				}

				//var a = ServerConnection.StreamReader.BaseStream.ReadByte();
				var httpCommand = await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken);
				var httpResponseHead = HttpResponseHeadParser.Parse(httpCommand);

				if (httpResponseHead.StatusCode == 0)
				{
					//Empty content in first-line, try again
					httpCommand = await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken);
					httpResponseHead = HttpResponseHeadParser.Parse(httpCommand);
				}

				Response.HttpVersion = httpResponseHead.Version;
				Response.ResponseStatusCode = httpResponseHead.StatusCode;
				Response.ResponseStatusDescription = httpResponseHead.StatusDescription;

				// For HTTP 1.1 comptibility server may send expect-continue even if not asked for it in request
				if (Response.ResponseStatusCode == 100
					&& Response.ResponseStatusDescription.ToLower().Equals("continue"))
				{
					// Read the next line after 100-continue 
					Response.Is100Continue = true;
					Response.ResponseStatusCode = 0;
					await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken);

					// Now receive response
					await ReceiveResponse(cancellationToken: cancellationToken);
					return;
				}

				if (Response.ResponseStatusCode == 417
					&& Response.ResponseStatusDescription.ToLower().Equals("expectation failed"))
				{
					//read next line after expectation failed response
					Response.ExpectationFailed = true;
					Response.ResponseStatusCode = 0;
					await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken);
					//now receive response 
					await ReceiveResponse(cancellationToken: cancellationToken);
					return;
				}

				//Read the Response headers
				//Read the response headers in to unique and non-unique header collections
				string tmpLine;
				while (
					!string.IsNullOrEmpty(
						tmpLine = await ServerConnection.StreamReader.ReadLineAsync(cancellationToken: cancellationToken)))
				{
					var header = tmpLine.Split(ProxyConstants.ColonSplit, 2);

					var newHeader = new HttpHeader(header[0], header[1]);

					//if header exist in non-unique header collection add it there
					if (Response.NonUniqueResponseHeaders.ContainsKey(newHeader.Name))
					{
						Response.NonUniqueResponseHeaders[newHeader.Name].Add(newHeader);
					}
					//if header is alread in unique header collection then move both to non-unique collection
					else if (Response.ResponseHeaders.ContainsKey(newHeader.Name))
					{
						var existing = Response.ResponseHeaders[newHeader.Name];

						var nonUniqueHeaders = new List<HttpHeader> { existing, newHeader };

						Response.NonUniqueResponseHeaders.Add(newHeader.Name, nonUniqueHeaders);
						Response.ResponseHeaders.Remove(newHeader.Name);
					}
					//add to unique header collection
					else
					{
						Response.ResponseHeaders.Add(newHeader.Name, newHeader);
					}
				}
			}
			catch (Exception ex)
			{

			}
		}
	}
}
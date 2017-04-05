﻿using NUnit.Framework;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.UnitTests.Helpers;

namespace Titanium.Web.Proxy.UnitTests.Models
{
	[TestFixture]
	public class HttpHeaderCollectionFixture
	{
		[Test]
		public void Add_should_handle_null_input()
		{
			var headerCollection = new HttpHeaderCollection { null };

			Assert.Zero(headerCollection.Count);
		}

		[Test]
		public void Should_initialize_correctly()
		{
			var fakeHeader = UnitTestHelpers.GetFakeObject<HttpHeader>("test", "test");

			var headerCollection = new HttpHeaderCollection { fakeHeader };

			Assert.AreEqual(headerCollection[fakeHeader.Name], fakeHeader);
		}


	}
}
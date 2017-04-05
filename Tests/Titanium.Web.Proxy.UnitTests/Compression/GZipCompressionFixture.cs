﻿using System;
using System.Collections;
using NUnit.Framework;
using Titanium.Web.Proxy.Compression;
using Titanium.Web.Proxy.UnitTests.Helpers;

namespace Titanium.Web.Proxy.UnitTests.Compression
{
	[TestFixture]
	public class GZipCompressionFixture
	{
		[TestCaseSource(typeof(GZipCompressionTestCaseSource), nameof(GZipCompressionTestCaseSource.CompressTestCases))]
		public GZipCompressedByteArray Compress_works_properly(byte[] data)
		{
			var compression = new GZipCompression();
			var result = compression.Compress(data).Result?.ToArray();

			return result != null ? new GZipCompressedByteArray(result) : null;
		}

		private class GZipCompressionTestCaseSource
		{
			public static IEnumerable CompressTestCases
			{
				get
				{
					yield return new TestCaseData(null)
						.Returns(null)
						.SetName("Handles null input");

					yield return new TestCaseData(Array.Empty<byte>())
						.Returns(GZipCompressedByteArray.Empty)
						.SetName("Handles empty input");

					yield return new TestCaseData(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })
						.Returns(new GZipCompressedByteArray(31, 139, 8, 0, 67, 166, 98, 88, 0, 255, 99, 96, 128, 0, 0, 105, 223, 34, 101, 8, 0, 0, 0))
						.SetName("Handles zero vector");
				}
			}
		}
	}
}
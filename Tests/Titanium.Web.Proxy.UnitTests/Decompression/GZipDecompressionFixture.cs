﻿using System;
using System.Collections;
using NUnit.Framework;
using Titanium.Web.Proxy.Decompression;
using UnitTests.Titanium.Web.Proxy.Helpers;

namespace Titanium.Web.Proxy.UnitTests.Decompression
{
    [TestFixture]
    public class GZipDecompressionFixture
    {
        private const int BufferSize = 16;

        [TestCaseSource(typeof(GZipDecompressionTestCaseSource), nameof(GZipDecompressionTestCaseSource.DecompressTestCases))]
        public byte[] Decompress_works_properly(GZipCompressedByteArray compressedData, int bufferSize)
        {
            var decompression = new GZipDecompression();

            return decompression.Decompress(compressedData?.Value, bufferSize).Result;
        }

        private class GZipDecompressionTestCaseSource
        {
            public static IEnumerable DecompressTestCases
            {
                get
                {
                    yield return new TestCaseData(null, BufferSize)
                        .Returns(null)
                        .SetName("Handles null input");

                    yield return new TestCaseData(GZipCompressedByteArray.Empty, BufferSize)
                        .Returns(Array.Empty<byte>())
                        .SetName("Handles empty input");

                    yield return new TestCaseData(new GZipCompressedByteArray(0, 0, 0, 0), 0)
                        .Returns(null)
                        .SetName("Handles invalid buffer size");

                    yield return new TestCaseData(new GZipCompressedByteArray(31, 139, 8, 0, 67, 166, 98, 88, 0, 255, 99, 96, 128, 0, 0, 105, 223, 34, 101, 8, 0, 0, 0), BufferSize)
                        .Returns(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })
                        .SetName("Decompresses compressed zero vector properly");
                }
            }
        }
    }
}
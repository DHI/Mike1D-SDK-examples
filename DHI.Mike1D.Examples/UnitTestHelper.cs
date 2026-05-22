using System;
using System.IO;
using NUnit.Framework;

namespace DHI.Mike1D.Examples
{
    public class UnitTestHelper
    {

        /// <summary>
        /// Relative path to test data. Must end with a \
        /// </summary>
        public static string TestDataRootRelative = @"..\..\..\data\";

        /// <summary>
        /// Path to test data
        /// </summary>
        public static string TestDataRoot => new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, TestDataRootRelative)).LocalPath;
    }
}
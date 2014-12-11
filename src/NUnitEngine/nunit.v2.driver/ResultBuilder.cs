// ***********************************************************************
// Copyright (c) 2014 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Xml;
using NUnit.Core;

namespace NUnit.Engine.Drivers
{
    public static class ResultBuilder
    {
        #region Public Methods

        /// <summary>
        /// Returns an XML string representing a test
        /// </summary>
        public static string From(Core.ITest test, bool recursive)
        {
            var output = new StringBuilder();
            var settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;

            using (var writer = XmlWriter.Create(output, settings))
            {
                WriteTestElement(writer, test, recursive);
            }

            return output.ToString();
        }

        /// <summary>
        /// Returns an XML string representing a TestResult
        /// </summary>
        public static string From(Core.TestResult result, bool recursive)
        {
            var output = new StringBuilder();
            var settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;

            using (var writer = XmlWriter.Create(output, settings))
            {
                WriteTestResultElement(writer, result, true);
            }

            return output.ToString();
        }

        #endregion

        #region Write Test Elements

        // Writes a complete test-suite or test-case element, without result info
        private static void WriteTestElement(XmlWriter writer, ITest test, bool recursive)
        {
            writer.WriteStartElement(test.IsSuite ? "test-suite" : "test-case");

            WriteTestAttributes(writer, test);
            WritePropertiesElement(writer, test);

            if (recursive && test.IsSuite)
                foreach (ITest child in test.Tests)
                    WriteTestElement(writer, child, recursive);

            writer.WriteEndElement();
        }

        // Writes all the attributes that are common to both tests and testresults.
        // NOTE: The XmlWriter requires all attributes to be written before any content
        private static void WriteTestAttributes(XmlWriter writer, ITest test)
        {
            if (test.IsSuite)
                writer.WriteAttributeString("type", test.TestType);
            writer.WriteAttributeString("id", test.TestName.TestID.ToString());
            writer.WriteAttributeString("name", Escape(test.TestName.Name));
            writer.WriteAttributeString("fullname", Escape(test.TestName.FullName));
            writer.WriteAttributeString("runstate", test.RunState.ToString());

            if (test.IsSuite)
                writer.WriteAttributeString("testcasecount", test.TestCount.ToString());
        }

        //Writes a properties element and its contents, provided any properties exist
        private static void WritePropertiesElement(XmlWriter writer, Core.ITest test)
        {
            if (test.Properties.Keys.Count > 0)
            {
                writer.WriteStartElement("properties");

                foreach (string key in test.Properties.Keys)
                {
                    object propValue = test.Properties[key];

                    // NOTE: This depends on categories being the only multi-valued 
                    // property. We can count on this because NUnit V2 is not 
                    // being developed any further.
                    if (key == "_CATEGORIES")
                        foreach (string category in (IList)propValue)
                            WritePropertyElement(writer, key, category);
                    else
                        WritePropertyElement(writer, key, propValue);
                }

                writer.WriteEndElement();
            }
        }

        // Writes a property element and its contents
        private static void WritePropertyElement(XmlWriter writer, string key, object val)
        {
            writer.WriteStartElement("property");
            writer.WriteAttributeString("name", key);
            writer.WriteAttributeString("value", val.ToString());
            writer.WriteEndElement();
        }

        #endregion

        #region Write TestResult Elements

        // Writes a complete test-suite or test-case element, with result info
        private static void WriteTestResultElement(XmlWriter writer, Core.TestResult result, bool recursive)
        {
            writer.WriteStartElement(result.Test.IsSuite ? "test-suite" : "test-case");

            WriteTestResultAttributes(writer, result);
            WritePropertiesElement(writer, result.Test);

            switch (result.ResultState)
            {
                case ResultState.Failure:
                case ResultState.Error:
                case ResultState.Cancelled:
                case ResultState.NotRunnable:
                    WriteFailureElement(writer, result);
                    break;
                case ResultState.Skipped:
                case ResultState.Ignored:
                    WriteReasonElement(writer, result);
                    break;
                case ResultState.Success:
                case ResultState.Inconclusive:
                    // Write Message if present
                    break;
            }
            //FailureElement
            //ReasonElement
            //OutputElement not available

            if (recursive && result.HasResults)
                foreach (TestResult child in result.Results)
                    WriteTestResultElement(writer, child, recursive);

            writer.WriteEndElement();
        }

        // Write all the attributes used for a TestResult
        private static void WriteTestResultAttributes(XmlWriter writer, TestResult result)
        {
            WriteTestAttributes(writer, result.Test);
            WriteResultStateAttributes(writer, result);

            //start-time not available
            //end-time not available
            writer.WriteAttributeString("duration", result.Time.ToString("0.000000", NumberFormatInfo.InvariantInfo));
            if (result.Test.IsSuite)
            {
                // TODO: These values all should be calculated
                writer.WriteAttributeString("total", result.Test.TestCount.ToString());
                writer.WriteAttributeString("passed", "0");
                writer.WriteAttributeString("failed", "0");
                writer.WriteAttributeString("inconclusive", "0");
                writer.WriteAttributeString("skipped", "0");
            }
            writer.WriteAttributeString("asserts", result.AssertCount.ToString());
        }

        // Writes V3 attributes corresponding to a V2 ResultState and FailureSite
        private static void WriteResultStateAttributes(XmlWriter writer, Core.TestResult v2Result)
        {
            switch (v2Result.ResultState)
            {
                case ResultState.Inconclusive:
                    writer.WriteAttributeString("result", "Inconclusive");
                    break;
                case ResultState.NotRunnable:
                    writer.WriteAttributeString("result", "Failed");
                    writer.WriteAttributeString("label", "Invalid");
                    break;
                case ResultState.Skipped:
                    writer.WriteAttributeString("result", "Skipped");
                    break;
                case ResultState.Ignored:
                    writer.WriteAttributeString("result", "Skipped");
                    writer.WriteAttributeString("label", "Ignored");
                    break;
                case ResultState.Success:
                    writer.WriteAttributeString("result", "Passed");
                    break;
                case ResultState.Failure:
                    writer.WriteAttributeString("result", "Failed");
                    break;
                case ResultState.Error:
                    writer.WriteAttributeString("result", "Failed");
                    writer.WriteAttributeString("label", "Error");
                    break;
                case ResultState.Cancelled:
                    writer.WriteAttributeString("result", "Failed");
                    writer.WriteAttributeString("label", "Cancelled");
                    break;
            }

            if (v2Result.FailureSite != FailureSite.Test)
                writer.WriteAttributeString("site", v2Result.FailureSite.ToString());
        }

        private static void WriteFailureElement(XmlWriter writer, TestResult result)
        {
            writer.WriteStartElement("failure");

            if (result.Message != null)
            {
                writer.WriteStartElement("message");
                writer.WriteCData(result.Message);
                writer.WriteEndElement();
            }

            if (result.StackTrace != null)
            {
                writer.WriteStartElement("stack-trace");
                writer.WriteCData(result.StackTrace);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private static void WriteReasonElement(XmlWriter writer, TestResult result)
        {
            writer.WriteStartElement("reason");

            writer.WriteStartElement("message");
            writer.WriteCData(result.Message);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        #endregion

        #region Helper Methods

        // Escapes a string for use in the XML
        private static string Escape(string original)
        {
            return original
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        #endregion
    }
}

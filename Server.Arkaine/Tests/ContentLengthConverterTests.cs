using NUnit.Framework;
using Server.Arkaine.B2;
using System.Text;
using System.Text.Json;

namespace Server.Arkaine.Tests
{
    public class ContentLengthConverterTests
    {
        [TestCase("\"20738\"", "20 KB", TestName = "01")]
        [TestCase("\"12177850\"", "11 MB", TestName = "02")]
        [TestCase("\"1\"", "1 B", TestName = "03")]
        public void Converter_Returns_Length(string input, string expected)
        {
            var converter = new ContentLengthCoverter();
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(input));
            reader.Read();
            var result = converter.Read(ref reader, typeof(string), new JsonSerializerOptions());
            Assert.AreEqual(expected, result);
        }
    }
}
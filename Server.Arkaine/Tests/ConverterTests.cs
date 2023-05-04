using NUnit.Framework;
using Server.Arkaine.Converter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Arkaine.Tests
{
    public class ConverterTests
    {
        [TestCase]
        public async Task TestGenerateThumbnail()
        {
            var converter = new VideoConverter();
            await converter.ExtractThumbnail();
        }
    }
}

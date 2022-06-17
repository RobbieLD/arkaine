using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class ContentLengthCoverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            long length = reader.GetInt64();
            return ToLargestUnit(length);
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }

        private string ToLargestUnit(long length) => length switch
        {
            < 1024 => $"{length} B",
            (>= 1024) and (< 1048576) => $"{length / 1024} KB",
            (>= 1048576) and (< 1073741824) => $"{length / 1048576} MB",
            >= 1073741824 => $"{length / 1073741824} GB"

        };
    }
}

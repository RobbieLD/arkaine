﻿using System.Text.Json.Serialization;

namespace Server.Arkaine.B2
{
    public class B2File
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = string.Empty;

        [JsonPropertyName("contentLength")]
        [JsonConverter(typeof(ContentLengthCoverter))]
        public string Size { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("fileId")]
        public string Id { get; set; } = string.Empty;
    }
}

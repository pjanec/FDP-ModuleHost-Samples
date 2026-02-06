using System;
using System.IO;
using System.Text.Json;

namespace Fdp.Examples.NetworkDemo.Configuration
{
    public static class MetadataManager
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static void Save(string filePath, RecordingMetadata metadata)
        {
            var json = JsonSerializer.Serialize(metadata, _options);
            File.WriteAllText(filePath, json);
        }

        public static RecordingMetadata Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Metadata file not found", filePath);

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<RecordingMetadata>(json, _options) ?? new RecordingMetadata();
        }
    }
}

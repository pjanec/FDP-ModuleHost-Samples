using System;
using System.IO;
using Fdp.Examples.NetworkDemo.Configuration;
using Xunit;

namespace Fdp.Examples.NetworkDemo.Tests
{
    public class MetadataTests
    {
        [Fact]
        public void Metadata_SaveLoad_PreservesData()
        {
            var meta = new RecordingMetadata
            {
                MaxEntityId = 12345,
                Timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                NodeId = 10
            };

            var tempFile = Path.GetTempFileName();
            
            try 
            {
                MetadataManager.Save(tempFile, meta);
                
                var loaded = MetadataManager.Load(tempFile);
                
                Assert.Equal(meta.MaxEntityId, loaded.MaxEntityId);
                Assert.Equal(meta.Timestamp, loaded.Timestamp);
                Assert.Equal(meta.NodeId, loaded.NodeId);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}

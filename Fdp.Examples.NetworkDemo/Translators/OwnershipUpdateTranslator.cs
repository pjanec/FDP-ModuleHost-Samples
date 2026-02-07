using System;
using Fdp.Kernel;
using Fdp.Interfaces;
using ModuleHost.Core.Abstractions;
using FDP.Toolkit.Replication.Components;
using ToolkitMsgs = FDP.Toolkit.Replication.Messages;
using TopicMsgs = ModuleHost.Network.Cyclone.Topics;
using ModuleHost.Core.Network; // For OwnershipExtensions
using IDescriptorTranslator = Fdp.Interfaces.IDescriptorTranslator;
using IDataReader = Fdp.Interfaces.IDataReader;
using IDataWriter = Fdp.Interfaces.IDataWriter;
using FDP.Kernel.Logging;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone; // For NetworkAppId

namespace Fdp.Examples.NetworkDemo.Translators
{
    public class OwnershipUpdateTranslator : IDescriptorTranslator
    {
        private readonly NodeIdMapper _nodeMapper;

        public string TopicName => "OwnershipUpdate";
        public long DescriptorOrdinal => -1; // Not a numbered descriptor
        
        public OwnershipUpdateTranslator(NodeIdMapper nodeMapper)
        {
            _nodeMapper = nodeMapper;
        }
        
        // This Translator handles the "OwnershipUpdate" topic.
        // It bridges the FDP EventBus (OwnershipUpdate event) <-> DDS (OwnershipUpdate topic).

        public void ApplyToEntity(Entity entity, object data, EntityRepository repo) 
        { 
             // Not used for Event-based translation.
        }

        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            // 1. Consume Events from View (Egress Step 1)
            // Use ConsumeEvents for unmanaged structs (ToolkitMsgs.OwnershipUpdate is struct)
            var toolkitEvents = view.ConsumeEvents<ToolkitMsgs.OwnershipUpdate>();
            
            foreach (var evt in toolkitEvents)
            {
                // 2. Map to Topic Struct
                var (typeId, instanceId) = OwnershipExtensions.UnpackKey(evt.PackedKey);
                
                // TRANSLATION: Internal ID -> External (Global) ID
                // The network message must carry the global ID (e.g. 200) so the receiver can map it correctly.
                int newOwnerGlobalId = -1;
                try 
                {
                    var extId = _nodeMapper.GetExternalId(evt.NewOwnerNodeId);
                    newOwnerGlobalId = extId.AppInstanceId;
                }
                catch (Exception ex)
                {
                    FdpLog<OwnershipUpdateTranslator>.Error($"Failed to map Internal ID {evt.NewOwnerNodeId} to External ID: {ex.Message}");
                    continue; 
                }

                var topicMsg = new TopicMsgs.OwnershipUpdate
                {
                    EntityId = evt.NetworkId.Value,
                    DescrTypeId = typeId,
                    InstanceId = instanceId,
                    NewOwner = newOwnerGlobalId
                };
                
                // 3. Write
                writer.Write(topicMsg);
                
                FdpLog<OwnershipUpdateTranslator>.Trace($"[OwnershipUpdateTranslator] Sending Update: Ent {evt.NetworkId.Value} Key {evt.PackedKey} -> Owner Internal:{evt.NewOwnerNodeId}/Global:{newOwnerGlobalId}");
            }
        }

        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            if (view is not EntityRepository repo) return; // Need Repo for Bus

            var samples = reader.TakeSamples();
            foreach (var sample in samples)
            {
                if (sample.Data is TopicMsgs.OwnershipUpdate topicMsg)
                {
                    // 1. Translation: External (Global) ID -> Internal ID
                    // The message contains Global ID (e.g. 200). We need Internal ID (e.g. 2).
                    // We use GetOrRegister so if we haven't seen this node before (unlikely for statically configured ones), we assign a new ID.
                    int internalOwnerId = _nodeMapper.GetOrRegisterInternalId(new TopicMsgs.NetworkAppId { AppDomainId = 0, AppInstanceId = topicMsg.NewOwner });

                    long packedKey = OwnershipExtensions.PackKey(topicMsg.DescrTypeId, topicMsg.InstanceId);
                    
                    var toolkitMsg = new ToolkitMsgs.OwnershipUpdate
                    {
                        NetworkId = new NetworkIdentity { Value = topicMsg.EntityId },
                        PackedKey = packedKey,
                        NewOwnerNodeId = internalOwnerId
                    };
                    
                    // 2. Inject into EventBus
                    repo.Bus.Publish(toolkitMsg);
                    
                    FdpLog<OwnershipUpdateTranslator>.Trace($"[OwnershipUpdateTranslator] Received Update: Ent {topicMsg.EntityId} -> Owner Global:{topicMsg.NewOwner}/Internal:{internalOwnerId}");
                }
            }
        }
    }
}

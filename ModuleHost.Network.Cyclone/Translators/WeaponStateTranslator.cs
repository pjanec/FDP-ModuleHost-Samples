using System;
using System.Collections.Generic;
using Fdp.Kernel;
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Network.Cyclone.Topics; // Fixed import

namespace ModuleHost.Network.Cyclone.Translators
{
    public class WeaponStateTranslator : IDescriptorTranslator
    {
        public string TopicName => "SST.WeaponState";
        
        private readonly Dictionary<long, Entity> _networkIdToEntity;
        private readonly int _localNodeId;
        
        public WeaponStateTranslator(
            int localNodeId,
            Dictionary<long, Entity> networkIdToEntity)
        {
            _localNodeId = localNodeId;
            _networkIdToEntity = networkIdToEntity ?? throw new ArgumentNullException(nameof(networkIdToEntity));
        }
        
        public void PollIngress(IDataReader reader, IEntityCommandBuffer cmd, ISimulationView view)
        {
            // Cache components locally for batch processing in this frame
            var batchCache = new Dictionary<Entity, WeaponStates>();

            foreach (var sample in reader.TakeSamples())
            {
                if (sample.Data is not WeaponStateDescriptor desc)
                    continue;
                
                if (!_networkIdToEntity.TryGetValue(desc.EntityId, out var entity))
                    continue; // Entity doesn't exist yet
                
                // Get or create WeaponStates component
                WeaponStates weaponStates;
                
                // Check local batch cache first
                if (batchCache.TryGetValue(entity, out var cachedStates))
                {
                    weaponStates = cachedStates;
                }
                else if (view.HasManagedComponent<WeaponStates>(entity))
                {
                    weaponStates = view.GetManagedComponentRO<WeaponStates>(entity);
                    batchCache[entity] = weaponStates;
                }
                else
                {
                    weaponStates = new WeaponStates(); 
                    // We need to set it later, cache it for now
                    batchCache[entity] = weaponStates;
                }
                
                var newState = new WeaponState
                {
                    AzimuthAngle = desc.AzimuthAngle,
                    ElevationAngle = desc.ElevationAngle,
                    AmmoCount = desc.AmmoCount,
                    Status = (ModuleHost.Core.Network.Messages.WeaponStatus)desc.Status
                };
                
                weaponStates.Weapons[desc.InstanceId] = newState;
            }
            
            // Apply all updates
            foreach (var kvp in batchCache)
            {
                cmd.SetManagedComponent(kvp.Key, kvp.Value);
            }
        }
        
        public void ScanAndPublish(ISimulationView view, IDataWriter writer)
        {
            foreach (var entity in _networkIdToEntity.Values)
            {
                if (!view.HasManagedComponent<WeaponStates>(entity))
                    continue;

                var weaponStates = view.GetManagedComponentRO<WeaponStates>(entity);
                if (weaponStates?.Weapons == null)
                    continue;

                // Determine base ownership
                int ownerId = 0;
                if (view.HasComponent<NetworkOwnership>(entity))
                {
                    var netOwn = view.GetComponentRO<NetworkOwnership>(entity);
                    ownerId = netOwn.PrimaryOwnerId;
                }

                // Check for partial ownership map
                DescriptorOwnership descOwn = null;
                if (view.HasManagedComponent<DescriptorOwnership>(entity))
                {
                    descOwn = view.GetManagedComponentRO<DescriptorOwnership>(entity);
                }

                // Check Identity to get EntityId
                long entityId = 0;
                if (view.HasComponent<NetworkIdentity>(entity))
                {
                    entityId = view.GetComponentRO<NetworkIdentity>(entity).Value;
                }
                else
                {
                    continue; // Should not happen for network entities
                }

                foreach (var kvp in weaponStates.Weapons)
                {
                    long instanceId = kvp.Key;
                    var state = kvp.Value;

                    // Determine ownership for this specific instance
                    int instanceOwner = ownerId;
                    
                    if (descOwn != null)
                    {
                        long packing = OwnershipExtensions.PackKey(NetworkConstants.WEAPON_STATE_DESCRIPTOR_ID, instanceId);
                        if (descOwn.Map.TryGetValue(packing, out int specificOwner))
                        {
                            instanceOwner = specificOwner;
                        }
                    }

                    if (instanceOwner == _localNodeId)
                    {
                        // Publish
                        var desc = new WeaponStateDescriptor
                        {
                            EntityId = entityId,
                            InstanceId = instanceId,
                            AzimuthAngle = state.AzimuthAngle,
                            ElevationAngle = state.ElevationAngle,
                            AmmoCount = state.AmmoCount,
                            Status = (Topics.WeaponStatus)state.Status
                        };
                        writer.Write(desc);
                    }
                }
            }
        }
    }
}

using System;
using CycloneDDS.Runtime;
using Fdp.Kernel;
// using Fdp.Interfaces; // Removed to avoid ambiguity
using ModuleHost.Core.Abstractions;
using ModuleHost.Core.Network;
using ModuleHost.Core.Network.Interfaces;
using FDP.Toolkit.Lifecycle;
using FDP.Toolkit.Lifecycle.Events;
using ModuleHost.Network.Cyclone.Services;
using ModuleHost.Network.Cyclone.Translators;
using ModuleHost.Network.Cyclone.Topics;
using ModuleHost.Network.Cyclone.Systems;
using ModuleHost.Network.Cyclone.Providers;
using FDP.Toolkit.Replication.Components;

namespace ModuleHost.Network.Cyclone.Modules
{
    /// <summary>
    /// Master module for CycloneDDS networking.
    /// Wires up all services, translators, and systems required for distributed simulation.
    /// </summary>
    public class CycloneNetworkModule : IModule
    {
        public string Name => "CycloneNetwork";
        
        public ExecutionPolicy Policy => ExecutionPolicy.Synchronous();

        private readonly DdsParticipant _participant;
        private readonly NodeIdMapper _nodeMapper;
        private readonly INetworkIdAllocator _idAllocator;
        private readonly INetworkTopology _topology;
        private readonly EntityLifecycleModule _elm;
        
        // Translators and Services
        private NetworkEntityMap _entityMap;
        private TypeIdMapper _typeMapper;
        private EntityMasterTranslator _masterTranslator;
        private EntityStateTranslator _stateTranslator;
        
        // DDS
        private DdsReader<EntityMasterTopic> _masterReader;
        private DdsWriter<EntityMasterTopic> _masterWriter;
        private DdsReader<EntityStateTopic> _stateReader;
        private DdsWriter<EntityStateTopic> _stateWriter;
        
        private NetworkGatewayModule _gatewayModule;

        public CycloneNetworkModule(
            DdsParticipant participant,
            NodeIdMapper nodeMapper,
            INetworkIdAllocator idAllocator,
            INetworkTopology topology,
            EntityLifecycleModule elm,
            Fdp.Interfaces.ISerializationRegistry? serializationRegistry = null)
        {
            _participant = participant ?? throw new ArgumentNullException(nameof(participant));
            _nodeMapper = nodeMapper ?? throw new ArgumentNullException(nameof(nodeMapper));
            _idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _elm = elm ?? throw new ArgumentNullException(nameof(elm));
            
            // Initialize Services
            _entityMap = new NetworkEntityMap();
            _typeMapper = new TypeIdMapper();

            if (serializationRegistry != null)
            {
                // Register Serialization Providers
                // Using arbitrary ordinals for now as placeholder for FDP-006 IDs
                serializationRegistry.Register(1001, new CycloneSerializationProvider<NetworkPosition>());
                serializationRegistry.Register(1002, new CycloneSerializationProvider<NetworkVelocity>());
                serializationRegistry.Register(1003, new CycloneSerializationProvider<NetworkIdentity>());
                serializationRegistry.Register(1004, new CycloneSerializationProvider<NetworkSpawnRequest>());
            }

            // Initialize Translators
            _masterTranslator = new EntityMasterTranslator(_entityMap, _nodeMapper, _typeMapper);
            _stateTranslator = new EntityStateTranslator(_entityMap);
            
            // Initialize DDS Entities
            _masterReader = new DdsReader<EntityMasterTopic>(_participant, "EntityMaster");
            _masterWriter = new DdsWriter<EntityMasterTopic>(_participant, "EntityMaster");
            
            _stateReader = new DdsReader<EntityStateTopic>(_participant, "EntityState");
            _stateWriter = new DdsWriter<EntityStateTopic>(_participant, "EntityState");
            
            _gatewayModule = new NetworkGatewayModule(101, _nodeMapper.LocalNodeId, _topology, _elm);
        }

        // Removed Initialize() method and moved logic to constructor to satisfy non-nullable checks.

        public void RegisterSystems(ISystemRegistry registry)
        {
            // Register Ingress (Read from Network -> Update World)
            registry.RegisterSystem(new CycloneNetworkIngressSystem(
                new IDescriptorTranslator[] { _masterTranslator, _stateTranslator },
                new IDataReader[] { 
                    new CycloneDataReader<EntityMasterTopic>(_masterReader, "EntityMaster"),
                    new CycloneDataReader<EntityStateTopic>(_stateReader, "EntityState")
                }
            ));
            
            // Register Egress (Read World -> Write to Network)
            registry.RegisterSystem(new CycloneEgressSystem(
                new IDescriptorTranslator[] { _masterTranslator, _stateTranslator },
                new IDataWriter[] { 
                    new CycloneDataWriter<EntityMasterTopic>(_masterWriter, "EntityMaster"),
                    new CycloneDataWriter<EntityStateTopic>(_stateWriter, "EntityState")
                }
            ));
            
            // Gateway handled via Tick delegation
        }

        public void Tick(ISimulationView view, float deltaTime)
        {
            // Delegate tick to gateway module which handles ACKs
             _gatewayModule.Tick(view, deltaTime);
        }
    }

    // Local implementation of Ingress System since it appears missing from Core
    [UpdateInPhase(SystemPhase.Input)]
    public class CycloneNetworkIngressSystem : IModuleSystem
    {
        private readonly IDescriptorTranslator[] _translators;
        private readonly IDataReader[] _readers;
        
        public CycloneNetworkIngressSystem(IDescriptorTranslator[] translators, IDataReader[] readers)
        {
             _translators = translators;
             _readers = readers;
        }
        
        public void Execute(ISimulationView view, float deltaTime)
        {
            // Migrated to Toolkit Replication. Legacy loop disabled.
            // Enabling for Demo since Toolkit Replication Ingress is not yet fully wired in this sample.
            for(int i=0; i<_translators.Length; i++)
            {
                 _translators[i].PollIngress(_readers[i], view.GetCommandBuffer(), view);
            }
        }
    }
}

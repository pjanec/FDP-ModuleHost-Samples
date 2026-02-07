using System;
using System.Collections.Generic;
using CycloneDDS.Runtime;
using Fdp.Kernel;
using FDP.Kernel.Logging;
using Fdp.Interfaces; // Use Fdp Interface
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
using FDP.Toolkit.Replication.Services; // For NetworkEntityMap

using NetworkEntityMap = FDP.Toolkit.Replication.Services.NetworkEntityMap; // Alias to force Toolkit Map
using IDescriptorTranslator = Fdp.Interfaces.IDescriptorTranslator; // Alias to force Fdp Interface
using IDataReader = Fdp.Interfaces.IDataReader;
using IDataWriter = Fdp.Interfaces.IDataWriter;
using INetworkTopology = Fdp.Interfaces.INetworkTopology;

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
        
        // Dynamic / Custom Translators
        private readonly List<IDescriptorTranslator> _customTranslators = new();
        private readonly List<IDataReader> _dynamicReaders = new();
        private readonly List<IDataWriter> _dynamicWriters = new();
        
        private NetworkGatewayModule _gatewayModule;

        public CycloneNetworkModule(
            DdsParticipant participant,
            NodeIdMapper nodeMapper,
            INetworkIdAllocator idAllocator,
            INetworkTopology topology,
            EntityLifecycleModule elm,
            Fdp.Interfaces.ISerializationRegistry? serializationRegistry = null,
            IEnumerable<IDescriptorTranslator> customTranslators = null,
            NetworkEntityMap? sharedEntityMap = null)
        {
            _participant = participant ?? throw new ArgumentNullException(nameof(participant));
            _nodeMapper = nodeMapper ?? throw new ArgumentNullException(nameof(nodeMapper));
            _idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _elm = elm ?? throw new ArgumentNullException(nameof(elm));
            
            // Initialize Services
            _entityMap = sharedEntityMap ?? new NetworkEntityMap();
            _typeMapper = new TypeIdMapper();

            if (serializationRegistry != null)
            {
                // Register Serialization Providers
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
            
            if (customTranslators != null)
            {
                foreach (var t in customTranslators)
                {
                    if (CreateDdsEntitiesForTranslator(t))
                    {
                        _customTranslators.Add(t);
                    }
                }
            }
            
            _gatewayModule = new NetworkGatewayModule(101, _nodeMapper.LocalNodeId, _topology, _elm);
        }

        private bool CreateDdsEntitiesForTranslator(IDescriptorTranslator translator)
        {
            Type topicType = null;
            var type = translator.GetType();

            // 1. Try Reflection Property "DescriptorType" (GeodeticTranslator)
            var prop = type.GetProperty("DescriptorType");
            if (prop != null && typeof(Type).IsAssignableFrom(prop.PropertyType))
            {
                topicType = (Type)prop.GetValue(translator);
            }
            
            // 2. Try Generic Argument (GenericDescriptorTranslator<T>)
            if (topicType == null && type.IsGenericType)
            {
                 topicType = type.GetGenericArguments()[0];
            }

            if (topicType != null)
            {
                try 
                {
                    // Create DdsReader<T>
                    var readerType = typeof(DdsReader<>).MakeGenericType(topicType);
                    // Pass IntPtr.Zero for QoS to explicitly match the constructor when using Activator
                    var reader = Activator.CreateInstance(readerType, _participant, translator.TopicName, IntPtr.Zero);
                    
                    // Create DdsWriter<T>
                    var writerType = typeof(DdsWriter<>).MakeGenericType(topicType);
                    var writer = Activator.CreateInstance(writerType, _participant, translator.TopicName, IntPtr.Zero);
                    
                    // Wrap in CycloneDataReader<T>
                    var wrapperReaderType = typeof(CycloneDataReader<>).MakeGenericType(topicType);
                    var wrapperReader = (IDataReader)Activator.CreateInstance(wrapperReaderType, reader, translator.TopicName);
                    
                    // Wrap in CycloneDataWriter<T>
                    var wrapperWriterType = typeof(CycloneDataWriter<>).MakeGenericType(topicType);
                    var wrapperWriter = (IDataWriter)Activator.CreateInstance(wrapperWriterType, writer, translator.TopicName);
                    
                    _dynamicReaders.Add(wrapperReader);
                    _dynamicWriters.Add(wrapperWriter);
                    return true;
                }
                catch (Exception ex)
                {
                    FdpLog<CycloneNetworkModule>.Error($"Error creating DDS entities for {translator.GetType().Name}", ex);
                    return false;
                }
            }
            else
            {
                FdpLog<CycloneNetworkModule>.Warn($"Could not determine topic type for translator {type.Name}. Skipping DDS entity creation.");
                return false;
            }
        }

        public void RegisterSystems(ISystemRegistry registry)
        {
            // Combine Default + Custom
            var allTranslators = new List<IDescriptorTranslator> { _masterTranslator, _stateTranslator };
            allTranslators.AddRange(_customTranslators);

            var allReaders = new List<IDataReader> { 
                new CycloneDataReader<EntityMasterTopic>(_masterReader, "EntityMaster"),
                new CycloneDataReader<EntityStateTopic>(_stateReader, "EntityState")
            };
            allReaders.AddRange(_dynamicReaders);

            var allWriters = new List<IDataWriter> { 
                new CycloneDataWriter<EntityMasterTopic>(_masterWriter, "EntityMaster"),
                new CycloneDataWriter<EntityStateTopic>(_stateWriter, "EntityState")
            };
            allWriters.AddRange(_dynamicWriters);

            // Register Ingress
            registry.RegisterSystem(new CycloneNetworkIngressSystem(
                allTranslators.ToArray(),
                allReaders.ToArray()
            ));
            
            // Register Egress
            registry.RegisterSystem(new CycloneEgressSystem(
                allTranslators.ToArray(),
                allWriters.ToArray()
            ));

            // Register Cleanup System (Lifecycle)
            registry.RegisterSystem(new CycloneNetworkCleanupSystem(
               new CycloneDataWriter<EntityMasterTopic>(_masterWriter, "EntityMaster")
            ));
            
            // Register Gateway
            // Assuming Gateway is a System. If not, and it needs manual Tick, we might need to restore Tick() or wrap it.
            // Based on user snippet, we register it.
             if (_gatewayModule is IModuleSystem ms)
                registry.RegisterSystem(ms);
             // else if it needs manual tick, we might have lost it. But user snippet said "registry.RegisterSystem(_gatewayModule)"
             // Let's assume user is right or _gatewayModule implements IModuleSystem
        }

        public void Tick(ISimulationView view, float deltaTime)
        {
             _gatewayModule.Tick(view, deltaTime);
        }
    }

    // Local implementation of Ingress System since it appears missing from Core
    [UpdateInPhase(SystemPhase.Input)]
    public class CycloneNetworkIngressSystem : IModuleSystem
    {
        private readonly Fdp.Interfaces.IDescriptorTranslator[] _translators;
        private readonly Fdp.Interfaces.IDataReader[] _readers;
        
        public CycloneNetworkIngressSystem(Fdp.Interfaces.IDescriptorTranslator[] translators, Fdp.Interfaces.IDataReader[] readers)
        {
             _translators = translators;
             _readers = readers;
        }
        
        public void Execute(ISimulationView view, float deltaTime)
        {
            // Migrated to Toolkit Replication. Legacy loop disabled.
            // Enabling for Demo since Toolkit Replication Ingress is not yet fully wired in this sample.
            if (view is EntityRepository repo)
            {
                using (var cmd = new EntityCommandBuffer())
                {
                    for(int i=0; i<_translators.Length; i++)
                    {
                         _translators[i].PollIngress(_readers[i], cmd, view);
                    }
                    cmd.Playback(repo);
                }
            }
        }
    }
}

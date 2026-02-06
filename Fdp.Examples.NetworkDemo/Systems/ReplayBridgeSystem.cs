using System;
using System.Collections.Generic;
using Fdp.Examples.NetworkDemo.Components;
using Fdp.Kernel;
using Fdp.Kernel.FlightRecorder;
using FDP.Toolkit.Replication.Components;
using ModuleHost.Core.Abstractions;

namespace Fdp.Examples.NetworkDemo.Systems
{
    public class ReplayBridgeSystem : IModuleSystem, IDisposable
    {
        private readonly string _recordingPath;
        private EntityRepository _shadowRepo;
        private RecordingReader _reader;
        private double _accumulator;
        private readonly Dictionary<long, Entity> _liveEntityMap = new Dictionary<long, Entity>();

        private const int CHASSIS_KEY = 5;
        private const int TURRET_KEY = 10;
        private readonly int _localNodeId;

        public ReplayBridgeSystem(string recordingPath, int localNodeId)
        {
            _recordingPath = recordingPath;
            _localNodeId = localNodeId;
            InitializeShadowWorld();
        }

        private void InitializeShadowWorld()
        {
            _shadowRepo = new EntityRepository();
            _shadowRepo.RegisterComponent<NetworkIdentity>();
            _shadowRepo.RegisterComponent<NetworkAuthority>();
            _shadowRepo.RegisterComponent<DemoPosition>();
            _shadowRepo.RegisterComponent<TurretState>();
            _shadowRepo.RegisterManagedComponent<DescriptorOwnership>();

            try
            {
                _reader = new RecordingReader(_recordingPath);
            }
            catch
            {
                _reader = null;
            }
        }

        public void Execute(ISimulationView view, float deltaTime)
        {
            if (_reader == null) return;
            
            EntityRepository liveRepo = view as EntityRepository;

            // 1. Advance Recording (Loop if EOF)
            if (!_reader.ReadNextFrame(_shadowRepo))
            {
                DisposeReader();
                InitializeShadowWorld();
                if (_reader != null)
                {
                    _reader.ReadNextFrame(_shadowRepo);
                }
            }

            _accumulator += deltaTime;

            // 2. Update Singleton
            if (liveRepo != null)
            {
                try { liveRepo.RegisterComponent<ReplayTime>(); } catch { }
                
                liveRepo.SetSingleton(new ReplayTime
                {
                    Time = _accumulator,
                    Frame = _shadowRepo.GlobalVersion
                });
            }

            // 3. Inject Entities
            var ecb = view.GetCommandBuffer();

            // Refresh Live Map
            _liveEntityMap.Clear();
            var liveQuery = view.Query().With<NetworkIdentity>().Build();
            foreach (var entity in liveQuery)
            {
                // Use GetComponentRO for view
                var id = view.GetComponentRO<NetworkIdentity>(entity).Value;
                _liveEntityMap[id] = entity;
            }

            // Sync Shadow Entities
            var shadowQuery = _shadowRepo.Query().With<NetworkIdentity>().Build();
            foreach (var shadowEntity in shadowQuery)
            {
                var netId = _shadowRepo.GetComponent<NetworkIdentity>(shadowEntity);

                // Check Root Authority (Key 0)
                if (!HasAuthority(shadowEntity, 0)) continue;

                Entity liveEntity;

                if (_liveEntityMap.TryGetValue(netId.Value, out liveEntity))
                {
                    // Existing entity
                }
                else
                {
                    // New entity
                    liveEntity = ecb.CreateEntity();
                    ecb.AddComponent(liveEntity, netId);

                    if (_shadowRepo.HasComponent<NetworkAuthority>(shadowEntity))
                    {
                        ecb.AddComponent(liveEntity, _shadowRepo.GetComponent<NetworkAuthority>(shadowEntity));
                    }

                    if (_shadowRepo.HasManagedComponent<DescriptorOwnership>(shadowEntity))
                    {
                        var src = _shadowRepo.GetComponent<DescriptorOwnership>(shadowEntity);
                        var dst = new DescriptorOwnership();
                        foreach (var kvp in src.Map) dst.Map[kvp.Key] = kvp.Value;
                        ecb.SetManagedComponent(liveEntity, dst);
                    }
                }

                // Inject Components based on sub-authority
                InjectIfAuthoritative<DemoPosition>(shadowEntity, liveEntity, CHASSIS_KEY, ecb);
                InjectIfAuthoritative<TurretState>(shadowEntity, liveEntity, TURRET_KEY, ecb);
            }
        }

        private void InjectIfAuthoritative<T>(Entity shadowEntity, Entity liveEntity, int key, IEntityCommandBuffer ecb) where T : unmanaged
        {
            if (_shadowRepo.HasComponent<T>(shadowEntity) && HasAuthority(shadowEntity, key))
            {
                var val = _shadowRepo.GetComponent<T>(shadowEntity);
                ecb.AddComponent(liveEntity, val);
            }
        }

        private bool HasAuthority(Entity entity, int key)
        {
            if (_shadowRepo.HasManagedComponent<DescriptorOwnership>(entity))
            {
                var ownership = _shadowRepo.GetComponent<DescriptorOwnership>(entity);
                if (ownership.Map.TryGetValue(key, out int ownerNode))
                {
                    return ownerNode == _localNodeId;
                }
            }

            if (_shadowRepo.HasComponent<NetworkAuthority>(entity))
            {
                var auth = _shadowRepo.GetComponent<NetworkAuthority>(entity);
                return auth.PrimaryOwnerId == _localNodeId;
            }

            return true;
        }

        private void DisposeReader()
        {
            _reader?.Dispose();
            _reader = null;
            _shadowRepo?.Dispose();
            _shadowRepo = null;
        }

        public void Dispose()
        {
            DisposeReader();
        }
    }
}

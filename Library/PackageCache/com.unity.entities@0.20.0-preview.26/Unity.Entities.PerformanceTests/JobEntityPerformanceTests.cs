﻿#if !SYSTEM_SOURCEGEN_DISABLED && DOTS_EXPERIMENTAL

using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Jobs;
using Unity.PerformanceTesting;
using static Unity.Entities.PerformanceTests.PerformanceTestHelpers;


namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    public partial class JobEntityPerformanceTests : EntitiesTestsFixture
    {
        public partial class JobEntityTestComponentSystem : SystemBase
        {
            protected override void OnUpdate()
            {
            }

            public JobHandle UpdateOneComponentEntityJob(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                    {
                        var job = new EcsTestUpdateOneComponentJob();
                        job.Run();
                        break;
                    }
                    case ScheduleType.ScheduleParallel:
                    {
                        var job = new EcsTestUpdateOneComponentJob();
                        Dependency = job.ScheduleParallel();
                        break;
                    }
                    case ScheduleType.Schedule:
                    {
                        var job = new EcsTestUpdateOneComponentJob();
                        Dependency = job.Schedule();
                        break;
                    }
                }

                return Dependency;
            }

            public JobHandle UpdateTwoComponentsEntityJob(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                    {
                        var job = new EcsTestUpdateTwoComponentsJob();
                        job.Run();
                        break;
                    }
                    case ScheduleType.ScheduleParallel:
                    {
                        var job = new EcsTestUpdateTwoComponentsJob();
                        Dependency = job.ScheduleParallel();
                        break;
                    }
                    case ScheduleType.Schedule:
                    {
                        var job = new EcsTestUpdateTwoComponentsJob();
                        Dependency = job.Schedule();
                        break;
                    }
                }

                return Dependency;
            }

            public JobHandle UpdateThreeComponentsEntityJob(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                    {
                        var job = new EcsTestUpdateThreeComponentsJob();
                        job.Run();
                        break;
                    }
                    case ScheduleType.ScheduleParallel:
                    {
                        var job = new EcsTestUpdateThreeComponentsJob();
                        Dependency = job.ScheduleParallel();
                        break;
                    }
                    case ScheduleType.Schedule:
                    {
                        var job = new EcsTestUpdateThreeComponentsJob();
                        Dependency = job.Schedule();
                        break;
                    }
                }

                return Dependency;
            }

            public JobHandle UpdateOneComponentUsingOtherComponentValuesJob(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                    {
                        var job = new EcsTestUpdateOneComponentWithValuesFromOtherComponentsJob();
                        job.Run();
                        break;
                    }
                    case ScheduleType.ScheduleParallel:
                    {
                        var job = new EcsTestUpdateOneComponentWithValuesFromOtherComponentsJob();
                        Dependency = job.ScheduleParallel();
                        break;
                    }
                    case ScheduleType.Schedule:
                    {
                        var job = new EcsTestUpdateOneComponentWithValuesFromOtherComponentsJob();
                        Dependency = job.Schedule();
                        break;
                    }
                }

                return Dependency;
            }

            public void UpdateComponentValueTo10Job_Run()
            {
                var job = new EcsTestSetComponentValueTo10();
                job.Run();
            }

            public JobHandle UpdateComponentValueTo10Job_ScheduleParallel(JobHandle jobHandle)
            {

                var job = new EcsTestSetComponentValueTo10();
                var newJobHandle = job.ScheduleParallel(jobHandle);
                return newJobHandle;
            }

            public JobHandle UpdateComponentValueTo10Job_Schedule(JobHandle jobHandle)
            {
                var job = new EcsTestSetComponentValueTo10();
                var newJobHandle = job.Schedule(jobHandle);
                return newJobHandle;
            }

            public JobHandle UpdateComponentValueTo10Job(JobHandle dependsOn = default)
            {
                var query = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { typeof(EcsTestData), typeof(EcsTestData2) }
                });
                var job = new EcsTestSetComponentValueTo10();
                var jobHandle = job.ScheduleParallel(query, dependsOn);
                return jobHandle;
            }

            public JobHandle UpdateOnlyComponentValueTo10Job_WithSharedComponentFilter_10(JobHandle dependsOn = default)
            {
                var query = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp) }
                });
                query.SetSharedComponentFilter(new EcsTestSharedComp {value = 10});
                var job = new EcsTestSetComponentValueTo10();
                var jobHandle = job.ScheduleParallel(query, dependsOn);
                return jobHandle;
            }

            public JobHandle UpdateFirstComponentValueTo10Job_WithSharedComponentFilter_0(bool withEcsTestData2, JobHandle dependsOn = default)
            {
                if (withEcsTestData2)
                {
                    var query = GetEntityQuery(new EntityQueryDesc
                    {
                        All = new ComponentType[] { typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp) }
                    });
                    query.SetSharedComponentFilter(new EcsTestSharedComp {value = 10});
                    var job = new EcsTestSetComponentValueTo10();
                    var jobHandle = job.ScheduleParallel(query, dependsOn);
                    return jobHandle;
                }
                else
                {
                    var query = GetEntityQuery(new EntityQueryDesc
                    {
                        All = new ComponentType[] { typeof(EcsTestData), typeof(EcsTestSharedComp) }
                    });
                    query.SetSharedComponentFilter(new EcsTestSharedComp {value = 10});
                    var job = new EcsTestSetComponentValueTo10();
                    var jobHandle = job.ScheduleParallel(query, dependsOn);
                    return jobHandle;
                }
            }
        }
        protected JobEntityTestComponentSystem JobEntityTestSystem => World.GetOrCreateSystem<JobEntityTestComponentSystem>();

        [Test, Performance]
        [Category("Performance")]
        public void JobEntityOnUpdate_Performance_UpdateOneComponent(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType,
            [Values(1, 1000, 100000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));

            using (var entities = new NativeArray<Entity>(entityCount, Allocator.TempJob))
            {
                m_Manager.CreateEntity(archetype, entities);

                var handle = default(JobHandle);
                Measure
                    .Method(() => { handle = JobEntityTestSystem.UpdateOneComponentEntityJob(scheduleType); })
                    .WarmupCount(5)
                    .CleanUp(() => {handle.Complete();})
                    .MeasurementCount(100)
                    .SampleGroup("JobEntityOnUpdate_Performance_UpdateOneComponent")
                    .Run();
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void JobEntityOnUpdate_Performance_UpdateTwoComponents(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType,
            [Values(1, 1000, 100000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            using (var entities = new NativeArray<Entity>(entityCount, Allocator.TempJob))
            {
                m_Manager.CreateEntity(archetype, entities);

                var handle = default(JobHandle);
                Measure
                    .Method(() => { handle = JobEntityTestSystem.UpdateTwoComponentsEntityJob(scheduleType); })
                    .WarmupCount(5)
                    .CleanUp(() => {handle.Complete();})
                    .MeasurementCount(100)
                    .SampleGroup("JobEntityOnUpdate_Performance_UpdateTwoComponents")
                    .Run();
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void JobEntityOnUpdate_Performance_UpdateThreeComponents(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType,
            [Values(1, 1000, 100000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));

            using (var entities = new NativeArray<Entity>(entityCount, Allocator.TempJob))
            {
                m_Manager.CreateEntity(archetype, entities);
                var handle = default(JobHandle);
                Measure
                    .Method(() => { handle = JobEntityTestSystem.UpdateThreeComponentsEntityJob(scheduleType); })
                    .WarmupCount(5)
                    .CleanUp(() => {handle.Complete();})
                    .MeasurementCount(100)
                    .SampleGroup("JobEntityOnUpdate_Performance_UpdateThreeComponents")
                    .Run();
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void JobEntityOnUpdate_Performance_UpdateOneComponentUsingOtherComponentValues(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType)
        {
            EntityArchetype archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));

            using (var entities = new NativeArray<Entity>(length: 1000000, Allocator.TempJob))
            {
                m_Manager.CreateEntity(archetype, entities);

                var handle = default(JobHandle);
                Measure
                    .Method(() => { handle = JobEntityTestSystem.UpdateOneComponentUsingOtherComponentValuesJob(scheduleType); })
                    .WarmupCount(5)
                    .CleanUp(() => {handle.Complete();})
                    .MeasurementCount(100)
                    .SampleGroup("JobEntityOnUpdate_Performance_UpdateOneComponentUsingOtherComponentValues")
                    .Run();
            }
        }

        public enum Type
        {
            JobEntity,
            JobEntityBatch
        }

        [Test, Performance]
        [Category("Performance")]
        public void JobEntityOnUpdate_SchedulePerformance(
            [Values(100, 10000, 5000000)] int numEntities,
            [Values(10, 100)] int numUniqueArchetypes,
            [Values(Type.JobEntity, Type.JobEntityBatch)] Type type)
        {
            using (var archetypes = CreateUniqueArchetypes(m_Manager, numUniqueArchetypes, Allocator.TempJob, typeof(EcsTestData)))
            using (var basicQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                for (int archetypeIndex = 0; archetypeIndex < numUniqueArchetypes; ++archetypeIndex)
                {
                    m_Manager.CreateEntity(archetypes[archetypeIndex], numEntities / numUniqueArchetypes);
                }

                switch (type)
                {
                    case Type.JobEntity:
                        var handle = default(JobHandle);
                        Measure
                            .Method(() =>
                            {
                                handle = JobEntityTestSystem.UpdateComponentValueTo10Job_Schedule(handle);
                            })
                            .WarmupCount(5)
                            .CleanUp(() => {handle.Complete();})
                            .SampleGroup("IJobEntity")
                            .Run();
                        break;
                    case Type.JobEntityBatch:
                        var handle_ = default(JobHandle);
                        var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
                        Measure.Method(() =>
                            {
                                handle_ = new EcsTestSetComponentValueTo10_BaseLine
                                {
                                    EcsTestDataRW = typeHandle
                                }.Schedule(basicQuery, handle_);
                            })
                            .WarmupCount(5)
                            .CleanUp(() => {handle_.Complete();})
                            .SampleGroup("IJobEntityBatch")
                            .Run();
                        break;
                }
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void JobEntityOnUpdate_ScheduleParallelPerformance(
            [Values(100, 10000, 5000000)] int numEntities,
            [Values(10, 100)] int numUniqueArchetypes,
            [Values(Type.JobEntity, Type.JobEntityBatch)] Type type)
        {
            using (var archetypes = CreateUniqueArchetypes(m_Manager, numUniqueArchetypes, Allocator.TempJob, typeof(EcsTestData)))
            using (var basicQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                for (int archetypeIndex = 0; archetypeIndex < numUniqueArchetypes; ++archetypeIndex)
                {
                    m_Manager.CreateEntity(archetypes[archetypeIndex], numEntities / numUniqueArchetypes);
                }

                switch (type)
                {
                    case Type.JobEntity:
                        var handle = default(JobHandle);
                        Measure
                            .Method(() =>
                            {
                                handle = JobEntityTestSystem.UpdateComponentValueTo10Job_ScheduleParallel(handle);
                            })
                            .WarmupCount(5)
                            .CleanUp(() => {handle.Complete();})
                            .SampleGroup("IJobEntity")
                            .Run();
                        break;
                    case Type.JobEntityBatch:
                        var handle_ = default(JobHandle);
                        var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
                        Measure.Method(() =>
                            {
                                handle_ = new EcsTestSetComponentValueTo10_BaseLine
                                {
                                    EcsTestDataRW = typeHandle
                                }.ScheduleParallel(basicQuery, 1, handle_);
                            })
                            .WarmupCount(5)
                            .CleanUp(() => {handle_.Complete();})
                            .SampleGroup("IJobEntityBatch")
                            .Run();
                        break;
                }
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void JobEntityOnUpdate_ExecutingPerformance(
            [Values(100, 10000, 5000000)] int numEntities,
            [Values(10, 100)] int numUniqueArchetypes,
            [Values(Type.JobEntity, Type.JobEntityBatch)] Type type)
        {
            using (var archetypes = CreateUniqueArchetypes(m_Manager, numUniqueArchetypes, Allocator.TempJob, typeof(EcsTestData)))
            using (var basicQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                for (int archetypeIndex = 0; archetypeIndex < numUniqueArchetypes; ++archetypeIndex)
                {
                    m_Manager.CreateEntity(archetypes[archetypeIndex], numEntities / numUniqueArchetypes);
                }

                switch (type)
                {
                    case Type.JobEntity:
                        Measure
                            .Method(() => { JobEntityTestSystem.UpdateComponentValueTo10Job_Run(); })
                            .SampleGroup("IJobEntity")
                            .WarmupCount(5)
                            .Run();
                        break;
                    case Type.JobEntityBatch:
                        var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
                        Measure.Method(() =>
                            {
                                new EcsTestSetComponentValueTo10_BaseLine
                                {
                                    EcsTestDataRW = typeHandle
                                }.Run(basicQuery);
                            })
                            .WarmupCount(5)
                            .SampleGroup("IJobEntityBatch")
                            .Run();
                        break;
                }
            }
        }

        [Test, Performance]
        public void SingleArchetype_SingleChunk_Unfiltered()
        {
            const int kEntityCount = 10;

            var archetype = m_Manager.CreateArchetype(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>());
            var entities = new NativeArray<Entity>(kEntityCount, Allocator.TempJob);
            m_Manager.CreateEntity(archetype, entities);

            var dependsOn = new JobHandle();

            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                        dependsOn = JobEntityTestSystem.UpdateComponentValueTo10Job(dependsOn);
                })
                .SampleGroup("Scheduling")
                .Run();

            dependsOn.Complete();

            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        var job = JobEntityTestSystem.UpdateComponentValueTo10Job(dependsOn);
                        job.Complete();
                    }
                })
                .SampleGroup("ScheduleAndRun")
                .Run();

            entities.Dispose();
        }

        [Test, Performance]
        public void SingleArchetype_TwoChunks_Filtered()
        {
            const int kEntityCount = 10;

            var archetype = m_Manager.CreateArchetype(
                ComponentType.ReadWrite<EcsTestData>(),
                ComponentType.ReadWrite<EcsTestData2>(),
                ComponentType.ReadWrite<EcsTestSharedComp>());

            var entities = new NativeArray<Entity>(kEntityCount, Allocator.TempJob);
            m_Manager.CreateEntity(archetype, entities);

            for (int i = kEntityCount / 2; i < kEntityCount; ++i)
            {
                m_Manager.SetSharedComponentData(entities[i], new EcsTestSharedComp {value = 10});
            }

            var dependsOn = new JobHandle();

            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                        dependsOn = JobEntityTestSystem.UpdateOnlyComponentValueTo10Job_WithSharedComponentFilter_10(dependsOn);
                })
                .SampleGroup("Scheduling")
                .Run();

            dependsOn.Complete();

            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        var job = JobEntityTestSystem.UpdateOnlyComponentValueTo10Job_WithSharedComponentFilter_10(dependsOn);
                        job.Complete();
                    }

                })
                .SampleGroup("ScheduleAndRun")
                .Run();

            entities.Dispose();
        }

        [Test, Performance]
        public void SingleArchetype_MultipleChunks_Filtered()
        {
            const int kEntityCount = 10000;

            var archetype = m_Manager.CreateArchetype(
                ComponentType.ReadWrite<EcsTestData>(),
                ComponentType.ReadWrite<EcsTestData2>(),
                ComponentType.ReadWrite<EcsTestSharedComp>());

            var entities = new NativeArray<Entity>(kEntityCount, Allocator.TempJob);
            m_Manager.CreateEntity(archetype, entities);

            for (int i = 0; i < kEntityCount; ++i)
            {
                m_Manager.SetSharedComponentData(entities[i], new EcsTestSharedComp {value = i % 10 });
            }

            var dependsOn = new JobHandle();
            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                        dependsOn = JobEntityTestSystem.UpdateFirstComponentValueTo10Job_WithSharedComponentFilter_0(withEcsTestData2: true, dependsOn);

                })
                .SampleGroup("Scheduling")
                .Run();

            dependsOn.Complete();

            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        var job = JobEntityTestSystem.UpdateFirstComponentValueTo10Job_WithSharedComponentFilter_0(withEcsTestData2: true);
                        job.Complete();
                    }
                })
                .SampleGroup("ScheduleAndRun")
                .Run();

            entities.Dispose();
        }

        [Test, Performance]
        public void MultipleArchetype_MultipleChunks_Filtered()
        {
            var allTypes = new ComponentType[5];
            allTypes[0] = ComponentType.ReadWrite<EcsTestSharedComp>();
            allTypes[1] = ComponentType.ReadWrite<EcsTestData>();
            allTypes[2] = ComponentType.ReadWrite<EcsTestData2>();
            allTypes[3] = ComponentType.ReadWrite<EcsTestData3>();
            allTypes[4] = ComponentType.ReadWrite<EcsTestData4>();

            var allArchetypes = new EntityArchetype[8];
            allArchetypes[0] = m_Manager.CreateArchetype(allTypes[0], allTypes[1]);
            allArchetypes[1] = m_Manager.CreateArchetype(allTypes[0], allTypes[1], allTypes[2]);
            allArchetypes[2] = m_Manager.CreateArchetype(allTypes[0], allTypes[1], allTypes[3]);
            allArchetypes[3] = m_Manager.CreateArchetype(allTypes[0], allTypes[1], allTypes[4]);
            allArchetypes[4] = m_Manager.CreateArchetype(allTypes[0], allTypes[1], allTypes[2], allTypes[3]);
            allArchetypes[5] = m_Manager.CreateArchetype(allTypes[0], allTypes[1], allTypes[2], allTypes[4]);
            allArchetypes[6] = m_Manager.CreateArchetype(allTypes[0], allTypes[1], allTypes[3], allTypes[4]);
            allArchetypes[7] = m_Manager.CreateArchetype(allTypes);

            const int kEntityCountPerArchetype = 1000;
            for (int i = 0; i < 8; ++i)
            {
                var entities = new NativeArray<Entity>(kEntityCountPerArchetype, Allocator.TempJob);
                m_Manager.CreateEntity(allArchetypes[i], entities);

                for (int j = 0; j < kEntityCountPerArchetype; ++j)
                {
                    m_Manager.SetSharedComponentData(entities[i], new EcsTestSharedComp {value = i % 10 });
                }

                entities.Dispose();
            }

            var dependsOn = new JobHandle();

            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        dependsOn = JobEntityTestSystem.UpdateFirstComponentValueTo10Job_WithSharedComponentFilter_0(withEcsTestData2: false, dependsOn);
                    }
                })
                .SampleGroup("Scheduling")
                .Run();

            dependsOn.Complete();

            Measure.Method(
                () =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        var job = JobEntityTestSystem.UpdateFirstComponentValueTo10Job_WithSharedComponentFilter_0(withEcsTestData2: false);
                        job.Complete();
                    }
                })
                .SampleGroup("ScheduleAndRun")
                .Run();
        }
    }
}
#endif

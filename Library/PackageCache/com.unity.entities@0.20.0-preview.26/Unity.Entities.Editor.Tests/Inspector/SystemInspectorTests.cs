﻿using NUnit.Framework;
using UnityEditor;

namespace Unity.Entities.Editor.Tests
{
    partial class SystemInspectorTests
    {
        World m_World;
        SystemInspectorTestSystem m_SystemInspectorTestSystem;
        SystemScheduleTestSystem1 m_SystemInspectorTestSystem1;
        SystemScheduleTestSystem2 m_SystemInspectorTestSystem2;

        WorldProxyManager m_WorldProxyManager;
        WorldProxy m_WorldProxy;

        partial class SystemInspectorTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .ForEach((ref SystemScheduleTestData1 data1, in SystemScheduleTestData2 data2) =>
                {
                }).Run();

                Entities
                    .WithNone<SystemScheduleTestData2>().ForEach((in SystemScheduleTestData1 data1) =>
                {
                }).Run();
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_World = new World("SystemInspectorTestWorld");
            m_SystemInspectorTestSystem = m_World.GetOrCreateSystem<SystemInspectorTestSystem>();
            m_SystemInspectorTestSystem1 = m_World.GetOrCreateSystem<SystemScheduleTestSystem1>();
            m_SystemInspectorTestSystem2 = m_World.GetOrCreateSystem<SystemScheduleTestSystem2>();

            m_World.GetOrCreateSystem<SimulationSystemGroup>().AddSystemToUpdateList(m_SystemInspectorTestSystem1);
            m_World.GetOrCreateSystem<SimulationSystemGroup>().AddSystemToUpdateList(m_SystemInspectorTestSystem2);
            m_World.GetOrCreateSystem<SimulationSystemGroup>().AddSystemToUpdateList(m_SystemInspectorTestSystem);

            m_WorldProxyManager = new WorldProxyManager();
            m_WorldProxyManager.CreateWorldProxiesForAllWorlds();
            m_WorldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(m_World);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_World.Dispose();
            if (EditorWindow.HasOpenInstances<SystemScheduleWindow>())
            {
                EditorWindow.GetWindow<SystemScheduleWindow>().Close();
            }
        }

        [Test]
        public void SystemInspector_QueriesTab_MatchingQueries()
        {
            var systemQueriesTab = new SystemQueries(m_World, new SystemProxy(m_SystemInspectorTestSystem, m_WorldProxy));

            Assert.That(systemQueriesTab.QueriesFromSystem.Length, Is.EqualTo(2));
            Assert.That(systemQueriesTab.QueriesFromSystem[0].GetEntityQueryDesc().All, Is.EquivalentTo(new[] { ComponentType.ReadWrite<SystemScheduleTestData1>(), ComponentType.ReadOnly<SystemScheduleTestData2>() }));
            Assert.That(systemQueriesTab.QueriesFromSystem[1].GetEntityQueryDesc().All, Is.EquivalentTo(new[] { ComponentType.ReadOnly<SystemScheduleTestData1>()}));
            Assert.That(systemQueriesTab.QueriesFromSystem[1].GetEntityQueryDesc().None, Is.EquivalentTo(new[] { ComponentType.Exclude<SystemScheduleTestData2>()}));
        }

        [Test]
        public void SystemInspector_RelationshipsTab_MatchingEntities()
        {
            var systemEntities = new SystemEntities(m_World, new SystemProxy(m_SystemInspectorTestSystem, m_WorldProxy));
            Assert.That(systemEntities.EntitiesFromQueries.Count, Is.EqualTo(2));
            Assert.That(systemEntities.EntitiesFromQueries[0].SystemName, Is.EqualTo("System Inspector Tests | System Inspector Test System"));
            Assert.That(systemEntities.EntitiesFromQueries[0].QueryOrder, Is.EqualTo(1));
            Assert.That(systemEntities.EntitiesFromQueries[0].World.Name, Is.EqualTo("SystemInspectorTestWorld"));
            Assert.That(systemEntities.EntitiesFromQueries[0].Query.GetEntityQueryDesc().All,
                Is.EquivalentTo(new[]
                {
                    ComponentType.ReadWrite<SystemScheduleTestData1>(),
                    ComponentType.ReadOnly<SystemScheduleTestData2>()
                }));

            Assert.That(systemEntities.EntitiesFromQueries[1].SystemName, Is.EqualTo("System Inspector Tests | System Inspector Test System"));
            Assert.That(systemEntities.EntitiesFromQueries[1].QueryOrder, Is.EqualTo(2));
            Assert.That(systemEntities.EntitiesFromQueries[1].World.Name, Is.EqualTo("SystemInspectorTestWorld"));
            Assert.That(systemEntities.EntitiesFromQueries[1].Query.GetEntityQueryDesc().All,
                Is.EquivalentTo(new[]
                {
                    ComponentType.ReadOnly<SystemScheduleTestData1>()
                }));
            Assert.That(systemEntities.EntitiesFromQueries[1].Query.GetEntityQueryDesc().None,
                Is.EquivalentTo(new[]
                {
                    ComponentType.Exclude<SystemScheduleTestData2>()
                }));
        }

        [Test]
        public void SystemInspector_RelationshipsTab_MatchingDependencies()
        {
            var systemDependencies = new SystemDependencies(m_World, new SystemProxy(m_SystemInspectorTestSystem1, m_WorldProxy));
            var systemDependencyViewDataList = systemDependencies.GetSystemDependencyViewDataList();
            Assert.That(systemDependencyViewDataList.Count, Is.EqualTo(2));
            Assert.That(systemDependencyViewDataList[0].Equals(new SystemDependencyViewData(new SystemProxy(m_SystemInspectorTestSystem1, m_WorldProxy), "System Schedule Test System 1", "Selected")), Is.True);
            Assert.That(systemDependencyViewDataList[1].Equals(new SystemDependencyViewData(new SystemProxy(m_SystemInspectorTestSystem2, m_WorldProxy), "System Schedule Test System 2", "After")), Is.True);
        }
    }
}


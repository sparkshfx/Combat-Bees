﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Properties;
using Unity.Properties.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class RelationshipsTab : ITabContent
    {
        static readonly ProfilerMarker k_UpdateMarker = new ProfilerMarker($"EntityInspector.{nameof(RelationshipsTab)}.{nameof(Update)}");
        readonly EntityInspectorContext m_Context;

        readonly List<QueryViewData> m_QueryCache = new List<QueryViewData>();
        readonly WorldProxyManager m_WorldProxyManager;

        [CreateProperty]
        List<SystemQueriesViewData> m_Systems;
        bool m_IsVisible;
        int m_LastWorldVersion;

        public RelationshipsTab(EntityInspectorContext entityInspectorContext)
        {
            m_Context = entityInspectorContext;
            m_Systems = new List<SystemQueriesViewData>();
            m_WorldProxyManager = new WorldProxyManager();
        }

        ~RelationshipsTab()
        {
            m_WorldProxyManager.Dispose();
        }

        void Update()
        {
            using var _ = k_UpdateMarker.Auto();
            var world = m_Context.World;
            var worldVersion = world.Version;
            if (m_LastWorldVersion == worldVersion)
                return;

            m_LastWorldVersion = worldVersion;
            m_WorldProxyManager.RebuildWorldProxyForGivenWorld(world);
            var worldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(world);
            m_Systems.Clear();
            GetMatchSystems(m_Context.Entity, world, m_QueryCache, m_Systems, worldProxy);
        }

        // internal for tests.
        internal static void GetMatchSystems(Entity entity, World world, List<QueryViewData> matchingQueries, List<SystemQueriesViewData> matchingSystems, WorldProxy worldProxy)
        {
            foreach (var system in worldProxy.AllSystemsInOrder)
            {
                GatherMatchingSystem(entity, world, system, matchingQueries, matchingSystems);
            }
        }

        static unsafe void GatherMatchingSystem(Entity entity, World world, SystemProxy system, List<QueryViewData> matchingQueries, List<SystemQueriesViewData> matchingSystems)
        {
            var ecs = world.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;

            GatherMatchingQueries(ecs, entity, ref system.StatePointerForQueryResults->EntityQueries, matchingQueries, world, system.NicifiedDisplayName);
            if (matchingQueries.Count > 0)
            {
                matchingSystems.Add(new SystemQueriesViewData(system, GetSystemKind(system), matchingQueries.ToArray()));
                matchingQueries.Clear();
            }
        }

        // internal for tests
        internal static unsafe void GatherMatchingQueries(EntityComponentStore* ecs, Entity entity, ref UnsafeList<EntityQuery> queries, List<QueryViewData> matchingQueries, World world, string systemName)
        {
            var archetype = ecs->GetArchetype(entity);
            for (var i = 0; i < queries.Length; i++)
            {
                var query = queries[i];
                if (query.IsEmpty)
                    continue;

                var matchingArchetypes = query._GetImpl()->_QueryData->MatchingArchetypes;
                var matchingArchetypeIndex = EntityQueryManager.FindMatchingArchetypeIndexForArchetype(ref matchingArchetypes, archetype);
                if (matchingArchetypeIndex == -1)
                    continue;

                if (query.HasFilter())
                {
                    var chunk = ecs->GetChunk(entity);
                    var match = matchingArchetypes.Ptr[matchingArchetypeIndex];
                    if (!chunk->MatchesFilter(match, ref query._GetImpl()->_Filter))
                        continue;
                }

                matchingQueries.Add(new QueryViewData(i + 1, query, systemName, world));
            }
        }

        public static SystemQueriesViewData.SystemKind GetSystemKind(SystemProxy system)
        {
            return system.Category switch
            {
                SystemCategory.Unmanaged => SystemQueriesViewData.SystemKind.Unmanaged,
                SystemCategory.ECBSystemBegin => SystemQueriesViewData.SystemKind.CommandBufferBegin,
                SystemCategory.ECBSystemEnd => SystemQueriesViewData.SystemKind.CommandBufferEnd,
                _ => SystemQueriesViewData.SystemKind.Regular
            };
        }

        public string TabName { get; } = L10n.Tr("Relationships");
        public void OnTabVisibilityChanged(bool isVisible) => m_IsVisible = isVisible;

        [UsedImplicitly]
        internal class RelationshipsTabView : Inspector<RelationshipsTab>
        {
            static readonly string k_MoreLabel = L10n.Tr("More systems are matching this entity. You can use the search to filter systems by name.");
            static readonly string k_MoreLabelWithFilter = L10n.Tr("More systems are matching this search. Refine the search terms to find a particular system.");
            static readonly ProfilerMarker k_ViewUpdateMarker = new ProfilerMarker($"EntityInspector.{nameof(RelationshipsTabView)}.{nameof(Update)}");
            readonly Cooldown m_Cooldown = new Cooldown(TimeSpan.FromMilliseconds(Constants.Inspector.CoolDownTime));
            // internal for tests
            internal readonly List<string> SearchTerms = new List<string>();
            internal SystemQueriesListView systemQueriesListView;

            public override VisualElement Build()
            {
                var root = new VisualElement();
                Resources.Templates.Inspector.RelationshipsTab.Root.AddStyles(root);
                root.AddToClassList(UssClasses.Inspector.RelationshipsTab.Container);

                var toolbarSearchField = new ToolbarSearchField();
                toolbarSearchField.AddToClassList(UssClasses.Inspector.RelationshipsTab.SearchField);

                systemQueriesListView = new SystemQueriesListView(new List<SystemQueriesViewData>(), SearchTerms, k_MoreLabel, k_MoreLabelWithFilter);
                toolbarSearchField.RegisterValueChangedCallback(evt =>
                {
                    SearchTerms.Clear();
                    if (evt.newValue.Length > 0)
                    {
                        SearchTerms.AddRange(evt.newValue.Split(' '));
                    }
                    systemQueriesListView.OnSearchTermsChanged();
                });

                root.Add(toolbarSearchField);
                root.Add(systemQueriesListView);

                return root;
            }

            public override void Update()
            {
                if (!Target.m_IsVisible || !m_Cooldown.Update(DateTime.Now))
                    return;

                using var _ = k_ViewUpdateMarker.Auto();
                Target.Update();

                systemQueriesListView.Update(Target.m_Systems);
            }
        }
    }
}

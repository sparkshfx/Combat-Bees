﻿using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class QueryView : FoldoutWithActionButton
    {
        QueryViewData m_Data;
        readonly Label m_EmptyMessage;

        public QueryView(in QueryViewData data)
        {
            Resources.Templates.QueryView.AddStyles(this);

            ActionButton.RegisterCallback<MouseDownEvent, QueryView>((evt, @this) =>
            {
                evt.StopPropagation();
                evt.PreventDefault();

                QueryWindowHelper.OpenNewWindow(@this.m_Data.Context.World, @this.m_Data.Context.Query, @this.m_Data.Context.SystemName, @this.m_Data.QueryId);
            }, this);

            m_EmptyMessage = new Label(L10n.Tr("All entities"));
            m_EmptyMessage.AddToClassList(UssClasses.QueryView.EmptyMessage);
            Add(m_EmptyMessage);

            Update(data);
        }

        public void Update(in QueryViewData data)
        {
            if (!m_Data.Equals(data))
            {
                m_Data = data;
                HeaderName.text = m_Data.QueryName;
            }

            var ui = this.Query<ComponentView>().ToList();

            m_EmptyMessage.SetVisibility(data.Components.Length == 0);

            var i = 0;
            for (; i < ui.Count && i < data.Components.Length; i++)
            {
                ui[i].Update(data.Components[i]);
            }

            for (; i < data.Components.Length; i++)
            {
                Add(new ComponentView(data.Components[i]));
            }

            for (; i < ui.Count; i++)
            {
                ui[i].RemoveFromHierarchy();
            }
        }
    }
}

using System.Collections.Generic;
using Inventory.Scripts.Core.Controllers.Highlights.Global;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Highlights
{
    [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Highlighter Provider")]
    public class HighlighterProviderSo : ScriptableObject
    {
        public HighlighterPrefabAnchorSo HighlighterPrefabAnchorSo
        {
            set => highlighterPrefabAnchorSo = value;
        }

        [Header("Configuration")]
        [SerializeField,
         Tooltip(
             "If set to true, will show debug logs. Used for debug the highlight system or also to validate if your routines are working properly")]
        private bool debug;

        [SerializeField] private HighlighterPrefabAnchorSo highlighterPrefabAnchorSo;

        [Header("Processors")] [SerializeField]
        private List<HighlightProcessor> processors;

        [SerializeField] private List<HighlightGlobalProcessor> globalProcessors;


        private RectTransform _highlightParent;

        public void NonHighlight()
        {
            Highlight(HighlighterContext.Empty());
        }

        public void Highlight(HighlighterContext context)
        {
            context.Debug = debug;

            var highlighterState = new HighlighterState();

            ProcessHighlightProcessors(context, highlighterState, processors);

            UpdateHighlight(highlighterState);
        }

        private static void ProcessHighlightProcessors(HighlighterContext context, HighlighterState state,
            IEnumerable<HighlightProcessor> processors)
        {
            foreach (var highlightProcessor in processors)
            {
                highlightProcessor.Process(context, state);
            }
        }

        private void UpdateHighlight(HighlighterState finalState)
        {
            var highlighter = highlighterPrefabAnchorSo.Value;

            highlighter.Show(finalState.Show);
            highlighter.SetColor(finalState.Color);
            highlighter.SetSizeDelta(finalState.Size);
            highlighter.SetLocalPosition(finalState.Position);

            if (_highlightParent != null)
            {
                _highlightParent.SetAsLastSibling();
            }
        }

        public void SetParent(RectTransform highlightParent)
        {
            var highlighter = highlighterPrefabAnchorSo.Value;

            highlighter.SetParent(highlightParent);

            _highlightParent = highlightParent;
        }

        public void HighlightGlobal(HighlightGlobalContext context)
        {
            context.Debug = debug;

            ProcessHighlightProcessors(context, globalProcessors);
        }

        private static void ProcessHighlightProcessors(HighlightGlobalContext context,
            IEnumerable<HighlightGlobalProcessor> processors)
        {
            foreach (var highlightProcessor in processors)
            {
                highlightProcessor.Process(context);
            }
        }
    }
}
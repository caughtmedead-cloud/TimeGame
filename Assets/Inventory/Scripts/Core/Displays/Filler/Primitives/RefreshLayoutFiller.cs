using Inventory.Scripts.Core.Grids.Helper;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Displays.Filler.Primitives
{
    [RequireComponent(typeof(RectTransform))]
    public class RefreshLayoutFiller : AbstractFiller
    {
        [SerializeField] private bool refreshOnSet;
        [SerializeField] private bool refreshOnReset;
        [SerializeField] private bool refreshOnRefresh = true;

        private RectTransform _rectTransform;

        public override void Init()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        protected override void OnSet(DisplayData displayData)
        {
            RectHelper.SetAnchorsForGrid(_rectTransform);

            RefreshIfNeeded(refreshOnSet);
        }

        protected override void OnReset()
        {
            RefreshIfNeeded(refreshOnReset);
        }

        public override void OnRefreshUI()
        {
            base.OnRefreshUI();

            RefreshIfNeeded(refreshOnRefresh);
        }

        private void RefreshIfNeeded(bool shouldRefresh)
        {
            if (shouldRefresh)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
            }
        }

        protected override bool ShouldFill(DisplayData displayData)
        {
            return true;
        }

        public void UpdateValues(
            bool refreshOnSetValue = default,
            bool refreshOnResetValue = default,
            bool refreshOnRefreshValue = default
        )
        {
            refreshOnSet = refreshOnSetValue;
            refreshOnReset = refreshOnResetValue;
            refreshOnRefresh = refreshOnRefreshValue;
        }
    }
}
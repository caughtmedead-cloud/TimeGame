using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Displays.Filler.Primitives;
using Inventory.Scripts.Core.Grids.Helper;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Displays.Filler
{
    public class DisplayFiller : MonoBehaviour
    {
        [field: Header("Display Information")]
        [field: SerializeReference]
        public DisplayData DisplayData { get; private set; }

        private RectTransform _rectTransform;
        private AbstractFiller[] _fillers;


        protected virtual void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _fillers = GetAllFillers();
        }

        protected virtual void OnDisable()
        {
            Close(DisplayData);
        }

        public void Set(DisplayData inventoryDisplay)
        {
            DisplayData = inventoryDisplay;

            OnAwakeDisplay();

            foreach (var filler in GetAllFillers())
            {
                filler.Fill(DisplayData);
                filler.OnRevalidate += RevalidateIfNeeded;
            }

            RectHelper.SetAnchorsForGrid(_rectTransform);
            transform.SetAsLastSibling();
            OnSetDisplay();
        }

        public void Reset()
        {
            gameObject.SetActive(false);
            DisplayData = null;

            foreach (var filler in GetAllFillers())
            {
                filler.FillReset();
                filler.OnRevalidate -= RevalidateIfNeeded;
            }

            Sort();
            OnResetDisplay();
        }

        protected virtual void OnAwakeDisplay()
        {
        }

        protected virtual void OnSetDisplay()
        {
        }

        protected virtual void OnResetDisplay()
        {
        }

        public void Open(DisplayData inventoryDisplay)
        {
            gameObject.SetActive(true);
            RefreshUI();
        }

        public void Close(DisplayData inventoryDisplay)
        {
            Reset();
        }

        public void Sort()
        {
            if (!gameObject.activeSelf) return;

            transform.SetAsFirstSibling();
        }

        // TODO: Put this code in some where that can be used also in the Window class.
        private AbstractFiller[] GetAllFillers()
        {
            if (_fillers != null)
            {
                return _fillers;
            }

            _fillers = FillerHelper.GetAllFillersFromGameObject(gameObject);

            return _fillers;
        }

        private void RevalidateIfNeeded(AbstractFiller fillerTrigger, List<Type> components)
        {
            var fillers = GetAllFillers()
                .Where(filler => fillerTrigger != filler);

            foreach (var abstractFiller in fillers)
            {
                if (components.All(componentFillerType => componentFillerType != abstractFiller.GetType())) continue;

                abstractFiller.Fill(DisplayData);
                abstractFiller.OnRefreshUI();
            }

            if (_rectTransform == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        }

        private void RefreshUI()
        {
            foreach (var childrenFiller in GetAllFillers())
            {
                childrenFiller.OnRefreshUI();
            }

            if (_rectTransform == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        }

        protected void AddFiller(AbstractFiller newFiller)
        {
            var abstractFillers = _fillers
                .ToList();

            abstractFillers.Add(newFiller);

            _fillers = abstractFillers.ToArray();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Displays.Filler;
using Inventory.Scripts.Core.Displays.Filler.Primitives;
using Inventory.Scripts.Core.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Windows
{
    public abstract class Window : MonoBehaviour, IPointerDownHandler
    {
        public RectTransform RectTransform { get; private set; }

        public DisplayData DisplayData { get; private set; }

        private AbstractFiller[] _fillers;

        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
            _fillers = GetAllFillers();
        }

        private AbstractFiller[] GetAllFillers()
        {
            if (_fillers != null)
            {
                return _fillers;
            }

            _fillers = FillerHelper.GetAllFillersFromGameObject(gameObject);

            return _fillers;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransform.SetAsLastSibling();
        }

        public void Set(ItemTable itemTable)
        {
            DisplayData = new DisplayData(itemTable);

            foreach (var filler in GetAllFillers())
            {
                filler.Fill(DisplayData);
                filler.OnRevalidate += RevalidateIfNeeded;
            }

            OnOpenWindow();
            RefreshUI();
        }

        public void CloseWindow()
        {
            gameObject.SetActive(false);

            foreach (var filler in GetAllFillers())
            {
                filler.FillReset();
                filler.OnRevalidate -= RevalidateIfNeeded;
            }

            OnCloseWindow();
            DisplayData = null;
        }

        private void RefreshUI()
        {
            foreach (var childrenFiller in GetAllFillers())
            {
                childrenFiller.OnRefreshUI();
            }

            if (RectTransform == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
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

            if (RectTransform == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
        }

        public bool IsWindowOpenedWith(ItemTable itemTable)
        {
            return DisplayData != null && DisplayData.ItemContainer == itemTable;
        }

        protected virtual void OnOpenWindow()
        {
        }

        protected virtual void OnCloseWindow()
        {
        }
    }
}
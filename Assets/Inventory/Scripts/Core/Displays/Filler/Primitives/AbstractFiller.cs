using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventory.Scripts.Core.Displays.Filler.Primitives
{
    public abstract class AbstractFiller : MonoBehaviour
    {
        public event Action<AbstractFiller, List<Type>> OnRevalidate;
        protected DisplayData DisplayData;

        /// <summary>
        /// Execute once the filler is get by the DisplayFiller class. After executed will no longer execute again.
        /// </summary>
        public abstract void Init();

        public void Fill(DisplayData displayData)
        {
            if (ShouldFill(displayData))
            {
                DisplayData = displayData;
                OnSet(DisplayData);
            }
        }

        /// <summary>
        /// Execute when a Display filler or Window is opened. This is used to set the texts, sprites and actions once is opened.
        /// </summary>
        /// <param name="displayData">Display Data that was opened</param>
        protected abstract void OnSet(DisplayData displayData);

        public void FillReset()
        {
            OnReset();
        }

        /// <summary>
        /// Will execute once the Window or the Display filler is closed or removed.
        /// Normally the method implementation will be to set the values to empty or null again. 
        /// </summary>
        protected abstract void OnReset();

        /// <summary>
        /// This method executes after the SetActive is called, normally will be used to refresh some UI to auto resize.
        /// </summary>
        public virtual void OnRefreshUI()
        {
        }

        protected virtual bool ShouldFill(DisplayData displayData)
        {
            return displayData != null;
        }

        protected void Revalidate(List<Type> components)
        {
            OnRevalidate?.Invoke(this, components);
        }
    }
}
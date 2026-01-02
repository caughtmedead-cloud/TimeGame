using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Highlights.Global
{
    public abstract class HighlightGlobalProcessor : ScriptableObject
    {
        public void Process(HighlightGlobalContext ctx)
        {
            if (!ShouldProcess(ctx)) return;

            HandleProcess(ctx);
        }

        protected abstract void HandleProcess(HighlightGlobalContext ctx);

        protected virtual bool ShouldProcess(HighlightGlobalContext ctx)
        {
            return true;
        }
    }
}
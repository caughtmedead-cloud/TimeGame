using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Highlights
{
    public abstract class HighlightProcessor : ScriptableObject
    {
        public void Process(HighlighterContext ctx, HighlighterState state)
        {
            if (!ShouldProcess(ctx, state)) return;

            HandleProcess(ctx, state);
        }

        protected abstract void HandleProcess(HighlighterContext ctx, HighlighterState state);

        protected virtual bool ShouldProcess(HighlighterContext ctx, HighlighterState state)
        {
            return true;
        }
    }
}
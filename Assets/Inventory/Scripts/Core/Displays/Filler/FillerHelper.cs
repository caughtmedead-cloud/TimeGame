using System.Linq;
using Inventory.Scripts.Core.Displays.Filler.Primitives;
using UnityEngine;

namespace Inventory.Scripts.Core.Displays.Filler
{
    public abstract class FillerHelper
    {
        public static AbstractFiller[] GetAllFillersFromGameObject(GameObject gameObject)
        {
            var abstractFillers = gameObject.GetComponents<AbstractFiller>();
            var childrenFillers = gameObject.GetComponentsInChildren<AbstractFiller>();

            var fillers = abstractFillers.Union(childrenFillers).ToArray();

            foreach (var childrenFiller in fillers)
            {
                childrenFiller.Init();
            }

            return fillers;
        }
    }
}
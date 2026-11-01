using UnityEngine;

namespace Modules.Menus.Scripts
{
    public abstract class SelectorChoice : MonoBehaviour
    {
        public virtual void SetSelected(bool selected) { }
        public virtual void SetDimAmount(float amount) { }
    }
}

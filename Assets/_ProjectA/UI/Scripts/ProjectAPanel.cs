using UnityEngine;

namespace ProjectA.UI
{
    public class ProjectAPanel : MonoBehaviour
    {
        public virtual void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }
    }
}

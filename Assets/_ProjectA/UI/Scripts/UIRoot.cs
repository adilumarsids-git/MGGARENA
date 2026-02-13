using UnityEngine;

namespace ProjectA.UI
{
    public class UIRoot : MonoBehaviour
    {
        private static UIRoot _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}

using UnityEngine;

namespace ProjectA.UI
{
    public class UIRoot : MonoBehaviour
    {
        private static UIRoot _instance;

        private void Awake()
        {
            if (_instance == null || !_instance)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                return;
            }

            if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}

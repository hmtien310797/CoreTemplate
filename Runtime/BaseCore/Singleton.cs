using UnityEngine;

namespace BaseCore
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance != null) return _instance;
            
                _instance = FindFirstObjectByType<T>();
                if (_instance != null) return _instance;
            
                var go = new GameObject(typeof(T).Name);
                _instance = go.AddComponent<T>();
                return _instance;
            }
        }

        protected virtual bool DontDestroyOnLoadEnabled => true;

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;

                if (DontDestroyOnLoadEnabled)
                    DontDestroyOnLoad(gameObject);

                OnSingletonAwake();
                return;
            }
        
            if (_instance != this)
                Destroy(gameObject);
        }
    
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
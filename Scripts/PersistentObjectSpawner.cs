using BaseCore;
using BaseCore.Setting;
using BaseCore.Sound;
using UnityEngine;


public static class PersistentObjectSpawner
{
    private const string ROOT_NAME = "[PersistentRoot]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Spawn()
    {
        if (GameObject.Find(ROOT_NAME) != null) return;

        var root = new GameObject(ROOT_NAME);
        Object.DontDestroyOnLoad(root);
        
        SpawnUnderRoot<SettingManager>(root.transform, "Setting/SettingManager");
        SpawnUnderRoot<SoundManager>(root.transform, "Sound/SoundManager");
    }

    private static T SpawnUnderRoot<T>(Transform parent, string resourcesPath) where T : Component
    {
        var prefab = Resources.Load<GameObject>(resourcesPath);
        if (prefab == null)
        {
            Debug.LogError($"Missing prefab: Resources/{resourcesPath}.prefab");
            return null;
        }

        var go = Object.Instantiate(prefab, parent);
        go.name = prefab.name;

        var comp = go.GetComponent<T>();
        if (comp == null)
        {
            Debug.LogError($"Prefab {prefab.name} missing component {typeof(T).Name}");
            return null;
        }
        
        Services.Register(comp);
        return comp;
    }
}
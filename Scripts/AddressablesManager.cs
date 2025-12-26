#if HAS_ADDRESSABLES && HAS_UNITASK
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BaseCore
{
    public static class AddressableManager 
    {

        // =========================================================
        // LOAD ASSET
        // =========================================================

        /// <summary>
        /// Load asset theo address (Sprite, Texture, AudioClip, Prefab, ...)
        /// Không cache – mỗi lần gọi là một handle mới.
        /// </summary>
        public static async UniTask<T> LoadAsync<T>(string address) where T : Object
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[AddressablesManager] Address is null or empty.");
                return null;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[AddressablesManager] Load failed: {address}\n{handle.OperationException}");
                Addressables.Release(handle);
                return null;
            }

            return handle.Result;
        }

        /// <summary>
        /// Release asset đã load bằng LoadAsync.
        /// </summary>
        public static void ReleaseAsset<T>(T asset) where T : Object
        {
            if (asset == null) return;
            Addressables.Release(asset);
        }

        // =========================================================
        // INSTANTIATE / RELEASE INSTANCE
        // =========================================================

        /// <summary>
        /// Instantiate prefab theo address.
        /// </summary>
        public static async UniTask<GameObject> InstantiateAsync(
            string address,
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError("[AddressablesManager] Address is null or empty.");
                return null;
            }

            AsyncOperationHandle<GameObject> handle;

            if (parent != null)
                handle = Addressables.InstantiateAsync(address, position, rotation, parent);
            else
                handle = Addressables.InstantiateAsync(address, position, rotation);

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[AddressablesManager] Instantiate failed: {address}\n{handle.OperationException}");
                Addressables.Release(handle);
                return null;
            }

            return handle.Result;
        }

        /// <summary>
        /// Release instance đã instantiate.
        /// </summary>
        public static void ReleaseInstance(GameObject instance)
        {
            if (instance == null) return;
            Addressables.ReleaseInstance(instance);
        }

        // =========================================================
        // CONVENIENCE APIs (cho UI)
        // =========================================================

        public static UniTask<Sprite> LoadSpriteAsync(string address)
            => LoadAsync<Sprite>(address);

        public static UniTask<Texture2D> LoadTextureAsync(string address)
            => LoadAsync<Texture2D>(address);

        public static UniTask<AudioClip> LoadAudioAsync(string address)
            => LoadAsync<AudioClip>(address);
    }
}
#endif

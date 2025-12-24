using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Grommel.Addressables
{
    public interface IAddressablesLoader
    {
        Task<T> LoadAssetAsync<T>(string key);
    }

    public class AddressablesLoader : IAddressablesLoader
    {
        public async Task<T> LoadAssetAsync<T>(string key)
        {
            AsyncOperationHandle<T> handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(key);
            try
            {
                return await handle.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Addressables failed to load '{key}': {ex.Message}");
                return default;
            }
            finally
            {
                UnityEngine.AddressableAssets.Addressables.Release(handle);
            }
        }
    }
}

using File_manager.Interfaces;
using File_manager.Models;

namespace File_manager.Interfaces
{
    // Розширення IRepository специфічне для асетів
    public interface IAssetRepository : IRepository<IAsset>
    {
        Dictionary<string, IAsset> LoadAsDictionary();
        void Remove(IAsset item);
    }
}
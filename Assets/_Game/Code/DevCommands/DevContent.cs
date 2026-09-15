using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;

namespace Game.DevCommands
{
    internal sealed class DevContent
    {
        public const string CatalogPath = "Assets/_Game/Data/FallacyCatalog.asset";
        public const string FallbackLibraryPath = "Assets/_Game/Data/FallbackLibrary.asset";
        public const string Unavailable = "content catalog is only available inside the Unity Editor";

        private DevContent(FallacyCatalogSO catalog, FallbackLibrarySO library)
        {
            Catalog = catalog;
            Library = library;
            RefutableIds = catalog.RefutableIds.ToList();
            ParadoxIds = catalog.All.Where(fallacy => !fallacy.IsRefutable).Select(fallacy => fallacy.Id).ToList();
            Validator = new ArgumentValidator(RefutableIds);
        }

        public FallacyCatalogSO Catalog { get; }

        public FallbackLibrarySO Library { get; }

        public IReadOnlyList<string> RefutableIds { get; }

        public IReadOnlyList<string> ParadoxIds { get; }

        public ArgumentValidator Validator { get; }

        public static bool TryLoad(out DevContent content)
        {
            content = null;
#if UNITY_EDITOR
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(CatalogPath);
            var library = UnityEditor.AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(FallbackLibraryPath);
            if (catalog != null && library != null)
            {
                content = new DevContent(catalog, library);
            }
#endif
            return content != null;
        }
    }
}

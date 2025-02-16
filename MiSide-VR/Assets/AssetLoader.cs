using System.IO;
using BepInEx;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MiSide_VR.Stripped;
using UnityEngine;
using static MiSide_VR.Plugin;
using AssetBundle = MiSide_VR.Stripped.AssetBundle;

namespace MiSide_VR.Assets {
    public static class AssetLoader {
        private const string assetsDir = "MiSide_VR/AssetBundles/";
        private static Il2CppReferenceArray<Object> assets;

        public static GameObject VRCameraRig { get; private set; }

        public static void LoadAssets() {
            var assetbundle = LoadBundle("vrassets");
            VRCameraRig = LoadAsset<GameObject>("[VRCameraRig]");

        }

        public static T LoadAsset<T>(string name) where T : Object {
            foreach (var asset in assets) {
                if (asset.name == name) {
                    return asset.Cast<T>();
                }
            }
            return null;
        }

        private static AssetBundle LoadBundle(string assetName) {
            var bundlepath = Path.Combine(Paths.PluginPath, Path.Combine(assetsDir, assetName));
            var myLoadedAssetBundle = AssetBundle.LoadFromFile(bundlepath);
            if (myLoadedAssetBundle == null) {
                Log.Error($"Failed to load AssetBundle {assetName}");
                return null;
            }

            assets = myLoadedAssetBundle.LoadAllAssets();
            foreach (var asset in assets) {
                asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }

            return myLoadedAssetBundle;
        }

    }
}
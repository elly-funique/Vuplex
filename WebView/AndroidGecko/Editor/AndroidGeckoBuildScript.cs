// Copyright (c) 2022 Vuplex Inc. All rights reserved.
//
// Licensed under the Vuplex Commercial Software Library License, you may
// not use this file except in compliance with the License. You may obtain
// a copy of the License at
//
//     https://vuplex.com/commercial-library-license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#if UNITY_ANDROID
#pragma warning disable CS0618
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView.Editor {

    /// <summary>
    /// Pre-build script that does the following:
    /// - validates the project's Graphics API settings.
    /// - deletes an old Assets/Plugins/Android/assets/vuplex-webview-gecko-extension
    ///   directory from a previous version of 3D WebView if it exists.
    /// </summary>
    public class AndroidGeckoBuildScript : IPreprocessBuild {

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildTarget buildTarget, string buildPath) {

            if (buildTarget != BuildTarget.Android) {
                return;
            }
            EditorUtils.ValidateAndroidGraphicsApi();
            EditorUtils.ForceAndroidInternetPermission();
            EditorUtils.AssertThatOculusLowOverheadModeIsDisabled();
            EditorUtils.AssertThatSrpBatcherIsDisabled();
            _deleteOldExtensionDirectoryIfNeeded();
            _setNativePluginsToPreloaded();
        }

        const string OLD_EXTENSION_DIRECTORY_NAME = "vuplex-webview-gecko-extension";

        /// <summary>
        /// AndroidGeckoBuildScript.cs used to copy a vuplex-webview-gecko-extension folder for 3D WebView's
        /// built-in Gecko extension to the Assets/Plugins/Android/assets folder to make Unity include it in
        /// the APK's assets, but Unity 2021.2 removed the ability to include assets like that and now fails
        /// the build if the Assets/Plugins/Android/assets folder exists. So, the extension is now included
        /// in an AAR file, and this method deletes the Assets/Plugins/Android/assets/vuplex-webview-gecko-extension
        /// directory if it exists.
        /// </summary>
        static void _deleteOldExtensionDirectoryIfNeeded() {

            var androidAssetsDirectoryPath = Path.Combine(Application.dataPath, "Plugins", "Android", "assets");
            var oldExtensionDirectoryPath = Path.Combine(androidAssetsDirectoryPath, OLD_EXTENSION_DIRECTORY_NAME);
            if (!Directory.Exists(oldExtensionDirectoryPath)) {
                return;
            }
            // Check if the assets directory contains other files or directories besides the vuplex-webview-gecko-extension folder and meta file.
            var otherFiles = Directory.GetFiles(androidAssetsDirectoryPath)
                                      .ToList()
                                      .Where(file => !(file.EndsWith(OLD_EXTENSION_DIRECTORY_NAME + ".meta") || file.EndsWith(".DS_Store")))
                                      .ToArray();
            var otherDirectories = Directory.GetDirectories(androidAssetsDirectoryPath)
                                            .ToList()
                                            .Where(directory => !directory.EndsWith(OLD_EXTENSION_DIRECTORY_NAME))
                                            .ToArray();
            var numberOfOtherFilesAndDirectories = otherFiles.Count() + otherDirectories.Count();
            if (numberOfOtherFilesAndDirectories > 0) {
                // The assets folder contains other files besides 3D WebView's old vuplex-webview-gecko-extension directory,
                // so only remove the vuplex-webview-gecko-extension directory.
                Directory.Delete(oldExtensionDirectoryPath, true);
                return;
            }
            // The assets directory only includes 3D WebView's old vuplex-webview-gecko-extension directory, so
            // the entire assets directory can be deleted.
            Directory.Delete(androidAssetsDirectoryPath, true);
        }

        /// <summary>
        /// Sets the libVuplexWebViewAndroidGecko.so plugin files to be preloaded, which is equivalent to
        /// enabling their "Load on Startup" checkbox. This is done via a script because the .meta files
        /// for these plugins was generated with an older version of Unity in order to be compatible with
        /// 2018.4, which doesn't support the preload option. Enabling preloading is required for Vulkan support.
        /// </summary>
        static void _setNativePluginsToPreloaded() {

            #if UNITY_2019_1_OR_NEWER
                var pluginAbsolutePaths = Directory.GetFiles(Application.dataPath, "libVuplexWebViewAndroidGecko.so", SearchOption.AllDirectories).ToList();
                // PluginImporter.GetAtPath() requires a relative path and doesn't support absolute paths.
                var pluginRelativePaths = pluginAbsolutePaths.Select(path => path.Replace(Application.dataPath, "Assets"));
                foreach (var filePath in pluginRelativePaths) {
                    var pluginImporter = (PluginImporter)PluginImporter.GetAtPath(filePath);
                    if (!pluginImporter.isPreloaded) {
                        pluginImporter.isPreloaded = true;
                        pluginImporter.SaveAndReimport();
                    }
                }
            #endif
        }
    }
}
#endif

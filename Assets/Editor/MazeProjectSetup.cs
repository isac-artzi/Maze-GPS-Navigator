// MazeProjectSetup — Editor menu "Maze" + command-line entry points.
//   Maze > 1. Set Up Project          scenes, build list, input handling, Android/Quest + OpenXR settings
//   Maze > 2. Generate Voice Clips    speaks every VoicePhrases entry into Assets/Resources/Voice/*.wav
//   Maze > 3. Build Desktop           macOS .app (on a Mac) or Windows .exe (on Windows)
//   Maze > 4. Build Quest APK         Android build with OpenXR + Meta Quest support
//   Maze > 5. Build & Install to Quest  build, then `adb install` + launch on a USB-connected headset
// Batch mode:  Unity -batchmode -quit -projectPath . -executeMethod MazeNav.EditorTools.MazeProjectSetup.SetupCLI
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Debug = UnityEngine.Debug;

namespace MazeNav.EditorTools
{
    public static class MazeProjectSetup
    {
        const string MainScene = "Assets/Scenes/Main.unity";
        const string MazeScene = "Assets/Scenes/Maze.unity";
        const string VoiceDir = "Assets/Resources/Voice";
        const string AppId = "com.isacartzi.mazenavigator";
        static string[] Scenes => new[] { MainScene, MazeScene };

        // =====================================================================================
        [MenuItem("Maze/1. Set Up Project (scenes, XR, player settings)", priority = 1)]
        public static void Setup()
        {
            CreateMaterials();
            CreateScenes();
            ConfigurePlayer();
            ConfigureXR();
            AssetDatabase.SaveAssets();
            Debug.Log("[MazeNav] Project set up. Open Assets/Scenes/Main and press Play.");
        }

        /// Material assets in Resources guarantee their shaders (and variants) ship in player builds.
        static void CreateMaterials()
        {
            Directory.CreateDirectory("Assets/Resources/Materials");
            MakeMaterialAsset("Assets/Resources/Materials/MazeLit.mat", false);
            MakeMaterialAsset("Assets/Resources/Materials/MazeLitEmissive.mat", true);
        }

        static void MakeMaterialAsset(string path, bool emissive)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var m = new Material(Shader.Find("Standard"));
            m.SetFloat("_Glossiness", 0.1f);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.05f, 0.6f, 0.2f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(m, path);
        }

        static void CreateScenes()
        {
            Directory.CreateDirectory("Assets/Scenes");
            MakeScene(MainScene, "Main Menu", typeof(MainMenu));
            MakeScene(MazeScene, "Maze Game", typeof(MazeGame));
            EditorBuildSettings.scenes = Scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
            EditorSceneManager.OpenScene(MainScene);
        }

        static void MakeScene(string path, string rootName, System.Type component)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            new GameObject(rootName).AddComponent(component);
            EditorSceneManager.SaveScene(scene, path);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Isac Artzi";
            PlayerSettings.productName = "Maze Navigator";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            // Quest: Android 12L (API 32)+, 64-bit IL2CPP, Vulkan, landscape.
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Use the Input System package for keyboard/UI, keep the legacy manager available too ("Both").
            var projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (projectSettings != null)
            {
                var so = new SerializedObject(projectSettings);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 2) { handler.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        static void ConfigureXR()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            // Quest: XR on from the first frame. Desktop: XR starts only if the player picks VR.
            AssignOpenXR(perTarget, BuildTargetGroup.Android, initOnStart: true);
            AssignOpenXR(perTarget, BuildTargetGroup.Standalone, initOnStart: false);

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            var androidXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            Enable<MetaQuestFeature>(androidXR);
            Enable<OculusTouchControllerProfile>(androidXR);
            var desktopXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            Enable<OculusTouchControllerProfile>(desktopXR);
            EditorUtility.SetDirty(perTarget);
        }

        static void AssignOpenXR(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group, bool initOnStart)
        {
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var settings = perTarget.SettingsForBuildTarget(group);
            settings.InitManagerOnStart = initOnStart;
            XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
            EditorUtility.SetDirty(settings);
        }

        static void Enable<T>(OpenXRSettings settings) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            if (settings == null) { Debug.LogWarning("[MazeNav] OpenXR settings missing."); return; }
            var f = settings.GetFeature<T>();
            if (f == null) { Debug.LogWarning($"[MazeNav] OpenXR feature {typeof(T).Name} not found."); return; }
            f.enabled = true;
            EditorUtility.SetDirty(f);
        }

        // =====================================================================================
        [MenuItem("Maze/2. Generate Voice Clips", priority = 2)]
        public static void GenerateVoice()
        {
            Directory.CreateDirectory(VoiceDir);
            bool mac = Application.platform == RuntimePlatform.OSXEditor;
            string voice = EditorPrefs.GetString("MazeNav.Voice", "Samantha");
            int made = 0;
            foreach (var kv in VoicePhrases.All)
            {
                string wav = Path.GetFullPath(Path.Combine(VoiceDir, kv.Key + ".wav"));
                string text = kv.Value.Replace("\"", "");
                bool ok = mac
                    ? Run("/usr/bin/say", $"-v \"{voice}\" -r 180 -o \"{wav}\" --data-format=LEI16@22050 \"{text}\"")
                    : Run("powershell", "-NoProfile -Command \"Add-Type -AssemblyName System.Speech; " +
                          "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Rate = 1; " +
                          $"$s.SetOutputToWaveFile('{wav}'); $s.Speak('{text.Replace("'", "''")}'); $s.Dispose()\"");
                if (ok) made++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[MazeNav] Generated {made}/{VoicePhrases.All.Count} voice clips in {VoiceDir}.");
        }

        // =====================================================================================
        [MenuItem("Maze/3. Build Desktop", priority = 20)]
        public static bool BuildDesktop()
        {
            bool mac = Application.platform == RuntimePlatform.OSXEditor;
            var target = mac ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64;
            string path = mac ? "Builds/Desktop/MazeNavigator.app" : "Builds/Desktop/MazeNavigator.exe";
            return Build(target, BuildTargetGroup.Standalone, path);
        }

        [MenuItem("Maze/4. Build Quest APK", priority = 21)]
        public static bool BuildQuest() => Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Quest/MazeNavigator.apk");

        [MenuItem("Maze/5. Build && Install to Quest", priority = 22)]
        public static void BuildAndInstallQuest()
        {
            if (!BuildQuest()) return;
            InstallOnQuest();
        }

        /// Called (via reflection) by the start screen's "Play on Meta Quest" button in Play mode.
        public static void BuildAndInstallAfterPlayMode()
        {
            void Handler(PlayModeStateChange s)
            {
                if (s != PlayModeStateChange.EnteredEditMode) return;
                EditorApplication.playModeStateChanged -= Handler;
                EditorApplication.delayCall += BuildAndInstallQuest;
            }
            EditorApplication.playModeStateChanged += Handler;
            EditorApplication.ExitPlaymode();
        }

        static bool Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes, locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None
            });
            var s = report.summary;
            Debug.Log($"[MazeNav] Build {target}: {s.result}, {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB -> {path}");
            return s.result == BuildResult.Succeeded;
        }

        // =====================================================================================
        static string AdbPath()
        {
            // The Android SDK that Unity Hub installs lives next to the Editor.
            string editor = EditorApplication.applicationPath;   // .../Unity.app  or  .../Editor/Unity.exe
            string[] candidates =
            {
                Path.Combine(Path.GetDirectoryName(editor) ?? "", "PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"),
                Path.Combine(Path.GetDirectoryName(editor) ?? "", "Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"),
            };
            return candidates.FirstOrDefault(File.Exists) ?? "adb";
        }

        public static bool InstallOnQuest()
        {
            string adb = AdbPath();
            string apk = Path.GetFullPath("Builds/Quest/MazeNavigator.apk");
            if (!Run(adb, "devices -l", out string devices) || !devices.Split('\n').Skip(1).Any(l => l.Contains("device ")))
            {
                Debug.LogError("[MazeNav] No Quest found. Plug it in with USB, enable Developer Mode, and accept 'Allow USB debugging' in the headset.");
                return false;
            }
            if (!Run(adb, $"install -r \"{apk}\"")) return false;
            Run(adb, $"shell monkey -p {AppId} -c android.intent.category.LAUNCHER 1");
            Debug.Log("[MazeNav] Installed and launched on the Quest. Put on the headset!");
            return true;
        }

        static bool Run(string exe, string args) => Run(exe, args, out _);

        static bool Run(string exe, string args, out string output)
        {
            output = "";
            try
            {
                var p = Process.Start(new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                });
                output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) Debug.LogWarning($"[MazeNav] {Path.GetFileName(exe)} {args}\n{output}");
                return p.ExitCode == 0;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MazeNav] Could not run {exe}: {e.Message}");
                return false;
            }
        }

        // =====================================================================================
        // Command-line wrappers (exit code 1 on failure so scripts/CI can tell).
        public static void SetupCLI() { Setup(); }
        public static void VoiceCLI() { GenerateVoice(); }
        public static void BuildDesktopCLI() { if (!BuildDesktop()) EditorApplication.Exit(1); }
        public static void BuildQuestCLI() { if (!BuildQuest()) EditorApplication.Exit(1); }
        public static void InstallQuestCLI() { if (!InstallOnQuest()) EditorApplication.Exit(1); }
    }
}

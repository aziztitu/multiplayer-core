using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Azeesoft.Multiplayer.Editor
{
    class NetworkSerializationGeneratorAssetProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (NetworkSerializationGeneratorRunner.ShouldAutoGenerate(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths))
                NetworkSerializationGeneratorRunner.ScheduleGenerate();
        }
    }

    public static class NetworkSerializationGeneratorRunner
    {
        const string MenuPath = "Tools/AZ/Generate Network Serialization";
        const string PendingKey = "Azeesoft.Multiplayer.NetCodeGen.Pending";
        const int MassImportSkipThreshold = 25;

        static bool s_Running;

        [InitializeOnLoadMethod]
        static void ResumePendingAfterReload()
        {
            if (SessionState.GetBool(PendingKey, false))
                EditorApplication.delayCall += RunWhenIdle;
        }

        [MenuItem(MenuPath)]
        public static void GenerateFromMenu()
        {
            RunGenerator();
        }

        public static void ScheduleGenerate()
        {
            if (SessionState.GetBool(PendingKey, false))
                return;

            SessionState.SetBool(PendingKey, true);
            EditorApplication.delayCall += RunWhenIdle;
        }

        public static bool ShouldAutoGenerate(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            int candidates = 0;
            bool attributedChange = false;

            foreach (var path in importedAssets)
            {
                if (!IsUserScript(path))
                    continue;
                candidates++;
                if (ScriptHasAttribute(path))
                    attributedChange = true;
            }

            foreach (var path in deletedAssets)
            {
                if (!IsUserScript(path))
                    continue;
                candidates++;
                if (GeneratedFileExistsForScript(path))
                    attributedChange = true;
            }

            foreach (var path in movedAssets)
            {
                if (!IsUserScript(path))
                    continue;
                candidates++;
                if (ScriptHasAttribute(path) || GeneratedFileExistsForScript(path))
                    attributedChange = true;
            }

            if (!attributedChange || candidates == 0)
                return false;

            // Project-open / mass reimport — use the menu if you pasted a lot of files at once.
            return candidates <= MassImportSkipThreshold;
        }

        static void RunWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunWhenIdle;
                return;
            }

            SessionState.SetBool(PendingKey, false);
            RunGenerator();
        }

        static void RunGenerator()
        {
            if (s_Running)
                return;

            s_Running = true;
            try
            {
                RunGeneratorUnscoped();
            }
            finally
            {
                s_Running = false;
            }
        }

        static void RunGeneratorUnscoped()
        {
            if (!TryFindDotnet(out string dotnet, out string findError))
            {
                Debug.LogError($"[NetworkSerialization] {findError}");
                return;
            }

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NetworkSerializationGeneratorRunner).Assembly);
            if (packageInfo == null)
            {
                Debug.LogError("[NetworkSerialization] Could not locate com.azeesoft.multiplayer-core. Is the package installed?");
                return;
            }

            string toolRoot = Path.Combine(packageInfo.resolvedPath, "Tools~", "NetCodeGenerator");
            string csproj = Path.Combine(toolRoot, "NetCodeGenerator.csproj");
            if (!File.Exists(csproj))
            {
                Debug.LogError($"[NetworkSerialization] Generator project not found at {csproj}");
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string workDir = Path.Combine(projectRoot, "Temp", "NetCodeGenerator");
            string outDir = Path.Combine(workDir, "out");
            string objDir = Path.Combine(workDir, "obj").Replace('\\', '/') + "/";
            string inputFolder = Application.dataPath;
            string outputFolder = Path.Combine(Application.dataPath, "Generated");
            string dll = Path.Combine(outDir, "NetCodeGenerator.dll");

            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(objDir);

            if (!TryCopyUnityDependencies(outDir, projectRoot, out string missing))
            {
                Debug.LogError($"[NetworkSerialization] Missing dependency DLL: {missing}. Open the project once so Unity compiles Netcode/Collections, then try again.");
                return;
            }

            Directory.CreateDirectory(outputFolder);

            if (!GeneratorBuildIsUpToDate(dll, toolRoot, csproj))
            {
                Debug.Log($"[NetworkSerialization] Building generator with {dotnet}...");
                if (!RunDotnet(
                        dotnet,
                        $"build \"{csproj}\" -c Release --nologo -o \"{outDir}\" -p:BaseIntermediateOutputPath=\"{objDir}\"",
                        workDir,
                        out string buildOutput))
                {
                    Debug.LogError($"[NetworkSerialization] dotnet build failed.\n{buildOutput}");
                    return;
                }
            }

            if (!File.Exists(dll))
            {
                Debug.LogError($"[NetworkSerialization] Built generator not found at {dll}");
                return;
            }

            if (!RunDotnet(dotnet, $"exec \"{dll}\" \"{inputFolder}\" \"{outputFolder}\"", outDir, out string runOutput))
            {
                Debug.LogError($"[NetworkSerialization] Generator failed.\n{runOutput}");
                return;
            }

            bool wrote = runOutput.IndexOf("Wrote ", StringComparison.Ordinal) >= 0;
            if (wrote)
            {
                Debug.Log("[NetworkSerialization] Updated generated files.\n" + runOutput);
                EditorApplication.delayCall += () => AssetDatabase.Refresh();
            }
            else if (runOutput.Length > 0)
            {
                Debug.Log("[NetworkSerialization] Generated files already up to date.");
            }
        }

        static bool IsUserScript(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return false;
            if (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                return false;
            if (path.IndexOf("/Generated/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }

        static bool ScriptHasAttribute(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", assetPath));
                if (!File.Exists(fullPath))
                    return false;
                return File.ReadAllText(fullPath).IndexOf("GenerateNetworkSerialization", StringComparison.Ordinal) >= 0;
            }
            catch
            {
                return false;
            }
        }

        static bool GeneratedFileExistsForScript(string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(name))
                return false;
            return File.Exists(Path.Combine(Application.dataPath, "Generated", name + "_NetworkSerialization.g.cs"));
        }

        static bool GeneratorBuildIsUpToDate(string dll, string toolRoot, string csproj)
        {
            if (!File.Exists(dll))
                return false;

            DateTime dllTime = File.GetLastWriteTimeUtc(dll);
            if (File.GetLastWriteTimeUtc(csproj) > dllTime)
                return false;

            foreach (var file in Directory.GetFiles(toolRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(file) > dllTime)
                    return false;
            }

            return true;
        }

        static bool TryFindDotnet(out string dotnet, out string error)
        {
            dotnet = null;
            error = null;

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string programW6432 = Environment.GetEnvironmentVariable("ProgramW6432");
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] candidates =
            {
                Path.Combine(programFiles, "dotnet", "dotnet.exe"),
                !string.IsNullOrEmpty(programW6432) ? Path.Combine(programW6432, "dotnet", "dotnet.exe") : null,
                Path.Combine(programFilesX86, "dotnet", "dotnet.exe"),
                Path.Combine(userProfile, ".dotnet", "dotnet.exe")
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    dotnet = candidate;
                    return true;
                }
            }

            error = "Could not find the .NET SDK (dotnet.exe). Install .NET 8+ from https://dotnet.microsoft.com/download and restart Unity. Unity Hub does not always put dotnet on PATH.";
            return false;
        }

        static bool TryCopyUnityDependencies(string outDir, string projectRoot, out string missing)
        {
            missing = null;
            string dest = Path.Combine(outDir, "Dependencies");
            Directory.CreateDirectory(dest);

            string data = EditorApplication.applicationContentsPath;
            string[] engineCandidates =
            {
                Path.Combine(data, "Managed", "UnityEngine.dll"),
                Path.Combine(data, "Managed", "UnityEngine", "UnityEngine.dll")
            };
            string[] coreCandidates =
            {
                Path.Combine(data, "Managed", "UnityEngine", "UnityEngine.CoreModule.dll"),
                Path.Combine(data, "Managed", "UnityEngine.CoreModule.dll")
            };

            string assemblies = Path.Combine(projectRoot, "Library", "ScriptAssemblies");
            var required = new[]
            {
                ("UnityEngine.dll", engineCandidates),
                ("UnityEngine.CoreModule.dll", coreCandidates),
                ("Unity.Collections.dll", new[] { Path.Combine(assemblies, "Unity.Collections.dll") }),
                ("Unity.Netcode.Runtime.dll", new[] { Path.Combine(assemblies, "Unity.Netcode.Runtime.dll") })
            };

            foreach (var (fileName, candidates) in required)
            {
                string found = Array.Find(candidates, File.Exists);
                if (found == null)
                {
                    missing = fileName;
                    return false;
                }

                File.Copy(found, Path.Combine(dest, fileName), overwrite: true);
            }

            return true;
        }

        static bool RunDotnet(string dotnet, string arguments, string workingDirectory, out string output)
        {
            var combined = new StringBuilder();

            var startInfo = new ProcessStartInfo
            {
                FileName = dotnet,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            string dotnetDir = Path.GetDirectoryName(dotnet);
            if (!string.IsNullOrEmpty(dotnetDir))
            {
                startInfo.EnvironmentVariables["DOTNET_ROOT"] = dotnetDir;
                string path = startInfo.EnvironmentVariables["PATH"] ?? "";
                if (!path.StartsWith(dotnetDir, StringComparison.OrdinalIgnoreCase))
                    startInfo.EnvironmentVariables["PATH"] = dotnetDir + Path.PathSeparator + path;
            }

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    output = "Failed to start dotnet.";
                    return false;
                }

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (combined)
                            combined.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (combined)
                            combined.AppendLine(e.Data);
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                output = combined.ToString();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }
    }
}

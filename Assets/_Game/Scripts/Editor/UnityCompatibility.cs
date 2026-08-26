using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RogueArena.EditorTools
{
    /// <summary>
    /// Reads the repository's compatibility contract and verifies the current
    /// editor, project marker and direct package pins. The project deliberately
    /// keeps its serialized baseline at the oldest supported editor so every
    /// listed Unity version can import it safely.
    /// </summary>
    public static class UnityCompatibility
    {
        const string ContractPath = "Tools/UnityCompatibility.json";

        [Serializable]
        sealed class Contract
        {
            public string baselineVersion;
            public SupportedEditor[] supportedEditors;
            public PackagePin[] packages;
        }

        [Serializable]
        sealed class SupportedEditor
        {
            public string version;
            public string revision;
        }

        [Serializable]
        sealed class PackagePin
        {
            public string name;
            public string version;
        }

        static Contract cachedContract;

        public static string SupportedVersionList
        {
            get
            {
                if (!TryLoadContract(out Contract contract, out _)) return "unavailable";
                var versions = new string[contract.supportedEditors.Length];
                for (int i = 0; i < versions.Length; i++)
                    versions[i] = contract.supportedEditors[i].version;
                return string.Join(", ", versions);
            }
        }

        public static bool IsCurrentEditorSupported
        {
            get
            {
                if (!TryLoadContract(out Contract contract, out _)) return false;
                return FindEditor(contract, Application.unityVersion) != null;
            }
        }

        /// <summary>Runs all version/package compatibility checks.</summary>
        public static bool Validate(Action<string> info, Action<string> error)
        {
            if (!TryLoadContract(out Contract contract, out string loadError))
            {
                error(loadError);
                return false;
            }

            bool valid = true;
            SupportedEditor current = FindEditor(contract, Application.unityVersion);
            if (current == null)
            {
                valid = false;
                error($"Unity {Application.unityVersion} is not in the supported matrix ({SupportedVersionList}).");
            }
            else
            {
                info($"supported Unity editor: {current.version} ({current.revision})");
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
            {
                error("could not locate the project root for compatibility checks.");
                return false;
            }

            string projectVersionPath = Path.Combine(root, "ProjectSettings/ProjectVersion.txt");
            if (!File.Exists(projectVersionPath))
            {
                valid = false;
                error("ProjectSettings/ProjectVersion.txt is missing.");
            }
            else
            {
                string marker = File.ReadAllText(projectVersionPath);
                Match versionMatch = Regex.Match(marker, @"^m_EditorVersion:\s*(\S+)", RegexOptions.Multiline);
                Match revisionMatch = Regex.Match(marker,
                    @"^m_EditorVersionWithRevision:\s*\S+\s+\(([0-9a-fA-F]+)\)", RegexOptions.Multiline);
                string markerVersion = versionMatch.Success ? versionMatch.Groups[1].Value : string.Empty;
                string markerRevision = revisionMatch.Success ? revisionMatch.Groups[1].Value : string.Empty;
                SupportedEditor markedEditor = FindEditor(contract, markerVersion);

                if (markedEditor == null)
                {
                    valid = false;
                    error($"project version marker '{markerVersion}' is not a supported editor.");
                }
                else if (!string.Equals(markerRevision, markedEditor.revision, StringComparison.OrdinalIgnoreCase))
                {
                    valid = false;
                    error($"project version marker revision '{markerRevision}' does not match " +
                          $"{markerVersion} ({markedEditor.revision}).");
                }
                else
                {
                    info($"project marker: {markerVersion} ({markerRevision})");
                }
            }

            string manifestPath = Path.Combine(root, "Packages/manifest.json");
            if (!File.Exists(manifestPath))
            {
                valid = false;
                error("Packages/manifest.json is missing.");
            }
            else
            {
                string manifest = File.ReadAllText(manifestPath);
                foreach (PackagePin package in contract.packages)
                {
                    string pattern = $"\\\"{Regex.Escape(package.name)}\\\"\\s*:\\s*" +
                                     $"\\\"{Regex.Escape(package.version)}\\\"";
                    if (Regex.IsMatch(manifest, pattern))
                        info($"package pin: {package.name}@{package.version}");
                    else
                    {
                        valid = false;
                        error($"package {package.name} must be pinned to {package.version} for the shared version matrix.");
                    }
                }
            }

            return valid;
        }

        [MenuItem("Tools/Rogue Arena/Unity Compatibility")]
        public static void ShowCompatibility()
        {
            var report = new StringBuilder();
            bool valid = Validate(
                message => report.AppendLine("ok      " + message),
                message => report.AppendLine("ERROR   " + message));

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog(
                "Rogue Arena — Unity Compatibility",
                (valid ? "Compatibility checks passed.\n\n" : "Compatibility checks failed. See the Console.\n\n") +
                "Supported editors:\n" + SupportedVersionList,
                "OK");
        }

        static bool TryLoadContract(out Contract contract, out string error)
        {
            if (cachedContract != null)
            {
                contract = cachedContract;
                error = null;
                return true;
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string path = string.IsNullOrEmpty(root) ? null : Path.Combine(root, ContractPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                contract = null;
                error = $"Unity compatibility contract is missing: {ContractPath}";
                return false;
            }

            try
            {
                contract = JsonUtility.FromJson<Contract>(File.ReadAllText(path));
                if (contract == null || string.IsNullOrEmpty(contract.baselineVersion) ||
                    contract.supportedEditors == null || contract.supportedEditors.Length == 0 ||
                    contract.packages == null || contract.packages.Length == 0)
                {
                    error = $"Unity compatibility contract is invalid: {ContractPath}";
                    return false;
                }

                cachedContract = contract;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                contract = null;
                error = $"Could not read {ContractPath}: {exception.Message}";
                return false;
            }
        }

        static SupportedEditor FindEditor(Contract contract, string version)
        {
            if (contract?.supportedEditors == null) return null;
            foreach (SupportedEditor editor in contract.supportedEditors)
            {
                if (editor != null && string.Equals(editor.version, version, StringComparison.Ordinal))
                    return editor;
            }
            return null;
        }
    }

    [InitializeOnLoad]
    static class UnityCompatibilityNotice
    {
        static UnityCompatibilityNotice()
        {
            EditorApplication.delayCall += WarnForUnsupportedEditor;
        }

        static void WarnForUnsupportedEditor()
        {
            if (UnityCompatibility.IsCurrentEditorSupported) return;

            string sessionKey = "RogueArena.UnityCompatibilityWarning." + Application.unityVersion;
            if (SessionState.GetBool(sessionKey, false)) return;
            SessionState.SetBool(sessionKey, true);
            Debug.LogWarning($"Rogue Arena does not support Unity {Application.unityVersion}. " +
                             $"Use one of: {UnityCompatibility.SupportedVersionList}.");
        }
    }
}

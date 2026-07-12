using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ArchiveNull.EditorTools
{
    /// <summary>
    /// Performs the one-time migration from a mixed Assets root to the ArchiveNull project tree.
    /// AssetDatabase moves preserve GUID references used by scenes, prefabs and materials.
    /// </summary>
    public static class ProjectStructureOrganizer
    {
        private const string Root = "Assets/ArchiveNull";

        [MenuItem("Archive Null/Tools/Organize Project Structure")]
        public static void Run()
        {
            EnsureFolder(Root);

            Dictionary<string, string> rootMoves = new()
            {
                ["Assets/Audio"] = $"{Root}/Audio",
                ["Assets/images"] = $"{Root}/Art/UI/Icons",
                ["Assets/Materiales"] = $"{Root}/Art/Materials",
                ["Assets/Prefabs"] = $"{Root}/Prefabs",
                ["Assets/Scenes"] = $"{Root}/Scenes",
                ["Assets/Scripts"] = $"{Root}/Scripts",
                ["Assets/Shaders"] = $"{Root}/Shaders",
                ["Assets/Settings"] = $"{Root}/Settings",
                ["Assets/Editor"] = $"{Root}/Editor",
                ["Assets/InputSystem_Actions.inputactions"] = $"{Root}/Settings/InputSystem_Actions.inputactions",
                ["Assets/archive-null-icon2.png"] = $"{Root}/Art/Brand/archive-null-icon2.png",
                ["Assets/Archive_NULL_Caso_La_Llave_Por_Dentro.docx"] = $"{Root}/Documentation/Archive_NULL_Caso_La_Llave_Por_Dentro.docx",
                ["Assets/GDD_Archive_NULL-V3.docx"] = $"{Root}/Documentation/GDD_Archive_NULL-V3.docx",
                ["Assets/GDD_Archive_NULL-V3_BACKUP.docx"] = $"{Root}/Documentation/GDD_Archive_NULL-V3_BACKUP.docx",
                ["Assets/GDD_Archive_NULL-V4.docx"] = $"{Root}/Documentation/GDD_Archive_NULL-V4.docx",
                ["Assets/GDD_Archive_NULL-V4.md"] = $"{Root}/Documentation/GDD_Archive_NULL-V4.md"
            };

            foreach ((string source, string destination) in rootMoves)
            {
                MoveIfPresent(source, destination);
            }

            OrganizeLooseScripts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectStructureOrganizer] Archive Null assets organized successfully.");
        }

        private static void OrganizeLooseScripts()
        {
            Dictionary<string, string> categories = new()
            {
                ["ArchiveCompletionMarker.cs"] = "UI",
                ["CrimeSceneTutorial.cs"] = "Narrative",
                ["CRTMainMenuController.cs"] = "Office",
                ["CRTMenuCameraFocus.cs"] = "Office",
                ["CRTRetroSoundBank.cs"] = "Components/Audio",
                ["GameLocalization.cs"] = "Core",
                ["GameSaveSystem.cs"] = "Core",
                ["GlobalInputBindings.cs"] = "Core",
                ["GraphicsSettingsManager.cs"] = "Core",
                ["MemorySceneLoader.cs"] = "SceneFlow",
                ["OfficeDissolveTransition.cs"] = "SceneFlow",
                ["OfficeFreeLookController.cs"] = "Office",
                ["OfficeSpeakerTutorial.cs"] = "Narrative",
                ["PlayerAssistanceSettings.cs"] = "Core",
                ["RuntimeConfirmationDialog.cs"] = "UI",
                ["SceneRebuildTransition.cs"] = "SceneFlow",
                ["StandInteractionReticle.cs"] = "UI",
                ["VRHeadsetArchiveStarter.cs"] = "Office"
            };

            foreach ((string fileName, string category) in categories)
            {
                string source = $"{Root}/Scripts/{fileName}";
                string destination = $"{Root}/Scripts/{category}/{fileName}";
                MoveIfPresent(source, destination);
            }
        }

        private static void MoveIfPresent(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null && !AssetDatabase.IsValidFolder(source))
            {
                return;
            }

            int separator = destination.LastIndexOf("/", StringComparison.Ordinal);
            EnsureFolder(destination[..separator]);
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Could not move '{source}' to '{destination}': {error}");
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}

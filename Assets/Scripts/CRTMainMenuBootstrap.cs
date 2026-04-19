using UnityEngine;

namespace ArchiveNull.UI
{
    public static class CRTMainMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateMainMenu()
        {
            if (Object.FindAnyObjectByType<CRTMainMenuController>() != null)
            {
                return;
            }

            GameObject root = new("CRT Main Menu");
            root.AddComponent<CRTMainMenuController>();
        }
    }
}

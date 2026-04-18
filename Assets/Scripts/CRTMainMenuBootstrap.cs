using UnityEngine;

namespace ArchiveNull.UI
{
    public static class CRTMainMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateMainMenu()
        {
            if (Object.FindFirstObjectByType<CRTMainMenuController>() != null)
            {
                return;
            }

            GameObject root = new("CRT Main Menu");
            root.AddComponent<CRTMainMenuController>();
        }
    }
}

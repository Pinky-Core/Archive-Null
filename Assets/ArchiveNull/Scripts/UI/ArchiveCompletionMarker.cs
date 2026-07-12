using UnityEngine;

namespace ArchiveNull.UI
{
    public sealed class ArchiveCompletionMarker : MonoBehaviour
    {
        private const string PrefUnlockedArchive = "crt.archive.unlocked";

        [SerializeField] private CRTMainMenuController _menuController;
        [SerializeField] private int _archiveIndex;
        [SerializeField] private int _archiveCount = 4;

        public void MarkCompleted()
        {
            if (_menuController != null)
            {
                _menuController.MarkArchiveCompleted(_archiveIndex);
                return;
            }

            int currentUnlocked = PlayerPrefs.GetInt(PrefUnlockedArchive, 1);
            int nextUnlocked = Mathf.Clamp(_archiveIndex + 2, 1, Mathf.Max(1, _archiveCount));
            if (nextUnlocked > currentUnlocked)
            {
                PlayerPrefs.SetInt(PrefUnlockedArchive, nextUnlocked);
                PlayerPrefs.Save();
            }
        }
    }
}

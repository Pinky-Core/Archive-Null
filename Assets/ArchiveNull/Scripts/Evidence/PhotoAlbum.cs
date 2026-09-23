using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArchiveNull.Evidence
{
    // Photographs are observations. They do not enter evidence or unlock memories by count.
    public static class PhotoAlbum
    {
        public const int Capacity = 96;
        private static readonly List<EvidenceData> photos = new();
        public static IReadOnlyList<EvidenceData> Photos => photos;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { photos.Clear(); Changed = null; }

        public static bool Add(Sprite image, string scene, string subject)
        {
            if (image == null || photos.Count >= Capacity) return false;
            EvidenceData photo = ScriptableObject.CreateInstance<EvidenceData>();
            photo.evidenceId = "photo_" + Guid.NewGuid().ToString("N");
            photo.evidenceName = string.IsNullOrEmpty(subject) ? GameLocalization.Text("Fotografía de campo", "Field photograph") : subject;
            photo.description = scene + " · " + DateTime.Now.ToString("dd/MM HH:mm");
            photo.sourceSceneName = scene;
            photo.photoSprite = image;
            photos.Add(photo);
            Changed?.Invoke();
            return true;
        }

        public static void Remove(string id)
        {
            EvidenceData photo = photos.Find(p => p.evidenceId == id);
            if (photo == null) return;
            photos.Remove(photo);
            Changed?.Invoke();
            GameSaveSystem.SaveNow();
            GameSaveSystem.DeletePhotoFile(id);
            Release(photo);
        }

        public static void Restore(IEnumerable<EvidenceData> restored)
        {
            foreach (EvidenceData photo in photos) Release(photo);
            photos.Clear();
            if (restored != null) photos.AddRange(restored);
            Changed?.Invoke();
        }

        private static void Release(EvidenceData photo)
        {
            bool shared = false;
            if (EvidenceInventory.ExistingInstance != null)
                foreach (EvidenceData evidence in EvidenceInventory.Instance.GetAllEvidence())
                    if (evidence.photoSprite == photo.photoSprite) { shared = true; break; }
            if (!shared && photo.photoSprite != null)
            {
                UnityEngine.Object.Destroy(photo.photoSprite.texture);
                UnityEngine.Object.Destroy(photo.photoSprite);
            }
            UnityEngine.Object.Destroy(photo);
        }
    }
}

using UnityEngine;
using ArchiveNull.Evidence;

namespace ArchiveNull.UI
{
    public sealed partial class CRTMainMenuController
    {
        private ModernOfficeDesktop desktop;
        private void InstallModernDesktop()
        {
            // Preserve serialized archive/event references while retiring the CRT presentation.
            foreach (Transform child in _screen) child.gameObject.SetActive(false);
            _screenImage.color = new Color(.025f,.055f,.10f);
            var label = CreateText("Desktop standby", _screen, "ARCHIVE OS\n\nOperador 253\nSesión disponible", 34,
                TMPro.TextAlignmentOptions.Center, Color.white, TMPro.FontStyles.Normal);
            label.font=Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            _scanlinesEnabled=_chromaticEnabled=false;
            _useProceduralRetroSounds=false;
            SetMonitorLightImmediate(.25f);
            desktop=gameObject.AddComponent<ModernOfficeDesktop>(); desktop.Initialize(this);
        }
        public int DesktopArchiveCount => GetEffectiveArchiveCount();
        public bool DesktopArchiveSupported(int index) => GetArchiveSceneName(index) is "F1-House" or "F1-Office";
        public string DesktopArchiveName(int index) => GetArchiveSceneName(index) switch
        {
            "F1-House" => "Casa Herrera",
            "F1-Office" => "Oficina de Víctor Salas",
            _ => GetArchiveName(index)
        };
        public bool DesktopArchiveUnlocked(int index)
        {
            EvidenceDrivenMemoryUnlock.RefreshProgress();
            _unlockedArchive=PlayerPrefs.GetInt(PrefUnlockedArchive,1);
            return index >= 0 && index < DesktopArchiveCount && DesktopArchiveSupported(index) && index < _unlockedArchive;
        }
        public bool DesktopMount(int index)
        {
            if (!DesktopArchiveUnlocked(index)) return false;
            MountArchive(index); return MountedArchiveIndex==index;
        }
        public void DesktopLeave() { if(_cameraFocus!=null) _cameraFocus.ReturnToFarPose(); else desktop.Close(); }
        public void DesktopReset() => ConfirmDeleteAllData();
    }
}

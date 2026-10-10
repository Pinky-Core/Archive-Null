using UnityEngine;
using ArchiveNull.Evidence;

namespace ArchiveNull.UI
{
    public sealed partial class CRTMainMenuController
    {
        private ModernOfficeDesktop desktop;
        [SerializeField, Range(.5f,1f)] private float desktopScreenHeight = .9f;
        private void InstallModernDesktop()
        {
            // Preserve serialized archive/event references while retiring the CRT presentation.
            foreach (Transform child in _screen) child.gameObject.SetActive(false);
            // Retire input from the authored CRT canvas permanently. Its inactive
            // labels must never remain pointer targets alongside the new desktop.
            foreach(var text in _canvas.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.raycastTarget=false;
            foreach(var raycaster in _canvas.GetComponents<UnityEngine.UI.GraphicRaycaster>()) raycaster.enabled=false;
            _screenImage.color = Color.black;
            _scanlinesEnabled=_chromaticEnabled=false;
            _useProceduralRetroSounds=false;
            SetMonitorLightImmediate(0f);
            desktop=gameObject.AddComponent<ModernOfficeDesktop>(); desktop.Initialize(this, _screen, _canvas, desktopScreenHeight);
        }
        public void SetDesktopPower(bool powered) => SetMonitorLightImmediate(powered ? .25f : 0f);
        public bool IsDesktopRebinding => desktop!=null && desktop.IsRebinding;
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

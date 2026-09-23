#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArchiveNull.Accusation;
using ArchiveNull.Evidence;
using ArchiveNull.InvestigationBoard;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit command-line integration check. Never runs in a normal game session.
public sealed class PlayableCaseSmokeTest : MonoBehaviour
{
    public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-archive-smoke") >= 0;
    private readonly List<string> results = new();
    private string output;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (!Requested) return;
        Application.runInBackground = true;
        var host = new GameObject("PlayableCaseSmokeTest");
        DontDestroyOnLoad(host);
        host.AddComponent<PlayableCaseSmokeTest>();
    }

    private void Start()
    {
        output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Validation");
        Directory.CreateDirectory(output);
        Application.logMessageReceived += OnLog;
        StartCoroutine(RunSafely());
    }

    private void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            results.Add("RUNTIME ERROR: " + condition + "\n" + stack);
    }

    private IEnumerator RunSafely()
    {
        IEnumerator routine = Run();
        while (true)
        {
            object yielded;
            try { if (!routine.MoveNext()) break; yielded = routine.Current; }
            catch (Exception exception) { results.Add("FAIL " + exception); break; }
            yield return yielded;
        }
        bool success = results.All(r => !r.StartsWith("FAIL") && !r.StartsWith("RUNTIME ERROR"));
        results.Add(success ? "PASS ALL" : "FAILED");
        File.WriteAllLines(Path.Combine(output, "smoke.txt"), results);
        Application.Quit(success ? 0 : 1);
    }

    private IEnumerator Run()
    {
        yield return null;
        PlayerPrefs.SetInt(AccusationPanel.CaseCompletedPref, 0);
        Check(EvidenceInventory.Instance.GetAllEvidence().Count == 0, "isolated new investigation");
        Check(!CaseEvidenceCatalog.HasArgument(0, out _), "empty report has no culprit argument");
        yield return SceneManager.LoadSceneAsync("F1-House");
        yield return new WaitForSecondsRealtime(2f);
        var cameraController = FindAnyObjectByType<EvidenceCameraController>();
        Check(cameraController != null && FindAnyObjectByType<FirstPersonMovement>() != null, "house player and camera installed");
        var boundary = ArchiveNull.UI.OfficeDissolveTransition.ActiveMemory;
        Check(boundary != null && Mathf.Approximately(boundary.MaximumMemoryRadius, 60f), "house has its configured fixed exploration boundary");
        Vector3 center = boundary.MemoryCenter;
        Vector3 outside = center + new Vector3(boundary.MaximumMemoryRadius + 10f, 2, 0);
        Vector3 constrained = boundary.ConstrainPosition(outside);
        Check(Mathf.Abs(constrained.x - center.x - (boundary.MaximumMemoryRadius - 0.35f)) < 0.01f && constrained.y == outside.y, "boundary prevents crossing while preserving vertical movement");
        Check(Resources.Load<Shader>("MemoryBoundaryPixels") != null && FindAnyObjectByType<ArchiveNull.UI.MemoryBoundaryPixels>() != null, "persistent animated boundary pixels exist in player build");
        Invoke(cameraController, "TryCapture");
        Check(PhotoAlbum.Photos.Count == 1, "shutter action adds a free photograph");
        Sprite photo = PhotoAlbum.Photos[0].photoSprite;
        Check(photo != null && photo.texture.width > photo.texture.height, "URP photograph preserves landscape framing");
        File.WriteAllBytes(Path.Combine(output, "house-capture.png"), photo.texture.EncodeToPNG());
        Check(EvidenceInventory.Instance.GetAllEvidence().Count == 0 && !CaseEvidenceCatalog.CanVisitContractor, "free photograph cannot unlock progress");
        Camera boundaryCamera = Camera.main;
        Vector3 cameraPosition = boundaryCamera.transform.position;
        Quaternion cameraRotation = boundaryCamera.transform.rotation;
        boundaryCamera.transform.position = center + new Vector3(boundary.MaximumMemoryRadius - 2f, 1.8f, 0);
        boundaryCamera.transform.rotation = Quaternion.LookRotation(Vector3.right);
        Sprite boundaryPhoto = (Sprite)Invoke(cameraController, "CaptureCameraPhoto");
        File.WriteAllBytes(Path.Combine(output, "memory-boundary.png"), boundaryPhoto.texture.EncodeToPNG());
        Check(boundary.MemoryCenter == center && boundary.MaximumMemoryRadius == 60f, "approaching boundary cannot move origin or expand radius");
        boundaryCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        Destroy(boundaryPhoto.texture); Destroy(boundaryPhoto);
        Check(Resources.Load<Shader>("ArchiveTrace") != null && UvRevealTarget.ActiveTargets.Count >= 24, "UV shader and powder survive build stripping");
        var inspect = FindObjectsByType<InspectObject>().First(i => i.enabled);
        var note = FindObjectsByType<EvidenceTarget>().First(t => t.EvidenceData.evidenceId == "f1_house_cube");
        Vector3 originalPosition = note.transform.position;
        Vector3 originalScale = note.transform.localScale;
        Invoke(inspect, "TryPickObject", note.gameObject);
        yield return new WaitForSecondsRealtime(0.4f);
        Check(InspectObject.IsAnyInspecting, "inspection reaches held state");
        Invoke(inspect, "ReleaseObject");
        yield return new WaitForSecondsRealtime(0.4f);
        Check(!InspectObject.IsAnyInspecting && Vector3.Distance(originalPosition, note.transform.position) < 0.01f &&
            Vector3.Distance(originalScale, note.transform.localScale) < 0.01f, "inspection restores position and scale");
        foreach (EvidenceTarget target in FindObjectsByType<EvidenceTarget>(FindObjectsInactive.Include))
        {
            Vector3 clue = target.transform.position;
            Check(Vector2.Distance(new Vector2(clue.x, clue.z), new Vector2(center.x, center.z)) < boundary.MaximumMemoryRadius - 0.5f, "house clue inside explorable radius " + target.EvidenceData.evidenceId);
            Check(target.GetComponentInChildren<Collider>(true) != null, "selectable house target " + target.EvidenceData.evidenceId);
            EvidenceInventory.Instance.RegisterEvidence(target.CreateCapturedEvidence(null));
        }
        Check(!CaseEvidenceCatalog.CanVisitContractor, "objects alone cannot unlock contractor memory");
        PhoneEvidenceReader phone = FindAnyObjectByType<PhoneEvidenceReader>();
        Invoke(phone, "OpenChat", 0);
        Type screenType = typeof(PhoneEvidenceReader).GetNestedType("PhoneScreen", BindingFlags.NonPublic);
        Invoke(phone, "OpenScreen", Enum.Parse(screenType, "Calls"));
        Canvas phoneCanvas = ((CanvasGroup)typeof(PhoneEvidenceReader).GetField("phoneGroup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(phone)).GetComponent<Canvas>();
        phoneCanvas.GetComponent<CanvasGroup>().alpha = 1f;
        phoneCanvas.gameObject.SetActive(true);
        Invoke(phone, "OpenChat", 0);
        CaptureUi(phoneCanvas, "phone-chat.png");
        Check(CaseEvidenceCatalog.CanVisitContractor, "note plus phone records unlock contractor memory");
        Check(EvidenceInventory.Instance.HasEvidence("f1_house_cup") && EvidenceInventory.Instance.HasEvidence("f1_house_guest_cup"), "both cups have distinct identities");
        yield return SceneManager.LoadSceneAsync("F1-Office");
        yield return new WaitForSecondsRealtime(2f);
        boundary = ArchiveNull.UI.OfficeDissolveTransition.ActiveMemory;
        Check(boundary != null && Mathf.Approximately(boundary.MaximumMemoryRadius, 40f), "contractor memory has its own fixed boundary");
        center = boundary.MemoryCenter;
        foreach (EvidenceTarget target in FindObjectsByType<EvidenceTarget>(FindObjectsInactive.Include))
        {
            Vector3 clue = target.transform.position;
            Check(Vector2.Distance(new Vector2(clue.x, clue.z), new Vector2(center.x, center.z)) < boundary.MaximumMemoryRadius - 0.5f, "contractor clue inside explorable radius " + target.EvidenceData.evidenceId);
            Check(target.GetComponentInChildren<Collider>(true) != null, "selectable contractor target " + target.EvidenceData.evidenceId);
            EvidenceInventory.Instance.RegisterEvidence(target.CreateCapturedEvidence(null));
        }
        Check(!CaseEvidenceCatalog.HasArgument(1, out _), "collecting everything does not replace deductions");
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return new WaitForSecondsRealtime(1f);
        Check(FindAnyObjectByType<EvidenceNotebookUI>() != null, "office gallery is available");
        AccusationPanel.OpenRuntime();
        var report = AccusationPanel.ExistingInstance;
        int[] selections = (int[])typeof(AccusationPanel).GetField("selections", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(report);
        for (int i = 0; i < selections.Length; i++) selections[i] = 1;
        Invoke(report, "SubmitReport");
        Check(PlayerPrefs.GetInt(AccusationPanel.CaseCompletedPref, 0) == 0, "correct guesses without links cannot close case");
        foreach (string[] links in CaseEvidenceCatalog.ArgumentLinks)
            foreach (string link in links)
            {
                string[] ids = link.Split('|');
                BoardSessionState.Connections.Add(BoardSessionState.GetConnectionKey(ids[0], ids[1]));
            }
        Check(!CaseEvidenceCatalog.HasArgument(3, out _), "exit puzzle is required");
        var puzzle = FindAnyObjectByType<DoorReconstructionUI>(FindObjectsInactive.Include);
        foreach (int i in new[] { 0, 2, 1, 3, 4 }) Invoke(puzzle, "Choose", i);
        Invoke(puzzle, "ValidateSequence");
        Check(!BoardSessionState.UnlockedConclusions.Contains(DoorReconstructionUI.ConclusionId), "wrong exit sequence is rejected");
        Invoke(puzzle, "ResetSequence");
        for (int i = 0; i < 5; i++) Invoke(puzzle, "Choose", i);
        Invoke(puzzle, "ValidateSequence");
        for (int i = 0; i < 5; i++) Check(CaseEvidenceCatalog.HasArgument(i, out _), "supported report argument " + i);
        // Removing any required connection must invalidate at least one argument.
        foreach (string key in BoardSessionState.Connections.ToArray())
        {
            BoardSessionState.Connections.Remove(key);
            Check(Enumerable.Range(0, 5).Any(i => !CaseEvidenceCatalog.HasArgument(i, out _)), "missing link invalidates report " + key);
            BoardSessionState.Connections.Add(key);
        }
        EvidenceInventory.Instance.SetNote("suspect_victor", "Persisted suspect hypothesis");
        EvidenceInventory.Instance.SetOperatorNotes("Persisted notebook");
        GameSaveSystem.SaveNow();
        int evidenceCount = EvidenceInventory.Instance.GetAllEvidence().Count;
        Invoke(FindAnyObjectByType<GameSaveSystem>(), "Load");
        Check(EvidenceInventory.Instance.GetAllEvidence().Count == evidenceCount && PhotoAlbum.Photos.Count == 1, "office save reloads evidence and photos");
        Check(EvidenceInventory.Instance.GetNote("suspect_victor") == "Persisted suspect hypothesis", "suspect form persists");
        PhotoAlbum.Remove(PhotoAlbum.Photos[0].evidenceId);
        Check(EvidenceInventory.Instance.GetAllEvidence().Count == evidenceCount && PhotoAlbum.Photos.Count == 0, "photo deletion preserves case evidence");
        Invoke(report, "SubmitReport");
        Check(PlayerPrefs.GetInt(AccusationPanel.CaseCompletedPref, 0) == 1, "fully supported accusation closes case");
        Invoke(report, "Close");
        var notebook = FindAnyObjectByType<EvidenceNotebookUI>();
        Invoke(notebook, "SetVisible", true);
        CaptureUi(notebook.GetComponentInChildren<Canvas>(), "office-notebook.png");
        Invoke(notebook, "SetVisible", false);
        GameSaveSystem.DeleteAllGameplayData();
        Check(EvidenceInventory.Instance.GetAllEvidence().Count == 0 && PlayerPrefs.GetInt(AccusationPanel.CaseCompletedPref, 0) == 0, "new investigation clears completion and evidence");
    }

    private void Check(bool condition, string message)
    {
        results.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new InvalidOperationException(message);
    }

    private void CaptureUi(Canvas canvas, string fileName)
    {
        var host = new GameObject("ValidationUiCamera", typeof(Camera));
        Camera camera = host.GetComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = new Vector3(10000, 10000, 10000);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.03f, 0.028f);
        camera.nearClipPlane = 0.1f; camera.farClipPlane = 10f;
        camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        RenderMode mode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float planeDistance = canvas.planeDistance;
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(output, fileName), texture.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = planeDistance;
            RenderTexture.active = previous; camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Destroy(texture); Destroy(host);
        }
    }

    private static object Invoke(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
}
#endif

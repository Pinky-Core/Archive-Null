using System;
using UnityEngine;

public static class PlayerAssistanceSettings
{
    public const string PrefHelpEnabled = "archive.assistance.help.enabled";
    public const string PrefEditorHelpEnabled = "archive.assistance.editor.enabled";
    public const string PrefActionFeedbackEnabled = "archive.assistance.action_feedback.enabled";

    public static event Action<bool> HelpEnabledChanged;
    public static event Action<bool> ActionFeedbackEnabledChanged;

    public static bool HelpEnabled
    {
        get => PlayerPrefs.GetInt(PrefHelpEnabled, 1) == 1;
        set
        {
            bool current = HelpEnabled;
            if (current == value)
            {
                return;
            }

            PlayerPrefs.SetInt(PrefHelpEnabled, value ? 1 : 0);
            PlayerPrefs.Save();
            HelpEnabledChanged?.Invoke(value);
        }
    }

    public static bool ShouldShowHelp
    {
        get => HelpEnabled;
    }

    public static bool ActionFeedbackEnabled
    {
        get => PlayerPrefs.GetInt(PrefActionFeedbackEnabled, 1) == 1;
        set
        {
            if (ActionFeedbackEnabled == value)
            {
                return;
            }

            PlayerPrefs.SetInt(PrefActionFeedbackEnabled, value ? 1 : 0);
            PlayerPrefs.Save();
            ActionFeedbackEnabledChanged?.Invoke(value);
        }
    }

    public static void ResetHelpProgress()
    {
        PlayerPrefs.DeleteKey("archive.tutorial.crime.completed");
        PlayerPrefs.DeleteKey("archive.tutorial.crime.v2.completed");
        PlayerPrefs.DeleteKey("archive.office.speaker_tutorial.completed");
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Archive Null/Assistance/Enable Help In Editor")]
    private static void EnableHelpInEditor()
    {
        PlayerPrefs.SetInt(PrefEditorHelpEnabled, 1);
        PlayerPrefs.Save();
        Debug.Log("[Archive Null] Editor help enabled.");
    }

    [UnityEditor.MenuItem("Archive Null/Assistance/Disable Help In Editor")]
    private static void DisableHelpInEditor()
    {
        PlayerPrefs.SetInt(PrefEditorHelpEnabled, 0);
        PlayerPrefs.Save();
        Debug.Log("[Archive Null] Editor help disabled.");
    }
#endif
}

using System;
using UnityEngine;

public static class PlayerAssistanceSettings
{
    public const string PrefHelpEnabled = "archive.assistance.help.enabled";

    public static event Action<bool> HelpEnabledChanged;

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

    public static void ResetHelpProgress()
    {
        PlayerPrefs.DeleteKey("archive.tutorial.crime.completed");
        PlayerPrefs.DeleteKey("archive.office.speaker_tutorial.completed");
        PlayerPrefs.Save();
    }
}

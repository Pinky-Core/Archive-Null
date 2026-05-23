using UnityEngine;
using UnityEngine.InputSystem;

public enum GameInputAction
{
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Run,
    Interact,
    Inspect,
    ReleaseInspect,
    Camera,
    Notebook,
    NotebookPrevious,
    NotebookNext,
    Pause
}

public static class GlobalInputBindings
{
    private const string Prefix = "archive.input.";

    public static Key GetKey(GameInputAction action)
    {
        string pref = Prefix + action;
        if (PlayerPrefs.HasKey(pref))
        {
            return (Key)PlayerPrefs.GetInt(pref);
        }

        return GetDefaultKey(action);
    }

    public static void SetKey(GameInputAction action, Key key)
    {
        if (key == Key.None)
        {
            return;
        }

        PlayerPrefs.SetInt(Prefix + action, (int)key);
        PlayerPrefs.Save();
    }

    public static bool WasPressed(GameInputAction action)
    {
        Key key = GetKey(action);
        return Keyboard.current != null && key != Key.None && Keyboard.current[key].wasPressedThisFrame;
    }

    public static bool IsPressed(GameInputAction action)
    {
        Key key = GetKey(action);
        return Keyboard.current != null && key != Key.None && Keyboard.current[key].isPressed;
    }

    public static string GetDisplayName(GameInputAction action)
    {
        return GetKey(action).ToString().ToUpperInvariant();
    }

    public static string GetLabel(GameInputAction action, bool spanish)
    {
        return action switch
        {
            GameInputAction.MoveForward => spanish ? "AVANZAR" : "MOVE FORWARD",
            GameInputAction.MoveBackward => spanish ? "RETROCEDER" : "MOVE BACKWARD",
            GameInputAction.MoveLeft => spanish ? "MOVER IZQ" : "MOVE LEFT",
            GameInputAction.MoveRight => spanish ? "MOVER DER" : "MOVE RIGHT",
            GameInputAction.Run => spanish ? "CORRER" : "RUN",
            GameInputAction.Interact => spanish ? "INTERACTUAR" : "INTERACT",
            GameInputAction.Inspect => spanish ? "INSPECCIONAR" : "INSPECT",
            GameInputAction.ReleaseInspect => spanish ? "SOLTAR INSPECCION" : "RELEASE INSPECT",
            GameInputAction.Camera => spanish ? "CAMARA" : "CAMERA",
            GameInputAction.Notebook => spanish ? "LIBRETA" : "NOTEBOOK",
            GameInputAction.NotebookPrevious => spanish ? "FOTO ANTERIOR" : "PREVIOUS PHOTO",
            GameInputAction.NotebookNext => spanish ? "FOTO SIGUIENTE" : "NEXT PHOTO",
            GameInputAction.Pause => spanish ? "PAUSA" : "PAUSE",
            _ => action.ToString()
        };
    }

    private static Key GetDefaultKey(GameInputAction action)
    {
        return action switch
        {
            GameInputAction.MoveForward => Key.W,
            GameInputAction.MoveBackward => Key.S,
            GameInputAction.MoveLeft => Key.A,
            GameInputAction.MoveRight => Key.D,
            GameInputAction.Run => Key.LeftShift,
            GameInputAction.Interact => Key.E,
            GameInputAction.Inspect => Key.E,
            GameInputAction.ReleaseInspect => Key.E,
            GameInputAction.Camera => Key.F,
            GameInputAction.Notebook => Key.Tab,
            GameInputAction.NotebookPrevious => Key.Q,
            GameInputAction.NotebookNext => Key.E,
            GameInputAction.Pause => Key.Escape,
            _ => Key.None
        };
    }
}

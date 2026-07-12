using UnityEngine;

public static class GameLocalization
{
    public static bool IsSpanish => PlayerPrefs.GetInt("crt.menu.language.index", 0) == 0;
    public static string Text(string spanish, string english) => IsSpanish ? spanish : english;
}

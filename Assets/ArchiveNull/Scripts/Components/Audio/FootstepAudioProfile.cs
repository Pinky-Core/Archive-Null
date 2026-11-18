using UnityEngine;

public sealed class FootstepAudioProfile : ScriptableObject
{
    public AudioClip clip;
    public float walkingPitch=.72f;
    public float runningPitch=.92f;
    public float volume=1f;
}

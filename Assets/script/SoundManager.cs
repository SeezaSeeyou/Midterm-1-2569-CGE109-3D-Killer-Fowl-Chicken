using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    [Serializable]
    private class SoundEffect
    {
        public string id = string.Empty;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        public bool allowOverlap;
    }

    [Header("Sound Effects")]
    [Tooltip("Call PlaySFX with an Id. One of its clips is chosen randomly each time.")]
    [SerializeField] private SoundEffect[] soundEffects = Array.Empty<SoundEffect>();

    [Header("3D Sound Distance")]
    [SerializeField, Min(0.01f)] private float minDistance = 2f;
    [SerializeField, Min(0.01f)] private float maxDistance = 20f;
    private readonly List<(AudioSource source, float volume, Transform owner, bool allowOverlap)> spatialSounds =
        new List<(AudioSource, float, Transform, bool)>();

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

    [SerializeField, HideInInspector] private AudioSource sfxSource;
    [SerializeField, HideInInspector] private AudioSource musicSource;
    private static SoundManager instance;

    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SoundManager>();
                if (instance != null)
                    instance.Initialize();
            }
            return instance;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.loop = true;
        ApplyVolumes();

        // Keep only the manager when it is placed under a scene object.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void PlaySFX(string soundId)
    {
        PlaySFXInternal(soundId, null);
    }

    public void PlaySFX(string soundId, Vector3 position, float pitch = 1f)
    {
        PlaySFXInternal(soundId, position, pitch: pitch);
    }

    public void PlaySFX(string soundId, Transform owner)
    {
        if (owner != null)
            PlaySFXInternal(soundId, owner.position, owner);
    }

    public void StopSFX(Transform owner)
    {
        if (owner == null)
            return;

        for (int i = spatialSounds.Count - 1; i >= 0; i--)
        {
            var sound = spatialSounds[i];
            if (sound.owner != owner)
                continue;
            spatialSounds.RemoveAt(i);
            if (sound.source != null)
            {
                sound.source.Stop();
                Destroy(sound.source.gameObject);
            }
        }
    }

    private void PlaySFXInternal(string soundId, Vector3? position, Transform owner = null, float pitch = 1f)
    {
        if (instance != this || string.IsNullOrWhiteSpace(soundId))
            return;

        foreach (SoundEffect sound in soundEffects)
        {
            if (sound == null || sound.id != soundId)
                continue;

            if (owner != null && !sound.allowOverlap)
                foreach (var active in spatialSounds)
                    if (active.owner == owner && !active.allowOverlap && active.source != null
                        && (active.source.isPlaying || AudioListener.pause))
                        return;

            int clipCount = 0;
            if (sound.clips != null)
            {
                foreach (AudioClip clip in sound.clips)
                    if (clip != null)
                        clipCount++;
            }

            if (clipCount == 0)
            {
                Debug.LogWarning($"SoundManager: '{soundId}' has no audio clips.", this);
                return;
            }

            int selectedClip = UnityEngine.Random.Range(0, clipCount);
            foreach (AudioClip clip in sound.clips)
            {
                if (clip != null && selectedClip-- == 0)
                {
                    if (position.HasValue)
                        PlaySpatialClip(clip, position.Value, Mathf.Clamp01(sound.volume), owner, sound.allowOverlap, pitch);
                    else
                        sfxSource.PlayOneShot(clip, Mathf.Clamp01(sound.volume));
                    return;
                }
            }
        }

        Debug.LogWarning($"SoundManager: unknown sound Id '{soundId}'.", this);
    }

    private void PlaySpatialClip(AudioClip clip, Vector3 position, float volume, Transform owner, bool allowOverlap, float pitch)
    {
        // Owned by the manager so death sounds survive destruction of the NPC.
        GameObject emitter = new GameObject("SFX: " + clip.name);
        emitter.transform.SetParent(transform);
        emitter.transform.position = position;
        AudioSource source = emitter.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = Mathf.Max(0.01f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance);
        source.dopplerLevel = 0f;
        source.volume = masterVolume * sfxVolume * volume;
        source.clip = clip;
        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        spatialSounds.Add((source, volume, owner, allowOverlap));
        source.Play();
        StartCoroutine(CleanupSpatialSound(source, volume, owner, allowOverlap));
    }

    private void LateUpdate()
    {
        foreach (var sound in spatialSounds)
            if (sound.source != null && sound.owner != null)
                sound.source.transform.position = sound.owner.position;
    }

    private IEnumerator CleanupSpatialSound(AudioSource source, float volume, Transform owner, bool allowOverlap)
    {
        yield return null;
        while (source != null && (source.isPlaying || AudioListener.pause))
            yield return null;

        spatialSounds.Remove((source, volume, owner, allowOverlap));
        if (source != null)
            Destroy(source.gameObject);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (instance != this || clip == null)
            return;
        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (instance != this)
            return;

        musicSource.Stop();
        musicSource.clip = null;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    private void OnValidate()
    {
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        if (sfxSource != null)
            sfxSource.volume = masterVolume * sfxVolume;
        if (musicSource != null)
            musicSource.volume = masterVolume * musicVolume;
        foreach (var sound in spatialSounds)
            if (sound.source != null)
                sound.source.volume = masterVolume * sfxVolume * sound.volume;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}

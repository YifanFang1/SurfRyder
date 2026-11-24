using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Sound
{
    public string name;                       // The ID you call from code, e.g. "Jump"
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.5f, 2f)] public float pitch = 1f;
    [Range(0f, 0.3f)] public float pitchVariance = 0f; // random pitch wiggle so repeats don't sound identical
    public bool loop = false;                 // mainly used for music
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sound Libraries")]
    public Sound[] musicTracks;
    public Sound[] sfx;
    public Sound[] ambience;                  // looping background beds (ocean, wind, etc.)

    [Header("Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float ambienceVolume = 1f;
    public int sfxPoolSize = 12;              // max simultaneous sound effects

    private Dictionary<string, Sound> musicLookup = new Dictionary<string, Sound>();
    private Dictionary<string, Sound> sfxLookup = new Dictionary<string, Sound>();
    private Dictionary<string, Sound> ambienceLookup = new Dictionary<string, Sound>();

    private AudioSource musicSourceA;
    private AudioSource musicSourceB;         // two sources so we can crossfade
    private AudioSource activeMusicSource;
    private List<AudioSource> sfxPool = new List<AudioSource>();
    private Coroutine fadeRoutine;
    private Sound currentMusic;

    private void Awake()
    {
        // Singleton: only one AudioManager, and it survives scene loads
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildLookups();
        CreateSources();

        // Load saved volume settings if they exist
        masterVolume = PlayerPrefs.GetFloat("MasterVol", masterVolume);
        musicVolume = PlayerPrefs.GetFloat("MusicVol", musicVolume);
        sfxVolume = PlayerPrefs.GetFloat("SfxVol", sfxVolume);
        ambienceVolume = PlayerPrefs.GetFloat("AmbVol", ambienceVolume);
    }

    private void BuildLookups()
    {
        foreach (var s in musicTracks) musicLookup[s.name] = s;
        foreach (var s in sfx) sfxLookup[s.name] = s;
        foreach (var s in ambience) ambienceLookup[s.name] = s;
    }

    private void CreateSources()
    {
        musicSourceA = gameObject.AddComponent<AudioSource>();
        musicSourceB = gameObject.AddComponent<AudioSource>();
        musicSourceA.playOnAwake = false;
        musicSourceB.playOnAwake = false;
        activeMusicSource = musicSourceA;

        for (int i = 0; i < sfxPoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sfxPool.Add(src);
        }
    }

    // ---------------------------------------------------------------- SFX

    /// <summary>Play a 2D sound effect (UI clicks, player sounds, etc.)</summary>
    public void PlaySFX(string name)
    {
        if (!sfxLookup.TryGetValue(name, out Sound s))
        {
            Debug.LogWarning($"AudioManager: SFX '{name}' not found.");
            return;
        }

        AudioSource src = GetFreeSfxSource();
        if (src == null) return; // pool exhausted, skip this sound

        src.spatialBlend = 0f;
        ConfigureAndPlay(src, s);
    }

    /// <summary>Play a 3D sound effect at a world position (explosions, enemies, etc.)</summary>
    public void PlaySFXAtPoint(string name, Vector3 position)
    {
        if (!sfxLookup.TryGetValue(name, out Sound s))
        {
            Debug.LogWarning($"AudioManager: SFX '{name}' not found.");
            return;
        }

        AudioSource src = GetFreeSfxSource();
        if (src == null) return;

        src.transform.position = position; // pool lives on this object, so we move it
        src.spatialBlend = 1f;
        ConfigureAndPlay(src, s);
    }

    private void ConfigureAndPlay(AudioSource src, Sound s)
    {
        src.clip = s.clip;
        src.volume = s.volume * sfxVolume * masterVolume;
        src.pitch = s.pitch + Random.Range(-s.pitchVariance, s.pitchVariance);
        src.loop = s.loop;
        src.Play();
    }

    private AudioSource GetFreeSfxSource()
    {
        foreach (var src in sfxPool)
            if (!src.isPlaying) return src;
        return null;
    }

    public void StopAllSFX()
    {
        foreach (var src in sfxPool) src.Stop();
    }

    // ---------------------------------------------------------------- Music

    /// <summary>Play a music track, crossfading from whatever is currently playing.</summary>
    public void PlayMusic(string name, float fadeTime = 1.5f)
    {
        if (!musicLookup.TryGetValue(name, out Sound s))
        {
            Debug.LogWarning($"AudioManager: Music '{name}' not found.");
            return;
        }

        if (currentMusic == s && activeMusicSource.isPlaying) return; // already playing

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossfadeMusic(s, fadeTime));
    }

    public void StopMusic(float fadeTime = 1f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutMusic(fadeTime));
    }

    private IEnumerator CrossfadeMusic(Sound next, float fadeTime)
    {
        AudioSource from = activeMusicSource;
        AudioSource to = (from == musicSourceA) ? musicSourceB : musicSourceA;

        to.clip = next.clip;
        to.loop = true;
        to.pitch = next.pitch;
        to.volume = 0f;
        to.Play();

        float targetVol = next.volume * musicVolume * masterVolume;
        float startVol = from.volume;
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime; // unscaled so music still fades if the game is paused
            float p = fadeTime > 0f ? t / fadeTime : 1f;
            from.volume = Mathf.Lerp(startVol, 0f, p);
            to.volume = Mathf.Lerp(0f, targetVol, p);
            yield return null;
        }

        from.Stop();
        to.volume = targetVol;
        activeMusicSource = to;
        currentMusic = next;
    }

    private IEnumerator FadeOutMusic(float fadeTime)
    {
        float startVol = activeMusicSource.volume;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            activeMusicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
            yield return null;
        }
        activeMusicSource.Stop();
        currentMusic = null;
    }

    // ---------------------------------------------------------------- Looping sounds (ambience + looping SFX)
    // Each loop gets its own AudioSource, so several can play at once (ocean + wind + low drone).
    // Names must be unique across the ambience and sfx arrays.

    private class ActiveLoop
    {
        public AudioSource source;
        public Sound sound;
        public bool isAmbience;
        public Coroutine fade;
    }

    private Dictionary<string, ActiveLoop> activeLoops = new Dictionary<string, ActiveLoop>();

    /// <summary>Start a looping background bed from the Ambience array.</summary>
    public void PlayAmbience(string name, float fadeTime = 2f) { StartLoop(name, ambienceLookup, true, fadeTime); }
    public void StopAmbience(string name, float fadeTime = 2f) { StopLoop(name, fadeTime); }

    public void StopAllAmbience(float fadeTime = 2f)
    {
        foreach (var key in new List<string>(activeLoops.Keys))
            if (activeLoops[key].isAmbience) StopLoop(key, fadeTime);
    }

    /// <summary>Start a looping SFX (engine boost, warning alarm, skidding...). Call Stop when the condition ends.</summary>
    public void PlayLoopSFX(string name, float fadeTime = 0.1f) { StartLoop(name, sfxLookup, false, fadeTime); }
    public void StopLoopSFX(string name, float fadeTime = 0.2f) { StopLoop(name, fadeTime); }

    public bool IsLoopPlaying(string name) { return activeLoops.ContainsKey(name); }

    private void StartLoop(string name, Dictionary<string, Sound> lookup, bool isAmbience, float fadeTime)
    {
        if (!lookup.TryGetValue(name, out Sound s))
        {
            Debug.LogWarning($"AudioManager: Loop '{name}' not found.");
            return;
        }

        ActiveLoop loop;
        if (activeLoops.TryGetValue(name, out loop))
        {
            // Already running (maybe fading out): just fade it back in
            if (loop.fade != null) StopCoroutine(loop.fade);
        }
        else
        {
            loop = new ActiveLoop { sound = s, isAmbience = isAmbience };
            loop.source = gameObject.AddComponent<AudioSource>();
            loop.source.clip = s.clip;
            loop.source.loop = true;
            loop.source.pitch = s.pitch;
            loop.source.spatialBlend = 0f;
            loop.source.volume = 0f;
            loop.source.Play();
            activeLoops[name] = loop;
        }

        loop.fade = StartCoroutine(FadeLoop(name, loop, LoopTargetVolume(loop), fadeTime, false));
    }

    private void StopLoop(string name, float fadeTime)
    {
        if (!activeLoops.TryGetValue(name, out ActiveLoop loop)) return;
        if (loop.fade != null) StopCoroutine(loop.fade);
        loop.fade = StartCoroutine(FadeLoop(name, loop, 0f, fadeTime, true));
    }

    private IEnumerator FadeLoop(string name, ActiveLoop loop, float target, float fadeTime, bool removeAtEnd)
    {
        float start = loop.source.volume;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            loop.source.volume = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        loop.source.volume = target;
        loop.fade = null;

        if (removeAtEnd)
        {
            loop.source.Stop();
            Destroy(loop.source);
            activeLoops.Remove(name);
        }
    }

    private float LoopTargetVolume(ActiveLoop l)
    {
        return l.sound.volume * (l.isAmbience ? ambienceVolume : sfxVolume) * masterVolume;
    }

    private void RefreshLoopVolumes()
    {
        foreach (var loop in activeLoops.Values)
            if (loop.fade == null) loop.source.volume = LoopTargetVolume(loop);
    }

    // ---------------------------------------------------------------- Volume controls (hook these to UI sliders)

    public void SetMasterVolume(float v) { masterVolume = v; PlayerPrefs.SetFloat("MasterVol", v); RefreshMusicVolume(); RefreshLoopVolumes(); }
    public void SetMusicVolume(float v) { musicVolume = v; PlayerPrefs.SetFloat("MusicVol", v); RefreshMusicVolume(); }
    public void SetSFXVolume(float v) { sfxVolume = v; PlayerPrefs.SetFloat("SfxVol", v); RefreshLoopVolumes(); }
    public void SetAmbienceVolume(float v) { ambienceVolume = v; PlayerPrefs.SetFloat("AmbVol", v); RefreshLoopVolumes(); }

    private void RefreshMusicVolume()
    {
        if (currentMusic != null && activeMusicSource.isPlaying)
            activeMusicSource.volume = currentMusic.volume * musicVolume * masterVolume;
    }
}
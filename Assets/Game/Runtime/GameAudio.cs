using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// Sound in four groups with their own volume (<see cref="GameSettings"/>): music, effects (one-shots, the jetpack,
    /// positional loops such as lava), ambience of the current place, and the master level.
    /// Clips come from Resources/Audio and Resources/Music by name, so any of them can be replaced by a file with the same
    /// name (wav, ogg or mp3).
    ///
    /// Music follows depth: each track has a weight that changes smoothly with the depth, and every change of weight is
    /// eased over several seconds, so tracks blend into each other instead of switching. Tracks loop by cross-fading
    /// their end into their own beginning, which also hides the seam that AAC leaves at the start of a clip.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Ambience clip per place: the yard, the house, each zone by index, the boss chamber.</summary>
        public const string Surface = "ambient_surface", House = "ambient_house", Boss = "ambient_boss";
        public static readonly string[] Zones = { "ambient_roots", "ambient_slate", "ambient_crystals", "ambient_magma" };
        public static IEnumerable<string> Ambiences { get { yield return Surface; yield return House; foreach (var zone in Zones) yield return zone; yield return Boss; } }
        /// <summary>Music: the yard, house and roots; the roots and slate; the crystals and magma; the boss.</summary>
        public static readonly string[] Music = { "music_1_yard", "music_2_roots", "music_3_deep", "music_4_boss" };

        private const float AmbientFade = 3f, AmbientLevel = .5f, MusicLevel = .75f;
        /// <summary>Seconds for a track to rise from silence to full weight (or back), and for a loop to cross-fade.</summary>
        private const float MusicFade = 8f, LoopOverlap = 5f;

        private sealed class Track
        {
            public AudioClip clip;
            public readonly AudioSource[] voices = new AudioSource[2];
            public int lead;
            public float weight, target, loopFade = -1;
            public bool paused;
        }

        private sealed class Emitted { public AudioSource source; public float volume; }

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly List<AudioSource> spots = new List<AudioSource>();
        private readonly List<Emitted> emitters = new List<Emitted>();
        private readonly AudioSource[] ambience = new AudioSource[2];
        private readonly List<Track> tracks = new List<Track>();
        private AudioSource loop;
        private float loopVolume = 1;
        private int next, nextSpot, current;
        private string ambientName;
        private bool muted;

        private void Awake()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio")) clips[clip.name] = clip;
            for (int i = 0; i < 6; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                sources.Add(source);
            }
            for (int i = 0; i < ambience.Length; i++)
            {
                ambience[i] = gameObject.AddComponent<AudioSource>();
                ambience[i].playOnAwake = false;
                ambience[i].loop = true;
                ambience[i].spatialBlend = 0;
                ambience[i].volume = 0;
            }
            for (int i = 0; i < 4; i++)
            {
                var spot = new GameObject("Sound spot", typeof(AudioSource)).GetComponent<AudioSource>();
                Setup3D(spot, 18);
                spots.Add(spot);
            }
            foreach (var name in Music)
            {
                var track = new Track { clip = Resources.Load<AudioClip>("Music/" + name) };
                for (int v = 0; v < 2; v++)
                {
                    var voice = gameObject.AddComponent<AudioSource>();
                    voice.playOnAwake = false;
                    voice.loop = false;
                    voice.spatialBlend = 0;
                    voice.volume = 0;
                    voice.clip = track.clip;
                    track.voices[v] = voice;
                }
                tracks.Add(track);
            }
            GameSettings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        private static float Effects => GameSettings.Current.effects;

        private void ApplyVolumes()
        {
            AudioListener.volume = muted ? 0 : GameSettings.Current.master;
            foreach (var source in sources) source.volume = Effects;
            foreach (var spot in spots) spot.volume = Effects;
            foreach (var emitted in emitters) if (emitted.source != null) emitted.source.volume = emitted.volume * Effects;
            if (loop != null) loop.volume = loopVolume * Effects;
        }

        public AudioClip Clip(string name) => clips.TryGetValue(name, out var clip) ? clip : null;

        public void Play(string name, float volume = 1, float pitch = 1, float jitter = 0.06f)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            var source = sources[next];
            next = (next + 1) % sources.Count;
            source.pitch = pitch * (1 + Random.Range(-jitter, jitter));
            source.PlayOneShot(clip, volume);
        }

        /// <summary>A one-shot heard from a place in the world: quieter and panned with distance.</summary>
        public void PlayAt(string name, Vector3 position, float volume = 1, float pitch = 1)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            var spot = spots[nextSpot];
            nextSpot = (nextSpot + 1) % spots.Count;
            spot.transform.position = position;
            spot.pitch = pitch * (1 + Random.Range(-.05f, .05f));
            spot.PlayOneShot(clip, volume);
        }

        /// <summary>A looping positional sound attached to an object, such as bubbling lava.</summary>
        public AudioSource Emitter(Transform parent, string name, float volume, float range)
        {
            var clip = Clip(name);
            var source = parent.gameObject.AddComponent<AudioSource>();
            Setup3D(source, range);
            source.loop = true;
            source.volume = volume * Effects;
            emitters.Add(new Emitted { source = source, volume = volume });
            if (clip == null) return source;
            source.clip = clip;
            // Start somewhere inside the loop so neighbouring emitters do not pulse together.
            source.time = Random.Range(0, clip.length * .9f);
            source.Play();
            return source;
        }

        private static void Setup3D(AudioSource source, float range)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = range;
            source.dopplerLevel = 0;
        }

        /// <summary>Starts or stops a looping sound, such as the jetpack hiss.</summary>
        public void Loop(string name, bool on, float volume = 1)
        {
            if (loop == null)
            {
                loop = gameObject.AddComponent<AudioSource>();
                loop.playOnAwake = false;
                loop.loop = true;
                loop.spatialBlend = 0;
            }
            if (!on) { if (loop.isPlaying) loop.Stop(); return; }
            if (!clips.TryGetValue(name, out var clip)) return;
            loopVolume = volume;
            loop.volume = volume * Effects;
            if (loop.isPlaying && loop.clip == clip) return;
            loop.clip = clip;
            loop.Play();
        }

        /// <summary>Cross-fades to the ambience of a place; a missing clip fades to silence.</summary>
        public void SetAmbience(string name)
        {
            if (name == ambientName) return;
            ambientName = name;
            current = 1 - current;
            var source = ambience[current];
            source.clip = Clip(name);
            if (source.clip == null) { source.Stop(); return; }
            source.time = Random.Range(0, source.clip.length * .9f);
            source.Play();
        }

        /// <summary>
        /// Sets the music for a depth: the first track until the roots, blending into the second through the roots,
        /// the second through the slate, the third from the crystals down; the boss has its own.
        /// </summary>
        public void SetMusicDepth(float depth, bool boss)
        {
            float first = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6, 26, depth));
            float third = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(55, 75, depth));
            float second = Mathf.Max(0, 1 - first - third);
            SetTargets(boss ? 0 : first, boss ? 0 : second, boss ? 0 : third, boss ? 1 : 0);
        }

        private void SetTargets(float a, float b, float c, float d)
        {
            if (tracks.Count < 4) return;
            tracks[0].target = a; tracks[1].target = b; tracks[2].target = c; tracks[3].target = d;
        }

        /// <summary>Master mute (the M key) that keeps the slider value.</summary>
        public void Mute(bool value) { muted = value; ApplyVolumes(); }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float step = dt / AmbientFade * AmbientLevel;
            float ambientTarget = AmbientLevel * GameSettings.Current.ambience;
            for (int i = 0; i < ambience.Length; i++)
            {
                var source = ambience[i];
                source.volume = Mathf.MoveTowards(source.volume, i == current && source.clip != null ? ambientTarget : 0, step);
                if (i != current && source.volume <= 0 && source.isPlaying) source.Stop();
            }
            // Music does not move while the whole game is paused (tab hidden, ad): AudioListener.pause holds it.
            if (!AudioListener.pause) foreach (var track in tracks) UpdateTrack(track, dt);
        }

        private void UpdateTrack(Track track, float dt)
        {
            if (track.clip == null) return;
            track.weight = Mathf.MoveTowards(track.weight, track.target, dt / MusicFade);
            var lead = track.voices[track.lead];
            var other = track.voices[1 - track.lead];
            if (track.weight <= .001f)
            {
                if (lead.isPlaying || other.isPlaying) { lead.Pause(); other.Pause(); track.paused = true; }
                return;
            }
            if (!lead.isPlaying && !other.isPlaying)
            {
                if (track.paused) { lead.UnPause(); if (track.loopFade >= 0) other.UnPause(); track.paused = false; }
                else { lead.time = 0; lead.Play(); }
            }
            // Equal-power gain keeps the sum of two blending tracks at the same loudness.
            float gain = Mathf.Sqrt(track.weight) * MusicLevel * GameSettings.Current.music;
            if (track.loopFade < 0 && lead.isPlaying && track.clip.length - lead.time < LoopOverlap)
            {
                other.time = 0;
                other.Play();
                track.loopFade = 0;
            }
            if (track.loopFade >= 0)
            {
                track.loopFade += dt;
                float k = Mathf.Clamp01(track.loopFade / LoopOverlap);
                lead.volume = gain * Mathf.Sqrt(1 - k);
                other.volume = gain * Mathf.Sqrt(k);
                if (k < 1) return;
                lead.Stop();
                track.lead = 1 - track.lead;
                track.loopFade = -1;
            }
            else lead.volume = gain;
        }

        /// <summary>Current weight of each music track (tests, debugging).</summary>
        public float MusicWeight(int track) => track < tracks.Count ? tracks[track].weight : 0;
        public bool MusicLoaded(int track) => track < tracks.Count && tracks[track].clip != null;

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
            foreach (var spot in spots) if (spot != null) Destroy(spot.gameObject);
        }

        /// <summary>Mutes all sound without a GameAudio at hand (tests); the instance keeps it in step on the next change.</summary>
        public static void SetMuted(bool muted) => AudioListener.volume = muted ? 0 : GameSettings.Current.master;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>One-shot 2D sound effects from Resources/Audio (made by Tools/make_sounds.py).</summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private int next;

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
        }

        public void Play(string name, float volume = 1, float pitch = 1, float jitter = 0.06f)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            var source = sources[next];
            next = (next + 1) % sources.Count;
            source.pitch = pitch * (1 + Random.Range(-jitter, jitter));
            source.PlayOneShot(clip, volume);
        }

        public static void SetMuted(bool muted) => AudioListener.volume = muted ? 0 : 1;
    }
}

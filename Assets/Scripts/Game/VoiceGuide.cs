// VoiceGuide — plays the pre-recorded GPS phrases (Resources/Voice/<key>.wav).
// * The same phrase is not repeated within a few seconds (no nagging).
// * If a phrase is playing, the newest request waits (only the latest is kept),
//   unless it is urgent ("Turn left." right at the corner), which interrupts.
// * Every phrase is also raised as a subtitle, so the game works muted.
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    public class VoiceGuide : MonoBehaviour
    {
        public bool Muted;
        public string LastKey { get; private set; }
        public event System.Action<string> Subtitle;

        AudioSource source;
        string pending;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastSaid = new Dictionary<string, float>();

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // "in your ear", like a car's GPS
        }

        public void Say(string key, bool urgent = false, float minRepeatSeconds = 4f)
        {
            if (lastSaid.TryGetValue(key, out float t) && Time.time - t < minRepeatSeconds) return;
            lastSaid[key] = Time.time;
            LastKey = key;
            Debug.Log($"[MazeNav] GPS: {VoicePhrases.Text(key)}");
            Subtitle?.Invoke(VoicePhrases.Text(key));
            if (Muted) return;
            if (source.isPlaying && !urgent) { pending = key; return; }
            Play(key);
        }

        public void Repeat()
        {
            if (LastKey == null) return;
            lastSaid.Remove(LastKey);
            Say(LastKey, urgent: true);
        }

        public void SetMuted(bool muted)
        {
            if (muted) { Say("guidance_off", true, 0); Muted = true; pending = null; }
            else { Muted = false; Say("guidance_on", true, 0); }
        }

        void Update()
        {
            if (pending != null && !source.isPlaying)
            {
                string k = pending;
                pending = null;
                Play(k);
            }
        }

        void Play(string key)
        {
            if (!cache.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>("Voice/" + key);
                cache[key] = clip;
                if (clip == null) Debug.LogWarning($"[MazeNav] Missing voice clip '{key}'. Run Maze > Generate Voice Clips.");
            }
            if (clip == null) return;
            source.Stop();
            source.clip = clip;
            source.Play();
        }
    }
}

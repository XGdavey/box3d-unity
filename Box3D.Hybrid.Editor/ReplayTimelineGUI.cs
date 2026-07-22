using UnityEditor;
using UnityEngine;

namespace Box3D.Hybrid.Editor
{
    /// <summary>Draws the scrub timeline shared by the replay components (frame slider, transport,
    /// divergence read-out). See <see cref="IReplayTimeline"/>.</summary>
    internal static class ReplayTimelineGUI
    {
        public static void Draw(UnityEditor.Editor editor, IReplayTimeline replay)
        {
            if (!Application.isPlaying || !replay.IsCreated)
            {
                EditorGUILayout.HelpBox("Enter Play mode with a recording loaded to scrub the timeline.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);

            int last = Mathf.Max(0, replay.FrameCount - 1);
            int frame = EditorGUILayout.IntSlider("Frame", replay.Frame, 0, last);
            if (frame != replay.Frame) replay.SeekFrame(frame);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀")) replay.SeekFrame(Mathf.Max(0, replay.Frame - 1));
                if (GUILayout.Button(replay.IsPlaying ? "❚❚ Pause" : "▶ Play")) replay.TogglePlay();
                if (GUILayout.Button("▶|")) replay.StepFrame();
                if (GUILayout.Button("Restart")) replay.Restart();
            }

            EditorGUILayout.LabelField("Frame", $"{replay.Frame} / {last}");

            if (replay.HasDiverged)
                EditorGUILayout.HelpBox($"Replay DIVERGED at frame {replay.DivergeFrame} — the sim is non-deterministic.", MessageType.Error);
            else
                EditorGUILayout.HelpBox("Deterministic so far.", MessageType.None);

            editor.Repaint(); // keep the read-out live during playback
        }
    }
}

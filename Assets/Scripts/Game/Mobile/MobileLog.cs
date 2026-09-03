// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Mirrors the Unity log into the app's Documents folder as Player.log, so a player can hand
// it over from the Files app. Unity on iOS writes its log only to the system console, which
// is unreadable without a Mac and a cable - every bug report from the device so far has had
// to be reconstructed from a screenshot (the black dungeon, the travel deaths). Rotates to
// Player-prev.log at 2 MB so the folder never grows without bound. Device builds only.

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileLog
    {
        public const string FileName = "Player.log";
        public const string PreviousFileName = "Player-prev.log";
        const long rotateAtBytes = 2L * 1024 * 1024;

        static readonly object gate = new object();
        static string path;
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (hooked || !MobileContentPath.Active)
                return;
            hooked = true;
            path = Path.Combine(Application.persistentDataPath, FileName);
            try
            {
                Rotate();
                File.AppendAllText(path, string.Format("\n===== session {0:yyyy-MM-dd HH:mm:ss} =====\n", DateTime.Now));
            }
            catch (Exception) { }
            Application.logMessageReceivedThreaded += OnLog;
        }

        /// <summary>Pure: does a file of this size need rotating before the next write?</summary>
        public static bool NeedsRotation(long sizeBytes)
        {
            return sizeBytes >= rotateAtBytes;
        }

        static void Rotate()
        {
            if (!File.Exists(path) || !NeedsRotation(new FileInfo(path).Length))
                return;
            string prev = Path.Combine(Application.persistentDataPath, PreviousFileName);
            if (File.Exists(prev))
                File.Delete(prev);
            File.Move(path, prev);
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ');
            if (type != LogType.Log)
                sb.Append('[').Append(type).Append("] ");
            sb.Append(condition).Append('\n');
            if ((type == LogType.Exception || type == LogType.Error) && !string.IsNullOrEmpty(stackTrace))
                sb.Append(stackTrace).Append('\n');
            lock (gate)
            {
                try { File.AppendAllText(path, sb.ToString()); }
                catch (Exception) { }
            }
        }
    }
}

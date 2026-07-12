using System.Collections.Generic;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    public static class BoardSessionState
    {
        public static readonly Dictionary<string, Vector2> CardPositions = new Dictionary<string, Vector2>();
        public static readonly Dictionary<string, Vector2> WorldPhotoPositions = new Dictionary<string, Vector2>();
        public static readonly Dictionary<string, string> EvidenceZones = new Dictionary<string, string>();
        public static readonly HashSet<string> Connections = new HashSet<string>();
        public static readonly HashSet<string> UnlockedConclusions = new HashSet<string>();
        public static readonly Dictionary<string, int> TimelineSlots = new Dictionary<string, int>();

        public static void Clear()
        {
            CardPositions.Clear();
            WorldPhotoPositions.Clear();
            EvidenceZones.Clear();
            Connections.Clear();
            UnlockedConclusions.Clear();
            TimelineSlots.Clear();
        }

        public static string GetConnectionKey(string a, string b)
        {
            if (string.CompareOrdinal(a, b) <= 0)
            {
                return a + "|" + b;
            }

            return b + "|" + a;
        }
    }
}

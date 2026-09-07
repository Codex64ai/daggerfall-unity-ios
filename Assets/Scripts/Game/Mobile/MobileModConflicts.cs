// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Some mods in the iOS pack overlap DREAM or each other, and the engine settles overlaps by
// load order (the mod lower in the MODS list wins) - an order a fresh install gets from the
// filesystem, so which mod draws the ground is luck. This asks once, at the title menu, for
// each known group whose members are installed and enabled, then writes the answer into the
// normal mod settings (load order for "look" groups, enabled flags for exclusive ones). The
// question comes back only when the set of installed members changes. Players can still
// change their mind in the MODS window.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileModConflicts
    {
        public enum Kind { Look, Exclusive }

        public class Member
        {
            public string Label;
            public string[] TitlePrefixes;   // case-insensitive prefixes of mod titles
            public Member(string label, params string[] prefixes) { Label = label; TitlePrefixes = prefixes; }
        }

        public class Group
        {
            public string Id;
            public Kind Kind;
            public string Question;
            public Member[] Members;
        }

        public static readonly Group[] Groups =
        {
            new Group
            {
                Id = "world-textures", Kind = Kind.Look,
                Question = "{mods} replace the ground, roads, trees and plants. Which look do you want? " +
                           "The others stay installed and still supply everything else they have. " +
                           "You can change this later by reordering mods in the MODS window.",
                Members = new[]
                {
                    new Member("DREAM", "dream - textures", "dream - sprites"),
                    new Member("Vanilla Enhanced", "vanilla enhanced - base"),
                    new Member("Kokey's Temperate", "kokey's temperate"),
                },
            },
            new Group
            {
                Id = "wall-fixes", Kind = Kind.Look,
                Question = "{mods} both replace a few building wall textures. Which should win? " +
                           "DREAM has high-resolution walls; UBLaMF Textures has corrected vanilla ones.",
                Members = new[]
                {
                    new Member("DREAM", "dream - textures"),
                    new Member("UBLaMF Textures", "ublamf - textures"),
                },
            },
            new Group
            {
                Id = "mq-dungeon-exteriors", Kind = Kind.Look,
                Question = "{mods} both replace the exteriors of two main quest dungeons. Which one should win there?",
                Members = new[]
                {
                    new Member("Fixed Dungeon Ext.", "fixed dungeon exteriors"),
                    new Member("Smaller MQ Dungeons", "smaller main quest dungeons"),
                },
            },
            new Group
            {
                Id = "ironman", Kind = Kind.Exclusive,
                Question = "{mods} are two variants of the same mod and only one can be on at a time. Which one do you want? The other will be switched off.",
                Members = new[]
                {
                    new Member("Ironman (infighting)", "ironman madness (infighting)"),
                    new Member("Ironman (no infighting)", "ironman madness (no infighting)"),
                },
            },
            new Group
            {
                Id = "beast", Kind = Kind.Exclusive,
                Question = "{mods} each give one condition at the start and cannot be combined. Which one do you want? The others will be switched off.",
                Members = new[]
                {
                    new Member("Vampire", "become a vampire"),
                    new Member("Werewolf", "become a werewolf"),
                    new Member("Wereboar", "become a wereboar"),
                },
            },
        };

        public const string RecordFileName = "Conflicts.txt";

        /// <summary>The group's question with {mods} replaced by the installed members' names.</summary>
        public static string QuestionFor(Group group, IList<string> presentLabels)
        {
            string list = presentLabels.Count <= 1 ? string.Join("", presentLabels.ToArray())
                : string.Join(", ", presentLabels.Take(presentLabels.Count - 1).ToArray()) + " and " + presentLabels[presentLabels.Count - 1];
            string q = group.Question.Replace("{mods}", list);
            if (presentLabels.Count > 2) q = q.Replace(" both ", " all ").Replace(" are two variants", " are variants").Replace("The other will", "The others will");
            return q;
        }

        /// <summary>True when the mod title belongs to the member.</summary>
        public static bool Matches(Member member, string modTitle)
        {
            if (string.IsNullOrEmpty(modTitle)) return false;
            foreach (string p in member.TitlePrefixes)
                if (modTitle.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Stable key for "these members were installed when the question was answered".</summary>
        public static string Signature(IEnumerable<string> presentLabels)
        {
            return string.Join("|", presentLabels.OrderBy(l => l, StringComparer.Ordinal).ToArray());
        }

        /// <summary>
        /// New load order: <paramref name="chosen"/> mods, together with everything that depends on them
        /// (transitively), keep their relative order and move to just after the last <paramref name="others"/>
        /// mod; nothing else moves. Pure. <paramref name="dependsOn"/> names what a mod requires.
        /// </summary>
        public static List<string> MoveBelow(IList<string> order, ICollection<string> chosen, ICollection<string> others,
            Func<string, IEnumerable<string>> dependsOn)
        {
            var block = new HashSet<string>(chosen);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (string mod in order)
                    if (!block.Contains(mod) && dependsOn(mod).Any(block.Contains))
                    { block.Add(mod); grew = true; }
            }
            var moving = order.Where(block.Contains).ToList();
            int lastRival = -1, firstMoving = int.MaxValue;
            for (int i = 0; i < order.Count; i++)
            {
                if (others.Contains(order[i]) && !block.Contains(order[i])) lastRival = i;
                if (block.Contains(order[i]) && i < firstMoving) firstMoving = i;
            }
            if (lastRival < 0 || moving.Count == 0 || firstMoving > lastRival)
                return new List<string>(order);      // no rival, or already below every rival
            var rest = order.Where(m => !block.Contains(m)).ToList();
            int insertAt = rest.IndexOf(order[lastRival]) + 1;
            rest.InsertRange(insertAt, moving);
            return rest;
        }

        // ---- runtime ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            var go = new GameObject("MobileModConflicts");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        static string RecordPath { get { return Path.Combine(ModManager.Instance.ModDataDirectory, RecordFileName); } }

        static Dictionary<string, string> LoadRecord()
        {
            var d = new Dictionary<string, string>();
            try
            {
                if (File.Exists(RecordPath))
                    foreach (string line in File.ReadAllLines(RecordPath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) d[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
            }
            catch (Exception) { }
            return d;
        }

        static void SaveRecord(Dictionary<string, string> d)
        {
            try
            {
                Directory.CreateDirectory(ModManager.Instance.ModDataDirectory);
                File.WriteAllLines(RecordPath, d.Select(kv => kv.Key + "=" + kv.Value).ToArray());
            }
            catch (Exception ex) { Debug.LogWarning("[ModConflicts] could not save record: " + ex.Message); }
        }

        /// <summary>Members with at least one enabled mod installed.</summary>
        static List<Member> PresentMembers(Group group, Mod[] mods)
        {
            return group.Members.Where(m => mods.Any(mod => mod.Enabled && Matches(m, mod.Title))).ToList();
        }

        static void Apply(Group group, Member choice, List<Member> present)
        {
            Mod[] mods = ModManager.Instance.GetAllMods(true);
            if (group.Kind == Kind.Exclusive)
            {
                foreach (Mod mod in mods)
                    if (mod.Enabled && !Matches(choice, mod.Title) && present.Any(m => Matches(m, mod.Title)))
                    {
                        mod.Enabled = false;
                        Debug.Log("[ModConflicts] " + group.Id + ": switched off " + mod.Title);
                    }
            }
            else
            {
                var titles = mods.Select(m => m.Title).Distinct().ToList();
                var chosen = new HashSet<string>(titles.Where(t => Matches(choice, t)));
                var others = new HashSet<string>(titles.Where(t => !chosen.Contains(t) && present.Any(m => Matches(m, t))));
                var byTitle = new Dictionary<string, Mod>();
                foreach (Mod m in mods)
                    if (!byTitle.ContainsKey(m.Title)) byTitle[m.Title] = m;   // duplicate titles: first one wins
                Func<string, IEnumerable<string>> dependsOn = t =>
                {
                    Mod mod;
                    if (!byTitle.TryGetValue(t, out mod) || mod.ModInfo == null || mod.ModInfo.Dependencies == null)
                        return Enumerable.Empty<string>();
                    return mod.ModInfo.Dependencies.Where(d => !d.IsPeer)
                        .Select(d => ModManager.Instance.GetModFromName(d.Name)).Where(m => m != null).Select(m => m.Title);
                };
                List<string> newOrder = MoveBelow(titles, chosen, others, dependsOn);
                for (int i = 0; i < newOrder.Count; i++)
                    byTitle[newOrder[i]].LoadPriority = i;
                ModManager.Instance.SortMods();
                Debug.Log("[ModConflicts] " + group.Id + ": " + choice.Label + " now wins; order: " + string.Join(" < ", newOrder.ToArray()));
            }
            ModManager.WriteModSettings();
        }

        class Driver : MonoBehaviour
        {
            bool started;
            float titleSince = -1f;
            Queue<Group> pending;
            Dictionary<string, string> record;

            void Update()
            {
                if (started) return;
                if (ModManager.Instance == null || DaggerfallUI.Instance == null || DaggerfallUI.UIManager == null) return;
                if (!(DaggerfallUI.UIManager.TopWindow is DaggerfallStartWindow)) { titleSince = -1f; return; }
                if (titleSince < 0f) { titleSince = Time.realtimeSinceStartup; return; }
                if (Time.realtimeSinceStartup - titleSince < 0.5f) return;   // let the title screen draw first
                started = true;
                record = LoadRecord();
                Mod[] mods = ModManager.Instance.GetAllMods();
                pending = new Queue<Group>();
                foreach (Group g in Groups)
                {
                    var present = PresentMembers(g, mods);
                    if (present.Count < 2) continue;
                    string sig = Signature(present.Select(m => m.Label));
                    string answered;
                    if (record.TryGetValue(g.Id, out answered) && answered == sig) continue;
                    pending.Enqueue(g);
                }
                Debug.Log("[ModConflicts] groups to ask: " + pending.Count);
                Next();
            }

            void Next()
            {
                if (pending.Count == 0) { Destroy(gameObject); return; }
                Group g = pending.Dequeue();
                var present = PresentMembers(g, ModManager.Instance.GetAllMods());
                if (present.Count < 2) { Next(); return; }
                var ui = DaggerfallUI.UIManager;
                var box = new DaggerfallMessageBox(ui, ui.TopWindow, true);
                box.SetText(QuestionFor(g, present.Select(m => m.Label).ToList()));
                box.AddButton(DaggerfallMessageBox.MessageBoxButtons.OK, true);
                box.OnButtonClick += (sender, button) =>
                {
                    sender.CloseWindow();
                    var picker = new DaggerfallListPickerWindow(ui, ui.TopWindow);
                    bool picked = false;
                    float shownAt = Time.realtimeSinceStartup;
                    foreach (Member m in present)
                        picker.ListBox.AddItem(m.Label);
                    Action<int> choose = index =>
                    {
                        if (picked || index < 0 || index >= present.Count) return;
                        picked = true;
                        Debug.Log("[ModConflicts] " + g.Id + ": picked " + present[index].Label);
                        try
                        {
                            Apply(g, present[index], present);
                            record[g.Id] = Signature(present.Select(m => m.Label));
                            SaveRecord(record);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("[ModConflicts] " + g.Id + ": applying the choice failed: " + ex);
                        }
                        picker.CloseWindow();      // the player must never be stuck on this list
                    };
                    picker.OnItemPicked += (index, text) => choose(index);
                    // A single tap is the choice: the list's "use" gesture is a double-click or Enter,
                    // which touch never produces. The first half second is ignored so the list's own
                    // initial selection cannot answer for the player.
                    picker.ListBox.OnSelectItem += () =>
                    {
                        if (Time.realtimeSinceStartup - shownAt > 0.5f)
                            choose(picker.ListBox.SelectedIndex);
                    };
                    picker.OnClose += () => { if (!picked) Debug.Log("[ModConflicts] " + g.Id + ": no choice made, will ask again next launch"); Next(); };
                    ui.PushWindow(picker);
                };
                ui.PushWindow(box);
            }
        }
    }
}

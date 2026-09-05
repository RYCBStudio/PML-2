using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Notify.NET.Abstractions;
using Notify.NET.Platform;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// Writes freedesktop.org "Desktop Actions" (launcher shortcut entries) into an application's
    /// <c>.desktop</c> file. Desktop Actions appear in the right-click menu of the launcher/taskbar
    /// icon on GNOME, KDE, Unity and other environments; each action's <c>Exec</c> relaunches the
    /// executable with a jump-list activation argument.
    ///
    /// The file's <c>Actions</c> key and all <c>[Desktop Action *]</c> groups are owned and managed
    /// by this writer — existing ones are replaced on each write. If no installed <c>.desktop</c>
    /// file exists for the application, a minimal one is created under
    /// <c>$XDG_DATA_HOME/applications</c> (default <c>~/.local/share/applications</c>).
    /// </summary>
    internal static class DesktopFileWriter
    {
        /// <summary>Registers the supplied tasks as Desktop Actions, creating/merging the file.</summary>
        internal static void WriteActions(
            string desktopFileId,
            string appName,
            string executablePath,
            IReadOnlyList<JumpListTask> tasks)
        {
            string path = ResolveUserDesktopPath(desktopFileId);
            string? source = FindExistingDesktopPath(desktopFileId) ?? (File.Exists(path) ? path : null);

            List<Section> sections = source != null
                ? ParseSections(File.ReadAllLines(source))
                : CreateMinimal(appName, executablePath);

            ApplyActions(sections, executablePath, tasks);
            WriteFile(path, sections);
        }

        /// <summary>Removes the <c>Actions</c> key and all action groups managed by this writer.</summary>
        internal static void RemoveActions(string desktopFileId)
        {
            string path = ResolveUserDesktopPath(desktopFileId);
            if (!File.Exists(path)) return;

            List<Section> sections = ParseSections(File.ReadAllLines(path));
            ApplyActions(sections, executablePath: null, tasks: Array.Empty<JumpListTask>());
            WriteFile(path, sections);
        }

        // ------------------------------------------------------------------
        // Path resolution
        // ------------------------------------------------------------------

        private static string StripSuffix(string id) =>
            id.EndsWith(".desktop", StringComparison.Ordinal) ? id.Substring(0, id.Length - 8) : id;

        private static string DataHome()
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdg)) return xdg!;
            string home = Environment.GetEnvironmentVariable("HOME") ?? "~";
            return Path.Combine(home, ".local", "share");
        }

        /// <summary>The user-writable path we always write to.</summary>
        internal static string ResolveUserDesktopPath(string desktopFileId)
        {
            string id = StripSuffix(desktopFileId);
            return Path.Combine(DataHome(), "applications", id + ".desktop");
        }

        /// <summary>
        /// Looks for an existing installed <c>.desktop</c> file (user dir first, then the system
        /// <c>XDG_DATA_DIRS</c>) to use as the merge source. Returns null if none exists.
        /// </summary>
        private static string? FindExistingDesktopPath(string desktopFileId)
        {
            string id = StripSuffix(desktopFileId);
            string fileName = id + ".desktop";

            string userPath = Path.Combine(DataHome(), "applications", fileName);
            if (File.Exists(userPath)) return userPath;

            string dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS")
                              ?? "/usr/local/share:/usr/share";
            foreach (string dir in dataDirs.Split(':'))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                string candidate = Path.Combine(dir, "applications", fileName);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Section model + parsing
        // ------------------------------------------------------------------

        private sealed class Section
        {
            public string Header = "";              // e.g. "[Desktop Entry]"
            public readonly List<string> Lines = new List<string>(); // body lines (excluding header)

            public bool IsHeader(string name) =>
                Header.Equals("[" + name + "]", StringComparison.Ordinal);

            public bool IsDesktopActionGroup =>
                Header.StartsWith("[Desktop Action ", StringComparison.Ordinal);
        }

        private static List<Section> ParseSections(string[] lines)
        {
            var sections = new List<Section>();
            Section? current = null;
            // Preserve any leading comments/blank lines before the first group as a headerless section.
            var preamble = new Section { Header = "" };

            foreach (string line in lines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    current = new Section { Header = trimmed };
                    sections.Add(current);
                }
                else if (current != null)
                {
                    current.Lines.Add(line);
                }
                else
                {
                    preamble.Lines.Add(line);
                }
            }

            if (preamble.Lines.Count > 0)
                sections.Insert(0, preamble);
            return sections;
        }

        private static List<Section> CreateMinimal(string appName, string executablePath)
        {
            var entry = new Section { Header = "[Desktop Entry]" };
            entry.Lines.Add("Type=Application");
            entry.Lines.Add("Name=" + appName);
            entry.Lines.Add("Exec=" + QuoteExec(executablePath));
            entry.Lines.Add("Terminal=false");
            return new List<Section> { entry };
        }

        // ------------------------------------------------------------------
        // Action application
        // ------------------------------------------------------------------

        private static void ApplyActions(
            List<Section> sections, string? executablePath, IReadOnlyList<JumpListTask> tasks)
        {
            // 1. Drop all existing Desktop Action groups (we own them).
            sections.RemoveAll(s => s.IsDesktopActionGroup);

            // 2. Find (or create) the [Desktop Entry] group and reset its Actions key.
            Section? entry = sections.Find(s => s.IsHeader("Desktop Entry"));
            if (entry == null)
            {
                entry = new Section { Header = "[Desktop Entry]" };
                sections.Insert(0, entry);
            }
            entry.Lines.RemoveAll(l => l.TrimStart().StartsWith("Actions=", StringComparison.Ordinal));

            if (tasks.Count == 0) return; // RemoveActions path: leave no Actions key, no groups.

            var ids = new StringBuilder();
            foreach (JumpListTask t in tasks) ids.Append(t.Id).Append(';');
            entry.Lines.Add("Actions=" + ids);

            // 3. Append a group per task.
            foreach (JumpListTask t in tasks)
            {
                var group = new Section { Header = "[Desktop Action " + t.Id + "]" };
                group.Lines.Add("Name=" + t.Title);
                group.Lines.Add("Exec=" + QuoteExec(executablePath!) +
                                " " + JumpListActivation.ActivationFlag + " " + t.Id);
                if (!string.IsNullOrEmpty(t.IconPath))
                    group.Lines.Add("Icon=" + t.IconPath);
                sections.Add(group);
            }
        }

        /// <summary>Quotes an executable path for a Desktop Entry <c>Exec</c> value if needed.</summary>
        private static string QuoteExec(string exec)
        {
            if (exec.IndexOf(' ') < 0) return exec;
            // Desktop spec uses double quotes; escape embedded backslashes and quotes.
            return "\"" + exec.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        // ------------------------------------------------------------------
        // Writing
        // ------------------------------------------------------------------

        private static void WriteFile(string path, List<Section> sections)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);

            var sb = new StringBuilder();
            bool first = true;
            foreach (Section s in sections)
            {
                if (!string.IsNullOrEmpty(s.Header))
                {
                    if (!first) sb.AppendLine();
                    sb.AppendLine(s.Header);
                }
                foreach (string line in s.Lines) sb.AppendLine(line);
                first = false;
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}

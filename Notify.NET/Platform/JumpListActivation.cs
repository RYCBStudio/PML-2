using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Notify.NET.Platform
{
    /// <summary>
    /// Shared helpers for the jump-list relaunch protocol used on Windows and Linux.
    ///
    /// When the user clicks a jump-list/launcher task the OS relaunches the executable with
    /// <see cref="ActivationFlag"/> followed by the task id, e.g.
    /// <c>myapp --notify-jumplist open-library</c>. The bundled single-instance layer parses this,
    /// forwards the id to the primary instance and exits.
    /// </summary>
    internal static class JumpListActivation
    {
        /// <summary>The command-line flag that precedes a jump-list task id on relaunch.</summary>
        internal const string ActivationFlag = "--notify-jumplist";

        /// <summary>
        /// Extracts the task id from a jump-list activation command line, or <c>null</c> if these
        /// arguments are not a jump-list activation.
        /// </summary>
        internal static string? TryParseTaskId(string[]? args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], ActivationFlag, StringComparison.Ordinal))
                {
                    string id = args[i + 1];
                    return string.IsNullOrWhiteSpace(id) ? null : id;
                }
            }
            return null;
        }

        /// <summary>
        /// Builds a stable channel name (named pipe on Windows, Unix-domain socket name on Linux)
        /// for the single-instance listener. Derived from a caller-supplied key (the AppUserModelId
        /// on Windows or the .desktop id on Linux) so that all instances of the same application —
        /// and only that application — rendezvous on the same channel.
        /// </summary>
        internal static string ChannelName(string key)
        {
            // Hash the key so the channel name is fixed-length and free of path-hostile characters.
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key ?? string.Empty));
            var sb = new StringBuilder("notifynet-jl-", 29);
            for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Best-effort absolute path to the current process executable, used as the relaunch target.
        /// </summary>
        internal static string CurrentExecutablePath()
        {
            try
            {
                string? path = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(path)) return path!;
            }
            catch
            {
                /* MainModule can throw for some hosts; fall through. */
            }
            return AppContext.BaseDirectory;
        }
    }
}

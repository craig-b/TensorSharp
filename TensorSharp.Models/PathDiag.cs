using System;
using System.Collections.Generic;

namespace TensorSharp.Models
{
    /// <summary>One-time stderr notes for capability gates that decline a fast
    /// path and fall back silently. Deduped per (path, reason), not per path,
    /// so a later, different decline still prints after an earlier one is
    /// fixed. Intentional opt-outs (TS_* env vars, never-supported backends,
    /// tensor-parallel mode) stay quiet by convention. Covers model-internal
    /// gates below the planner's visibility; the planner's own choices are
    /// recorded by ExecutionPlan.Rejections / BatchExecutor.LogPlanTransition.</summary>
    public static class PathDiag
    {
        private static readonly HashSet<string> s_printed = new(StringComparer.Ordinal);

        /// <summary>One-time "[path] not engaged: reason; using fallback." stderr note.</summary>
        public static void DeclineOnce(string path, string reason, string fallback)
        {
            lock (s_printed)
            {
                if (!s_printed.Add(path + "\n" + reason))
                    return;
            }
            Console.Error.WriteLine($"[{path}] not engaged: {reason}; using {fallback}.");
        }
    }
}

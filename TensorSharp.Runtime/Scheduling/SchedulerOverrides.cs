namespace TensorSharp.Runtime.Scheduling
{
    /// <summary>Typed host overrides for the scheduler / speculative-decoding
    /// knobs that were historically carried by <c>TS_SCHED_*</c> /
    /// <c>TS_MTP_*</c> env writes. The host (server CLI) sets
    /// <see cref="Current"/> once at startup; each null property falls back to
    /// the existing env-var read at the existing read site, so an absent or
    /// all-null instance is byte-identical to the env-only behaviour.
    /// Process-wide ambient state by design, following the
    /// <c>KvCacheDtypeConfig</c> precedent: consumers (BatchExecutor's
    /// per-step options, engine construction, model MTP gates) have no
    /// per-request host context to thread a parameter through.</summary>
    public sealed record SchedulerOverrides
    {
        /// <summary>CLI: <c>--no-continuous-batching</c>. Env: <c>TS_SCHED_DISABLE_BATCHED</c>.</summary>
        public bool? DisableBatched { get; init; }

        /// <summary>CLI: <c>--prefill-chunk-size</c>. Env: <c>TS_SCHED_PREFILL_CHUNK</c>.</summary>
        public int? PrefillChunkSize { get; init; }

        /// <summary>CLI: <c>--mtp-spec</c> / <c>--no-mtp-spec</c>. Env: <c>TS_MTP_SPEC</c>.</summary>
        public bool? MtpSpeculative { get; init; }

        /// <summary>CLI: <c>--mtp-draft</c>. Env: <c>TS_MTP_DRAFT</c>.</summary>
        public int? MtpMaxDraftTokens { get; init; }

        /// <summary>CLI: <c>--mtp-pmin</c>. Env: <c>TS_MTP_PMIN</c>.</summary>
        public float? MtpMinDraftProb { get; init; }

        /// <summary>CLI: <c>--mtp-draft-model</c>. Env: <c>TS_MTP_DRAFT_MODEL</c>.</summary>
        public string? MtpDraftModelPath { get; init; }

        /// <summary>CLI: <c>--draft-model</c>. Env: <c>TS_DSV4_DSPARK</c>.</summary>
        public string? Dsv4DsparkPath { get; init; }

        /// <summary>True when any MTP/speculative field is set (drives the
        /// host's "configured via CLI" startup log).</summary>
        public bool HasMtpOverrides =>
            MtpSpeculative.HasValue || MtpMaxDraftTokens.HasValue || MtpMinDraftProb.HasValue
            || MtpDraftModelPath != null || Dsv4DsparkPath != null;

        /// <summary>Ambient overrides; null means env-only behaviour everywhere.</summary>
        public static SchedulerOverrides? Current { get; set; }
    }
}

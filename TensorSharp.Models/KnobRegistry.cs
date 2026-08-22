using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TensorSharp.Models
{
    /// <summary>Which options record declares a knob.</summary>
    public enum KnobScope
    {
        Model,
        Qwen35,
    }

    /// <summary>Value shape of a knob.</summary>
    public enum KnobKind
    {
        Bool,
        Int,
    }

    /// <summary>How a boolean knob's env var is interpreted. Exact truth
    /// tables, byte-for-byte: <see cref="KnobResolver"/> IS the knob's env
    /// read, so a registry entry that misstates its dialect changes runtime
    /// behaviour, not just docs.</summary>
    public enum BoolDialect
    {
        /// <summary>Unset → on; off only when exactly (Ordinal) "0" —
        /// "false" and every other value → on.</summary>
        LooseZeroOnly,

        /// <summary>Unset → on; "0" (Ordinal) or "false" (case-insensitive)
        /// → off; anything else → on.</summary>
        LooseZeroOrFalse,

        /// <summary>Off unless the value is exactly (Ordinal) "1".</summary>
        StrictOptIn,

        /// <summary>DISABLE_*-style opt-out: the feature is off only when the
        /// var is exactly (Ordinal) "1"; unset or any other value → on.
        /// The property is positive-sense.</summary>
        InvertedDisableOne,

        /// <summary>DISABLE_*-style opt-out: off when the var is "1" (Ordinal)
        /// or "true" (case-insensitive); unset or anything else → on.
        /// The property is positive-sense.</summary>
        InvertedDisableOneOrTrue,

        /// <summary>Unset → on; set → on only when exactly (Ordinal) "1".</summary>
        OnRequiresExactlyOneWhenSet,
    }

    /// <summary>One declared tuning knob: the single source of truth tying an
    /// options-record property to its env var, parse dialect, and (optionally)
    /// CLI flags. Inert metadata consumed by the config layer, docs generation,
    /// and completeness tests.</summary>
    public sealed record KnobDef
    {
        /// <summary>Property name on <see cref="ModelOptions"/> / <see cref="Qwen35Options"/>.</summary>
        public string Property { get; init; }

        public KnobScope Scope { get; init; }

        /// <summary>Environment variable read by the knob's site. Canonical
        /// operator-facing name; never aliased.</summary>
        public string EnvVar { get; init; }

        public KnobKind Kind { get; init; }

        /// <summary>Bool knobs only.</summary>
        public BoolDialect? Dialect { get; init; }

        /// <summary>Int knobs only: values below this are ignored by the read site.</summary>
        public int? IntMin { get; init; }

        /// <summary>One-line description for generated docs and --help.</summary>
        public string Summary { get; init; }

        /// <summary>CLI flags that carry this knob (positive then negative
        /// forms, including aliases); empty for env/config-only knobs.</summary>
        public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();

        /// <summary>False for the rare knob whose env var cannot be resolved
        /// centrally at model construction (today only
        /// <c>TS_MLX_EVAL_EVERY_N_LAYERS</c>: per-model alias vars shadow the
        /// canonical name and per-family defaults differ, so its read sites
        /// keep their own env fallback behind the options override).</summary>
        public bool EnvResolvedAtCreate { get; init; } = true;

        /// <summary>Configuration key the knob binds from.</summary>
        public string ConfigKey => "Model:" + Property;
    }

    /// <summary>The knob table. Every settable property of the options records
    /// appears here exactly once (enforced by tests).</summary>
    public static class KnobRegistry
    {
        static KnobDef B(string property, KnobScope scope, string envVar, BoolDialect dialect, string summary, params string[] flags)
            => new() { Property = property, Scope = scope, EnvVar = envVar, Kind = KnobKind.Bool, Dialect = dialect, Summary = summary, Flags = flags };

        static KnobDef I(string property, KnobScope scope, string envVar, int intMin, string summary)
            => new() { Property = property, Scope = scope, EnvVar = envVar, Kind = KnobKind.Int, IntMin = intMin, Summary = summary };

        public static readonly IReadOnlyList<KnobDef> All = new[]
        {
            // ---- ModelOptions ---------------------------------------------
            B(nameof(ModelOptions.MlxMlockGguf), KnobScope.Model, "TS_MLX_MLOCK_GGUF", BoolDialect.OnRequiresExactlyOneWhenSet,
                "mlock(2) the GGUF mapping so weights stay resident (MLX)."),
            B(nameof(ModelOptions.GgmlF32Resident), KnobScope.Model, "TS_GGML_F32_RESIDENT", BoolDialect.LooseZeroOnly,
                "Keep F32 linear weights device-resident on GGML instead of rebinding per call."),
            B(nameof(ModelOptions.FusedDenseFfn), KnobScope.Model, "TS_DISABLE_FUSED_DENSE_FFN", BoolDialect.InvertedDisableOneOrTrue,
                "Fused dense norm+FFN+add chain."),
            B(nameof(ModelOptions.GgmlTpFusedMatmul), KnobScope.Model, "TS_GGML_TP_FUSED_MATMUL", BoolDialect.StrictOptIn,
                "Submit both TP ranks' linears from one thread."),
            B(nameof(ModelOptions.MlxDeviceKvCopy), KnobScope.Model, "TS_MLX_DEVICE_KV_COPY", BoolDialect.LooseZeroOnly,
                "On-device MLX KV scatter."),
            B(nameof(ModelOptions.MlxFusedKvWrite), KnobScope.Model, "TS_MLX_FUSED_KV_WRITE", BoolDialect.LooseZeroOnly,
                "Single multi-dim slice_update per KV block (MLX)."),
            B(nameof(ModelOptions.PrefillWarmup), KnobScope.Model, "TS_PREFILL_WARMUP", BoolDialect.LooseZeroOnly,
                "Startup dummy long-prompt prefill warmup."),
            I(nameof(ModelOptions.PrefillWarmupLength), KnobScope.Model, "TS_PREFILL_WARMUP_LEN", 2,
                "Warmup prompt length; unset means backend-derived default."),
            B(nameof(ModelOptions.MlxKernelWarmup), KnobScope.Model, "TS_MLX_KERNEL_WARMUP", BoolDialect.StrictOptIn,
                "Force MLX kernel warmup despite large resident quantized weights."),
            B(nameof(ModelOptions.EncoderYield), KnobScope.Model, "TS_ENCODER_YIELD", BoolDialect.LooseZeroOnly,
                "Yield the GPU compute lock during encoder work."),
            I(nameof(ModelOptions.PrefillChunk), KnobScope.Model, "TS_PREFILL_CHUNK", 1,
                "Prompt-prefill chunk width; unset means backend default."),
            I(nameof(ModelOptions.MlxEvalEveryNLayers), KnobScope.Model, "TS_MLX_EVAL_EVERY_N_LAYERS", 0,
                "MLX graph-eval interval in layers; unset means 16.") with { EnvResolvedAtCreate = false },
            B(nameof(ModelOptions.CudaMoeOnDevice), KnobScope.Model, "TS_CUDA_MOE_ONDEVICE", BoolDialect.LooseZeroOnly,
                "On-device direct-CUDA MoE decode."),
            B(nameof(ModelOptions.CudaMoePrefillOnDevice), KnobScope.Model, "TS_CUDA_MOE_PREFILL_ONDEVICE", BoolDialect.StrictOptIn,
                "On-device batched MoE prefill."),
            B(nameof(ModelOptions.CudaMoePrefillGrouped), KnobScope.Model, "TS_CUDA_MOE_PREFILL_GROUPED", BoolDialect.LooseZeroOnly,
                "Grouped gather/scatter CUDA MoE prefill."),

            // ---- Qwen35Options --------------------------------------------
            B(nameof(Qwen35Options.FullDecode), KnobScope.Qwen35, "TS_QWEN35_FULL_DECODE", BoolDialect.LooseZeroOnly,
                "Whole-model fused decode graph (CUDA/Vulkan/Metal)."),
            B(nameof(Qwen35Options.MetalTokenInput), KnobScope.Qwen35, "TS_QWEN35_METAL_TOKEN_INPUT", BoolDialect.LooseZeroOnly,
                "Metal token-id input vs legacy host-dequantized embedding."),
            B(nameof(Qwen35Options.FusedVerify), KnobScope.Qwen35, "TS_QWEN35_FUSED_VERIFY", BoolDialect.LooseZeroOnly,
                "Fused multi-token MTP-verify trunk."),
            B(nameof(Qwen35Options.VerifyResident), KnobScope.Qwen35, "TS_QWEN35_VERIFY_RESIDENT", BoolDialect.StrictOptIn,
                "Device-resident GDN verify state."),
            B(nameof(Qwen35Options.MtpFusedDraft), KnobScope.Qwen35, "TS_MTP_FUSED_DRAFT", BoolDialect.LooseZeroOnly,
                "Fused MTP draft/catch-up block."),
            B(nameof(Qwen35Options.PrefillVerify), KnobScope.Qwen35, "TS_QWEN35_PREFILL_VERIFY", BoolDialect.LooseZeroOnly,
                "Whole-model fused prefill."),
            I(nameof(Qwen35Options.CudaPrefillGraphMaxSeqLen), KnobScope.Qwen35, "TS_CUDA_PREFILL_GRAPH_MAX_SEQLEN", 0,
                "Max prefill seqlen for CUDA graph capture; 0 = unlimited; unset means 512."),
            B(nameof(Qwen35Options.Batched), KnobScope.Qwen35, "TS_QWEN35_BATCHED", BoolDialect.LooseZeroOrFalse,
                "Master switch for the batched (continuous-batching) path.",
                "--continuous-batching", "--no-continuous-batching", "--paged-batching", "--no-paged-batching"),
            B(nameof(Qwen35Options.BatchedFused), KnobScope.Qwen35, "TS_QWEN35_BATCHED_FUSED", BoolDialect.LooseZeroOrFalse,
                "Fused batched decode graph."),
            B(nameof(Qwen35Options.BfdNoMirror), KnobScope.Qwen35, "TS_QWEN35_BFD_NOMIRROR", BoolDialect.StrictOptIn,
                "Skip host mirror in batched fused decode."),
            B(nameof(Qwen35Options.BatchedGdnNative), KnobScope.Qwen35, "TS_QWEN35_BATCHED_GDN_NATIVE", BoolDialect.StrictOptIn,
                "Native batched GDN kernels."),
            B(nameof(Qwen35Options.Migrate), KnobScope.Qwen35, "TS_QWEN35_MIGRATE", BoolDialect.LooseZeroOnly,
                "Sequence migration between batched and per-seq paths."),
            B(nameof(Qwen35Options.MlxTensorPagedAttn), KnobScope.Qwen35, "TS_QWEN35_MLX_TENSOR_PAGED_ATTN", BoolDialect.StrictOptIn,
                "MLX tensor-level paged attention."),
            B(nameof(Qwen35Options.FusedRecPrefill), KnobScope.Qwen35, "TS_QWEN35_FUSED_REC_PREFILL", BoolDialect.LooseZeroOnly,
                "Fused recurrent prefill."),
            B(nameof(Qwen35Options.CudaGdnNative), KnobScope.Qwen35, "TS_CUDA_QWEN35_GDN_NATIVE", BoolDialect.LooseZeroOrFalse,
                "Native CUDA GDN kernels."),
            B(nameof(Qwen35Options.MlxGdnPackedKernels), KnobScope.Qwen35, "TS_MLX_QWEN35_GDN_PACKED_KERNELS", BoolDialect.LooseZeroOnly,
                "Packed MLX GDN kernels."),
            B(nameof(Qwen35Options.MetalGdnInplaceState), KnobScope.Qwen35, "TS_QWEN35_METAL_GDN_INPLACE_STATE", BoolDialect.LooseZeroOnly,
                "In-place Metal GDN state updates."),
            B(nameof(Qwen35Options.GdnChunkedPrefill), KnobScope.Qwen35, "GDN_DISABLE_CHUNKED_PREFILL", BoolDialect.InvertedDisableOne,
                "Chunked GDN prefill."),
            I(nameof(Qwen35Options.GdnChunkPrefillMinSeqLen), KnobScope.Qwen35, "GDN_CHUNK_PREFILL_MIN_SEQ_LEN", 1,
                "Min seqlen before GDN prefill chunks; unset means backend default."),
            B(nameof(Qwen35Options.GdnVerifyChunked), KnobScope.Qwen35, "GDN_VERIFY_CHUNKED", BoolDialect.StrictOptIn,
                "Chunked GDN verify (CI/debug)."),
            B(nameof(Qwen35Options.FusedQkNormRope), KnobScope.Qwen35, "TS_FUSED_QKNORM_ROPE", BoolDialect.LooseZeroOnly,
                "Fused QK-norm + RoPE."),
            I(nameof(Qwen35Options.FusedAttnLayerMinSeqLen), KnobScope.Qwen35, "FUSED_ATTN_LAYER_MIN_SEQ_LEN", 1,
                "Min seqlen for the fused attention layer kernel; unset means 1."),
            I(nameof(Qwen35Options.MlxFlashAttnDecodeMinSeqLen), KnobScope.Qwen35, "TS_MLX_FLASH_ATTN_DECODE_MIN_SEQ_LEN", 1,
                "Min seqlen for MLX flash-attention decode."),
            B(nameof(Qwen35Options.MlxChunkedVectorPrefill), KnobScope.Qwen35, "TS_MLX_CHUNKED_VECTOR_PREFILL", BoolDialect.StrictOptIn,
                "Chunked MLX vector prefill."),
            B(nameof(Qwen35Options.MlxGpuDeinterleave), KnobScope.Qwen35, "TS_MLX_QWEN_GPU_DEINTERLEAVE", BoolDialect.StrictOptIn,
                "GPU strided-view Q/gate deinterleave."),
            B(nameof(Qwen35Options.MropeNative), KnobScope.Qwen35, "TS_QWEN35_MROPE_NATIVE", BoolDialect.LooseZeroOnly,
                "Native MRoPE position tables."),
            B(nameof(Qwen35Options.FusedFfnPrefill), KnobScope.Qwen35, "QWEN35_DISABLE_FUSED_FFN", BoolDialect.InvertedDisableOne,
                "Fused FFN during prefill."),
            B(nameof(Qwen35Options.StackedMoe), KnobScope.Qwen35, "TS_QWEN35_STACKED_MOE", BoolDialect.LooseZeroOnly,
                "Stacked-experts MoE weights."),
            B(nameof(Qwen35Options.MlxBatchedMoeDecode), KnobScope.Qwen35, "TS_MLX_BATCHED_MOE_DECODE", BoolDialect.LooseZeroOnly,
                "Batched MLX MoE decode (doubles MLX weight memory)."),
            B(nameof(Qwen35Options.MlxMoeFusedGateUpSilu), KnobScope.Qwen35, "TS_MLX_MOE_FUSED_GATE_UP_SILU", BoolDialect.LooseZeroOnly,
                "Fused MLX MoE gate/up/SiLU."),
            B(nameof(Qwen35Options.MlxDeviceRouter), KnobScope.Qwen35, "TS_MLX_DEVICE_ROUTER", BoolDialect.LooseZeroOnly,
                "On-device MLX MoE router."),
            B(nameof(Qwen35Options.MlxEvalDecodeLayerBoundaries), KnobScope.Qwen35, "TS_MLX_EVAL_DECODE_LAYER_BOUNDARIES", BoolDialect.StrictOptIn,
                "MLX eval at decode layer boundaries."),
            B(nameof(Qwen35Options.MlxEvalFinalLogits), KnobScope.Qwen35, "TS_MLX_EVAL_FINAL_LOGITS", BoolDialect.StrictOptIn,
                "MLX eval of final logits only."),
            B(nameof(Qwen35Options.TpFused), KnobScope.Qwen35, "TS_QWEN35_TP_FUSED", BoolDialect.LooseZeroOnly,
                "Fused tensor-parallel path."),
            B(nameof(Qwen35Options.TpFusedDecode), KnobScope.Qwen35, "TS_QWEN35_TP_FUSED_DECODE", BoolDialect.LooseZeroOnly,
                "Fused TP decode."),
            B(nameof(Qwen35Options.TpFusedPrefill), KnobScope.Qwen35, "TS_QWEN35_TP_FUSED_PREFILL", BoolDialect.LooseZeroOnly,
                "Fused TP prefill."),
            B(nameof(Qwen35Options.TpMoePrefillOnDevice), KnobScope.Qwen35, "TS_TP_MOE_PREFILL_ONDEVICE", BoolDialect.LooseZeroOnly,
                "On-device TP MoE prefill."),
            B(nameof(Qwen35Options.VencFused), KnobScope.Qwen35, "TS_QWEN35_VENC_FUSED", BoolDialect.LooseZeroOnly,
                "Fused vision encoder."),
            B(nameof(Qwen35Options.VencFusedAttn), KnobScope.Qwen35, "TS_QWEN35_VENC_FUSED_ATTN", BoolDialect.LooseZeroOnly,
                "Fused vision-encoder attention."),
            B(nameof(Qwen35Options.VencTrace), KnobScope.Qwen35, "TS_QWEN35_VENC_TRACE", BoolDialect.StrictOptIn,
                "Vision-encoder tracing."),
            B(nameof(Qwen35Options.LayerTrace), KnobScope.Qwen35, "TS_QWEN35_LAYER_TRACE", BoolDialect.StrictOptIn,
                "Per-layer tracing."),
        };

        public static KnobDef ByProperty(string property) => All.FirstOrDefault(k => k.Property == property);
        public static KnobDef ByEnvVar(string envVar) => All.FirstOrDefault(k => k.EnvVar == envVar);

        /// <summary>Knob-reference table for docs/knobs.md. A test keeps the
        /// committed file in sync with this output.</summary>
        public static string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Tuning knobs");
            sb.AppendLine();
            sb.AppendLine("Generated from `KnobRegistry` — do not edit by hand; regenerate via");
            sb.AppendLine("`KnobRegistryTests.CommittedKnobDocIsInSync` when the registry changes.");
            sb.AppendLine();
            sb.AppendLine("Every knob can be set per-instance via the typed options records; unset");
            sb.AppendLine("knobs are resolved from their environment variable once, at model");
            sb.AppendLine("construction, with the exact dialect listed below (KnobResolver). Env");
            sb.AppendLine("changes after a model is created do not affect that model.");
            sb.AppendLine();
            sb.AppendLine("On the server, any knob is reachable without a dedicated flag:");
            sb.AppendLine("`--set ENV_VAR=VALUE` (bools take `1`/`0`) applies at CLI precedence, and a");
            sb.AppendLine("`--config` file's `\"presets\"` object holds per-model blocks keyed by GGUF");
            sb.AppendLine("file name whose keys are the Property column below. Precedence, lowest to");
            sb.AppendLine("highest: env var, per-model preset, CLI flag / `--set`.");
            sb.AppendLine();
            sb.AppendLine("| Property | Env var | Type | Dialect | CLI flags | Description |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var k in All)
            {
                string type = k.Kind == KnobKind.Bool ? "bool" : $"int (≥ {k.IntMin})";
                string dialect = k.Kind != KnobKind.Bool ? "" : k.Dialect switch
                {
                    BoolDialect.LooseZeroOnly => "loose, default on; off only on `0`",
                    BoolDialect.LooseZeroOrFalse => "loose, default on; off on `0`/`false`",
                    BoolDialect.StrictOptIn => "strict opt-in, default off",
                    BoolDialect.InvertedDisableOne => "inverted opt-out, default on; `1` disables",
                    BoolDialect.InvertedDisableOneOrTrue => "inverted opt-out, default on; `1`/`true` disables",
                    BoolDialect.OnRequiresExactlyOneWhenSet => "default on; set keeps on only when `1`",
                    _ => "",
                };
                string flags = string.Join(" ", k.Flags.Select(f => $"`{f}`"));
                sb.AppendLine($"| `{k.Property}` | `{k.EnvVar}` | {type} | {dialect} | {flags} | {k.Summary} |");
            }
            return sb.ToString();
        }
    }
}

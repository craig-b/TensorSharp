using System;
using System.Collections.Generic;

namespace TensorSharp.Models
{
    /// <summary>Typed, per-instance overrides for the model-layer tuning knobs
    /// that have historically been environment-variable-only. The model-layer
    /// counterpart of the planner's <c>ExecutionOptions</c>: every property is
    /// nullable and unset (null) means "exactly the existing env-var behaviour,
    /// read at the same time with the same parse dialect", so passing no options
    /// — or <see cref="Default"/> — is byte-identical to today. A set property
    /// takes precedence over its env var for this model instance only.
    ///
    /// Plain init-only record by design: hosts can bind it from configuration
    /// (e.g. <c>IConfiguration.Bind</c>) and hand it to
    /// <c>ModelBase.Create</c>, instead of mutating process-global env vars.</summary>
    public record ModelOptions
    {
        /// <summary>All-null options: pure env-var behaviour. Safe to share.</summary>
        public static ModelOptions Default { get; } = new();

        /// <summary>mlock(2) the GGUF mapping so weights stay resident (MLX).
        /// Env: <c>TS_MLX_MLOCK_GGUF</c> (default on; only exactly "1" keeps it on when set).</summary>
        public bool? MlxMlockGguf { get; init; }

        /// <summary>Keep F32 linear weights device-resident on GGML instead of
        /// rebinding per call. Env: <c>TS_GGML_F32_RESIDENT</c> (loose, default on).</summary>
        public bool? GgmlF32Resident { get; init; }

        /// <summary>Fused dense norm+FFN+add chain; disable to A/B the unfused
        /// path. Env: <c>TS_DISABLE_FUSED_DENSE_FFN</c> (inverted strict opt-out,
        /// default enabled).</summary>
        public bool? FusedDenseFfn { get; init; }

        /// <summary>Submit both TP ranks' linears from one thread.
        /// Env: <c>TS_GGML_TP_FUSED_MATMUL</c> (strict opt-in, default off).</summary>
        public bool? GgmlTpFusedMatmul { get; init; }

        /// <summary>On-device MLX KV scatter. Env: <c>TS_MLX_DEVICE_KV_COPY</c>
        /// (loose, default on).</summary>
        public bool? MlxDeviceKvCopy { get; init; }

        /// <summary>Single multi-dim slice_update per KV block instead of a
        /// per-head loop (MLX). Env: <c>TS_MLX_FUSED_KV_WRITE</c> (loose, default on).</summary>
        public bool? MlxFusedKvWrite { get; init; }

        /// <summary>Startup dummy long-prompt prefill warmup.
        /// Env: <c>TS_PREFILL_WARMUP</c> (loose, default on).</summary>
        public bool? PrefillWarmup { get; init; }

        /// <summary>Warmup prompt length; null → backend-derived default.
        /// Env: <c>TS_PREFILL_WARMUP_LEN</c> (int ≥ 2).</summary>
        public int? PrefillWarmupLength { get; init; }

        /// <summary>Force MLX kernel warmup despite large resident quantized
        /// weights. Env: <c>TS_MLX_KERNEL_WARMUP</c> (strict opt-in, default off).</summary>
        public bool? MlxKernelWarmup { get; init; }

        /// <summary>Yield the GPU compute lock during encoder work.
        /// Env: <c>TS_ENCODER_YIELD</c> (loose, default on).</summary>
        public bool? EncoderYield { get; init; }

        /// <summary>Prompt-prefill chunk width; null → backend default.
        /// Env: <c>TS_PREFILL_CHUNK</c> (int ≥ 1).</summary>
        public int? PrefillChunk { get; init; }

        /// <summary>MLX graph-eval interval in layers; null → 16.
        /// Env: <c>TS_MLX_EVAL_EVERY_N_LAYERS</c> (int ≥ 0).</summary>
        public int? MlxEvalEveryNLayers { get; init; }

        /// <summary>On-device direct-CUDA MoE decode.
        /// Env: <c>TS_CUDA_MOE_ONDEVICE</c> (loose, default on).</summary>
        public bool? CudaMoeOnDevice { get; init; }

        /// <summary>On-device batched MoE prefill (currently slower; see the
        /// site comment). Env: <c>TS_CUDA_MOE_PREFILL_ONDEVICE</c> (strict
        /// opt-in, default off).</summary>
        public bool? CudaMoePrefillOnDevice { get; init; }

        /// <summary>Grouped gather/scatter CUDA MoE prefill.
        /// Env: <c>TS_CUDA_MOE_PREFILL_GROUPED</c> (loose, default on).</summary>
        public bool? CudaMoePrefillGrouped { get; init; }

        /// <summary>One-line summary of the explicitly-set overrides (empty when
        /// everything is null/default). Reflection is fine here: called once at
        /// model load for logging.</summary>
        public string DescribeOverrides()
        {
            var parts = new List<string>();
            foreach (var p in GetType().GetProperties())
            {
                if (p.Name == nameof(Default) || p.GetIndexParameters().Length != 0)
                    continue;
                object v = p.GetValue(this);
                if (v != null)
                    parts.Add($"{p.Name}={v}");
            }
            return string.Join(", ", parts);
        }
    }
}

using System;

namespace TensorSharp.Models
{
    /// <summary>Qwen 3.5 tuning knobs, extending <see cref="ModelOptions"/> with
    /// the model-specific gates. Same contract: null = existing env-var
    /// behaviour, unchanged; set = per-instance override. Properties whose env
    /// var is an inverted "disable" flag are expressed positive-sense here.</summary>
    public sealed record Qwen35Options : ModelOptions
    {
        /// <summary>All-null options: pure env-var behaviour. Safe to share.</summary>
        public static new Qwen35Options Default { get; } = new();

        // ---- whole-model fused paths -----------------------------------

        /// <summary>Whole-model fused decode graph (CUDA/Vulkan/Metal).
        /// Env: <c>TS_QWEN35_FULL_DECODE</c> (loose, default on).</summary>
        public bool? FullDecode { get; init; }

        /// <summary>Metal token-id input vs legacy host-dequantized embedding.
        /// Env: <c>TS_QWEN35_METAL_TOKEN_INPUT</c> (loose, default on).</summary>
        public bool? MetalTokenInput { get; init; }

        /// <summary>Fused multi-token MTP-verify trunk.
        /// Env: <c>TS_QWEN35_FUSED_VERIFY</c> (loose, default on).</summary>
        public bool? FusedVerify { get; init; }

        /// <summary>Device-resident GDN verify state (known-broken, see site).
        /// Env: <c>TS_QWEN35_VERIFY_RESIDENT</c> (strict opt-in, default off).</summary>
        public bool? VerifyResident { get; init; }

        /// <summary>Fused MTP draft/catch-up block.
        /// Env: <c>TS_MTP_FUSED_DRAFT</c> (loose, default on).</summary>
        public bool? MtpFusedDraft { get; init; }

        /// <summary>Whole-model fused prefill.
        /// Env: <c>TS_QWEN35_PREFILL_VERIFY</c> (loose, default on).</summary>
        public bool? PrefillVerify { get; init; }

        /// <summary>Max prefill seqlen for CUDA graph capture; 0 = unlimited;
        /// null → 512. Env: <c>TS_CUDA_PREFILL_GRAPH_MAX_SEQLEN</c> (int ≥ 0).</summary>
        public int? CudaPrefillGraphMaxSeqLen { get; init; }

        // ---- batched / continuous-batching path ------------------------

        /// <summary>Master switch for the batched (continuous-batching) path.
        /// Env: <c>TS_QWEN35_BATCHED</c> (loose tolerant, default on).</summary>
        public bool? Batched { get; init; }

        /// <summary>True token-batched fused decode.
        /// Env: <c>TS_QWEN35_BATCHED_FUSED</c> (loose tolerant, default on).</summary>
        public bool? BatchedFused { get; init; }

        /// <summary>Skip mirroring fresh K/V slots back to the host paged pool.
        /// Env: <c>TS_QWEN35_BFD_NOMIRROR</c> (strict opt-in, default off).</summary>
        public bool? BfdNoMirror { get; init; }

        /// <summary>Native batched GDN kernel.
        /// Env: <c>TS_QWEN35_BATCHED_GDN_NATIVE</c> (strict opt-in, default off).</summary>
        public bool? BatchedGdnNative { get; init; }

        /// <summary>Linear-to-paged KV/GDN state migration for the N=1 owner.
        /// Env: <c>TS_QWEN35_MIGRATE</c> (loose, default on).</summary>
        public bool? Migrate { get; init; }

        /// <summary>Tensor-path paged attention on MLX.
        /// Env: <c>TS_QWEN35_MLX_TENSOR_PAGED_ATTN</c> (strict opt-in, default off).</summary>
        public bool? MlxTensorPagedAttn { get; init; }

        // ---- GDN recurrence --------------------------------------------

        /// <summary>Device-resident fused recurrent-layer prefill.
        /// Env: <c>TS_QWEN35_FUSED_REC_PREFILL</c> (loose, default on).</summary>
        public bool? FusedRecPrefill { get; init; }

        /// <summary>Native packed GDN recurrence on direct CUDA.
        /// Env: <c>TS_CUDA_QWEN35_GDN_NATIVE</c> (loose tolerant, default on).</summary>
        public bool? CudaGdnNative { get; init; }

        /// <summary>MLX packed GDN prefill kernels (Models-side gate; the MLX
        /// backend latches the same var independently).
        /// Env: <c>TS_MLX_QWEN35_GDN_PACKED_KERNELS</c> (loose, default on).</summary>
        public bool? MlxGdnPackedKernels { get; init; }

        /// <summary>Metal in-place GDN recurrent-state layout; the Metal/non-TP
        /// backend guard still applies when set.
        /// Env: <c>TS_QWEN35_METAL_GDN_INPLACE_STATE</c> (loose, default on).</summary>
        public bool? MetalGdnInplaceState { get; init; }

        /// <summary>Chunked GDN prefill.
        /// Env: <c>GDN_DISABLE_CHUNKED_PREFILL</c> (inverted strict opt-out,
        /// default enabled).</summary>
        public bool? GdnChunkedPrefill { get; init; }

        /// <summary>Min seqlen for the chunked GDN prefill kernel; null →
        /// backend default. Env: <c>GDN_CHUNK_PREFILL_MIN_SEQ_LEN</c> (int ≥ 1).</summary>
        public int? GdnChunkPrefillMinSeqLen { get; init; }

        /// <summary>Inline chunked-vs-per-token GDN correctness check (~2× GDN
        /// cost; CI/debug). Env: <c>GDN_VERIFY_CHUNKED</c> (strict opt-in, default off).</summary>
        public bool? GdnVerifyChunked { get; init; }

        // ---- attention / kernels ---------------------------------------

        /// <summary>Fused QK-RMSNorm + NeoX RoPE CUDA kernel.
        /// Env: <c>TS_FUSED_QKNORM_ROPE</c> (loose, default on).</summary>
        public bool? FusedQkNormRope { get; init; }

        /// <summary>Min total seqlen for the fully-fused per-layer attention
        /// decode kernel; null → 1. Env: <c>FUSED_ATTN_LAYER_MIN_SEQ_LEN</c> (int ≥ 1).</summary>
        public int? FusedAttnLayerMinSeqLen { get; init; }

        /// <summary>Min seqlen to prefer MLX flash attention on decode; null → 1.
        /// Env: <c>TS_MLX_FLASH_ATTN_DECODE_MIN_SEQ_LEN</c> (int ≥ 1).</summary>
        public int? MlxFlashAttnDecodeMinSeqLen { get; init; }

        /// <summary>Allow MLX chunked-vector prefill attention for head dims
        /// &gt; 128 (always allowed at ≤ 128).
        /// Env: <c>TS_MLX_CHUNKED_VECTOR_PREFILL</c> (strict opt-in, default off).</summary>
        public bool? MlxChunkedVectorPrefill { get; init; }

        /// <summary>GPU strided-view Q/gate deinterleave (always used for
        /// seqLen 1 or head dim 256). Env: <c>TS_MLX_QWEN_GPU_DEINTERLEAVE</c>
        /// (strict opt-in, default off).</summary>
        public bool? MlxGpuDeinterleave { get; init; }

        /// <summary>Native ggml_rope_multi vs managed MRoPE loop.
        /// Env: <c>TS_QWEN35_MROPE_NATIVE</c> (loose, default on).</summary>
        public bool? MropeNative { get; init; }

        /// <summary>Fully fused dense-FFN prefill graph dispatch.
        /// Env: <c>QWEN35_DISABLE_FUSED_FFN</c> (inverted strict opt-out,
        /// default enabled).</summary>
        public bool? FusedFfnPrefill { get; init; }

        // ---- MoE / MLX graph pacing ------------------------------------

        /// <summary>GGML stacked-MoE forward (single ggml_mul_mat_id).
        /// Env: <c>TS_QWEN35_STACKED_MOE</c> (inverted "0" disables, default on).</summary>
        public bool? StackedMoe { get; init; }

        /// <summary>Batched MLX MoE decode (1 dispatch per gate/up/down;
        /// doubles MLX weight memory). Env: <c>TS_MLX_BATCHED_MOE_DECODE</c>
        /// (loose, default on).</summary>
        public bool? MlxBatchedMoeDecode { get; init; }

        /// <summary>Fused gate-matmul + up-matmul + SiLUMul Metal kernel.
        /// Env: <c>TS_MLX_MOE_FUSED_GATE_UP_SILU</c> (inverted "0" disables,
        /// default on).</summary>
        public bool? MlxMoeFusedGateUpSilu { get; init; }

        /// <summary>On-device MoE top-K + softmax router.
        /// Env: <c>TS_MLX_DEVICE_ROUTER</c> (loose, default on).</summary>
        public bool? MlxDeviceRouter { get; init; }

        /// <summary>Force MLX graph eval at decode layer boundaries.
        /// Env: <c>TS_MLX_EVAL_DECODE_LAYER_BOUNDARIES</c> (strict opt-in, default off).</summary>
        public bool? MlxEvalDecodeLayerBoundaries { get; init; }

        /// <summary>Force an MLX eval on the final logits.
        /// Env: <c>TS_MLX_EVAL_FINAL_LOGITS</c> (strict opt-in, default off).</summary>
        public bool? MlxEvalFinalLogits { get; init; }

        // ---- tensor-parallel -------------------------------------------

        /// <summary>Fused per-rank TP attention blocks.
        /// Env: <c>TS_QWEN35_TP_FUSED</c> (loose, default on).</summary>
        public bool? TpFused { get; init; }

        /// <summary>Fused whole-model per-rank TP decode graph.
        /// Env: <c>TS_QWEN35_TP_FUSED_DECODE</c> (loose, default on).</summary>
        public bool? TpFusedDecode { get; init; }

        /// <summary>Fused whole-model per-rank TP prefill graph.
        /// Env: <c>TS_QWEN35_TP_FUSED_PREFILL</c> (loose, default on).</summary>
        public bool? TpFusedPrefill { get; init; }

        /// <summary>On-device TP MoE prefill.
        /// Env: <c>TS_TP_MOE_PREFILL_ONDEVICE</c> (loose, default on).</summary>
        public bool? TpMoePrefillOnDevice { get; init; }

        // ---- vision encoder / diagnostics ------------------------------

        /// <summary>Whole-encoder fused native path in the vision encoder.
        /// Env: <c>TS_QWEN35_VENC_FUSED</c> (loose, default on).</summary>
        public bool? VencFused { get; init; }

        /// <summary>Fused native attention subgraph in the per-block vision
        /// path. Env: <c>TS_QWEN35_VENC_FUSED_ATTN</c> (loose, default on).</summary>
        public bool? VencFusedAttn { get; init; }

        /// <summary>Per-stage vision-encoder checksum trace (forces host reads).
        /// Env: <c>TS_QWEN35_VENC_TRACE</c> (strict opt-in, default off).</summary>
        public bool? VencTrace { get; init; }

        /// <summary>Per-layer residual trace of the first TP forward.
        /// Env: <c>TS_QWEN35_LAYER_TRACE</c> (strict opt-in, default off).</summary>
        public bool? LayerTrace { get; init; }
    }
}

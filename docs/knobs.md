# Tuning knobs

Generated from `KnobRegistry` — do not edit by hand; regenerate via
`KnobRegistryTests.CommittedKnobDocIsInSync` when the registry changes.

Every knob can be set per-instance via the typed options records; unset
knobs are resolved from their environment variable once, at model
construction, with the exact dialect listed below (KnobResolver). Env
changes after a model is created do not affect that model.

On the server, any knob is reachable without a dedicated flag:
`--set ENV_VAR=VALUE` (bools take `1`/`0`) applies at CLI precedence, and a
`--config` file's `"presets"` object holds per-model blocks keyed by GGUF
file name whose keys are the Property column below. Precedence, lowest to
highest: env var, per-model preset, CLI flag / `--set`.

| Property | Env var | Type | Dialect | CLI flags | Description |
|---|---|---|---|---|---|
| `MlxMlockGguf` | `TS_MLX_MLOCK_GGUF` | bool | default on; set keeps on only when `1` |  | mlock(2) the GGUF mapping so weights stay resident (MLX). |
| `GgmlF32Resident` | `TS_GGML_F32_RESIDENT` | bool | loose, default on; off only on `0` |  | Keep F32 linear weights device-resident on GGML instead of rebinding per call. |
| `FusedDenseFfn` | `TS_DISABLE_FUSED_DENSE_FFN` | bool | inverted opt-out, default on; `1`/`true` disables |  | Fused dense norm+FFN+add chain. |
| `GgmlTpFusedMatmul` | `TS_GGML_TP_FUSED_MATMUL` | bool | strict opt-in, default off |  | Submit both TP ranks' linears from one thread. |
| `MlxDeviceKvCopy` | `TS_MLX_DEVICE_KV_COPY` | bool | loose, default on; off only on `0` |  | On-device MLX KV scatter. |
| `MlxFusedKvWrite` | `TS_MLX_FUSED_KV_WRITE` | bool | loose, default on; off only on `0` |  | Single multi-dim slice_update per KV block (MLX). |
| `PrefillWarmup` | `TS_PREFILL_WARMUP` | bool | loose, default on; off only on `0` |  | Startup dummy long-prompt prefill warmup. |
| `PrefillWarmupLength` | `TS_PREFILL_WARMUP_LEN` | int (≥ 2) |  |  | Warmup prompt length; unset means backend-derived default. |
| `MlxKernelWarmup` | `TS_MLX_KERNEL_WARMUP` | bool | strict opt-in, default off |  | Force MLX kernel warmup despite large resident quantized weights. |
| `EncoderYield` | `TS_ENCODER_YIELD` | bool | loose, default on; off only on `0` |  | Yield the GPU compute lock during encoder work. |
| `PrefillChunk` | `TS_PREFILL_CHUNK` | int (≥ 1) |  |  | Prompt-prefill chunk width; unset means backend default. |
| `MlxEvalEveryNLayers` | `TS_MLX_EVAL_EVERY_N_LAYERS` | int (≥ 0) |  |  | MLX graph-eval interval in layers; unset means 16. |
| `CudaMoeOnDevice` | `TS_CUDA_MOE_ONDEVICE` | bool | loose, default on; off only on `0` |  | On-device direct-CUDA MoE decode. |
| `CudaMoePrefillOnDevice` | `TS_CUDA_MOE_PREFILL_ONDEVICE` | bool | strict opt-in, default off |  | On-device batched MoE prefill. |
| `CudaMoePrefillGrouped` | `TS_CUDA_MOE_PREFILL_GROUPED` | bool | loose, default on; off only on `0` |  | Grouped gather/scatter CUDA MoE prefill. |
| `FullDecode` | `TS_QWEN35_FULL_DECODE` | bool | loose, default on; off only on `0` |  | Whole-model fused decode graph (CUDA/Vulkan/Metal). |
| `MetalTokenInput` | `TS_QWEN35_METAL_TOKEN_INPUT` | bool | loose, default on; off only on `0` |  | Metal token-id input vs legacy host-dequantized embedding. |
| `FusedVerify` | `TS_QWEN35_FUSED_VERIFY` | bool | loose, default on; off only on `0` |  | Fused multi-token MTP-verify trunk. |
| `VerifyResident` | `TS_QWEN35_VERIFY_RESIDENT` | bool | strict opt-in, default off |  | Device-resident GDN verify state. |
| `MtpFusedDraft` | `TS_MTP_FUSED_DRAFT` | bool | loose, default on; off only on `0` |  | Fused MTP draft/catch-up block. |
| `PrefillVerify` | `TS_QWEN35_PREFILL_VERIFY` | bool | loose, default on; off only on `0` |  | Whole-model fused prefill. |
| `CudaPrefillGraphMaxSeqLen` | `TS_CUDA_PREFILL_GRAPH_MAX_SEQLEN` | int (≥ 0) |  |  | Max prefill seqlen for CUDA graph capture; 0 = unlimited; unset means 512. |
| `Batched` | `TS_QWEN35_BATCHED` | bool | loose, default on; off on `0`/`false` | `--continuous-batching` `--no-continuous-batching` `--paged-batching` `--no-paged-batching` | Master switch for the batched (continuous-batching) path. |
| `BatchedFused` | `TS_QWEN35_BATCHED_FUSED` | bool | loose, default on; off on `0`/`false` |  | Fused batched decode graph. |
| `BfdNoMirror` | `TS_QWEN35_BFD_NOMIRROR` | bool | strict opt-in, default off |  | Skip host mirror in batched fused decode. |
| `BatchedGdnNative` | `TS_QWEN35_BATCHED_GDN_NATIVE` | bool | strict opt-in, default off |  | Native batched GDN kernels. |
| `Migrate` | `TS_QWEN35_MIGRATE` | bool | loose, default on; off only on `0` |  | Sequence migration between batched and per-seq paths. |
| `MlxTensorPagedAttn` | `TS_QWEN35_MLX_TENSOR_PAGED_ATTN` | bool | strict opt-in, default off |  | MLX tensor-level paged attention. |
| `FusedRecPrefill` | `TS_QWEN35_FUSED_REC_PREFILL` | bool | loose, default on; off only on `0` |  | Fused recurrent prefill. |
| `CudaGdnNative` | `TS_CUDA_QWEN35_GDN_NATIVE` | bool | loose, default on; off on `0`/`false` |  | Native CUDA GDN kernels. |
| `MlxGdnPackedKernels` | `TS_MLX_QWEN35_GDN_PACKED_KERNELS` | bool | loose, default on; off only on `0` |  | Packed MLX GDN kernels. |
| `MetalGdnInplaceState` | `TS_QWEN35_METAL_GDN_INPLACE_STATE` | bool | loose, default on; off only on `0` |  | In-place Metal GDN state updates. |
| `GdnChunkedPrefill` | `GDN_DISABLE_CHUNKED_PREFILL` | bool | inverted opt-out, default on; `1` disables |  | Chunked GDN prefill. |
| `GdnChunkPrefillMinSeqLen` | `GDN_CHUNK_PREFILL_MIN_SEQ_LEN` | int (≥ 1) |  |  | Min seqlen before GDN prefill chunks; unset means backend default. |
| `GdnVerifyChunked` | `GDN_VERIFY_CHUNKED` | bool | strict opt-in, default off |  | Chunked GDN verify (CI/debug). |
| `FusedQkNormRope` | `TS_FUSED_QKNORM_ROPE` | bool | loose, default on; off only on `0` |  | Fused QK-norm + RoPE. |
| `FusedAttnLayerMinSeqLen` | `FUSED_ATTN_LAYER_MIN_SEQ_LEN` | int (≥ 1) |  |  | Min seqlen for the fused attention layer kernel; unset means 1. |
| `MlxFlashAttnDecodeMinSeqLen` | `TS_MLX_FLASH_ATTN_DECODE_MIN_SEQ_LEN` | int (≥ 1) |  |  | Min seqlen for MLX flash-attention decode. |
| `MlxChunkedVectorPrefill` | `TS_MLX_CHUNKED_VECTOR_PREFILL` | bool | strict opt-in, default off |  | Chunked MLX vector prefill. |
| `MlxGpuDeinterleave` | `TS_MLX_QWEN_GPU_DEINTERLEAVE` | bool | strict opt-in, default off |  | GPU strided-view Q/gate deinterleave. |
| `MropeNative` | `TS_QWEN35_MROPE_NATIVE` | bool | loose, default on; off only on `0` |  | Native MRoPE position tables. |
| `FusedFfnPrefill` | `QWEN35_DISABLE_FUSED_FFN` | bool | inverted opt-out, default on; `1` disables |  | Fused FFN during prefill. |
| `StackedMoe` | `TS_QWEN35_STACKED_MOE` | bool | loose, default on; off only on `0` |  | Stacked-experts MoE weights. |
| `MlxBatchedMoeDecode` | `TS_MLX_BATCHED_MOE_DECODE` | bool | loose, default on; off only on `0` |  | Batched MLX MoE decode (doubles MLX weight memory). |
| `MlxMoeFusedGateUpSilu` | `TS_MLX_MOE_FUSED_GATE_UP_SILU` | bool | loose, default on; off only on `0` |  | Fused MLX MoE gate/up/SiLU. |
| `MlxDeviceRouter` | `TS_MLX_DEVICE_ROUTER` | bool | loose, default on; off only on `0` |  | On-device MLX MoE router. |
| `MlxEvalDecodeLayerBoundaries` | `TS_MLX_EVAL_DECODE_LAYER_BOUNDARIES` | bool | strict opt-in, default off |  | MLX eval at decode layer boundaries. |
| `MlxEvalFinalLogits` | `TS_MLX_EVAL_FINAL_LOGITS` | bool | strict opt-in, default off |  | MLX eval of final logits only. |
| `TpFused` | `TS_QWEN35_TP_FUSED` | bool | loose, default on; off only on `0` |  | Fused tensor-parallel path. |
| `TpFusedDecode` | `TS_QWEN35_TP_FUSED_DECODE` | bool | loose, default on; off only on `0` |  | Fused TP decode. |
| `TpFusedPrefill` | `TS_QWEN35_TP_FUSED_PREFILL` | bool | loose, default on; off only on `0` |  | Fused TP prefill. |
| `TpMoePrefillOnDevice` | `TS_TP_MOE_PREFILL_ONDEVICE` | bool | loose, default on; off only on `0` |  | On-device TP MoE prefill. |
| `VencFused` | `TS_QWEN35_VENC_FUSED` | bool | loose, default on; off only on `0` |  | Fused vision encoder. |
| `VencFusedAttn` | `TS_QWEN35_VENC_FUSED_ATTN` | bool | loose, default on; off only on `0` |  | Fused vision-encoder attention. |
| `VencTrace` | `TS_QWEN35_VENC_TRACE` | bool | strict opt-in, default off |  | Vision-encoder tracing. |
| `LayerTrace` | `TS_QWEN35_LAYER_TRACE` | bool | strict opt-in, default off |  | Per-layer tracing. |

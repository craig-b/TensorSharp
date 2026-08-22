# Tuning knobs

Generated from `KnobRegistry` — do not edit by hand; regenerate via
`KnobRegistryTests.CommittedKnobDocIsInSync` when the registry changes.

Every knob can be set per-instance via the typed options records; unset
knobs are resolved from their environment variable once, at model
construction (KnobResolver). Env changes after a model is created do
not affect that model. Bool knobs take `1`/`0`. Unset or empty means
the default below; an unrecognized value warns once on stderr and uses
the default. Inverted `DISABLE_*` vars disable their feature on `1`;
their property stays positive-sense. (The common alternative bool
spellings `true`/`false`, `yes`/`no`, `on`/`off` are also accepted,
case-insensitive.)

On the server, any knob is reachable without a dedicated flag:
`--set ENV_VAR=VALUE` (bools take `1`/`0`) applies at CLI precedence, and a
`--config` file's `"presets"` object holds per-model blocks keyed by GGUF
file name whose keys are the Property column below. Precedence, lowest to
highest: env var, per-model preset, CLI flag / `--set`.

| Property | Env var | Type | Dialect | CLI flags | Description |
|---|---|---|---|---|---|
| `MlxMlockGguf` | `TS_MLX_MLOCK_GGUF` | bool | default on |  | mlock(2) the GGUF mapping so weights stay resident (MLX). |
| `GgmlF32Resident` | `TS_GGML_F32_RESIDENT` | bool | default on |  | Keep F32 linear weights device-resident on GGML instead of rebinding per call. |
| `FusedDenseFfn` | `TS_DISABLE_FUSED_DENSE_FFN` | bool | inverted `DISABLE_*` var, default on |  | Fused dense norm+FFN+add chain. |
| `GgmlTpFusedMatmul` | `TS_GGML_TP_FUSED_MATMUL` | bool | default off (opt-in) |  | Submit both TP ranks' linears from one thread. |
| `MlxDeviceKvCopy` | `TS_MLX_DEVICE_KV_COPY` | bool | default on |  | On-device MLX KV scatter. |
| `MlxFusedKvWrite` | `TS_MLX_FUSED_KV_WRITE` | bool | default on |  | Single multi-dim slice_update per KV block (MLX). |
| `PrefillWarmup` | `TS_PREFILL_WARMUP` | bool | default on |  | Startup dummy long-prompt prefill warmup. |
| `PrefillWarmupLength` | `TS_PREFILL_WARMUP_LEN` | int (≥ 2) |  |  | Warmup prompt length; unset means backend-derived default. |
| `MlxKernelWarmup` | `TS_MLX_KERNEL_WARMUP` | bool | default off (opt-in) |  | Force MLX kernel warmup despite large resident quantized weights. |
| `EncoderYield` | `TS_ENCODER_YIELD` | bool | default on |  | Yield the GPU compute lock during encoder work. |
| `PrefillChunk` | `TS_PREFILL_CHUNK` | int (≥ 1) |  |  | Prompt-prefill chunk width; unset means backend default. |
| `MlxEvalEveryNLayers` | `TS_MLX_EVAL_EVERY_N_LAYERS` | int (≥ 0) |  |  | MLX graph-eval interval in layers; unset means 16. |
| `CudaMoeOnDevice` | `TS_CUDA_MOE_ONDEVICE` | bool | default on |  | On-device direct-CUDA MoE decode. |
| `CudaMoePrefillOnDevice` | `TS_CUDA_MOE_PREFILL_ONDEVICE` | bool | default off (opt-in) |  | On-device batched MoE prefill. |
| `CudaMoePrefillGrouped` | `TS_CUDA_MOE_PREFILL_GROUPED` | bool | default on |  | Grouped gather/scatter CUDA MoE prefill. |
| `FullDecode` | `TS_QWEN35_FULL_DECODE` | bool | default on |  | Whole-model fused decode graph (CUDA/Vulkan/Metal). |
| `MetalTokenInput` | `TS_QWEN35_METAL_TOKEN_INPUT` | bool | default on |  | Metal token-id input vs legacy host-dequantized embedding. |
| `FusedVerify` | `TS_QWEN35_FUSED_VERIFY` | bool | default on |  | Fused multi-token MTP-verify trunk. |
| `VerifyResident` | `TS_QWEN35_VERIFY_RESIDENT` | bool | default off (opt-in) |  | Device-resident GDN verify state. |
| `MtpFusedDraft` | `TS_MTP_FUSED_DRAFT` | bool | default on |  | Fused MTP draft/catch-up block. |
| `PrefillVerify` | `TS_QWEN35_PREFILL_VERIFY` | bool | default on |  | Whole-model fused prefill. |
| `CudaPrefillGraphMaxSeqLen` | `TS_CUDA_PREFILL_GRAPH_MAX_SEQLEN` | int (≥ 0) |  |  | Max prefill seqlen for CUDA graph capture; 0 = unlimited; unset means 512. |
| `Batched` | `TS_QWEN35_BATCHED` | bool | default on | `--continuous-batching` `--no-continuous-batching` `--paged-batching` `--no-paged-batching` | Master switch for the batched (continuous-batching) path. |
| `BatchedFused` | `TS_QWEN35_BATCHED_FUSED` | bool | default on |  | Fused batched decode graph. |
| `BfdNoMirror` | `TS_QWEN35_BFD_NOMIRROR` | bool | default off (opt-in) |  | Skip host mirror in batched fused decode. |
| `BatchedGdnNative` | `TS_QWEN35_BATCHED_GDN_NATIVE` | bool | default off (opt-in) |  | Native batched GDN kernels. |
| `Migrate` | `TS_QWEN35_MIGRATE` | bool | default on |  | Sequence migration between batched and per-seq paths. |
| `MlxTensorPagedAttn` | `TS_QWEN35_MLX_TENSOR_PAGED_ATTN` | bool | default off (opt-in) |  | MLX tensor-level paged attention. |
| `FusedRecPrefill` | `TS_QWEN35_FUSED_REC_PREFILL` | bool | default on |  | Fused recurrent prefill. |
| `CudaGdnNative` | `TS_CUDA_QWEN35_GDN_NATIVE` | bool | default on |  | Native CUDA GDN kernels. |
| `MlxGdnPackedKernels` | `TS_MLX_QWEN35_GDN_PACKED_KERNELS` | bool | default on |  | Packed MLX GDN kernels. |
| `MetalGdnInplaceState` | `TS_QWEN35_METAL_GDN_INPLACE_STATE` | bool | default on |  | In-place Metal GDN state updates. |
| `GdnChunkedPrefill` | `GDN_DISABLE_CHUNKED_PREFILL` | bool | inverted `DISABLE_*` var, default on |  | Chunked GDN prefill. |
| `GdnChunkPrefillMinSeqLen` | `GDN_CHUNK_PREFILL_MIN_SEQ_LEN` | int (≥ 1) |  |  | Min seqlen before GDN prefill chunks; unset means backend default. |
| `GdnVerifyChunked` | `GDN_VERIFY_CHUNKED` | bool | default off (opt-in) |  | Chunked GDN verify (CI/debug). |
| `FusedQkNormRope` | `TS_FUSED_QKNORM_ROPE` | bool | default on |  | Fused QK-norm + RoPE. |
| `FusedAttnLayerMinSeqLen` | `FUSED_ATTN_LAYER_MIN_SEQ_LEN` | int (≥ 1) |  |  | Min seqlen for the fused attention layer kernel; unset means 1. |
| `MlxFlashAttnDecodeMinSeqLen` | `TS_MLX_FLASH_ATTN_DECODE_MIN_SEQ_LEN` | int (≥ 1) |  |  | Min seqlen for MLX flash-attention decode. |
| `MlxChunkedVectorPrefill` | `TS_MLX_CHUNKED_VECTOR_PREFILL` | bool | default off (opt-in) |  | Chunked MLX vector prefill. |
| `MlxGpuDeinterleave` | `TS_MLX_QWEN_GPU_DEINTERLEAVE` | bool | default off (opt-in) |  | GPU strided-view Q/gate deinterleave. |
| `MropeNative` | `TS_QWEN35_MROPE_NATIVE` | bool | default on |  | Native MRoPE position tables. |
| `FusedFfnPrefill` | `QWEN35_DISABLE_FUSED_FFN` | bool | inverted `DISABLE_*` var, default on |  | Fused FFN during prefill. |
| `StackedMoe` | `TS_QWEN35_STACKED_MOE` | bool | default on |  | Stacked-experts MoE weights. |
| `MlxBatchedMoeDecode` | `TS_MLX_BATCHED_MOE_DECODE` | bool | default on |  | Batched MLX MoE decode (doubles MLX weight memory). |
| `MlxMoeFusedGateUpSilu` | `TS_MLX_MOE_FUSED_GATE_UP_SILU` | bool | default on |  | Fused MLX MoE gate/up/SiLU. |
| `MlxDeviceRouter` | `TS_MLX_DEVICE_ROUTER` | bool | default on |  | On-device MLX MoE router. |
| `MlxEvalDecodeLayerBoundaries` | `TS_MLX_EVAL_DECODE_LAYER_BOUNDARIES` | bool | default off (opt-in) |  | MLX eval at decode layer boundaries. |
| `MlxEvalFinalLogits` | `TS_MLX_EVAL_FINAL_LOGITS` | bool | default off (opt-in) |  | MLX eval of final logits only. |
| `TpFused` | `TS_QWEN35_TP_FUSED` | bool | default on |  | Fused tensor-parallel path. |
| `TpFusedDecode` | `TS_QWEN35_TP_FUSED_DECODE` | bool | default on |  | Fused TP decode. |
| `TpFusedPrefill` | `TS_QWEN35_TP_FUSED_PREFILL` | bool | default on |  | Fused TP prefill. |
| `TpMoePrefillOnDevice` | `TS_TP_MOE_PREFILL_ONDEVICE` | bool | default on |  | On-device TP MoE prefill. |
| `VencFused` | `TS_QWEN35_VENC_FUSED` | bool | default on |  | Fused vision encoder. |
| `VencFusedAttn` | `TS_QWEN35_VENC_FUSED_ATTN` | bool | default on |  | Fused vision-encoder attention. |
| `VencTrace` | `TS_QWEN35_VENC_TRACE` | bool | default off (opt-in) |  | Vision-encoder tracing. |
| `LayerTrace` | `TS_QWEN35_LAYER_TRACE` | bool | default off (opt-in) |  | Per-layer tracing. |

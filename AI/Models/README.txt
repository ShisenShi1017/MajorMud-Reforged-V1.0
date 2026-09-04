RealmGate - Local GGUF Models
======================================

Place Borin's existing Qwen GGUF model in this folder.

Recommended local path:
  AI\Models\qwen-14b-instruct.gguf

The model file is intentionally not included in update ZIPs and is ignored by Git.
You may copy the GGUF currently used by LM Studio here; RealmGate loads the GGUF
directly and does not require LM Studio to be running.

If the filename differs, either update AI\Config\borin-brain.json or leave
autoDiscoverModel enabled. With auto-discovery enabled, RealmGate selects the
largest GGUF in this folder. Keeping only Borin's intended model here avoids
selecting the wrong file.

The first message to Borin loads the model. A 14B model may take noticeable time
and requires sufficient system RAM and/or GPU memory. The default configuration
offloads 20 layers through Vulkan. Set gpuLayerCount to 0 for CPU-only operation
or raise it gradually when more GPU memory is available.

#!/usr/bin/env python3
import json
import subprocess
import sys
from pathlib import Path

def main():
    if len(sys.argv) < 3:
        print("Usage: python sample_piper_speakers.py <model.onnx> <model.json> [output_dir]", file=sys.stderr)
        sys.exit(1)

    model_path = Path(sys.argv[1]).expanduser().resolve()
    config_path = Path(sys.argv[2]).expanduser().resolve()
    output_dir = Path(sys.argv[3]).expanduser().resolve() if len(sys.argv) >= 4 else Path.cwd() / "piper_speaker_samples"

    if not model_path.exists():
        print(f"Model not found: {model_path}", file=sys.stderr)
        sys.exit(1)
    if not config_path.exists():
        print(f"Config not found: {config_path}", file=sys.stderr)
        sys.exit(1)

    with open(config_path, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    speaker_map = cfg.get("speaker_id_map") or cfg.get("speakers")
    if not speaker_map:
        print("No speaker_id_map/speakers found in JSON; model may be single-speaker.", file=sys.stderr)
        sys.exit(1)

    # speakers may be dict of name->id
    speakers = sorted([(name, sid) for name, sid in speaker_map.items()], key=lambda x: x[1])
    output_dir.mkdir(parents=True, exist_ok=True)

    for name, sid in speakers:
        out_wav = output_dir / f"speaker_{sid}_{name}.wav"
        cmd = ["piper", "--model", str(model_path), "--speaker", str(sid), "--text", f"This is speaker {name} sample", "--output_file", str(out_wav)]
        print("Sampling", name, sid)
        subprocess.run(cmd, check=False)

    print("Done. Samples in", output_dir)

if __name__ == "__main__":
    main()

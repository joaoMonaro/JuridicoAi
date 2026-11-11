"""Reproducible smoke calibration on synthetic fixtures; never a production gate.

Usage: python3 tools/documents/calibrate_ocr.py --tesseract /path/to/tesseract
       --tessdata /path/to/tessdata --output /tmp/document-ocr-report.json
"""
import argparse
import csv
import hashlib
import io
import json
import pathlib
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--tesseract", default="tesseract")
    parser.add_argument("--tessdata", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    corpus = pathlib.Path(__file__).resolve().parents[2] / "tests/JuridicoAI.Api.Tests/Fixtures/Documents"
    expected = json.loads((corpus / "expected.json").read_text())
    results = []
    for name in ("scan.png", "blurred.png", "rotated.png"):
        run = subprocess.run([args.tesseract, str(corpus / name), "stdout", "--tessdata-dir", args.tessdata,
                              "-l", "por+eng", "--psm", "3", "tsv"], capture_output=True, text=True, timeout=60, check=True)
        groups = {}
        for row in csv.DictReader(io.StringIO(run.stdout), delimiter="\t"):
            if row["level"] != "5" or not row["text"].strip():
                continue
            key = tuple(row[k] for k in ("page_num", "block_num", "par_num"))
            groups.setdefault(key, []).append((row["text"], float(row["conf"])))
        units = []
        truth = " ".join(expected[name])
        for words in groups.values():
            text = " ".join(w for w, _ in words)
            average = sum(len(w) * c for w, c in words) / sum(len(w) for w, _ in words)
            accepted = average >= 97 and all(c >= 90 for _, c in words) and all(
                c >= 98 for w, c in words if any(ch.isdigit() for ch in w) or "$" in w)
            units.append({"text": text, "weightedConfidence": average, "accepted": accepted,
                          "matchesAnnotatedText": text in truth})
        results.append({"file": name, "sha256": hashlib.sha256((corpus / name).read_bytes()).hexdigest(),
                        "units": units, "acceptedUnits": sum(u["accepted"] for u in units),
                        "falseAccepts": sum(u["accepted"] and not u["matchesAnnotatedText"] for u in units)})
    report = {"policy": "qualidade-v1", "scope": "synthetic-smoke-only", "externalRolloutApproved": False,
              "reason": "Small corpus and insufficient useful coverage; broaden calibration before rollout.",
              "tesseractVersion": subprocess.run([args.tesseract, "--version"], capture_output=True, text=True, check=True).stdout.splitlines()[0],
              "models": {lang: hashlib.sha256((pathlib.Path(args.tessdata) / (lang + ".traineddata")).read_bytes()).hexdigest() for lang in ("por", "eng")},
              "results": results}
    pathlib.Path(args.output).write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"accepted": sum(r["acceptedUnits"] for r in results),
                      "falseAccepts": sum(r["falseAccepts"] for r in results), "rolloutApproved": False}))


if __name__ == "__main__":
    main()

"""Reads an image from stdin and prints what RapidOCR found, as JSON.

Invoked by RapidOcrService (the .NET adapter). Deliberately shaped like the Tesseract
adapter's contract rather than like RapidOCR's CLI:

- The image arrives on **stdin**, so no caller-controlled value ever becomes a path or
  an argument. The CLI only accepts `-img <path>`, which would mean writing a temp file
  for every scan and widening that surface for nothing.
- Output carries per-line confidence and geometry, because the parser needs both: the
  confidence gate drops background noise, and top/height are what let it rejoin a name
  the bag printed across two lines.

RapidOCR returns each line as (box, text, score), where box is four corner points in
image coordinates. Top and height are derived from the box rather than reported, so a
rotated line yields the height of its bounding box, which is what the parser's band
grouping expects.

One JSON object per line of output, so a partial read is still parseable and a failure
is visible in stderr rather than swallowed into an empty document.
"""

import json
import sys

from rapidocr_onnxruntime import RapidOCR

# Loading the models is the expensive part (a few hundred ms), and it happens once per
# process. The adapter starts one process per scan, which is the same bargain the
# Tesseract adapter already makes and keeps the two comparable.
_ocr = RapidOCR()


def main() -> int:
    image = sys.stdin.buffer.read()
    if not image:
        print("rapidocr: empty input on stdin", file=sys.stderr)
        return 2

    result, _ = _ocr(image)
    for box, text, score in result or []:
        if not text or not text.strip():
            continue
        ys = [point[1] for point in box]
        print(
            json.dumps(
                {
                    "text": text.strip(),
                    # RapidOCR scores 0..1; the parser's gate is the 0..100 scale
                    # Tesseract reports, so convert here rather than teaching the
                    # application layer that engines differ.
                    "conf": round(float(score) * 100, 2),
                    "top": int(min(ys)),
                    "height": int(max(ys) - min(ys)),
                },
                ensure_ascii=False,
            )
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())

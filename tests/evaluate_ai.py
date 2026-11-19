"""Leave one image out of the gallery. Run from any directory."""
import io
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Marketplace.AIService"))
from main import embedding

groups = (("tee","polo"), ("tote","pack"), ("mouse","audio"), ("cup","bottle"),
          ("book","binder"), ("weight","mat"))
files = sorted((ROOT/"Marketplace.WebAPI/wwwroot/images/demo").glob("*.png"))
assert len(files) == 120
labels = np.array([next(i for i,g in enumerate(groups) if p.stem.split("-")[0] in g) for p in files])
def feature(raw):
    return embedding(raw)
gallery = np.array([feature(p.read_bytes()) for p in files])
for scenario in ("PNG", "JPEG-60", "rotation-8"):
    queries = []
    for path in files:
        with Image.open(path) as im:
            out = io.BytesIO()
            if scenario == "rotation-8":
                im = im.rotate(8, resample=Image.Resampling.BILINEAR, fillcolor=im.getpixel((0,0)))
            im.save(out, "JPEG" if scenario == "JPEG-60" else "PNG", quality=60)
            queries.append(feature(out.getvalue()))
    scores = np.asarray(queries) @ gallery.T
    np.fill_diagonal(scores, -np.inf)
    top = np.argsort(-scores, axis=1, kind="stable")[:,:5]
    relevant = labels[top] == labels[:,None]
    print(f"{scenario:12} n={len(files)} gallery/query=119  accuracy@1={relevant[:,0].mean():.3f}"
          f"  category precision@5={relevant.mean():.3f}")


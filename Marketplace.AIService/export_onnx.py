"""Export the trained classifier's 1024-dimensional penultimate activation and logits."""
import json
from pathlib import Path

import torch
from torchvision.models import MobileNet_V3_Small_Weights, mobilenet_v3_small


class Encoder(torch.nn.Module):
    def __init__(self):
        super().__init__()
        self.network = mobilenet_v3_small(weights=MobileNet_V3_Small_Weights.IMAGENET1K_V1)
        self.head = self.network.classifier[-1]
        self.network.classifier[-1] = torch.nn.Identity()

    def forward(self, images):
        hidden = self.network(images)
        return torch.nn.functional.normalize(hidden, dim=1), self.head(hidden)


if __name__ == "__main__":
    root = Path(__file__).resolve().parent
    encoder = Encoder().cpu().eval()
    torch.set_num_threads(2)
    with torch.inference_mode():
        torch.onnx.export(encoder, torch.zeros(2, 3, 224, 224), str(root / "mobilenet.onnx"),
                          input_names=["images"], output_names=["embedding", "logits"],
                          dynamic_axes={key: {0: "batch"} for key in ("images", "embedding", "logits")},
                          opset_version=17, dynamo=False)
    (root / "labels.json").write_text(json.dumps(MobileNet_V3_Small_Weights.IMAGENET1K_V1.meta["categories"]), encoding="utf8")

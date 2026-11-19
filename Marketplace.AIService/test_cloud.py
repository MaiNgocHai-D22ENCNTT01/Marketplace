"""Run separately: python -m unittest test_cloud -v. No database is used."""
import os
os.environ["MARKETPLACE_INFERENCE_ONLY"] = "true"
os.environ["MARKETPLACE_REQUIRE_API_KEY"] = "true"
os.environ["MARKETPLACE_AI_API_KEY"] = "test-only-key"

import io
import unittest
from fastapi.testclient import TestClient
from PIL import Image
from main import app


class CloudTests(unittest.TestCase):
    def test_cloud_without_database(self):
        with TestClient(app) as client:
            health = client.get("/health/ready").json()
            self.assertTrue(health["ready"])
            self.assertEqual(health["mode"], "inference-only")
            self.assertEqual(client.post("/ai/features").status_code, 401)
            headers = {"X-AI-Key": "test-only-key"}
            self.assertEqual(client.post("/ai/reindex-all", headers=headers).status_code, 503)
            buffer = io.BytesIO()
            Image.new("RGB", (224, 224), "red").save(buffer, "WEBP")
            response = client.post("/ai/features", headers=headers,
                                   files={"file": ("image.webp", buffer.getvalue(), "image/webp")})
            self.assertEqual(response.status_code, 200)
            self.assertEqual(len(response.json()["embedding"]), 1024)
            self.assertIn("label", response.json())
            self.assertEqual(client.post("/ai/features", headers=headers,
                                        files={"file": ("bad.png", b"invalid")}).status_code, 400)


if __name__ == "__main__":
    unittest.main()

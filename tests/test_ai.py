"""Runs only on the disposable PostgreSQL cluster in run_postgres.py --ai."""
import html
import io
import os
import re
from contextlib import closing
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen, build_opener, HTTPCookieProcessor
from http.cookiejar import CookieJar
import json
import psycopg2
from PIL import Image
from test_step3 import call, login

API = os.environ["MARKETPLACE_TEST_API"]
WEB = os.environ["MARKETPLACE_TEST_WEB"]
AI = os.environ["AiBaseUrl"]
ROOT = Path(__file__).resolve().parents[1]
image = ROOT / "Marketplace.WebAPI/wwwroot/images/demo/tee-mint.png"

def upload(url, raw, browser=None, token=None):
    boundary = "MarketplaceFormBoundary"
    part = f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="sample"\r\n\r\n'.encode() +raw+b"\r\n"
    if token:
        part += f'--{boundary}\r\nContent-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n{token}\r\n'.encode()
    request = Request(url, data=part+f'--{boundary}--\r\n'.encode(), headers={
        "Content-Type":"multipart/form-data; boundary="+boundary})
    return (browser.open if browser else urlopen)(request, timeout=15)

def run(online):
    backend = API+"/api/products/visual-search"
    raw = image.read_bytes()
    with closing(psycopg2.connect(os.environ["MARKETPLACE_AI_DSN"])) as db, db.cursor() as cur:
        cur.execute("SELECT count(*) FROM product_images WHERE json_array_length(feature_vector::json)=1024")
        assert cur.fetchone()[0] == 120
        if not online:
            try:
                # Cached predictions remain available during an AI outage.
                assert upload(backend, raw).headers["X-Visual-Cache"] == "HIT"
                upload(backend, raw + b"cold-key")
            except HTTPError as exc:
                assert exc.code == 503, exc.code
            else:
                raise AssertionError("Expected unavailable visual search")
            with urlopen(API+"/api/products/recommendations") as res:
                assert res.headers["X-Recommendation-Engine"] == "catalog"
            return
        for fmt in ("JPEG","PNG","WEBP"):
            buf = io.BytesIO()
            Image.open(image).save(buf,fmt)
            result = json.load(upload(backend,buf.getvalue()))
            assert result["engine"] == ("python" if online else "csharp"), result["engine"]
            assert result["category"] == "Thời trang" and 0 < result["confidence"] <= 1
            first = result["products"][0]
            assert first["product"]["imageUrl"].endswith("tee-mint.png") and first["similarityScore"] > .97
        for invalid, status in ((b"broken",400),(b"x" * (5242880+1),413)):
            try: upload(backend,invalid)
            except HTTPError as exc: assert exc.code == status, exc.code
            else: raise AssertionError("Invalid image accepted")
        pid = first["product"]["id"]
        if online:
            vec = json.load(upload(AI+"/ai/extract-embedding",raw))["embedding"]
            cur.execute("SELECT feature_vector FROM product_images WHERE product_id=%s",(pid,))
            stored = json.loads(cur.fetchone()[0])
            assert len(vec)==len(stored)==1024 and max(abs(a-b) for a,b in zip(vec,stored)) < 1e-5
            with urlopen(API+"/api/products/recommendations") as res:
                assert res.headers["X-Recommendation-Engine"] == "python" and len(json.load(res))==8
            token = login("buyer")["token"]
            call("GET","products/"+pid+"?clicked=true",token)
            cur.execute("SELECT action_type FROM user_interaction_logs WHERE product_id=%s",(pid,))
            assert {x[0] for x in cur.fetchall()} >= {"VIEW","CLICK"}
            personal = call("GET","products/recommendations",token)
            assert personal[0]["category"] == "Thời trang"
            similar = call("GET","products/recommendations?productId="+pid)
            assert all(p["id"] != pid for p in similar) and similar[0]["category"]=="Thời trang"
            assert call("GET","products/recommendations")[0]["id"]==pid
        for state in ("HIDDEN","DISCONTINUED"):
            cur.execute("UPDATE products SET status=%s WHERE id=%s",(state,pid))
            db.commit()
            assert pid not in [p["product"]["id"] for p in json.load(upload(backend,raw))["products"]]
            assert pid not in [p["id"] for p in call("GET","products/recommendations")]
        cur.execute("UPDATE products SET status='ACTIVE' WHERE id=%s",(pid,))
        db.commit()
    browser=build_opener(HTTPCookieProcessor(CookieJar()))
    home=html.unescape(browser.open(WEB).read().decode())
    assert "Gợi ý dành riêng cho bạn" in home and "visual-open" in home
    token=re.search(r'name="__RequestVerificationToken"[^>]+value="([^"]+)"',home)[1]
    page=html.unescape(upload(WEB+"/Products/Visual",raw,browser,token).read().decode())
    assert "Thời trang" in page and "% Giống nhau" in page and "Độ tin cậy" in page
    assert "Sản phẩm tương tự" in browser.open(WEB+"/Products/Detail/"+pid).read().decode()
    print("PASS visual formats, rejection, visibility, recommendations and MVC;", "Python" if online else "C#")

if __name__ == "__main__":
    import sys
    run("--online" in sys.argv)

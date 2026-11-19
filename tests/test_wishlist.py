"""Run against the isolated seeded API started by run_postgres.py."""
from concurrent.futures import ThreadPoolExecutor
from urllib.parse import quote
from test_step3 import call, login

buyer = login("buyer")["token"]
seller = login("seller")["token"]
suggest = lambda q: call("GET", "products/suggestions?q=" + quote(q))
assert suggest("a") == []
assert 0 < len(suggest("dien tu")) <= 6
assert suggest("DIỆN TỬ") == suggest("dien tu")
rows = suggest("AO THUN")
assert rows and all("imageUrl" in p and p["price"] > 0 for p in rows)
pid = rows[0]["id"]
toggle = "wishlists/toggle/" + pid
call("GET", "wishlists", expect=401)
call("POST", toggle, expect=401)
assert call("POST", toggle, buyer)["isWishlisted"]
assert [p["id"] for p in call("GET", "wishlists", buyer)] == [pid]
assert call("GET", "wishlists", seller) == []
assert not call("POST", toggle, buyer)["isWishlisted"]
with ThreadPoolExecutor(2) as pool:
    assert sorted(pool.map(lambda _: call("POST", toggle, buyer)["isWishlisted"], range(2))) == [False, True]
assert call("GET", "wishlists", buyer) == []
print("PASS: suggestions, authentication, wishlist isolation and concurrent toggles.")


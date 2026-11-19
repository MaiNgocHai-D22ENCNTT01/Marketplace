"""Integration suite. Run against an isolated, newly seeded Development API."""
import json
import os
import itertools
from concurrent.futures import ThreadPoolExecutor
from urllib.error import HTTPError
from urllib.request import Request, urlopen

BASE = os.environ["MARKETPLACE_TEST_API"].rstrip("/")
PASSWORD = "Marketplace123!"
counter = itertools.count(1)


def call(method, path, token=None, data=None, expect=200):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    request = Request(BASE + "/api/" + path, method=method, headers=headers,
                      data=None if data is None else json.dumps(data).encode())
    try:
        response = urlopen(request, timeout=15)
    except HTTPError as exc:
        response = exc
    with response:
        body = response.read().decode()
        assert response.status == expect, (method, path, response.status, expect, body[:1500])
        return json.loads(body) if body and expect < 300 else None


def login(account):
    return call("POST", "auth/login", data={"email": account + "@example.invalid", "password": PASSWORD})


def register(account):
    call("POST", "auth/register", data={"email": account + "@example.invalid",
         "password": PASSWORD, "fullName": account}, expect=201)
    return login(account)["token"]


def main():
    buyer = login("buyer")["token"]
    seller = login("seller")["token"]
    admin_login = login("admin")
    assert set(admin_login["roles"]) == {"ADMIN", "SUPER_ADMIN"}
    admin = admin_login["token"]
    baseline = call("GET","admin/dashboard",admin)
    seller_baseline = call("GET","seller/analytics",seller)
    assert baseline["orders"] == 30
    assert sum(r["reviewCount"] for r in call("GET","products")) == 30
    for demo in (p for p in call("GET","products") if p["reviewCount"]):
        rows = call("GET", f"products/{demo['id']}/reviews?withComment=true")["results"]["items"]
        assert len(rows) == 5 and all(r["comment"].strip() and r["buyer"] and r["createdAt"] for r in rows)
        assert any(r["sellerReply"] for r in rows)
    other = register("second")
    call("GET", "admin/dashboard", seller, expect=403)
    call("GET", "seller/orders", buyer, expect=403)
    call("POST", "seller/register-shop", other, {"name": "Test shop"}, 201)
    call("POST", "seller/register-shop", other, {"name": "Another shop"}, 409)
    shop, = call("GET", "admin/shops/pending", admin)
    new_shop = "shops/" + shop["id"]
    call("GET",new_shop,expect=404)
    call("GET","shops/00000000-0000-0000-0000-000000000001",expect=404)
    call("PUT", f"admin/shops/{shop['id']}/approve", admin, expect=204)
    call("PUT", f"admin/shops/{shop['id']}/approve", admin, expect=409)
    assert call("GET",new_shop)["totalProducts"] == 0
    other = login("second")["token"]
    assert call("GET", "seller/products", other) == []
    categories = call("GET", "seller/categories", seller)
    template = dict(name="Integration product", categoryId=categories[0]["id"], description="Demo",
                    imageUrl="/images/demo/tee-mint.png")

    def create(owner):
        product = dict(template, variants=[dict(sku="TEST-" + str(next(counter)), price=12345, stockQuantity=12)])
        return call("POST", "seller/products", owner, product, 201)["id"]

    def product(pid, owner=seller):
        return next(p for p in call("GET", "seller/products", owner) if p["id"] == pid)

    pid = create(seller)
    row = product(pid)["data"]
    row["name"] = "Edited"
    row["variants"][0].update(stockQuantity=10, price=12000)
    call("PUT", "seller/products/" + pid, other, row, 404)
    call("DELETE", "seller/products/" + pid, other, expect=404)
    call("PUT", "seller/products/" + pid, seller, row, 204)
    variant = product(pid)["data"]["variants"][0]
    owner_shop = call("GET", "products/" + pid)["shop"]["id"]
    path_shop = "shops/" + owner_shop
    full = call("GET", path_shop + "?pageSize=48")
    assert full["status"] == "ACTIVE" and full["products"][0]["id"] == pid
    assert full["totalProducts"] == full["filteredTotal"] == len(full["products"])
    assert all(p["shopId"] == owner_shop and p["shopName"] == full["shopName"] for p in full["products"])
    matching = call("GET", path_shop + "?keyword=EDITED&categoryId=" + row["categoryId"])
    assert matching["filteredTotal"] == 1 and matching["products"][0]["id"] == pid
    assert matching["totalProducts"] == full["totalProducts"]
    for sort in ("price_asc","price_desc"):
        parts = [call("GET",path_shop + f"?sort={sort}&pageSize=12&page={n}") for n in (1,2)]
        products = parts[0]["products"] + parts[1]["products"]
        assert len({p["id"] for p in products}) == full["totalProducts"]
        prices = [p["price"] for p in products]
        assert prices == sorted(prices,reverse=sort=="price_desc")
    foreign_id = create(other)
    assert [p["id"] for p in call("GET",new_shop)["products"]] == [foreign_id]
    foreign_variant = product(foreign_id, other)["data"]["variants"][0]
    bad = dict(row, variants=[foreign_variant])
    call("PUT", "seller/products/" + pid, seller, bad, 400)

    def checkout(split=False):
        call("POST", "cart/items", buyer, dict(variantId=variant["id"], quantity=2), 204)
        if split:
            call("POST", "cart/items", buyer, dict(variantId=foreign_variant["id"], quantity=1), 204)
        group = call("POST", "orders/checkout", buyer,
                     dict(recipientName="Demo", phone="0901234567", detailAddress="Test address"))
        assert group["orderCount"] == (2 if split else 1)
        return next(o for o in call("GET", "orders/my-orders", buyer)
                    if o["orderGroupId"] == group["orderGroupId"] and o["total"] == 24000)

    cancelled = checkout(True)
    oid = cancelled["id"]
    call("PUT", f"orders/{oid}/cancel", other, expect=404)
    assert product(pid)["data"]["variants"][0]["stockQuantity"] == 8
    # Race two requests on the same stock reservation.
    def cancel(_):
        try:
            call("PUT", f"orders/{oid}/cancel", buyer, expect=204)
            return 204
        except AssertionError as exc:
            assert exc.args[0][2] == 409, exc
            return 409
    with ThreadPoolExecutor(2) as pool:
        assert sorted(pool.map(cancel, range(2))) == [204,409]
    assert product(pid)["data"]["variants"][0]["stockQuantity"] == 10
    assert all(o["id"] != oid for o in call("GET", "seller/orders", other))
    order = checkout()
    oid = order["id"]
    item = dict(orderItemId=order["items"][0]["orderItemId"], rating=5, comment="Good")
    call("POST", "reviews", buyer, item, 409)
    path = f"seller/orders/{oid}/status"
    call("PUT", path, other, {"status":"CONFIRMED"}, 404)
    call("PUT", path, seller, {"status":"SHIPPING"}, 409)
    call("PUT", f"orders/{oid}/confirm-delivery", buyer, expect=409)
    call("PUT", path, seller, {"status":"CONFIRMED"}, 204)
    call("PUT", f"orders/{oid}/cancel", buyer, expect=409)
    call("PUT", path, seller, {"status":"SHIPPING"}, 204)
    call("PUT", f"orders/{oid}/confirm-delivery", buyer, expect=204)
    call("POST", "reviews", other, item, 404)
    call("POST", "reviews", buyer, dict(item, rating=6), 400)
    call("POST","reviews",buyer,dict(item,images=["javascript:void(0)"]),400)
    call("POST","reviews",buyer,dict(item,comment="a"*1001),400)
    item["comment"] = ""
    item["images"] = ["https://example.invalid/photo.jpg"]
    created = call("POST", "reviews", buyer, item, 201)
    review_id = created["id"]
    history = f"products/{pid}/reviews"
    public = call("GET", history + "?rating=5&withImages=true&pageSize=1")
    assert not call("GET",history + "?withComment=true")["results"]["items"]
    assert public["starCounts"] == [0,0,0,0,1] and public["ratingAvg"] == 5
    assert public["results"]["items"][0]["images"] == item["images"]
    assert not call("GET",history + "?page=2&pageSize=1")["results"]["items"]
    assert call("GET",history + "?rating=1")["reviewCount"] == 1
    reply = f"seller/reviews/{review_id}/reply"
    call("PUT",reply,other,{"reply":"hello"},404)
    call("PUT",reply,seller,{"reply":"Cảm ơn!"},204)
    assert call("GET",history)["results"]["items"][0]["sellerReply"] == "Cảm ơn!"
    moderate = f"admin/reviews/{review_id}/status"
    call("PUT",moderate,seller,{"status":"HIDDEN"},403)
    call("PUT",moderate,admin,{"status":"HIDDEN"},204)
    assert not call("GET",history)["results"]["items"]
    assert call("GET","products/"+pid)["product"]["reviewCount"] == 0
    call("PUT",moderate,admin,{"status":"ACTIVE"},204)
    summary = call("GET","products/"+pid)["product"]
    assert summary["ratingAvg"] == 5 and summary["reviewCount"] == 1
    call("POST", "reviews", buyer, item, 409)
    order2 = checkout()
    for status in ("CONFIRMED","SHIPPING","COMPLETED"):
        call("PUT", f"seller/orders/{order2['id']}/status", seller, {"status":status}, 204)
    call("DELETE", "seller/products/" + pid, seller, expect=204)
    assert product(pid)["status"] == "DISCONTINUED"
    assert any(o["id"] == oid and o["items"][0]["reviewed"] for o in call("GET","orders/my-orders",buyer))
    unsold = create(seller)
    call("DELETE", "seller/products/" + unsold, seller, expect=204)
    assert product(unsold)["status"] == "HIDDEN"
    stats = call("GET", "seller/analytics", seller)
    assert stats["revenue"] == seller_baseline["revenue"] + 48000 and stats["orders"] == seller_baseline["orders"] + 3
    assert dict(name="Edited",quantity=4) in stats["bestSellers"]
    assert call("GET","seller/analytics",other)["revenue"] == 0
    call("PUT", f"admin/products/{foreign_id}/status", admin, dict(status="HIDDEN"), 204)
    call("GET", "products/" + foreign_id, expect=404)
    assert call("GET",new_shop)["products"] == []
    uid = login("second")["userId"]
    call("PUT", f"admin/users/{uid}/status", admin, dict(status="BANNED"), 204)
    call("GET",new_shop,expect=404)
    call("GET", "seller/orders", other, expect=401)
    call("POST", "auth/login", data=dict(email="second@example.invalid",password=PASSWORD),expect=401)
    call("PUT", f"admin/users/{uid}/status", admin, dict(status="ACTIVE"), 204)
    login("second")
    stats = call("GET","admin/dashboard",admin)
    assert stats["revenue"] == baseline["revenue"] + 48000 and stats["orders"] == baseline["orders"] + 4
    logs = call("GET","admin/audit-logs",admin)
    assert sorted(l["action"] for l in logs) == ["APPROVE_SHOP","PRODUCT_STATUS","REVIEW_STATUS","REVIEW_STATUS","USER_STATUS","USER_STATUS"]
    assert all(l["admin"] == "admin@example.invalid" and json.loads(l["details"])["current"] for l in logs)
    print("PASS: seed, roles, approval, products, isolation, split checkout, concurrent cancellation,")
    print("delivery, reviews, history, moderation, account lock, analytics and transactional audit.")


if __name__ == "__main__":
    main()

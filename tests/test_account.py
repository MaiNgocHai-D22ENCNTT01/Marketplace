"""Account integration checks; run via run_postgres.py on an isolated database."""
import html
import re
import os
from concurrent.futures import ThreadPoolExecutor
from http.cookiejar import CookieJar
from urllib.parse import urlencode
from urllib.request import build_opener, HTTPCookieProcessor
from urllib.error import HTTPError
from test_step3 import call, register, login, PASSWORD

owner = register("profile-owner")
other = register("profile-other")
call("GET", "account/profile", expect=401)
for method, path, body in [("PUT", "account/profile", {"fullName":"Guest"}),
    ("POST", "account/change-password", {"currentPassword":PASSWORD,"newPassword":"Changed123!"}),
    ("GET", "account/addresses", None), ("POST", "account/addresses", {})]:
    call(method, path, data=body, expect=401)
for role in ("buyer", "seller", "admin"):
    assert call("GET", "account/profile", login(role)["token"])["roles"]
p = call("GET", "account/profile", owner)
assert p["email"] == "profile-owner@example.invalid" and p["roles"] == ["CUSTOMER"]
assert "passwordHash" not in p and "avatarUrl" in p and p["createdAt"]
call("PUT", "account/profile", owner, {"fullName":" Nguyễn An ","phoneNumber":"0901234567"}, 204)
p = call("GET", "account/profile", owner)
assert p["fullName"] == "Nguyễn An" and p["phoneNumber"] == "0901234567"
call("PUT", "account/profile", owner, {"fullName":"   "}, 400)
call("PUT", "account/profile", owner, {"fullName":"An","phoneNumber":"invalid"}, 400)
address = dict(recipientName="Nguyễn An", phone="0901234567", province="Hà Nội",
               district="Cầu Giấy", ward="Dịch Vọng", detailAddress="12 Đường A", isDefault=False)
call("POST", "account/addresses", owner, dict(address, province=""), 400)
a = call("POST", "account/addresses", owner, address, 201)
assert a["isDefault"]
b = call("POST", "account/addresses", owner, dict(address, detailAddress="34 Đường B"), 201)
assert not b["isDefault"]
for method, suffix in [("PUT", "/default"), ("DELETE", "")]:
    call(method, "account/addresses/" + a["id"] + suffix, other, expect=404)
assert call("GET", "account/addresses", other) == []
with ThreadPoolExecutor(max_workers=8) as pool:
    list(pool.map(lambda i: call("POST", "account/addresses", owner,
        dict(address, detailAddress=str(i), isDefault=True), 201), range(8)))
assert sum(x["isDefault"] for x in call("GET", "account/addresses", owner)) == 1
with ThreadPoolExecutor(max_workers=8) as pool:
    list(pool.map(lambda i: call("PUT", "account/addresses/" + (a if i % 2 else b)["id"] + "/default",
        owner, expect=204), range(12)))
assert sum(x["isDefault"] for x in call("GET", "account/addresses", owner)) == 1
call("PUT", "account/addresses/" + a["id"] + "/default", owner, expect=204)
call("DELETE", "account/addresses/" + a["id"], owner, expect=204)
assert sum(x["isDefault"] for x in call("GET", "account/addresses", owner)) == 1
call("POST", "account/change-password", owner, {"currentPassword":"wrong","newPassword":"Changed123!"}, 409)
call("POST", "account/change-password", owner, {"currentPassword":PASSWORD,"newPassword":"short"}, 400)
call("POST", "account/change-password", owner, {"currentPassword":PASSWORD,"newPassword":PASSWORD}, 409)
call("POST", "account/change-password", owner, {"currentPassword":PASSWORD,"newPassword":"Changed123!"}, 204)
call("POST", "auth/login", data={"email":p["email"],"password":PASSWORD}, expect=401)
owner = call("POST", "auth/login", data={"email":p["email"],"password":"Changed123!"})["token"]

base = os.environ["MARKETPLACE_TEST_WEB"]
browser = build_opener(HTTPCookieProcessor(CookieJar()))
def page(path, data=None):
    with browser.open(base + path, None if data is None else urlencode(data).encode(), timeout=20) as response:
        return html.unescape(response.read().decode())
def token(markup):
    return re.search(r'name="__RequestVerificationToken"[^>]+value="([^"]+)"', markup)[1]
assert "Đăng nhập" in page("/Account/Profile")
page("/Account/Login", dict(email=p["email"],password="Changed123!",
    __RequestVerificationToken=token(page("/Account/Login"))))
profile = page("/Account/Profile")
assert "Nguyễn An" in profile and "Hồ sơ của tôi" in profile
try:
    page("/Account/UpdateProfile", {"FullName":"Forged"})
    raise AssertionError("Missing antiforgery accepted")
except HTTPError as e:
    assert e.code == 400
updated = page("/Account/UpdateProfile", dict(FullName="An Updated", PhoneNumber="0987654321",
    __RequestVerificationToken=token(profile)))
assert "An Updated" in updated and "Đã lưu thay đổi thành công" in updated
page("/Account/AddAddress", dict(RecipientName="Web Receiver", Phone="0901234567", Province="Hà Nội",
    District="Cầu Giấy", Ward="Dịch Vọng", DetailAddress="99 Web Street", IsDefault="true",
    __RequestVerificationToken=token(page("/Account/Profile?tab=addresses"))))
book = page("/Account/Profile?tab=addresses")
assert "Web Receiver" in book and 'id="address-dialog"' in book
# Put an item in the cart, then verify server-rendered checkout prefills the default.
products = call("GET", "products")
for product in products:
    detail = call("GET", "products/" + product["id"])
    variants = detail.get("productVariants", [])
    variant = next((v for v in variants if v["stockQuantity"] > 0), None)
    if variant:
        call("POST", "cart/items", owner, {"variantId":variant["id"], "quantity":1}, 204)
        break
else:
    raise AssertionError("No available fixture product")
checkout = page("/Cart")
assert 'id="saved-address"' in checkout and 'value="Web Receiver"' in checkout
assert "99 Web Street, Dịch Vọng, Cầu Giấy, Hà Nội</textarea>" in checkout
password_page = page("/Account/Profile?tab=password")
failed = page("/Account/ChangePassword", dict(CurrentPassword="Changed123!", NewPassword="Next12345!",
    ConfirmPassword="Mismatch123!", __RequestVerificationToken=token(password_page)))
assert "alert-danger" in failed
call("POST", "auth/login", data={"email":p["email"],"password":"Changed123!"})
for item in call("GET", "account/addresses", owner):
    call("DELETE", "account/addresses/" + item["id"], owner, expect=204)
assert call("GET", "account/addresses", owner) == []
print("PASS account: authorization, ownership, validation, password hashing, concurrent defaults, deletion, MVC CSRF, profile forms, checkout prefill")

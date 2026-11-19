"""Runs after the existing suites on the disposable PostgreSQL fixture."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from http.cookiejar import CookieJar
from urllib.parse import urlencode
from urllib.request import build_opener, HTTPCookieProcessor
from urllib.error import HTTPError
import html
import re
from urllib.request import urlopen, Request
import json
import os
from test_step3 import BASE, call, login, register

seller = login("seller")["token"]
admin = login("admin")["token"]
window = call("GET", "flash-sales/current")
assert window["campaign"]["status"] == "ACTIVE" and len(window["items"]) == 8
offer = next(i for i in window["items"] if i["sku"] == "DEMO-TEE-MINT")
vid = offer["variantId"]
today = datetime.now(timezone.utc)
times = dict(title="Test scheduled sale", startTime=(today+timedelta(days=1)).isoformat(),
             endTime=(today+timedelta(days=1,hours=2)).isoformat())
call("POST","admin/flash-sales",seller,times,403)
cid = call("POST","admin/flash-sales",admin,times,201)["id"]
assert cid in [c["id"] for c in call("GET","seller/flash-sales/available",seller)]
path = f"seller/flash-sales/{cid}/register"
data = dict(variantId=vid, flashPrice=100, totalQuota=4,maxPerUser=2)
foreign = next(i for i in window["items"] if i["shopName"] != offer["shopName"])
call("POST",path,seller,dict(data,variantId=foreign["variantId"]),404)
call("POST",path,seller,dict(data,totalQuota=100000),409)
reg = call("POST",path,seller,data,201)["id"]
call("POST",path,seller,data,409)
call("PUT",f"admin/flash-sales/items/{reg}/approve",admin,{"status":"APPROVED"},204)
polo = next(i for i in window["items"] if i["sku"]=="DEMO-POLO-MINT")
rejected = call("POST",path,seller,dict(data,variantId=polo["variantId"]),201)["id"]
call("PUT",f"admin/flash-sales/items/{rejected}/approve",admin,{"status":"REJECTED"},204)
assert {reg,rejected} <= {l["targetId"] for l in call("GET","admin/audit-logs",admin) if l["action"]=="FLASH_SALE_DECISION"}

def sold():
    return next(i["soldCount"] for i in call("GET","flash-sales/current")["items"] if i["variantId"]==vid)

def stock():
    return next(v["stockQuantity"] for v in call("GET","products/"+offer["productId"])["productVariants"] if v["id"]==vid)

address = dict(recipientName="Sale",phone="0900000000",detailAddress="Test")
def add(token, qty):
    call("POST","cart/items",token,dict(variantId=vid,quantity=qty),204)
def checkout(token):
    return call("POST","orders/checkout",token,address)
def cancel(token, group):
    oid = next(o["id"] for o in call("GET","orders/my-orders",token) if o["orderGroupId"]==group)
    call("PUT",f"orders/{oid}/cancel",token,expect=204)
    return oid

original = stock()
customers = [register("flash-"+str(i)) for i in range(6)]
buyer = customers[0]
add(buyer,2)
assert call("GET","cart",buyer)["total"] == offer["flashPrice"]*2
group = checkout(buyer)
assert group["total"] == offer["flashPrice"]*2
order = call("GET","orders/my-orders",buyer)[0]
assert order["items"][0]["unitPrice"] == offer["flashPrice"]
add(buyer,1)
call("POST","orders/checkout",buyer,address,409)
assert sold()==2 and stock()==original-2 and call("GET","cart",buyer)["quantity"]==1
oid = cancel(buyer,group["orderGroupId"])
call("PUT",f"orders/{oid}/cancel",buyer,expect=409)
assert sold()==0 and stock()==original
add(buyer,1)
for token in customers[1:]: add(token,2)

def buy(token):
    request = Request(BASE+"/api/orders/checkout",data=json.dumps(address).encode(),
                      headers={"Content-Type":"application/json","Authorization":"Bearer "+token})
    try:
        with urlopen(request,timeout=20) as response: return response.status,json.load(response)
    except HTTPError as e:
        assert e.code == 409 and "suất" in e.read().decode()
        return e.code,None

with ThreadPoolExecutor(6) as pool:
    results = list(pool.map(buy,customers))
assert sorted(s for s,_ in results) == [200]*5+[409]
assert sold()==10 and stock()==original-10
for token,(status,result) in zip(customers,results):
    if status==200: cancel(token,result["orderGroupId"])
assert sold()==0 and stock()==original
with urlopen(os.environ["MARKETPLACE_TEST_WEB"] + "/", timeout=20) as response:
    home = response.read().decode()
    assert "data-flash-deadline=" in home and vid in home and home.index('class="hero"') < home.index('class="flash-window"')
# Submit the actual MVC forms using separate antiforgery-protected sessions.
web = os.environ["MARKETPLACE_TEST_WEB"]
def session(account):
    browser = build_opener(HTTPCookieProcessor(CookieJar()))
    def page(path, data=None):
        with browser.open(web + path, data=None if data is None else urlencode(data).encode(), timeout=20) as response:
            return html.unescape(response.read().decode())
    login_page = page("/Account/Login")
    page("/Account/Login", dict(email=account+"@example.invalid",password="Marketplace123!",
                                __RequestVerificationToken=csrf(login_page)))
    return page
def csrf(markup):
    return re.search(r'name="__RequestVerificationToken"[^>]+value="([^"]+)"',markup)[1]

admin_page, seller_page = session("admin"), session("seller")
admin_form = admin_page("/FlashSales/Admin")
assert "Tạo khung giờ" in admin_form
schedule = today + timedelta(days=4)
saved = admin_page("/FlashSales/Create", dict(title="MVC flash sale",startTime=schedule.strftime("%Y-%m-%dT12:00"),
                    endTime=schedule.strftime("%Y-%m-%dT14:00"),__RequestVerificationToken=csrf(admin_form)))
assert "Đã tạo khung giờ Flash Sale" in saved
campaign = next(c for c in call("GET","seller/flash-sales/available",seller) if c["title"]=="MVC flash sale")
seller_form = seller_page("/FlashSales/Seller")
saved = seller_page("/FlashSales/Register", dict(**data,campaignId=campaign["id"],__RequestVerificationToken=csrf(seller_form)))
assert "Đã gửi đăng ký chờ duyệt" in saved
pending = next(i for i in call("GET","admin/flash-sales/items",admin) if i["campaignId"]==campaign["id"])
saved = admin_page("/FlashSales/Decide/"+pending["id"], dict(status="APPROVED",
                   __RequestVerificationToken=csrf(admin_page("/FlashSales/Admin"))))
assert "Đã xử lý đăng ký" in saved
print("PASS: flash sale roles, registration, moderation, discounted cart and checkout, per-user limit, concurrent quota, cancellation and home banner.")

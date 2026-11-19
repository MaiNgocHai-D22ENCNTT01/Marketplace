"""Exercise the rendered MVC forms with session cookies and antiforgery tokens."""
import html
import os
import re
from http.cookiejar import CookieJar
from urllib.parse import urlencode
from urllib.request import build_opener, HTTPCookieProcessor

base = os.environ["MARKETPLACE_TEST_WEB"]
guest = build_opener()
def visit(path):
    return html.unescape(guest.open(base + path, timeout=20).read().decode())
home = visit("/?keyword=polo")
shop_link = re.search(r'href="(/Shops/Detail/[0-9a-f-]+)"', home)[1]
store = visit(shop_link + "?sort=price_desc")
assert "Studio Vải" in store and "Đang hoạt động" in store and "Ngày tham gia" in store
next_page = re.search(r'href="([^"]+)"[^>]*>Trang sau', store)[1]
assert "sort=price_desc" in next_page and "Trang 2 /" in visit(next_page)
assert 'class="product-card"' not in visit(shop_link + "?keyword=doesnotmatch")
for account, path, marker in (("admin","Admin","Nhật ký quản trị"),("seller","Seller","Sản phẩm bán chạy"),
                              ("buyer","Orders","Đăng ký mở gian hàng")):
    browser = build_opener(HTTPCookieProcessor(CookieJar()))
    def page(path, data=None):
        with browser.open(base + "/" + path, None if data is None else urlencode(data).encode(), timeout=15) as response:
            return html.unescape(response.read().decode())
    def token(html):
        return re.search(r'name="__RequestVerificationToken"[^>]+value="([^"]+)"', html)[1]
    page("Account/Login", dict(email=account + "@example.invalid",password="Marketplace123!",
                               __RequestVerificationToken=token(page("Account/Login"))))
    assert marker in page(path), (account, path)
    if account == "admin":
        reviews = page("Admin/Reviews")
        action = re.search(r'action="([^"]*/Admin/ReviewStatus/[^"]+)"', reviews)[1].lstrip("/")
        page(action, dict(hidden="false", page=1, __RequestVerificationToken=token(reviews)))
        hidden = page("Admin/Reviews?hidden=true")
        assert "/" + action in hidden
        page(action, dict(hidden="true", page=1, __RequestVerificationToken=token(hidden)))
    if account == "buyer":
        orders = page("Orders")
        assert 'name="comment" maxlength="1000"' in orders
        item = re.search(r'data-review-item="([^"]+)"', orders)[1]
        reviewed = page("Orders/Review", dict(orderItemId=item, rating=4, comment="á"*1000,
                        __RequestVerificationToken=token(orders)))
        assert item not in re.findall(r'data-review-item="([^"]+)"', reviewed) and "Đã đánh giá" in reviewed
        from test_step3 import call
        mint = next(p for p in call("GET","products") if p["name"] == "Áo thun cổ tròn – Xanh bạc hà")
        detail_path = "Products/Detail/" + mint["id"]
        preview = page(detail_path + "?withComment=true")
        assert "Mình cao 1m65" in preview and "Phản hồi của Người bán" in preview and "review-avatar" in preview
        assert "Có bình luận chữ" in preview and "Màu ngoài đời" not in page(detail_path + "?rating=5")
        suggestions = page("Products/Suggestions?q=ao+thun")
        detail_url = re.search(r'href="([^"]*/Products/Detail/[^"]+)"', suggestions, re.I)[1]
        assert "<img" in suggestions and "₫" in suggestions
        detail = page(detail_url.lstrip("/"))
        assert shop_link in detail and ">Xem Shop</a>" in detail
        assert "Đánh giá của khách hàng" in detail and '<progress max="100"' in detail
        chat_url = re.search(r'href="([^"]*/Chat[^"]*)"[^>]*>Chat với Shop', detail)[1]
        chat = page(chat_url.lstrip("/"))
        assert 'id="attachment"' in chat and 'id="compose"' in chat
        assert 'signalr.min.js' in chat and 'data-product=""' not in chat
        assert '"token"' in page("Chat/Token", dict(__RequestVerificationToken=token(chat)))
        toggle_url = re.search(r'<form[^>]+class="wish-form"[^>]+action="([^"]+)"', detail)[1]
        def toggle_from(markup):
            return page(toggle_url.lstrip("/"), dict(returnUrl="/Wishlists",
                        __RequestVerificationToken=token(markup)))
        liked = toggle_from(detail)
        assert 'aria-pressed="true"' in liked and "bi-heart-fill" in liked
        assert 'aria-pressed="true"' in page(detail_url.lstrip("/"))
        assert 'class="wish-form"' not in toggle_from(liked)
    if account == "seller":
        reviews = page("Seller/Reviews")
        action = re.search(r'action="([^"]*/Seller/Reply/[^"]+)"', reviews)[1]
        saved = page(action.lstrip("/"), dict(reply="MVC seller reply", page=1, __RequestVerificationToken=token(reviews)))
        assert "MVC seller reply" in saved
        form = page("Seller/Edit")
        category = re.search(r'<option value="([0-9a-f-]{36})"', form)[1]
        result = page("Seller/Save", {"Product.Name":"MVC-created","Product.CategoryId":category,
            "Product.Description":"MVC test","Product.ImageUrl":"/images/demo/tee-mint.png",
            "Product.Variants[0].Sku":"MVC-SKU","Product.Variants[0].Price":"1100",
            "Product.Variants[0].StockQuantity":"3","__RequestVerificationToken":token(form)})
        assert "MVC-created" in result and "Đã lưu sản phẩm." in result, result[-3000:]
print("PASS: MVC logins, role pages, JWT session and seller product form.")

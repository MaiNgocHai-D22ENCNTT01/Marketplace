"""Chat integration on the isolated PostgreSQL fixture. Requires Python websockets."""
import json
import uuid
from concurrent.futures import ThreadPoolExecutor
from urllib.parse import quote
from urllib.request import Request, urlopen
from websockets.sync.client import connect
from test_step3 import BASE, call, login, register

buyer = login("buyer")["token"]
seller = login("seller")["token"]
other = register("chat-observer")
pid = call("GET", "products/suggestions?q=ao%20thun")[0]["id"]
product = call("GET", "products/" + pid)
shop = product["shop"]["id"]
call("GET", "chat/conversations", expect=401)
call("POST", "../hubs/chat/negotiate?negotiateVersion=1", expect=401)
origin = "http://localhost:5215"
for route in ("/hubs/chat/negotiate", "/api/chat/send"):
    req = Request(BASE + route, method="OPTIONS", headers={"Origin": origin,
        "Access-Control-Request-Method": "POST", "Access-Control-Request-Headers": "authorization,content-type"})
    with urlopen(req) as res:
        assert res.headers["Access-Control-Allow-Origin"] == origin
sep = chr(30)
def socket(token):
    ws = connect(BASE.replace("http://", "ws://") + "/hubs/chat?access_token=" + quote(token), origin=origin)
    ws.send(json.dumps({"protocol":"json","version":1}) + sep)
    assert json.loads(ws.recv(timeout=5).split(sep)[0]) == {}
    return ws

def received(ws, expected):
    while True:
        for frame in ws.recv(timeout=5).split(sep):
            if frame:
                event = json.loads(frame)
                if event.get("target") == "MessageReceived":
                    assert event["arguments"][0]["messageId"] == expected
                    return

with socket(buyer) as b, socket(seller) as s, socket(other) as outsider:
    request = {"shopId": shop, "content":"Còn hàng không?", "productId":pid, "messageId":str(uuid.uuid4())}
    with ThreadPoolExecutor(2) as pool:
        first, again = pool.map(lambda _: call("POST","chat/send",buyer,request), range(2))
    assert first == again and first["product"]["id"] == pid
    for client in (b,s): received(client, first["id"])
    thread = first["conversationId"]
    path = "chat/conversations/" + thread
    assert call("GET","chat/conversations",seller)[0]["unread"] == 1
    assert len(call("GET",path+"/messages",buyer)) == 1
    assert not call("GET",path+"/messages",seller)[0]["mine"]
    call("GET",path+"/messages",other,expect=404)
    call("POST","chat/send",other,{"conversationId":thread,"content":"test"},404)
    call("POST",path+"/read",other,[first["id"]],404)
    call("POST",path+"/read",seller,[first["id"]],204)
    reply = call("POST","chat/send",seller,{"conversationId":thread,"content":"Còn hàng bạn nhé."})
    for client in (b,s): received(client, reply["id"])
    older = call("GET",path+"/messages?before="+reply["id"],buyer)
    assert [m["id"] for m in older] == [first["id"]] and older[0]["isRead"]
    assert call("GET","chat/conversations",other) == []
    try:
        outsider.recv(timeout=.4)
    except TimeoutError:
        pass
    else:
        raise AssertionError("Another account received a chat event")
print("PASS: JWT hub, CORS, two-way live delivery, retry, product card, read state, history and participant isolation.")

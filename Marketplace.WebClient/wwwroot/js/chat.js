(() => {
    const $ = id => document.getElementById(id);
    const config = $('chat').dataset;
    const base = new URL(config.api.replace(/\/?$/, '/'), location.href);
    const threads = $('threads'), messages = $('messages'), form = $('compose');
    let active = '', draftShop = config.shop, attachment = config.product, sending = false;
    let visible = new Map(), listVersion = 0, viewVersion = 0, more = false, retry;
    let token, connected = false;
    const setError = text => { $('chat-error').textContent = text; };
    const textNode = (tag, text) => Object.assign(document.createElement(tag), { textContent: text });

    async function getToken() {
        const body = new FormData();
        body.set('__RequestVerificationToken', form.elements.__RequestVerificationToken.value);
        const response = await fetch(config.token, { method: 'POST', body, cache: 'no-store' });
        if (!response.ok) throw new Error('Phiên đăng nhập đã hết. Hãy đăng nhập lại.');
        token = (await response.json()).token;
        return token;
    }
    async function api(path, data) {
        const headers = { Authorization: 'Bearer ' + token };
        const options = { headers, cache: 'no-store' };
        if (data !== undefined) {
            options.method = 'POST';
            headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(data);
        }
        const response = await fetch(new URL('api/chat/' + path, base), options);
        if (!response.ok) {
            if (response.status === 401) throw new Error('Phiên đăng nhập đã hết. Hãy đăng nhập lại.');
            throw new Error(response.status === 400 ? (await response.text()).slice(0, 200) : 'Không thể tải hoặc gửi tin nhắn.');
        }
        return response.status === 204 ? null : response.json();
    }
    function renderMessages() {
        const nodes = [];
        for (const m of [...visible.values()].sort((a,b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id))) {
            const bubble = document.createElement('article');
            if (m.mine) bubble.className = 'mine';
            if (m.product) {
                const p = m.product, card = document.createElement('a');
                const link = new URL(config.detail, location.href);
                link.searchParams.set('id', p.id);
                card.href = link;
                if (p.imageUrl) {
                    try {
                        const src = new URL(p.imageUrl, base);
                        if (['http:', 'https:'].includes(src.protocol)) {
                            const img = new Image(); img.src = src; img.alt = ''; card.append(img);
                        }
                    } catch { /* The product name remains available. */ }
                }
                card.append(textNode('strong', p.name + ' · ' + p.price.toLocaleString('vi-VN') + ' ₫'));
                bubble.append(card);
            }
            bubble.append(textNode('p', m.content), textNode('time', new Date(m.createdAt).toLocaleString('vi-VN')));
            nodes.push(bubble);
        }
        messages.replaceChildren(...nodes);
        $('older').hidden = !more;
    }
    async function loadMessages(older = false) {
        if (!active) return;
        const id = active, version = ++viewVersion;
        const first = [...visible.values()].sort((a,b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id))[0];
        const suffix = older && first ? '?before=' + first.id : '';
        const rows = await api('conversations/' + id + '/messages' + suffix);
        if (id !== active || version !== viewVersion) return;
        const oldHeight = messages.scrollHeight;
        const atEnd = messages.scrollTop + messages.clientHeight >= oldHeight - 20;
        // A reconnect can span more than one page. Leave the gap accessible through the history cursor.
        if (!older && visible.size && rows.length === 50 && !rows.some(m => visible.has(m.id))) visible.clear();
        if (older || visible.size === 0) more = rows.length === 50;
        rows.forEach(m => visible.set(m.id, m));
        renderMessages();
        if (older) messages.scrollTop += messages.scrollHeight - oldHeight;
        else if (atEnd) messages.scrollTop = messages.scrollHeight;
        const unread = rows.filter(m => !m.mine && !m.isRead).map(m => m.id);
        if (unread.length && !document.hidden) {
            await api('conversations/' + id + '/read', unread);
            await list();
        }
    }
    function detach() {
        attachment = '';
        $('attachment')?.remove();
    }
    function select(id, name, keepDraft = false) {
        if (sending) return;
        if (!keepDraft) { detach(); draftShop = ''; $('content').value = ''; retry = null; }
        active = id;
        ++viewVersion;
        visible.clear();
        more = false;
        renderMessages();
        $('peer').textContent = name;
        form.hidden = false;
        for (const button of threads.children) button.setAttribute('aria-current', String(button.dataset.id === id));
    }
    async function list() {
        const version = ++listVersion;
        const rows = await api('conversations');
        if (version !== listVersion) return;
        threads.replaceChildren();
        for (const row of rows) {
            const button = document.createElement('button');
            button.type = 'button';
            button.dataset.id = row.id;
            button.setAttribute('aria-current', String(row.id === active));
            button.append(textNode('strong', row.otherName + (row.unread ? ' (' + row.unread + ')' : '')),
                textNode('small', row.preview || 'Thẻ sản phẩm'));
            button.onclick = () => {
                if (sending || row.id === active) return;
                select(row.id, row.otherName);
                loadMessages().catch(e => setError(e.message));
            };
            threads.append(button);
        }
        if (!rows.length) threads.append(textNode('p', 'Chưa có cuộc hội thoại.'));
        if (!active && !sending) {
            const row = draftShop ? rows.find(r => r.asCustomer && r.shopId === draftShop) : rows[0];
            if (row) select(row.id, row.otherName, Boolean(draftShop));
        }
    }
    async function refresh() {
        await list();
        await loadMessages();
    }
    $('detach')?.addEventListener('click', detach);
    $('older').onclick = () => loadMessages(true).catch(e => setError(e.message));
    form.onsubmit = async event => {
        event.preventDefault();
        const content = $('content').value.trim();
        if (sending || (!content && !attachment)) return;
        const payload = { conversationId: active || null, shopId: active ? null : draftShop,
            content, productId: attachment || null };
        const key = JSON.stringify(payload);
        if (retry?.key !== key) {
            // getRandomValues is also available on HTTP development hosts.
            const bytes = crypto.getRandomValues(new Uint8Array(16));
            bytes[6] = (bytes[6] & 15) | 64;
            bytes[8] = (bytes[8] & 63) | 128;
            const hex = Array.from(bytes, n => n.toString(16).padStart(2, '0')).join('');
            retry = { key, id: [hex.slice(0,8), hex.slice(8,12), hex.slice(12,16), hex.slice(16,20), hex.slice(20)].join('-') };
        }
        sending = true;
        $('send').disabled = $('content').disabled = true;
        setError('');
        try {
            const m = await api('send', { ...payload, messageId: retry.id });
            active = m.conversationId;
            visible.set(m.id, m);
            renderMessages();
            messages.scrollTop = messages.scrollHeight;
            detach();
            retry = null;
            $('content').value = '';
            await list();
        } catch (e) { setError(e.message); }
        finally { sending = false; $('send').disabled = $('content').disabled = false; }
    };

    const hub = new signalR.HubConnectionBuilder().withUrl(new URL('hubs/chat', base).href, {
        accessTokenFactory: getToken, withCredentials: false
    }).withAutomaticReconnect().build();
    hub.on('MessageReceived', () => refresh().catch(e => setError(e.message)));
    hub.onreconnecting(() => { connected = false; $('connection').textContent = 'Đang kết nối lại…'; });
    function online() {
        connected = true;
        $('connection').textContent = 'Trực tuyến';
        refresh().catch(e => setError(e.message));
    }
    hub.onreconnected(online);
    hub.onclose(() => { connected = false; $('connection').textContent = 'Đang kết nối lại…'; });
    async function start() {
        if (hub.state !== signalR.HubConnectionState.Disconnected) return;
        try { await hub.start(); online(); }
        catch { $('connection').textContent = 'Chưa kết nối trực tuyến. Đang thử lại…'; }
    }
    async function init() {
        await getToken();
        if (draftShop) select('', config.shopName || 'Gian hàng', true);
        await refresh();
        await start();
        setInterval(() => { if (!connected && !document.hidden) start(); }, 5000);
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) refresh().catch(e => setError(e.message));
        });
    }
    init().catch(e => setError(e.message));
})();

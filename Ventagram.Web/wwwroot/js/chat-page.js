(() => {
  let chatHubConnection = null;
  let chatHubStartPromise = null;
  let activeChatConversationId = 0;
  let chatSidebarRefreshPromise = null;
  let chatThreadRefreshPromise = null;

  document.addEventListener("DOMContentLoaded", async () => {
    const root = document.querySelector("[data-chat-page]");
    if (!root) return;

    await loadChatPage();
    initRealtimeChat().catch(console.error);
  });

  async function loadChatPage() {
    const root = getChatRoot();
    if (!root) return;

    const conversationId = Number(root.dataset.initialConversationId || 0);
    try {
      const model = await fetchChatJson(conversationId > 0 ? `/api/chat/page/${conversationId}` : "/api/chat/page");
      renderChatShell(model);
      wireChatThread(document);
    } catch (error) {
      console.error(error);
      renderFatalError(error?.message || "No se pudo cargar el chat.");
    }
  }

  async function initRealtimeChat() {
    const root = getChatRoot();
    if (!root || document.body?.dataset.userAuthenticated !== "true" || !window.signalR) {
      return;
    }

    if (chatHubStartPromise) {
      return chatHubStartPromise;
    }

    chatHubConnection = new window.signalR.HubConnectionBuilder()
      .withUrl(buildChatHubUrl(), {
        withCredentials: true
      })
      .withAutomaticReconnect()
      .build();

    chatHubConnection.on("ConversationUpdated", () => {
      refreshChatSidebar().catch(console.error);
    });

    chatHubConnection.on("MessageReceived", payload => {
      if (!payload?.message) return;

      if (Number(payload?.conversationId || 0) === activeChatConversationId) {
        appendChatMessage(payload.message);
        if (document.visibilityState === "visible") {
          window.setTimeout(() => {
            markActiveConversationRead().catch(() => {});
          }, 120);
        }
      }

      refreshChatSidebar().catch(console.error);
    });

    chatHubConnection.on("MessagesRead", payload => {
      applyReadReceiptUpdate(payload);
    });

    chatHubConnection.onreconnected(() => {
      openActiveConversation().catch(() => {});
    });

    chatHubStartPromise = chatHubConnection.start()
      .catch(error => {
        console.error(error);
        chatHubStartPromise = null;
      });

    await chatHubStartPromise;
  }

  function wireChatThread(root = document) {
    const app = root.querySelector("[data-chat-page]");
    if (!app) {
      activeChatConversationId = 0;
      return;
    }

    const composeForm = app.querySelector("[data-chat-compose-form]");
    const input = app.querySelector("[data-chat-compose-input]");
    const counter = app.querySelector("[data-chat-compose-counter]");
    const thread = app.querySelector("[data-chat-thread]");
    const messageList = app.querySelector("[data-chat-message-list]");
    const inbox = app.querySelector("[data-chat-inbox]");
    activeChatConversationId = Number(thread?.dataset.conversationId || 0);

    if (messageList) {
      messageList.scrollTop = messageList.scrollHeight;
    }

    if (inbox && inbox.dataset.bound !== "true") {
      inbox.dataset.bound = "true";
      inbox.addEventListener("click", async event => {
        const link = event.target.closest("[data-chat-inbox-item]");
        if (!link) return;

        event.preventDefault();
        const conversationId = Number(link.dataset.conversationId || 0);
        if (conversationId <= 0) return;

        await selectChatConversation(conversationId, {
          pushState: true,
          revealThreadOnStackedLayout: true
        });
      });
    }

    if (counter && input) {
      const syncCounter = () => {
        counter.textContent = `${input.value.length} / 2000`;
      };

      syncCounter();
      if (input.dataset.boundCounter !== "true") {
        input.dataset.boundCounter = "true";
        input.addEventListener("input", syncCounter);
      }
    }

    if (composeForm && input && composeForm.dataset.bound !== "true") {
      composeForm.dataset.bound = "true";
      composeForm.addEventListener("submit", async event => {
        event.preventDefault();
        const body = input.value.trim();
        if (!body || activeChatConversationId <= 0) return;

        const submitButton = composeForm.querySelector("button[type='submit']");
        submitButton?.setAttribute("disabled", "disabled");

        try {
          await initRealtimeChat();
          await openActiveConversation();
          await chatHubConnection.invoke("SendMessage", activeChatConversationId, body);
          input.value = "";
          input.dispatchEvent(new Event("input", { bubbles: true }));
        } catch (error) {
          window.alert(error?.message || "No se pudo enviar el mensaje.");
        } finally {
          submitButton?.removeAttribute("disabled");
          input.focus();
        }
      });
    }

    if (activeChatConversationId > 0) {
      openActiveConversation().catch(console.error);

      if (document.body.dataset.chatVisibilityBound !== "true") {
        document.body.dataset.chatVisibilityBound = "true";
        document.addEventListener("visibilitychange", () => {
          if (document.visibilityState === "visible") {
            markActiveConversationRead().catch(() => {});
          }
        });
      }
    }

    if (app.dataset.chatPopstateBound !== "true") {
      app.dataset.chatPopstateBound = "true";
      window.addEventListener("popstate", () => {
        const match = window.location.pathname.match(/^\/Mensajes\/(\d+)$/i);
        const conversationId = match ? Number(match[1]) : 0;
        selectChatConversation(conversationId, { pushState: false }).catch(console.error);
      });
    }
  }

  async function openActiveConversation() {
    if (!chatHubConnection || activeChatConversationId <= 0) {
      return;
    }

    if (chatHubConnection.state !== "Connected") {
      await initRealtimeChat();
      if (!chatHubConnection || chatHubConnection.state !== "Connected") {
        return;
      }
    }

    await chatHubConnection.invoke("OpenConversation", activeChatConversationId);
    await markActiveConversationRead();
  }

  async function selectChatConversation(conversationId, options = {}) {
    const safeConversationId = Math.max(0, Number(conversationId || 0));
    activeChatConversationId = safeConversationId;

    await Promise.all([
      refreshChatThread(safeConversationId),
      refreshChatSidebar(safeConversationId)
    ]);

    if (options.pushState) {
      const nextUrl = safeConversationId > 0 ? `/Mensajes/${safeConversationId}` : "/Mensajes";
      window.history.pushState({}, "", nextUrl);
    }

    if (options.revealThreadOnStackedLayout) {
      revealChatThreadOnStackedLayout();
    }

    await openActiveConversation();
  }

  function revealChatThreadOnStackedLayout() {
    if (!window.matchMedia("(max-width: 1100px)").matches) {
      return;
    }

    const thread = document.querySelector(".chat-thread");
    if (!thread) {
      return;
    }

    thread.scrollIntoView({
      behavior: "smooth",
      block: "start"
    });
  }

  async function markActiveConversationRead() {
    if (!chatHubConnection || activeChatConversationId <= 0 || chatHubConnection.state !== "Connected") {
      return;
    }

    await chatHubConnection.invoke("MarkConversationRead", activeChatConversationId);
  }

  async function refreshChatSidebar(conversationId = activeChatConversationId) {
    const container = document.querySelector("[data-chat-sidebar-content]");
    if (!container || chatSidebarRefreshPromise) return chatSidebarRefreshPromise;

    const safeConversationId = Math.max(0, Number(conversationId || 0));
    const url = safeConversationId > 0
      ? `/api/chat/inbox/${safeConversationId}`
      : "/api/chat/inbox";

    chatSidebarRefreshPromise = fetchChatJson(url)
      .then(payload => {
        container.innerHTML = renderInbox(payload?.inbox || [], Number(payload?.selectedConversationId || 0));
        wireChatThread(document);
      })
      .finally(() => {
        chatSidebarRefreshPromise = null;
      });

    return chatSidebarRefreshPromise;
  }

  async function refreshChatThread(conversationId = activeChatConversationId) {
    const container = document.querySelector("[data-chat-thread-container]");
    if (!container || chatThreadRefreshPromise) return chatThreadRefreshPromise;

    const safeConversationId = Math.max(0, Number(conversationId || 0));
    const url = safeConversationId > 0
      ? `/api/chat/thread/${safeConversationId}`
      : "/api/chat/thread";

    chatThreadRefreshPromise = fetchChatJson(url)
      .then(payload => {
        container.innerHTML = renderThread(payload?.selectedConversation || null);
        wireChatThread(document);
      })
      .finally(() => {
        chatThreadRefreshPromise = null;
      });

    return chatThreadRefreshPromise;
  }

  function renderChatShell(model) {
    const root = getChatRoot();
    if (!root) return;

    root.dataset.currentUserId = String(Number(model?.currentUserId || 0));
    activeChatConversationId = Number(model?.selectedConversation?.conversationId || 0);
    root.innerHTML = `
      <aside class="chat-sidebar">
        <div data-chat-sidebar-content>${renderInbox(model?.inbox || [], activeChatConversationId)}</div>
      </aside>
      <section class="chat-thread" data-chat-thread-container>
        ${renderThread(model?.selectedConversation || null)}
      </section>
    `;
  }

  function renderInbox(inbox, selectedConversationId) {
    const safeInbox = Array.isArray(inbox) ? inbox : [];

    if (safeInbox.length === 0) {
      return `
        <div class="chat-sidebar-header">
          <div>
            <span class="eyebrow">Mensajes</span>
            <h1>Conversaciones</h1>
          </div>
          <span class="chat-sidebar-count">0</span>
        </div>
        <div class="chat-inbox" data-chat-inbox>
          <div class="empty-state compact-empty">
            <h3>Sin mensajes todavia</h3>
            <p>Cuando escribas desde un anuncio, tus conversaciones apareceran aca.</p>
          </div>
        </div>
      `;
    }

    const items = safeInbox.map(item => {
      const isActive = Number(item?.conversationId || 0) === Number(selectedConversationId || 0);
      const unread = Number(item?.unreadCount || 0);
      return `
        <a class="chat-inbox-item ${isActive ? "is-active" : ""}"
           href="/Mensajes/${escapeAttribute(item?.conversationId)}"
           data-chat-inbox-item
           data-skip-system-loading="true"
           data-conversation-id="${escapeAttribute(item?.conversationId)}">
          <div class="chat-inbox-copy">
            <div class="chat-inbox-topline">
              <strong>${escapeHtml(item?.otherParticipantName || "Usuario")}</strong>
              <time datetime="${escapeAttribute(item?.lastMessageAtUtc || "")}">${escapeHtml(formatInboxTime(item?.lastMessageAtUtc))}</time>
            </div>
            <div class="chat-inbox-publication">${escapeHtml(item?.publicationTitle || "")}</div>
          </div>
          <div class="chat-inbox-meta">
            ${unread > 0 ? `<span class="chat-unread-pill">${unread}</span>` : ""}
          </div>
        </a>
      `;
    }).join("");

    return `
      <div class="chat-sidebar-header">
        <div>
          <span class="eyebrow">Mensajes</span>
          <h1>Conversaciones</h1>
        </div>
        <span class="chat-sidebar-count">${safeInbox.length}</span>
      </div>
      <div class="chat-inbox" data-chat-inbox>${items}</div>
    `;
  }

  function renderThread(conversation) {
    if (!conversation) {
      return `
        <div class="empty-state chat-empty-thread">
          <div>
            <h2>Selecciona una conversacion</h2>
            <p>Elige un hilo a la izquierda o entra desde el boton de chat de un anuncio.</p>
          </div>
        </div>
      `;
    }

    const messages = (Array.isArray(conversation.messages) ? conversation.messages : []).map(message => `
      <article class="chat-message ${message?.isMine ? "is-mine" : ""}" data-chat-message-id="${escapeAttribute(message?.id)}">
        <div class="chat-message-bubble">
          <p>${escapeHtml(message?.body || "")}</p>
        </div>
        <div class="chat-message-meta">
          <time datetime="${escapeAttribute(message?.createdAtUtc || "")}">${escapeHtml(formatMessageTime(message?.createdAtUtc))}</time>
          ${message?.isMine ? `<span class="chat-message-read-state" data-message-read-status>${message?.readAtUtc ? `Leido ${escapeHtml(formatMessageTime(message.readAtUtc))}` : "Enviado"}</span>` : ""}
        </div>
      </article>
    `).join("");

    return `
      <div class="chat-thread-shell" data-chat-thread data-conversation-id="${escapeAttribute(conversation?.conversationId)}">
        <header class="chat-thread-header">
          <div class="chat-thread-summary">
            <div class="chat-thread-participant">
              <span class="eyebrow">Conversacion</span>
              <strong>${escapeHtml(conversation?.otherParticipantName || "Usuario")}</strong>
            </div>
            <div class="chat-thread-participant">
              <span class="eyebrow">Anuncio</span>
              <a class="chat-thread-publication-link" href="${escapeAttribute(conversation?.publicationDetailsUrl || "#")}">${escapeHtml(conversation?.publicationTitle || "")}</a>
              <span>${escapeHtml(`${conversation?.publicationPrice || ""} · ${conversation?.publicationLocality || ""}`)}</span>
            </div>
          </div>
        </header>
        <div class="chat-message-list" data-chat-message-list>${messages}</div>
        <form class="chat-compose" data-chat-compose-form>
          <label class="visually-hidden" for="chatMessageBody">Escribe tu mensaje</label>
          <textarea id="chatMessageBody"
                    name="body"
                    rows="4"
                    maxlength="2000"
                    placeholder="Escribe sobre este anuncio..."
                    data-chat-compose-input></textarea>
          <div class="chat-compose-footer">
            <span class="field-hint" data-chat-compose-counter>0 / 2000</span>
            <button type="submit" class="primary-pill">Enviar</button>
          </div>
        </form>
      </div>
    `;
  }

  function appendChatMessage(message) {
    const app = document.querySelector("[data-chat-page]");
    const messageList = app?.querySelector("[data-chat-message-list]");
    const currentUserId = Number(app?.dataset.currentUserId || 0);
    if (!messageList || !message || !message.id) return;
    if (messageList.querySelector(`[data-chat-message-id="${message.id}"]`)) return;

    const isMine = Number(message.senderUserId || 0) === currentUserId || Boolean(message.isMine);
    const readState = isMine
      ? `<span class="chat-message-read-state" data-message-read-status>${message.readAtUtc ? `Leido ${escapeHtml(formatMessageTime(message.readAtUtc))}` : "Enviado"}</span>`
      : "";

    messageList.insertAdjacentHTML("beforeend", `
      <article class="chat-message ${isMine ? "is-mine" : ""}" data-chat-message-id="${escapeAttribute(message.id)}">
        <div class="chat-message-bubble">
          <p>${escapeHtml(message.body || "")}</p>
        </div>
        <div class="chat-message-meta">
          <time datetime="${escapeAttribute(message.createdAtUtc || "")}">${escapeHtml(formatMessageTime(message.createdAtUtc))}</time>
          ${readState}
        </div>
      </article>
    `);

    messageList.scrollTop = messageList.scrollHeight;
  }

  function applyReadReceiptUpdate(payload) {
    if (Number(payload?.conversationId || 0) !== activeChatConversationId) {
      return;
    }

    const readAt = formatMessageTime(payload?.readAtUtc);
    const messageIds = Array.isArray(payload?.messageIds) ? payload.messageIds : [];
    messageIds.forEach(messageId => {
      const status = document.querySelector(`[data-chat-message-id="${messageId}"] [data-message-read-status]`);
      if (status) {
        status.textContent = `Leido ${readAt}`;
      }
    });
  }

  async function fetchChatJson(path) {
    const url = buildChatServiceUrl(path);
    if (!url) {
      throw new Error("Configura la URL del servicio de chat.");
    }

    const response = await fetch(url, {
      headers: { "X-Requested-With": "fetch" },
      credentials: "include"
    });

    if (response.status === 401) {
      redirectToLogin();
      throw new Error("Sesion vencida.");
    }

    if (!response.ok) {
      throw new Error("No se pudo cargar el chat.");
    }

    return response.json();
  }

  function buildChatServiceUrl(path) {
    const root = getChatRoot();
    const baseUrl = String(root?.dataset.chatApiBaseUrl || root?.dataset.chatBaseUrl || "").trim().replace(/\/+$/, "");
    const normalizedPath = path
      ? (path.startsWith("/") ? path : `/${path}`)
      : "";

    if (!baseUrl) {
      return normalizedPath;
    }

    return `${baseUrl}${normalizedPath}`;
  }

  function buildChatHubUrl() {
    const root = getChatRoot();
    const hubUrl = String(root?.dataset.chatHubUrl || "").trim();
    if (hubUrl) {
      return hubUrl;
    }

    return buildChatServiceUrl("/hubs/chat");
  }

  function redirectToLogin() {
    const returnUrl = `${window.location.pathname}${window.location.search}`;
    window.location.href = `/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  function renderFatalError(message) {
    const root = getChatRoot();
    if (!root) return;

    root.innerHTML = `
      <section class="chat-thread">
        <div class="empty-state chat-empty-thread">
          <div>
            <h2>No se pudo cargar el chat</h2>
            <p>${escapeHtml(message)}</p>
          </div>
        </div>
      </section>
    `;
  }

  function getChatRoot() {
    return document.querySelector("[data-chat-page]");
  }

  function formatInboxTime(value) {
    const date = value ? new Date(value) : null;
    if (!date || Number.isNaN(date.getTime())) {
      return "";
    }

    const now = new Date();
    const deltaMs = now.getTime() - date.getTime();
    const deltaMinutes = Math.floor(deltaMs / 60000);
    const deltaHours = deltaMs / 3600000;
    const deltaDays = deltaMs / 86400000;

    if (deltaMinutes < 1) {
      return "Ahora";
    }

    if (deltaHours < 1) {
      return `Hace ${deltaMinutes} min`;
    }

    if (deltaDays < 1) {
      return formatLocalTime(date, { hour: "2-digit", minute: "2-digit" });
    }

    return formatLocalTime(date, { day: "2-digit", month: "2-digit" });
  }

  function formatMessageTime(value) {
    const date = value ? new Date(value) : null;
    if (!date || Number.isNaN(date.getTime())) {
      return "";
    }

    return formatLocalTime(date, {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    });
  }

  function formatLocalTime(date, options) {
    return date.toLocaleString("es-AR", options).replace(",", "");
  }

  function escapeHtml(value) {
    return String(value || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll("\"", "&quot;")
      .replaceAll("'", "&#39;");
  }

  function escapeAttribute(value) {
    return escapeHtml(value);
  }
})();

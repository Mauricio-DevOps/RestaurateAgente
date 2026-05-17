function formatCurrency(cents) {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(cents / 100);
}

function setupColorPickers() {
  document.querySelectorAll(".color-picker-row").forEach((row) => {
    const hexInput = row.querySelector(".color-hex");
    const colorInput = row.querySelector(".color-picker");
    if (!hexInput || !colorInput) return;

    function normalizeHex(value) {
      const trimmed = value.trim();
      const normalized = trimmed.startsWith("#") ? trimmed : `#${trimmed}`;
      return /^#[0-9a-fA-F]{6}$/.test(normalized) ? normalized.toUpperCase() : null;
    }

    const initialColor = normalizeHex(hexInput.value);
    if (initialColor) {
      hexInput.value = initialColor;
      colorInput.value = initialColor;
    }

    colorInput.addEventListener("input", () => {
      hexInput.value = colorInput.value.toUpperCase();
    });

    hexInput.addEventListener("input", () => {
      const color = normalizeHex(hexInput.value);
      if (color) {
        colorInput.value = color;
      }
    });

    hexInput.addEventListener("blur", () => {
      const color = normalizeHex(hexInput.value);
      if (color) {
        hexInput.value = color;
        colorInput.value = color;
      }
    });
  });
}

function setupPromotionForms() {
  document.querySelectorAll(".promotion-fields").forEach((root) => {
    const toggle = root.querySelector(".promotion-toggle");
    const duration = root.querySelector(".promotion-duration");
    const customRange = root.querySelector(".promotion-custom-range");
    const customInputs = customRange ? customRange.querySelectorAll("input") : [];
    if (!toggle || !duration || !customRange) return;

    function sync() {
      const enabled = toggle.checked;
      const showCustomRange = enabled && duration.value === "CUSTOM";
      root.classList.toggle("is-disabled", !enabled);
      duration.disabled = !enabled;
      customRange.hidden = !showCustomRange;
      customInputs.forEach((input) => {
        input.disabled = !showCustomRange;
        input.required = showCustomRange;
      });
    }

    toggle.addEventListener("change", sync);
    duration.addEventListener("change", sync);
    sync();
  });
}

function setupMenuQrGenerator() {
  const root = document.getElementById("menu-qr-generator");
  if (!root) return;

  const tableSelect = document.getElementById("menu-qr-table");
  const linkInput = document.getElementById("menu-qr-link");
  const generateButton = document.getElementById("menu-qr-generate");
  const downloadLink = document.getElementById("menu-qr-download");
  const preview = document.getElementById("menu-qr-preview");
  const image = document.getElementById("menu-qr-image");

  function buildMenuUrl() {
    const url = new URL(root.dataset.publicMenuPath, window.location.origin);
    const tableNumber = tableSelect?.value || "";
    if (tableNumber) {
      url.searchParams.set("mesa", tableNumber);
    }
    return url.toString();
  }

  function buildQrUrl() {
    const url = new URL(root.dataset.qrUrl, window.location.origin);
    const tableNumber = tableSelect?.value || "";
    if (tableNumber) {
      url.searchParams.set("mesa", tableNumber);
    }
    return url.toString();
  }

  function syncLink() {
    if (linkInput) {
      linkInput.value = buildMenuUrl();
    }
    if (downloadLink) {
      downloadLink.hidden = true;
    }
    if (preview) {
      preview.hidden = true;
    }
  }

  tableSelect?.addEventListener("change", syncLink);
  generateButton?.addEventListener("click", () => {
    const qrUrl = buildQrUrl();
    const tableNumber = tableSelect?.value || "";
    if (image) {
      image.src = `${qrUrl}${qrUrl.includes("?") ? "&" : "?"}_=${Date.now()}`;
    }
    if (downloadLink) {
      const safeTableNumber = tableNumber.replace(/[^\w.-]+/g, "-") || "mesa";
      downloadLink.href = qrUrl;
      downloadLink.download = tableNumber ? `cardapio-mesa-${safeTableNumber}.svg` : "cardapio-delivery.svg";
      downloadLink.hidden = false;
    }
    if (preview) {
      preview.hidden = false;
    }
  });
  syncLink();
}

function setupPublicMenu() {
  const root = document.getElementById("public-menu");
  if (!root) return;

  const restaurantId = root.dataset.restaurantId;
  const orderMode = root.dataset.orderMode || "delivery";
  const isDelivery = orderMode === "delivery";
  const currentTableId = root.dataset.tableId || "";
  const hasInvalidTable = root.dataset.invalidTable === "true";
  const tableSelect = document.getElementById("public-table-select");
  const feedback = document.getElementById("public-feedback");
  const cartLines = document.getElementById("cart-lines");
  const cartSubtotal = document.getElementById("cart-subtotal");
  const cartTotal = document.getElementById("cart-total");
  const cartCount = document.getElementById("cart-count");
  const couponCodeInput = document.getElementById("coupon-code");
  const applyCouponButton = document.getElementById("apply-coupon");
  const removeCouponButton = document.getElementById("remove-coupon");
  const couponFeedback = document.getElementById("coupon-feedback");
  const cartDiscountRow = document.getElementById("cart-discount-row");
  const cartDiscountLabel = document.getElementById("cart-discount-label");
  const cartDiscount = document.getElementById("cart-discount");
  const orderFeedbackPanel = document.getElementById("order-feedback-panel");
  const orderFeedbackForm = document.getElementById("order-feedback-form");
  const deliveryNameInput = document.getElementById("delivery-name");
  const deliveryPhoneInput = document.getElementById("delivery-phone");
  const deliveryAddressInput = document.getElementById("delivery-address");
  const cart = [];
  let appliedCoupon = null;
  const storageKey = `restaurant-table-${restaurantId}`;
  let selectionSource = "manual";
  const confirmedTablesWithoutOpenTab = new Set();

  const savedTable = tableSelect ? window.localStorage.getItem(storageKey) : "";
  if (tableSelect) {
    if (savedTable && tableSelect.querySelector(`option[value="${savedTable}"]`)) {
      tableSelect.value = savedTable;
      selectionSource = "saved";
      validateSavedTable(savedTable);
    } else if (savedTable) {
      window.localStorage.removeItem(storageKey);
    }
  }

  function setFeedback(message, isError = false) {
    feedback.textContent = message;
    feedback.style.borderColor = isError ? "#f3b5b5" : "#b7d9c0";
    feedback.style.background = isError ? "#fff5f5" : "#f0fff4";
  }

  function setCouponFeedback(message, isError = false) {
    if (!couponFeedback) return;
    couponFeedback.textContent = message;
    couponFeedback.classList.toggle("is-error", isError);
  }

  function clearAppliedCoupon(message = "") {
    appliedCoupon = null;
    if (removeCouponButton) {
      removeCouponButton.hidden = true;
    }
    if (couponCodeInput) {
      couponCodeInput.disabled = false;
    }
    setCouponFeedback(message);
  }

  function showOrderFeedback(orderId) {
    if (!orderFeedbackPanel || !orderFeedbackForm || !orderId) return;
    const orderIdInput = orderFeedbackForm.querySelector('input[name="orderId"]');
    orderFeedbackForm.reset();
    if (orderIdInput) {
      orderIdInput.value = orderId;
    }
    orderFeedbackPanel.hidden = false;
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, (character) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      "\"": "&quot;",
      "'": "&#039;",
    })[character]);
  }

  async function fetchTableSession(tableId) {
    const response = await fetch(`/api/public/restaurants/${restaurantId}/table-session?tableId=${encodeURIComponent(tableId)}`, {
      cache: "no-store",
    });
    if (!response.ok) {
      throw new Error("Não foi possível validar a mesa.");
    }
    return response.json();
  }

  async function validateSavedTable(tableId) {
    try {
      const session = await fetchTableSession(tableId);
      if (!session.valid && tableSelect.value === tableId) {
        window.localStorage.removeItem(storageKey);
        tableSelect.value = "";
        selectionSource = "manual";
        confirmedTablesWithoutOpenTab.delete(tableId);
      }
    } catch {
      if (tableSelect.value === tableId) {
        setFeedback("Não foi possível validar a mesa salva. Confirme a mesa antes de enviar.", true);
      }
    }
  }

  function ensureDeliveryReady() {
    if (!isDelivery) return true;
    const customerName = deliveryNameInput?.value.trim() || "";
    const customerPhone = deliveryPhoneInput?.value.trim() || "";
    const deliveryAddress = deliveryAddressInput?.value.trim() || "";
    if (!customerName) {
      setFeedback("Informe seu nome.", true);
      deliveryNameInput?.focus();
      return false;
    }
    if (!customerPhone) {
      setFeedback("Informe seu telefone.", true);
      deliveryPhoneInput?.focus();
      return false;
    }
    if (!deliveryAddress) {
      setFeedback("Informe o endereco de entrega.", true);
      deliveryAddressInput?.focus();
      return false;
    }
    return true;
  }

  async function ensureTableReady() {
    if (hasInvalidTable) {
      setFeedback("Mesa nao encontrada. Use o QR Code correto ou abra o cardapio sem mesa para delivery.", true);
      return false;
    }
    if (isDelivery) {
      return ensureDeliveryReady();
    }
    if (!tableSelect) {
      if (currentTableId) return true;
      setFeedback("Mesa nao encontrada.", true);
      return false;
    }
    if (!tableSelect.value) {
      setFeedback("Selecione uma mesa.", true);
      return false;
    }

    const tableId = tableSelect.value;
    let session;
    try {
      session = await fetchTableSession(tableId);
    } catch (error) {
      setFeedback(error.message, true);
      return false;
    }

    if (!session.valid) {
      window.localStorage.removeItem(storageKey);
      tableSelect.value = "";
      selectionSource = "manual";
      confirmedTablesWithoutOpenTab.delete(tableId);
      setFeedback("Mesa não encontrada. Selecione a mesa atual.", true);
      tableSelect.focus();
      return false;
    }

    if (session.hasOpenTab) {
      confirmedTablesWithoutOpenTab.delete(tableId);
      return true;
    }

    if (selectionSource === "saved" && !confirmedTablesWithoutOpenTab.has(tableId)) {
      const tableLabel = session.tableNumber
        ? `mesa ${session.tableNumber}`
        : (tableSelect.selectedOptions[0]?.textContent || "mesa selecionada").trim().toLowerCase();
      if (!window.confirm(`Você ainda está na ${tableLabel}?`)) {
        window.localStorage.removeItem(storageKey);
        tableSelect.value = "";
        selectionSource = "manual";
        confirmedTablesWithoutOpenTab.delete(tableId);
        setFeedback("Selecione a mesa atual para continuar.", true);
        tableSelect.focus();
        return false;
      }
      confirmedTablesWithoutOpenTab.add(tableId);
    }

    return true;
  }

  function renderCart() {
    cartLines.innerHTML = "";
    let subtotal = 0;
    let quantity = 0;
    if (!cart.length) {
      cartLines.innerHTML = `
        <div class="cart-empty">
          <strong>Sua sacola está vazia</strong>
          <p>Adicione um item do cardápio para começar o pedido.</p>
        </div>`;
    }
    for (const item of cart) {
      subtotal += item.priceCents * item.quantity;
      quantity += item.quantity;
      const line = document.createElement("article");
      line.className = "cart-line";
      line.innerHTML = `
        <div class="cart-line-copy">
          <strong>${escapeHtml(item.name)}</strong>
          <span>${item.quantity} x ${formatCurrency(item.priceCents)}</span>
        </div>
        <div class="cart-line-controls" aria-label="Controles de ${escapeHtml(item.name)}">
          <button class="button" type="button" data-action="decrease" aria-label="Diminuir quantidade">-</button>
          <strong>${item.quantity}</strong>
          <button class="button" type="button" data-action="increase" aria-label="Aumentar quantidade">+</button>
          <button class="button" type="button" data-action="remove">Remover</button>
        </div>`;
      line.querySelector('[data-action="decrease"]').addEventListener("click", () => {
        if (appliedCoupon) clearAppliedCoupon("Cupom removido. Aplique novamente apos alterar a sacola.");
        item.quantity -= 1;
        if (item.quantity <= 0) {
          const index = cart.indexOf(item);
          if (index >= 0) cart.splice(index, 1);
        }
        renderCart();
      });
      line.querySelector('[data-action="increase"]').addEventListener("click", () => {
        if (appliedCoupon) clearAppliedCoupon("Cupom removido. Aplique novamente apos alterar a sacola.");
        item.quantity += 1;
        renderCart();
      });
      line.querySelector('[data-action="remove"]').addEventListener("click", () => {
        if (appliedCoupon) clearAppliedCoupon("Cupom removido. Aplique novamente apos alterar a sacola.");
        const index = cart.indexOf(item);
        if (index >= 0) cart.splice(index, 1);
        renderCart();
      });
      cartLines.appendChild(line);
    }
    const discount = appliedCoupon ? Number(appliedCoupon.discountCents || 0) : 0;
    const total = Math.max(0, subtotal - discount);
    if (cartSubtotal) {
      cartSubtotal.textContent = formatCurrency(subtotal);
    }
    cartTotal.textContent = formatCurrency(total);
    if (cartDiscountRow && cartDiscount && cartDiscountLabel) {
      const hasDiscount = discount > 0 && appliedCoupon;
      cartDiscountRow.hidden = !hasDiscount;
      if (hasDiscount) {
        cartDiscountLabel.textContent = `Cupom ${appliedCoupon.couponCode}`;
        cartDiscount.textContent = `-${formatCurrency(discount)}`;
      }
    }
    if (removeCouponButton) {
      removeCouponButton.hidden = !appliedCoupon;
    }
    if (cartCount) {
      cartCount.textContent = `${quantity} ${quantity === 1 ? "item" : "itens"}`;
    }
  }

  root.querySelectorAll(".add-cart").forEach((button) => {
    button.addEventListener("click", () => {
      if (appliedCoupon) clearAppliedCoupon("Cupom removido. Aplique novamente apos alterar a sacola.");
      const itemId = button.dataset.itemId;
      const current = cart.find((item) => item.itemId === itemId);
      if (current) {
        current.quantity += 1;
      } else {
        cart.push({
          itemId,
          name: button.dataset.name,
          priceCents: Number(button.dataset.priceCents),
          quantity: 1,
        });
      }
      renderCart();
      setFeedback(`${button.dataset.name} adicionado à sacola.`);
    });
  });

  if (tableSelect) {
    tableSelect.addEventListener("change", () => {
      selectionSource = "manual";
      confirmedTablesWithoutOpenTab.clear();
      if (tableSelect.value) {
        window.localStorage.setItem(storageKey, tableSelect.value);
      } else {
        window.localStorage.removeItem(storageKey);
      }
    });
  }

  async function postJson(url, body) {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.error || "Não foi possível concluir a ação.");
    }
    return payload;
  }

  if (applyCouponButton) {
    applyCouponButton.addEventListener("click", async () => {
      if (cart.length === 0) return setCouponFeedback("Adicione itens antes de aplicar um cupom.", true);
      const couponCode = couponCodeInput?.value.trim() || "";
      if (!couponCode) return setCouponFeedback("Digite o cupom.", true);

      applyCouponButton.disabled = true;
      try {
        const payload = await postJson(`/api/public/restaurants/${restaurantId}/coupon/validate`, {
          restaurantId,
          couponCode,
          items: cart.map((item) => ({ menuItemId: item.itemId, quantity: item.quantity })),
        });
        appliedCoupon = payload.data;
        if (couponCodeInput) {
          couponCodeInput.value = appliedCoupon.couponCode;
          couponCodeInput.disabled = true;
        }
        setCouponFeedback(`${payload.message || "Cupom aplicado."} Desconto: ${formatCurrency(appliedCoupon.discountCents)}.`);
        renderCart();
      } catch (error) {
        appliedCoupon = null;
        setCouponFeedback(error.message, true);
        renderCart();
      } finally {
        applyCouponButton.disabled = false;
      }
    });
  }

  if (removeCouponButton) {
    removeCouponButton.addEventListener("click", () => {
      clearAppliedCoupon("Cupom removido.");
      renderCart();
    });
  }

  document.getElementById("submit-order").addEventListener("click", async () => {
    if (!(await ensureTableReady())) return;
    if (cart.length === 0) return setFeedback("Adicione ao menos um item.", true);
    try {
      const orderPayload = {
        restaurantId,
        tableId: isDelivery ? null : currentTableId,
        couponCode: appliedCoupon?.couponCode || "",
        items: cart.map((item) => ({ menuItemId: item.itemId, quantity: item.quantity })),
      };
      if (isDelivery) {
        orderPayload.customerName = deliveryNameInput?.value.trim() || "";
        orderPayload.customerPhone = deliveryPhoneInput?.value.trim() || "";
        orderPayload.deliveryAddress = deliveryAddressInput?.value.trim() || "";
      }
      const payload = await postJson(`/api/public/restaurants/${restaurantId}/order`, orderPayload);
      cart.splice(0, cart.length);
      clearAppliedCoupon();
      renderCart();
      if (tableSelect) {
        window.localStorage.setItem(storageKey, tableSelect.value);
        selectionSource = "saved";
        confirmedTablesWithoutOpenTab.delete(tableSelect.value);
      }
      setFeedback(payload.message || "Pedido enviado.");
      showOrderFeedback(payload.data?.orderId);
    } catch (error) {
      setFeedback(error.message, true);
    }
  });

  if (orderFeedbackForm) {
    orderFeedbackForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      const formData = new FormData(orderFeedbackForm);
      try {
        const payload = await postJson(`/api/public/restaurants/${restaurantId}/feedback`, {
          orderId: formData.get("orderId"),
          rating: Number(formData.get("rating")),
          comment: formData.get("comment")?.toString() ?? "",
        });
        if (orderFeedbackPanel) {
          orderFeedbackPanel.hidden = true;
        }
        setFeedback(payload.message || "Obrigado pelo feedback.");
      } catch (error) {
        setFeedback(error.message, true);
      }
    });
  }

  root.querySelectorAll(".service-request").forEach((button) => {
    button.addEventListener("click", async () => {
      if (!(await ensureTableReady())) return;
      try {
        const payload = await postJson(`/api/public/restaurants/${restaurantId}/service-request`, {
          restaurantId,
          tableId: currentTableId,
          type: button.dataset.requestType,
        });
        if (tableSelect) {
          window.localStorage.setItem(storageKey, tableSelect.value);
          selectionSource = "saved";
        }
        setFeedback(payload.message || "Solicitação enviada.");
      } catch (error) {
        setFeedback(error.message, true);
      }
    });
  });

  renderCart();
}

function setupFeedbackDashboard() {
  const root = document.querySelector("[data-feedback-dashboard]");
  if (!root) return;

  const buttons = root.querySelectorAll("[data-feedback-filter]");
  const comments = root.querySelectorAll(".feedback-comment");
  const visibleCount = root.querySelector("[data-feedback-visible-count]");
  const emptyMessage = root.querySelector(".feedback-empty-filter");

  function applyFilter(value) {
    let count = 0;
    comments.forEach((comment) => {
      const isVisible = value === "all" || comment.dataset.feedbackRating === value;
      comment.hidden = !isVisible;
      if (isVisible) {
        count += 1;
      }
    });

    if (visibleCount) {
      visibleCount.textContent = String(count);
    }
    if (emptyMessage) {
      emptyMessage.hidden = count > 0;
    }
    buttons.forEach((button) => {
      button.classList.toggle("is-active", button.dataset.feedbackFilter === value);
    });
  }

  buttons.forEach((button) => {
    button.addEventListener("click", () => applyFilter(button.dataset.feedbackFilter || "all"));
  });
}

function setupWaiterQueue() {
  const root = document.getElementById("waiter-queue");
  if (!root) return;
  let selectedQueueTab = "active";
  let timerStarted = false;

  async function postJson(url, body) {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const payload = await response.json();
    if (!response.ok) throw new Error(payload.error || "Falha ao atualizar.");
    return payload;
  }

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, (character) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      "\"": "&quot;",
      "'": "&#039;",
    })[character]);
  }

  function statusLabel(status) {
    if (status === "EM_ATENDIMENTO") return "Atendendo";
    if (status === "RESOLVIDO") return "Resolvido";
    return "Pendente";
  }

  function formatDuration(seconds) {
    const safeSeconds = Math.max(0, Number(seconds) || 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);
    const remainingSeconds = safeSeconds % 60;
    if (hours > 0) return `${hours}h ${String(minutes).padStart(2, "0")}min`;
    if (minutes > 0) return `${minutes}min ${String(remainingSeconds).padStart(2, "0")}s`;
    return `${remainingSeconds}s`;
  }

  function elapsedSeconds(start, end) {
    if (!start) return 0;
    const startMs = new Date(start).getTime();
    const endMs = end ? new Date(end).getTime() : Date.now();
    if (!Number.isFinite(startMs) || !Number.isFinite(endMs)) return 0;
    return Math.max(0, Math.floor((endMs - startMs) / 1000));
  }

  function durationSpan(label, start, end) {
    return `
      <span class="sla-pill">
        ${label}
        <strong data-duration-start="${escapeHtml(start)}" data-duration-end="${escapeHtml(end)}">--</strong>
      </span>`;
  }

  function renderOrderSla(event) {
    if (event.eventKind !== "ORDER") return "";
    const currentSlaLabel = event.currentSlaMinutes ? `SLA ${event.currentSlaMinutes} min` : "Sem SLA";
    return `
      <div class="sla-strip">
        <span class="sla-pill current-sla ${event.isOverSla ? "over-sla" : ""}" data-sla-current-pill>
          ${escapeHtml(event.currentStageLabel || "Etapa")}
          <strong data-sla-current>--</strong>
          <small>${currentSlaLabel}</small>
        </span>
        ${durationSpan("Pendente", event.createdAt, event.acknowledgedAt || "")}
        ${durationSpan("Atendendo", event.acknowledgedAt || "", event.resolvedAt || "")}
      </div>`;
  }

  function updateQueueTimers() {
    root.querySelectorAll(".queue-event[data-event-kind=\"ORDER\"]").forEach((card) => {
      const currentElapsed = elapsedSeconds(card.dataset.currentStageStartedAt, card.dataset.currentStageEndedAt);
      const currentCounter = card.querySelector("[data-sla-current]");
      if (currentCounter) currentCounter.textContent = formatDuration(currentElapsed);

      card.querySelectorAll("[data-duration-start]").forEach((counter) => {
        const startedAt = counter.dataset.durationStart;
        counter.textContent = startedAt
          ? formatDuration(elapsedSeconds(startedAt, counter.dataset.durationEnd))
          : "Nao iniciou";
      });

      const currentLimit = Number(card.dataset.currentSlaMinutes || 0);
      const isClosedStage = Boolean(card.dataset.currentStageEndedAt);
      const isOverSla = !isClosedStage && currentLimit > 0 && currentElapsed > currentLimit * 60;
      card.classList.toggle("is-over-sla", isOverSla);
      const currentPill = card.querySelector("[data-sla-current-pill]");
      if (currentPill) currentPill.classList.toggle("over-sla", isOverSla);
    });
  }

  function render(view) {
    const activeEvents = view.queue.filter((event) => event.status !== "RESOLVIDO");
    const completedEvents = view.queue.filter((event) => event.status === "RESOLVIDO");
    const visibleEvents = selectedQueueTab === "completed" ? completedEvents : activeEvents;
    root.innerHTML = `
      <h2>Fila</h2>
      <div class="queue-tabs" role="tablist" aria-label="Filtro da fila">
        <button class="queue-tab ${selectedQueueTab === "active" ? "is-active" : ""}"
                type="button"
                role="tab"
                aria-selected="${selectedQueueTab === "active"}"
                data-queue-tab="active">
          Ativos <span>${activeEvents.length}</span>
        </button>
        <button class="queue-tab ${selectedQueueTab === "completed" ? "is-active" : ""}"
                type="button"
                role="tab"
                aria-selected="${selectedQueueTab === "completed"}"
                data-queue-tab="completed">
          Concluidos <span>${completedEvents.length}</span>
        </button>
      </div>
      <div class="table-list"></div>`;
    root.querySelectorAll("button[data-queue-tab]").forEach((button) => {
      button.addEventListener("click", () => {
        selectedQueueTab = button.dataset.queueTab;
        render(view);
      });
    });

    const list = root.querySelector(".table-list");
    if (!visibleEvents.length) {
      list.innerHTML = selectedQueueTab === "completed"
        ? "<p class=\"muted\">Nenhum evento concluido no momento.</p>"
        : "<p class=\"muted\">Nenhum evento ativo no momento.</p>";
      return;
    }

    for (const event of visibleEvents) {
      const card = document.createElement("article");
      card.className = `row-card queue-event ${event.isOverSla ? "is-over-sla" : ""}`;
      card.dataset.eventKind = event.eventKind;
      card.dataset.currentSlaMinutes = event.currentSlaMinutes || "";
      card.dataset.currentStageStartedAt = event.currentStageStartedAt || "";
      card.dataset.currentStageEndedAt = event.currentStageEndedAt || "";
      const items = event.items?.length
        ? `<ul>${event.items.map((item) => `<li>${item.quantity}x ${escapeHtml(item.name)} (${escapeHtml(item.lineTotalLabel)})</li>`).join("")}</ul>`
        : "";
      const couponSummary = event.discountCents > 0 && event.couponSummary
        ? `<p class="queue-coupon">${escapeHtml(event.couponSummary)}</p>`
        : "";
      card.innerHTML = `
        <div>
          <strong>${escapeHtml(event.title)}</strong>
          <p>${escapeHtml(event.summary)}</p>
          ${couponSummary}
          <p>${event.status.replaceAll("_", " ")} · ${event.ownershipLabel}</p>
          ${renderOrderSla(event)}
          ${items}
        </div>
        <div class="row-actions">
          ${event.status === "PENDENTE" ? `<button class="button" data-next="EM_ATENDIMENTO">Atender</button>` : ""}
          ${event.status !== "RESOLVIDO" ? `<button class="button primary" data-next="RESOLVIDO">Resolver</button>` : ""}
        </div>`;
      card.querySelectorAll("button[data-next]").forEach((button) => {
        button.addEventListener("click", async () => {
          try {
            await postJson(root.dataset.statusUrl, {
              eventKind: event.eventKind,
              eventId: event.id,
              nextStatus: button.dataset.next,
            });
            await refresh();
          } catch (error) {
            alert(error.message);
          }
        });
      });
      list.appendChild(card);
    }
    updateQueueTimers();
  }

  async function refresh() {
    const response = await fetch(root.dataset.refreshUrl, { cache: "no-store" });
    if (!response.ok) {
      root.innerHTML = "<p class=\"validation\">Não foi possível carregar a fila.</p>";
      return;
    }
    render(await response.json());
  }

  refresh();
  if (!timerStarted) {
    window.setInterval(updateQueueTimers, 1000);
    timerStarted = true;
  }
  window.setInterval(refresh, 5000);
}

function setupDeliveryOrders() {
  const root = document.getElementById("delivery-orders");
  if (!root) return;
  let selectedTab = "active";

  async function postJson(url, body) {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const payload = await response.json();
    if (!response.ok) throw new Error(payload.error || "Falha ao atualizar.");
    return payload;
  }

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, (character) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      "\"": "&quot;",
      "'": "&#039;",
    })[character]);
  }

  function render(orders) {
    const activeOrders = orders.filter((order) => order.status !== "RESOLVIDO");
    const completedOrders = orders.filter((order) => order.status === "RESOLVIDO");
    const visibleOrders = selectedTab === "completed" ? completedOrders : activeOrders;
    root.innerHTML = `
      <h2>Pedidos delivery</h2>
      <div class="queue-tabs" role="tablist" aria-label="Filtro de delivery">
        <button class="queue-tab ${selectedTab === "active" ? "is-active" : ""}"
                type="button"
                role="tab"
                aria-selected="${selectedTab === "active"}"
                data-delivery-tab="active">
          Ativos <span>${activeOrders.length}</span>
        </button>
        <button class="queue-tab ${selectedTab === "completed" ? "is-active" : ""}"
                type="button"
                role="tab"
                aria-selected="${selectedTab === "completed"}"
                data-delivery-tab="completed">
          Concluidos <span>${completedOrders.length}</span>
        </button>
      </div>
      <div class="table-list"></div>`;
    root.querySelectorAll("button[data-delivery-tab]").forEach((button) => {
      button.addEventListener("click", () => {
        selectedTab = button.dataset.deliveryTab;
        render(orders);
      });
    });

    const list = root.querySelector(".table-list");
    if (!visibleOrders.length) {
      list.innerHTML = selectedTab === "completed"
        ? "<p class=\"muted\">Nenhum pedido delivery concluido.</p>"
        : "<p class=\"muted\">Nenhum pedido delivery ativo.</p>";
      return;
    }

    for (const order of visibleOrders) {
      const card = document.createElement("article");
      card.className = "row-card delivery-order";
      const items = order.items?.length
        ? `<ul>${order.items.map((item) => `<li>${item.quantity}x ${escapeHtml(item.name)} (${escapeHtml(item.lineTotalLabel)})</li>`).join("")}</ul>`
        : "";
      const couponSummary = order.discountCents > 0 && order.couponSummary
        ? `<p class="queue-coupon">${escapeHtml(order.couponSummary)}</p>`
        : "";
      card.innerHTML = `
        <div>
          <strong>Pedido delivery - ${escapeHtml(order.customerName)}</strong>
          <p>${escapeHtml(order.summary)}</p>
          ${couponSummary}
          <p>${escapeHtml(order.customerPhone)} · ${escapeHtml(order.deliveryAddress)}</p>
          <p>${order.status.replaceAll("_", " ")}</p>
          ${items}
        </div>
        <div class="row-actions">
          ${order.status === "PENDENTE" ? `<button class="button" data-next="EM_ATENDIMENTO">Atender</button>` : ""}
          ${order.status !== "RESOLVIDO" ? `<button class="button primary" data-next="RESOLVIDO">Resolver</button>` : ""}
        </div>`;
      card.querySelectorAll("button[data-next]").forEach((button) => {
        button.addEventListener("click", async () => {
          try {
            await postJson(root.dataset.statusUrl, {
              orderId: order.id,
              nextStatus: button.dataset.next,
            });
            await refresh();
          } catch (error) {
            alert(error.message);
          }
        });
      });
      list.appendChild(card);
    }
  }

  async function refresh() {
    const response = await fetch(root.dataset.refreshUrl, { cache: "no-store" });
    if (!response.ok) {
      root.innerHTML = "<p class=\"validation\">Nao foi possivel carregar os pedidos delivery.</p>";
      return;
    }
    const payload = await response.json();
    render(payload.orders || []);
  }

  refresh();
  window.setInterval(refresh, 5000);
}

document.addEventListener("DOMContentLoaded", () => {
  setupColorPickers();
  setupPromotionForms();
  setupMenuQrGenerator();
  setupPublicMenu();
  setupFeedbackDashboard();
  setupWaiterQueue();
  setupDeliveryOrders();
});

"use strict";


const messageInput = document.getElementById("message");
const sendBtn = document.getElementById("sendBtn");
const resultBox = document.getElementById("resultBox");
const loginBtn = document.getElementById("loginBtn");
const assistantAddress =
    document.getElementById("assistantAddress");

let currentProducts = [];
let cart = [];

// ======================================================
// HELPERS
// ======================================================

function escapeHtml(value) {
    if (value === null || value === undefined) {
        return "";
    }

    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function formatPrice(value) {
    const n = Number(value);

    if (!Number.isFinite(n)) {
        return "0.00 грн";
    }

    return n.toFixed(2) + " грн";
}

// ======================================================
// IMAGE
// ======================================================

function getProductImage(product) {
    if (!product) {
        return "";
    }

    let image =
        product.image ??
        product.Image ??
        product.imageUrl ??
        product.ImageUrl ??
        product.photoUrl ??
        product.PhotoUrl ??
        "";

    if (typeof image !== "string") {
        return "";
    }

    image = image.trim();

    // [Текст](URL)
    const markdown = image.match(
        /\[([^\]]+)\]\((https?:\/\/[^)]+)\)/
    );

    if (markdown) {
        image = markdown[2];
    }

    image = image
        .replace(/^["']+/, "")
        .replace(/["']+$/, "")
        .trim();

    if (
        image.startsWith("https://") ||
        image.startsWith("http://")
    ) {
        return image;
    }

    return "";
}

// ======================================================
// PRODUCT CARD
// ======================================================

function createProductCard(product) {
    const name =
        product?.name ??
        product?.Name ??
        "Товар";

    const price =
        product?.price ??
        product?.Price ??
        0;

    const oldPrice =
        product?.oldPrice ??
        product?.OldPrice ??
        null;

    const stock =
        product?.stock ??
        product?.Stock ??
        "";

    const displayRatio =
        product?.displayRatio ??
        product?.DisplayRatio ??
        "";

    const image = getProductImage(product);

    let imageHtml = "";

    if (image) {
        imageHtml = `
            <img
                class="product-image"
                src="${escapeHtml(image)}"
                alt="${escapeHtml(name)}"
                loading="lazy"
                onerror="this.style.display='none';"
            >
        `;
    } else {
        imageHtml = `
            <div class="product-image-placeholder">
                Немає фото
            </div>
        `;
    }

    let oldPriceHtml = "";

    if (
        oldPrice !== null &&
        oldPrice !== undefined &&
        Number(oldPrice) > Number(price)
    ) {
        oldPriceHtml = `
            <span class="product-old-price">
                ${formatPrice(oldPrice)}
            </span>
        `;
    }

    return `
        <article class="product-card">

            <div class="product-image-wrapper">
                ${imageHtml}
            </div>

            <div class="product-info">

                <h3 class="product-name">
                    ${escapeHtml(name)}
                </h3>

                <div class="product-price">
                    ${oldPriceHtml}

                    <span class="current-price">
                        ${formatPrice(price)}
                    </span>
                </div>

                ${displayRatio
            ? `
                            <div class="product-meta">
                                ${escapeHtml(displayRatio)}
                            </div>
                          `
            : ""
        }

                ${stock !== ""
            ? `
                            <div class="product-stock">
                                В наявності: ${escapeHtml(stock)}
                            </div>
                          `
            : ""
        }

                <button
                    type="button"
                    class="btn btn-info"
                    data-product-name="${escapeHtml(name)}">
                    🛒 Додати в кошик
                </button>

            </div>

        </article>
    `;
}

// ======================================================
// PARSE RESPONSE
// ======================================================

function parseResponse(data) {
    console.log("RAW RESPONSE:", data);

    // --------------------------------------------------
    // 1. JSON прийшов як string
    // --------------------------------------------------

    if (typeof data === "string") {
        try {
            return parseResponse(JSON.parse(data));
        }
        catch {
            return {
                success: true,
                message: data,
                budget: 0,
                total: 0,
                items: [],
                branchId: "",
                deliveryType: "",
                timeslot: null
            };
        }
    }

    // --------------------------------------------------
    // 2. Некоректна відповідь
    // --------------------------------------------------

    if (!data || typeof data !== "object") {
        return {
            success: false,
            message: "Некоректна відповідь сервера.",
            budget: 0,
            total: 0,
            items: [],
            branchId: "",
            deliveryType: "",
            timeslot: null
        };
    }

    // --------------------------------------------------
    // 3. Помилка backend
    // --------------------------------------------------

    if (
        data.success === false ||
        data.Success === false
    ) {
        return {
            success: false,

            message:
                data.message ??
                data.Message ??
                "Сталася помилка.",

            budget: 0,

            total: 0,

            items: [],

            branchId:
                data.branchId ??
                data.BranchId ??
                "",

            deliveryType:
                data.deliveryType ??
                data.DeliveryType ??
                "",

            timeslot:
                data.timeslot ??
                data.Timeslot ??
                null
        };
    }

    // --------------------------------------------------
    // 4. JSON може бути всередині message
    // --------------------------------------------------

    if (
        typeof data.message === "string" &&
        data.message.trim().startsWith("{")
    ) {
        try {
            const messageObject =
                JSON.parse(data.message);

            if (
                messageObject &&
                typeof messageObject === "object"
            ) {
                return parseResponse(messageObject);
            }
        }
        catch {
            // message — звичайний текст
        }
    }

    // --------------------------------------------------
    // 5. Отримуємо товари
    // Підтримуємо старий і новий backend
    // --------------------------------------------------

    const products =
        Array.isArray(data.products)
            ? data.products
            : Array.isArray(data.Products)
                ? data.Products
                : Array.isArray(data.items)
                    ? data.items
                    : Array.isArray(data.Items)
                        ? data.Items
                        : [];

    // --------------------------------------------------
    // 6. Нормалізований результат
    // --------------------------------------------------

    return {
        success: true,

        message:
            data.answer ??
            data.Answer ??
            data.message ??
            data.Message ??
            "",

        budget: Number(
            data.budget ??
            data.Budget ??
            0
        ),

        total: Number(
            data.total ??
            data.Total ??
            0
        ),

        items: products,

        branchId:
            data.branchId ??
            data.BranchId ??
            "",

        deliveryType:
            data.deliveryType ??
            data.DeliveryType ??
            "",

        timeslot:
            data.timeslot ??
            data.Timeslot ??
            null
    };
}

// ======================================================
// RENDER ASSISTANT RESULT
// ======================================================

function renderAssistantResult(data) {
    console.log("RENDER DATA:", data);

    const result = parseResponse(data);

    if (!result) {
        resultBox.innerHTML = `
            <div class="alert alert-danger">
                Некоректна відповідь сервера.
            </div>
        `;

        return;
    }

    if (result.success === false) {
        resultBox.innerHTML = `
            <div class="alert alert-danger">
                ${escapeHtml(result.message)}
            </div>
        `;

        return;
    }

    const items =
        Array.isArray(result.items)
            ? result.items
            : [];

    currentProducts = items;

    const total =
        Number.isFinite(Number(result.total))
            ? Number(result.total)
            : 0;

    const budget =
        Number.isFinite(Number(result.budget))
            ? Number(result.budget)
            : 0;

    const remaining =
        budget - total;

    console.log("ITEMS:", items);
    console.log("TOTAL:", total);
    console.log("BUDGET:", budget);

    // ==================================================
    // PRODUCT CARDS
    // ==================================================

    let cardsHtml = "";

    if (items.length > 0) {
        cardsHtml = items
            .map(product => createProductCard(product))
            .join("");
    }
    else {
        cardsHtml = `
            <div class="alert alert-warning">
                Товари не знайдені.
            </div>
        `;
    }

    // ==================================================
    // DELIVERY
    // ==================================================

    let deliveryHtml = "";

    if (
        result.branchId ||
        result.deliveryType ||
        result.timeslot
    ) {
        let timeText = "";

        if (
            result.timeslot &&
            result.timeslot.start &&
            result.timeslot.end
        ) {
            timeText = `
                <div>
                    <strong>Час доставки:</strong>
                    ${escapeHtml(result.timeslot.start)}
                    –
                    ${escapeHtml(result.timeslot.end)}
                </div>
            `;
        }

        deliveryHtml = `
            <div class="shopping-summary">

                ${result.deliveryType
                ? `
                            <div>
                                <strong>Тип доставки:</strong>
                                ${escapeHtml(result.deliveryType)}
                            </div>
                          `
                : ""
            }

                ${timeText}

            </div>
        `;
    }

    // ==================================================
    // ASSISTANT RESULT
    // ==================================================

    resultBox.innerHTML = `
        <div class="assistant-result">

            <div class="assistant-result-header">

                <h3>
                    Результат
                </h3>

                ${result.message &&
            !result.message.trim().startsWith("{")
            ? `
                            <p class="assistant-message">
                                ${escapeHtml(result.message)}
                            </p>
                          `
            : ""
        }

            </div>

            ${total > 0 || budget > 0
            ? `
                        <div class="shopping-summary">

                            ${total > 0
                ? `
                                        <div>
                                            <strong>Сума:</strong>
                                            ${formatPrice(total)}
                                        </div>
                                      `
                : ""
            }

                            ${budget > 0
                ? `
                                        <div>
                                            <strong>Бюджет:</strong>
                                            ${formatPrice(budget)}
                                        </div>

                                        <div>
                                            <strong>Залишок:</strong>
                                            ${formatPrice(remaining)}
                                        </div>
                                      `
                : ""
            }

                        </div>
                      `
            : ""
        }

            ${deliveryHtml}

            <div class="product-grid">
                ${cardsHtml}
            </div>

        </div>
    `;

    // ==================================================
    // ADD TO CART BUTTONS
    // ==================================================

    resultBox
        .querySelectorAll("[data-product-name]")
        .forEach(button => {

            button.addEventListener(
                "click",
                function () {

                    const productName =
                        this.dataset.productName;

                    addToCartByName(productName);
                }
            );
        });
}

// ======================================================
// ASK ASSISTANT
// ======================================================

async function askAssistant() {
    const message =
        messageInput.value.trim();

    if (!message) {
        return;
    }

    const address =
        assistantAddress
            ? assistantAddress.value.trim()
            : "";

    if (!address) {
        resultBox.innerHTML = `
            <div class="alert alert-warning">
                Вкажіть адресу доставки Silpo.
            </div>
        `;

        if (assistantAddress) {
            assistantAddress.focus();
        }

        return;
    }

    sendBtn.disabled = true;
    sendBtn.textContent = "Шукаю...";

    resultBox.innerHTML = `
        <div class="text-center p-4">

            <div
                class="spinner-border"
                role="status">
            </div>

            <div class="mt-2">
                Визначаю філію та шукаю товари Silpo...
            </div>

        </div>
    `;

    try {
        const response =
            await fetch(
                "/api/assistant",
                {
                    method: "POST",

                    headers: {
                        "Content-Type":
                            "application/json"
                    },

                    body:
                        JSON.stringify({
                            message: message,
                            address: address
                        })
                }
            );

        let data;

        try {
            data = await response.json();
        }
        catch {
            throw new Error(
                `Сервер повернув некоректний JSON. HTTP ${response.status}`
            );
        }

        console.log(
            "ВІДПОВІДЬ API:",
            data
        );

        if (!response.ok) {
            throw new Error(
                data.message ??
                data.Message ??
                `HTTP ${response.status}`
            );
        }

        renderAssistantResult(data);
    }
    catch (error) {
        console.error(
            "Assistant error:",
            error
        );

        resultBox.innerHTML = `
            <div class="alert alert-danger">

                <strong>
                    Помилка:
                </strong>

                ${escapeHtml(error.message)}

            </div>
        `;
    }
    finally {
        sendBtn.disabled = false;
        sendBtn.textContent = "Запитати";
    }
}
// ======================================================
// ADD TO CART
// ======================================================

function addToCartByName(productName) {
    const product =
        currentProducts.find(
            p =>
                (p.name ??
                    p.Name ??
                    "") === productName
        );

    if (!product) {
        console.error(
            "Товар не знайдений:",
            productName
        );

        return;
    }

    const existing =
        cart.find(
            item =>
                item.name === productName
        );

    if (existing) {
        existing.quantity++;
    }
    else {
        cart.push({
            name: productName,

            price: Number(
                product.price ??
                product.Price ??
                0
            ),

            quantity: 1
        });
    }

    renderCart();
}

// ======================================================
// RENDER CART
// ======================================================

function renderCart() {
    const cartBox =
        document.getElementById(
            "cartBox"
        );

    if (!cartBox) {
        console.error(
            "cartBox не знайдений"
        );

        return;
    }

    if (cart.length === 0) {
        cartBox.innerHTML = `
            <h3>
                🛒 Кошик
            </h3>

            <p>
                Кошик порожній
            </p>

            <strong>
                Разом: 0.00 грн
            </strong>
        `;

        return;
    }

    let total = 0;
    let quantityTotal = 0;

    const itemsHtml =
        cart.map(
            (item, index) => {

                const itemTotal =
                    item.price *
                    item.quantity;

                total += itemTotal;

                quantityTotal +=
                    item.quantity;

                return `
                    <div class="cart-item">

                        <div class="cart-item-info">

                            <strong>
                                ${escapeHtml(item.name)}
                            </strong>

                            <div>
                                ${formatPrice(item.price)}
                            </div>

                        </div>

                        <div class="cart-item-controls">

                            <button
                                type="button"
                                class="btn btn-sm btn-outline-secondary"
                                data-cart-minus="${index}">
                                −
                            </button>

                            <span class="cart-quantity">
                                ${item.quantity}
                            </span>

                            <button
                                type="button"
                                class="btn btn-sm btn-outline-secondary"
                                data-cart-plus="${index}">
                                +
                            </button>

                            <strong class="cart-item-total">
                                ${formatPrice(itemTotal)}
                            </strong>

                            <button
                                type="button"
                                class="btn btn-sm btn-outline-danger"
                                data-cart-remove="${index}">
                                ✕
                            </button>

                        </div>

                    </div>
                `;
            }
        ).join("");

    cartBox.innerHTML = `
        <h3>
            🛒 Кошик
            <span>(${quantityTotal})</span>
        </h3>

        <div class="cart-items">
            ${itemsHtml}
        </div>

        <div class="cart-total">

            <strong>
                Разом: ${formatPrice(total)}
            </strong>

        </div>

        <div class="cart-actions">

            <button
                type="button"
                class="btn btn-outline-secondary"
                id="clearCartBtn">
                Очистити
            </button>

            <button
                type="button"
                class="btn btn-primary"
                id="checkoutBtn">
                Оформити замовлення
            </button>

        </div>
    `;

    // ==================================================
    // PLUS
    // ==================================================

    cartBox
        .querySelectorAll("[data-cart-plus]")
        .forEach(button => {

            button.addEventListener(
                "click",
                function () {

                    const index =
                        Number(
                            this.dataset.cartPlus
                        );

                    if (cart[index]) {

                        cart[index].quantity++;

                        renderCart();
                    }
                }
            );
        });

    // ==================================================
    // MINUS
    // ==================================================

    cartBox
        .querySelectorAll("[data-cart-minus]")
        .forEach(button => {

            button.addEventListener(
                "click",
                function () {

                    const index =
                        Number(
                            this.dataset.cartMinus
                        );

                    if (!cart[index]) {
                        return;
                    }

                    cart[index].quantity--;

                    if (
                        cart[index].quantity <= 0
                    ) {
                        cart.splice(index, 1);
                    }

                    renderCart();
                }
            );
        });

    // ==================================================
    // REMOVE
    // ==================================================

    cartBox
        .querySelectorAll("[data-cart-remove]")
        .forEach(button => {

            button.addEventListener(
                "click",
                function () {

                    const index =
                        Number(
                            this.dataset.cartRemove
                        );

                    if (cart[index]) {

                        cart.splice(index, 1);

                        renderCart();
                    }
                }
            );
        });

    // ==================================================
    // CLEAR CART
    // ==================================================

    const clearCartBtn =
        document.getElementById(
            "clearCartBtn"
        );

    if (clearCartBtn) {

        clearCartBtn.addEventListener(
            "click",
            function () {

                cart = [];

                renderCart();
            }
        );
    }

    // ==================================================
    // CHECKOUT
    // ==================================================

    const checkoutBtn =
        document.getElementById(
            "checkoutBtn"
        );

    if (checkoutBtn) {

        checkoutBtn.addEventListener(
            "click",
            function () {

                if (cart.length === 0) {

                    alert(
                        "Кошик порожній."
                    );

                    return;
                }

                showCheckoutForm();
            }
        );
    }
}

// ======================================================
// CHECKOUT FORM
// ======================================================

function showCheckoutForm() {
    const cartBox =
        document.getElementById(
            "cartBox"
        );

    if (!cartBox) {
        return;
    }

    let total = 0;

    cart.forEach(item => {
        total +=
            item.price *
            item.quantity;
    });

    cartBox.innerHTML = `
        <div class="checkout-form">

            <h3>
                📦 Оформлення замовлення
            </h3>

            <div class="mb-3">

                <label class="form-label">
                    Ім'я
                </label>

                <input
                    type="text"
                    class="form-control"
                    id="customerName"
                    placeholder="Ваше ім'я"
                >

            </div>

            <div class="mb-3">

                <label class="form-label">
                    Телефон
                </label>

                <input
                    type="tel"
                    class="form-control"
                    id="customerPhone"
                    placeholder="+380..."
                >

            </div>

            <div class="mb-3">

                <label class="form-label">
                    Адреса доставки
                </label>

                <input
                    type="text"
                    class="form-control"
                    id="deliveryAddress"
                    placeholder="Введіть адресу"
                >

            </div>

            <div class="cart-total mb-3">

                <strong>
                    До сплати:
                    ${formatPrice(total)}
                </strong>

            </div>

            <div class="d-flex gap-2">

                <button
                    type="button"
                    class="btn btn-outline-secondary"
                    id="backToCartBtn">

                    ← Назад

                </button>

                <button
                    type="button"
                    class="btn btn-primary"
                    id="confirmOrderBtn">

                    Підтвердити замовлення

                </button>

            </div>

        </div>
    `;

    // ==========================================
    // BACK TO CART
    // ==========================================

    const backToCartBtn =
        document.getElementById(
            "backToCartBtn"
        );

    if (backToCartBtn) {

        backToCartBtn.addEventListener(
            "click",
            function () {

                renderCart();
            }
        );
    }

    // ==========================================
    // CONFIRM ORDER
    // ==========================================

    const confirmOrderBtn =
        document.getElementById(
            "confirmOrderBtn"
        );

    if (confirmOrderBtn) {

        confirmOrderBtn.addEventListener(
            "click",
            function () {

                const name =
                    document
                        .getElementById(
                            "customerName"
                        )
                        .value
                        .trim();

                const phone =
                    document
                        .getElementById(
                            "customerPhone"
                        )
                        .value
                        .trim();

                const address =
                    document
                        .getElementById(
                            "deliveryAddress"
                        )
                        .value
                        .trim();

                if (!name) {

                    alert(
                        "Введіть ваше ім'я."
                    );

                    return;
                }

                if (!phone) {

                    alert(
                        "Введіть номер телефону."
                    );

                    return;
                }

                if (!address) {

                    alert(
                        "Введіть адресу доставки."
                    );

                    return;
                }

                const order = {
                    customerName: name,
                    phone: phone,
                    address: address,
                    items: cart,
                    total: total
                };

                console.log(
                    "ГОТОВЕ ЗАМОВЛЕННЯ:",
                    order
                );

                showOrderSuccess(order);
            }
        );
    }
}

// ======================================================
// ORDER SUCCESS
// ======================================================

function showOrderSuccess(order) {
    const cartBox =
        document.getElementById(
            "cartBox"
        );

    if (!cartBox) {
        return;
    }

    cartBox.innerHTML = `
        <div class="alert alert-success">

            <h3>
                ✅ Замовлення оформлено!
            </h3>

            <p>
                Дякуємо,
                <strong>
                    ${escapeHtml(
        order.customerName
    )}
                </strong>!
            </p>

            <p>
                Сума замовлення:
                <strong>
                    ${formatPrice(
        order.total
    )}
                </strong>
            </p>

            <p>
                Адреса доставки:
                <strong>
                    ${escapeHtml(
        order.address
    )}
                </strong>
            </p>

            <button
                type="button"
                class="btn btn-primary"
                id="newOrderBtn">

                Нове замовлення

            </button>

        </div>
    `;

    cart = [];

    const newOrderBtn =
        document.getElementById(
            "newOrderBtn"
        );

    if (newOrderBtn) {

        newOrderBtn.addEventListener(
            "click",
            function () {

                renderCart();
            }
        );
    }
}

// ======================================================
// EVENTS
// ======================================================

if (sendBtn) {

    sendBtn.addEventListener(
        "click",
        askAssistant
    );
}

if (messageInput) {

    messageInput.addEventListener(
        "keydown",
        function (event) {

            if (
                event.key === "Enter" &&
                !event.shiftKey
            ) {

                event.preventDefault();

                askAssistant();
            }
        }
    );
}

// ======================================================
// LOGIN
// ======================================================

if (loginBtn) {

    loginBtn.addEventListener(
        "click",
        function () {

            window.location.href =
                "/api/silpo/login";
        }
    );
}
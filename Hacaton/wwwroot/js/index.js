"use strict";

const messageInput = document.getElementById("message");
const sendBtn = document.getElementById("sendBtn");
const resultBox = document.getElementById("resultBox");
const loginBtn = document.getElementById("loginBtn");
let currentProducts = [];

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

    // [URL](URL)
    const markdown = image.match(
        /\[([^\]]+)\]\((https?:\/\/[^)]+)\)/
    );

    if (markdown) {
        image = markdown[2];
    }

    // прибираємо зайві лапки
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


    let imageHtml;

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

           <div class="product-image-wrapper">${imageHtml}</div>

           <div class="product-info"><h3 class="product-name">${escapeHtml(name)}</h3>

           <div class="product-price">${oldPriceHtml}

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
            
           
            <button type="button"  class="btn btn-info" data-product-name="${escapeHtml(name)}">🛒 Додати в кошик</button>
           

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

            const parsed = JSON.parse(data);

            return parseResponse(parsed);

        }
        catch {

            return {
                success: true,
                message: data,
                budget: 0,
                total: 0,
                items: []
            };
        }
    }


    if (!data || typeof data !== "object") {

        return {
            success: false,
            message: "Некоректна відповідь сервера.",
            budget: 0,
            total: 0,
            items: []
        };
    }


    // --------------------------------------------------
    // 2. Якщо JSON знаходиться всередині message
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
                Array.isArray(messageObject.items)
            ) {

                console.log(
                    "JSON знайдений всередині message"
                );

                return messageObject;
            }

        }
        catch {
            // це просто звичайний текст message
        }
    }


    // --------------------------------------------------
    // 3. Нормальний backend response
    // --------------------------------------------------

    return {

        success:
            data.success !== false &&
            data.Success !== false,

        message:
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

        items:
            Array.isArray(data.items)
                ? data.items
                : Array.isArray(data.Items)
                    ? data.Items
                    : []
    };
}


// ======================================================
// RENDER
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


    const items = Array.isArray(result.items)
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
    // CARDS
    // ==================================================

    let cardsHtml = "";

    if (items.length > 0) {

        cardsHtml = items
            .map(product => createProductCard(product))
            .join("");

    } else {

        cardsHtml = `
            <div class="alert alert-warning">
                Товари не знайдені.
            </div>
        `;
    }


    // ==================================================
    // RESULT
    // ==================================================

    resultBox.innerHTML = `

        <div class="assistant-result">

            <div class="assistant-result-header">

                <h3>Результат</h3>

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


            <div class="shopping-summary">

                <div>
                    <strong>Сума:</strong>
                    ${formatPrice(total)}
                </div>

                <div>
                    <strong>Бюджет:</strong>
                    ${formatPrice(budget)}
                </div>

                <div>
                    <strong>Залишок:</strong>
                    ${formatPrice(remaining)}
                </div>

            </div>


            <div class="product-grid">

                ${cardsHtml}

            </div>

        </div>

    `;
    resultBox.querySelectorAll("[data-product-name]")
        .forEach(button => {

            button.addEventListener("click", function () {

                const productName =
                    this.dataset.productName;

                addToCartByName(productName);

            });

        });
}


// ======================================================
// ASK ASSISTANT
// ======================================================

async function askAssistant() {

    const message = messageInput.value.trim();

    if (!message) {
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
                Шукаю товари Silpo...
            </div>

        </div>

    `;


    try {

        const response = await fetch(
            "/api/assistant",
            {
                method: "POST",

                headers: {
                    "Content-Type": "application/json"
                },

                body: JSON.stringify({
                    message: message
                })
            }
        );


        if (!response.ok) {

            throw new Error(
                `HTTP ${response.status}`
            );
        }


        let data = await response.json();


        console.log(
            "ВІДПОВІДЬ API:",
            data
        );


        renderAssistantResult(data);

    }
    catch (error) {

        console.error(
            "Assistant error:",
            error
        );


        resultBox.innerHTML = `

            <div class="alert alert-danger">

                <strong>Помилка:</strong>

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
// CART
// ======================================================

let cart = [];

function addToCartByName(productName) {

    // Знаходимо товар серед останніх отриманих товарів
    const product = currentProducts.find(
        p => (p.name ?? p.Name ?? "") === productName
    );

    if (!product) {
        console.error("Товар не знайдений:", productName);
        return;
    }

    const existing = cart.find(
        item => item.name === productName
    );

    if (existing) {
        existing.quantity++;
    } else {
        cart.push({
            name: productName,
            price: Number(product.price ?? product.Price ?? 0),
            quantity: 1
        });
    }

    renderCart();
}


// ======================================================
// CART RENDER
// ======================================================

function renderCart() {

    const cartBox = document.getElementById("cartBox");

    if (!cartBox) {
        console.error("cartBox не знайдений");
        return;
    }

    if (cart.length === 0) {

        cartBox.innerHTML = `
            <h3>🛒 Кошик</h3>
            <p>Кошик порожній</p>
            <strong>Разом: 0.00 грн</strong>
        `;

        return;
    }

    let total = 0;

    const itemsHtml = cart.map((item, index) => {

        const itemTotal =
            item.price * item.quantity;

        total += itemTotal;

        return `
            <div class="cart-item">

                <div>
                    <strong>
                        ${escapeHtml(item.name)}
                    </strong>

                    <div>
                        ${formatPrice(item.price)}
                        × ${item.quantity}
                    </div>
                </div>

                <div>
                    <strong>
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
    }).join("");

    cartBox.innerHTML = `
        <h3>🛒 Кошик</h3>

        ${itemsHtml}

        <div class="cart-total">
            <strong>
                Разом: ${formatPrice(total)}
            </strong>
        </div>

        <button
            type="button"
            class="btn btn-danger"
            id="clearCartBtn">
            Очистити
        </button>

        <button
            type="button"
            class="btn btn-primary">
            Оформити замовлення
        </button>
    `;
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
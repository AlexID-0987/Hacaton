// =====================================================
// ELEMENTS
// =====================================================

const messageInput = document.getElementById('message');
const sendBtn = document.getElementById('sendBtn');
const resultBox = document.getElementById('resultBox');

const categoryFilter =
    document.getElementById('categoryFilter');

const mealFilter =
    document.getElementById('mealFilter');

const fruitSelect =
    document.getElementById('fruitSelect');

const vegetableSelect =
    document.getElementById('vegetableSelect');

const fruitList =
    document.getElementById('fruitList');

const vegetableList =
    document.getElementById('vegetableList');

const loginBtn =
    document.getElementById('loginBtn');

// =====================================================
// STATE
// =====================================================

let allProducts = [];

let selectedBranchId = null;
let selectedDeliveryType = null;

let selectedTimeSlot = null;


// =====================================================
// DEFAULT IMAGE
// =====================================================

const defaultImage =
    'https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=600&q=80';


// =====================================================
// HELPERS
// =====================================================

function escapeHtml(value) {

    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}


function getProductName(product) {

    return (
        product?.name ??
        product?.Name ??
        product?.title ??
        product?.productName ??
        ''
    );
}


function getProductCategory(product) {

    return (
        product?.category ??
        product?.Category ??
        ''
    );
}


function getProductPrice(product) {

    const price =
        product?.price ??
        product?.Price ??
        product?.currentPrice ??
        product?.salePrice ??
        0;

    return Number(price) || 0;
}


function getProductImage(product) {

    return (
        product?.image ??
        product?.imageUrl ??
        product?.ImageUrl ??
        product?.photoUrl ??
        product?.photo ??
        defaultImage
    );
}


function formatPrice(price) {

    const number =
        Number(price) || 0;

    return number.toFixed(2);
}


// =====================================================
// LOCAL PRODUCT CARD
// =====================================================

function createProductCard(product) {

    const name =
        getProductName(product);

    const category =
        getProductCategory(product);

    const price =
        getProductPrice(product);

    const image =
        getProductImage(product);

    return `
        <article class="product-card">

            <img
                src="${escapeHtml(image)}"
                alt="${escapeHtml(name)}"
                onerror="this.onerror=null;this.src='${defaultImage}'">

            <div class="product-info">

                <div class="product-name">
                    ${escapeHtml(name)}
                </div>

                <div class="product-details">

                    <span>
                        ${escapeHtml(category)}
                    </span>

                    <strong>
                        ${formatPrice(price)} грн
                    </strong>

                </div>

            </div>

        </article>
    `;
}


// =====================================================
// SILPO PRODUCT CARD
// =====================================================

function createSilpoProductCard(product) {

    const name =
        product?.name ??
        product?.title ??
        product?.productName ??
        'Товар Silpo';

    const price =
        product?.price ??
        product?.currentPrice ??
        product?.salePrice ??
        0;

    const oldPrice =
        product?.oldPrice;

    const image =
        product?.image ??
        product?.imageUrl ??
        product?.photoUrl ??
        product?.photo ??
        defaultImage;

    const stock =
        product?.stock ?? 0;

    const available =
        product?.available !== false;

    const displayRatio =
        product?.displayRatio ?? '';

    const specialPrices =
        product?.specialPrices ?? [];

    let specialPriceHtml = '';

    if (
        Array.isArray(specialPrices) &&
        specialPrices.length > 0
    ) {

        const special =
            specialPrices[0];

        if (special?.price != null) {

            specialPriceHtml = `
                <div class="meta">
                    Акційна ціна від
                    ${formatPrice(special.price)} грн
                    ${special.count ? `від ${special.count} шт.` : ''}
                </div>
            `;
        }
    }

    return `
        <article class="product-card">

            <img
                src="${escapeHtml(image)}"
                alt="${escapeHtml(name)}"
                onerror="this.onerror=null;this.src='${defaultImage}'">

            <div class="product-info">

                <div class="product-name">
                    ${escapeHtml(name)}
                </div>

                ${displayRatio
            ? `
                            <div class="meta">
                                ${escapeHtml(displayRatio)}
                            </div>
                          `
            : ''
        }

                <div class="product-details">

                    <span>
                        ${available
            ? `В наявності: ${stock}`
            : 'Немає в наявності'
        }
                    </span>

                    <strong>
                        ${formatPrice(price)} грн
                    </strong>

                </div>

                ${oldPrice != null
            ? `
                            <div class="meta">
                                Стара ціна:
                                ${formatPrice(oldPrice)} грн
                            </div>
                          `
            : ''
        }

                ${specialPriceHtml}

            </div>

        </article>
    `;
}


// =====================================================
// PARSE SILPO MCP RESPONSE
// =====================================================

function parseSilpoProducts(mcpResponse) {

    try {

        /*
         * Наш backend вже повертає:
         *
         * {
         *   success: true,
         *   result: {
         *      success: true,
         *      summary: "...",
         *      queries: [
         *          {
         *              query: "Молоко",
         *              totalFound: 62,
         *              products: [...]
         *          }
         *      ]
         *   }
         * }
         */

        const result =
            mcpResponse?.result;

        if (!result) {

            console.warn(
                'У відповіді немає result:',
                mcpResponse
            );

            return [];
        }


        // =============================================
        // НОВИЙ ФОРМАТ
        // =============================================

        if (Array.isArray(result.queries)) {

            const products = [];

            for (const query of result.queries) {

                if (
                    Array.isArray(query.products)
                ) {

                    products.push(
                        ...query.products
                    );
                }
            }

            return products;
        }


        // =============================================
        // Якщо backend повернув старий MCP формат
        // =============================================

        if (
            result.content &&
            Array.isArray(result.content) &&
            result.content.length > 0
        ) {

            const text =
                result.content[0]?.text;

            if (text) {

                const data =
                    typeof text === 'string'
                        ? JSON.parse(text)
                        : text;

                if (Array.isArray(data)) {
                    return data;
                }

                if (Array.isArray(data.products)) {
                    return data.products;
                }

                if (Array.isArray(data.items)) {
                    return data.items;
                }

                if (Array.isArray(data.results)) {
                    return data.results;
                }
            }
        }


        // =============================================
        // Якщо result сам є масивом
        // =============================================

        if (Array.isArray(result)) {
            return result;
        }


        // =============================================
        // Інші можливі формати
        // =============================================

        if (Array.isArray(result.products)) {
            return result.products;
        }

        if (Array.isArray(result.items)) {
            return result.items;
        }

        if (Array.isArray(result.results)) {
            return result.results;
        }


        console.warn(
            'Не знайдено масив товарів Silpo:',
            mcpResponse
        );

        return [];

    }
    catch (error) {

        console.error(
            'Помилка розбору Silpo MCP:',
            error
        );

        return [];
    }
}


// =====================================================
// RENDER SILPO PRODUCTS
// =====================================================

function renderSilpoProducts(products, title = 'Товари Silpo') {

    if (!Array.isArray(products)) {
        products = [];
    }


    if (products.length === 0) {

        resultBox.innerHTML += `
            <div class="result-box">

                <h3>
                    Товари Silpo не знайдено
                </h3>

                <div class="meta">
                    Спробуйте змінити запит.
                </div>

            </div>
        `;

        return;
    }


    resultBox.innerHTML += `

        <div class="result-box">

            <h3>
                ${escapeHtml(title)}
            </h3>

            <div class="meta">
                Знайдено товарів:
                ${products.length}
            </div>

            <div class="product-grid">

                ${products
            .slice(0, 30)
            .map(createSilpoProductCard)
            .join('')}

            </div>

        </div>
    `;
}


// =====================================================
// GET SILPO PRODUCTS
// =====================================================

async function loadSilpoProducts(productNames) {

    if (
        !Array.isArray(productNames) ||
        productNames.length === 0
    ) {
        return;
    }


    try {

        resultBox.innerHTML += `
            <div class="meta">
                ⏳ Шукаю актуальні товари Silpo...
            </div>
        `;


        /*
         * Максимум 30 товарів,
         * як дозволяє наш backend.
         */

        const names =
            productNames
                .filter(x => x && String(x).trim())
                .slice(0, 30);


        if (!names.length) {
            return;
        }


        const query =
            names
                .map(name =>
                    `products=${encodeURIComponent(name)}`
                )
                .join('&');


        const response =
            await fetch(
                `/api/silpo/products?${query}`
            );


        const data =
            await response.json();


        if (!response.ok) {

            throw new Error(
                data?.message ??
                data?.error ??
                'Помилка отримання товарів Silpo'
            );
        }


        const products =
            parseSilpoProducts(data);


        /*
         * Прибираємо повідомлення
         * "Шукаю актуальні товари..."
         */

        const temporaryMessages =
            resultBox.querySelectorAll(
                '.meta'
            );

        if (temporaryMessages.length > 0) {

            const last =
                temporaryMessages[
                temporaryMessages.length - 1
                ];

            if (
                last.textContent.includes(
                    'Шукаю актуальні товари Silpo'
                )
            ) {
                last.remove();
            }
        }


        renderSilpoProducts(
            products,
            '🛒 Актуальні товари Silpo'
        );

    }
    catch (error) {

        console.error(
            'Silpo products error:',
            error
        );

        resultBox.innerHTML += `

            <div class="meta">

                <strong>
                    Не вдалося отримати товари Silpo
                </strong>

                <br>

                ${escapeHtml(error.message)}

            </div>
        `;
    }
}


// =====================================================
// MEAL MAP
// =====================================================

const mealMap = {

    'сніданок': [
        'Яйця',
        'Молоко',
        'Хліб',
        'Йогурт',
        'Яблука',
        'Банани',
        'Сир',
        'Огірки'
    ],

    'обід': [
        'Куряче філе',
        'Огірки',
        'Помідори',
        'Картопля',
        'Яблука',
        'Сметана',
        'Яловичина'
    ],

    'вечеря': [
        'Лосось',
        'Куряче філе',
        'Картопля',
        'Капуста',
        'Яблука',
        'Груша',
        'Молоко'
    ],

    'пікнік': [
        'Хліб',
        'Банани',
        'Яблука',
        'Вода',
        'Йогурт',
        'Сир',
        'Огірки',
        'Яйця'
    ]
};


// =====================================================
// RENDER LOCAL PRODUCTS
// =====================================================

function renderProducts(
    products,
    title,
    subtitle
) {

    if (!products.length) {

        resultBox.innerHTML = `

            <h3>
                Товари не знайдено
            </h3>

            <div class="meta">
                Спробуйте іншу категорію
                або тип прийому їжі.
            </div>
        `;

        return;
    }


    resultBox.innerHTML = `

        <h3>
            ${escapeHtml(title)}
        </h3>

        <div class="meta">
            ${escapeHtml(subtitle)}
        </div>

        <div class="product-grid">

            ${products
            .map(createProductCard)
            .join('')}

        </div>
    `;
}


// =====================================================
// FILTER PRODUCTS
// =====================================================

function updateFilteredProducts() {

    const category =
        categoryFilter.value;

    const meal =
        mealFilter.value;


    let filtered =
        [...allProducts];


    if (category !== 'all') {

        filtered =
            filtered.filter(
                product =>
                    getProductCategory(product) === category
            );
    }


    if (meal !== 'all') {

        const allowedNames =
            mealMap[meal] ?? [];


        filtered =
            filtered.filter(
                product =>
                    allowedNames.includes(
                        getProductName(product)
                    )
            );
    }


    const title =
        category === 'all'
            ? 'Усі товари'
            : category;


    const subtitle =
        meal === 'all'
            ? 'Показано всі доступні товари'
            : `Тип прийому їжі: ${meal}`;


    renderProducts(
        filtered,
        title,
        subtitle
    );
}


// =====================================================
// UPDATE AI MESSAGE
// =====================================================

function updateAssistantMessage() {

    const category =
        categoryFilter.value;

    const meal =
        mealFilter.value;


    if (
        category === 'all' &&
        meal === 'all'
    ) {

        messageInput.value =
            'Підбери продукти для сніданку до 350 грн';

        return;
    }


    const categoryText =
        category === 'all'
            ? 'продукти'
            : `продукти категорії ${category}`;


    const mealText =
        meal === 'all'
            ? 'для повсякденного меню'
            : `для ${meal}`;


    messageInput.value =
        `Підбери ${categoryText} ${mealText} до 350 грн`;
}


// =====================================================
// MINI PRODUCT
// =====================================================

function renderSelectedProduct(
    category,
    select,
    target
) {

    const selectedName =
        select.value;


    const products =
        allProducts.filter(
            product =>
                getProductCategory(product) === category
        );


    const product =
        products.find(
            item =>
                getProductName(item) === selectedName
        );


    if (!product) {

        target.innerHTML = `
            <div class="mini-product-item">

                <div class="name">
                    Оберіть товар
                </div>

            </div>
        `;

        return;
    }


    target.innerHTML = `

        <div class="mini-product-item">

            <img
                src="${escapeHtml(
        getProductImage(product)
    )}"
                alt="${escapeHtml(
        getProductName(product)
    )}">

            <div class="name">
                ${escapeHtml(
        getProductName(product)
    )}
            </div>

            <div class="price">
                ${formatPrice(
        getProductPrice(product)
    )} грн
            </div>

        </div>
    `;
}


// =====================================================
// SELECT OPTIONS
// =====================================================

function populateProductSelects() {

    const fruits =
        allProducts.filter(
            product =>
                getProductCategory(product) === 'Фрукти'
        );


    const vegetables =
        allProducts.filter(
            product =>
                getProductCategory(product) === 'Овочі'
        );


    fruitSelect.innerHTML =
        `
        <option value="">
            Оберіть фрукти
        </option>
        ` +
        fruits
            .map(product => {

                const name =
                    getProductName(product);

                return `
                    <option value="${escapeHtml(name)}">
                        ${escapeHtml(name)}
                    </option>
                `;
            })
            .join('');


    vegetableSelect.innerHTML =
        `
        <option value="">
            Оберіть овочі
        </option>
        ` +
        vegetables
            .map(product => {

                const name =
                    getProductName(product);

                return `
                    <option value="${escapeHtml(name)}">
                        ${escapeHtml(name)}
                    </option>
                `;
            })
            .join('');
}


// =====================================================
// LOAD LOCAL PRODUCTS
// =====================================================

async function loadProducts() {

    try {

        const response =
            await fetch(
                '/api/assistant/products'
            );


        if (!response.ok) {

            throw new Error(
                'Не вдалося завантажити товари'
            );
        }


        allProducts =
            await response.json();


        populateProductSelects();

        updateFilteredProducts();

    }
    catch (error) {

        resultBox.innerHTML = `

            <h3>
                Помилка завантаження товарів
            </h3>

            <div class="meta">
                ${escapeHtml(error.message)}
            </div>
        `;
    }
}


// =====================================================
// AI ASSISTANT
// =====================================================

async function askAssistant() {

    const message =
        messageInput.value.trim();


    if (!message) {

        resultBox.innerHTML =
            '<h3>Спочатку введіть запит.</h3>';

        return;
    }


    resultBox.innerHTML = `

        <h3>
            🤖 Підбираю товари...
        </h3>

        <div class="meta">
            AI аналізує ваш запит.
        </div>
    `;


    sendBtn.disabled = true;


    try {

        // =============================================
        // 1. AI
        // =============================================

        const response =
            await fetch(
                '/api/assistant',
                {
                    method: 'POST',

                    headers: {
                        'Content-Type':
                            'application/json'
                    },

                    body: JSON.stringify({
                        message
                    })
                }
            );


        const data =
            await response.json();


        if (!response.ok) {

            throw new Error(
                data?.message ??
                data?.error ??
                'Помилка запиту AI'
            );
        }


        // =============================================
        // 2. Показуємо результат AI
        // =============================================

        renderAssistantResult(data);


        // =============================================
        // 3. Визначаємо назви товарів
        // =============================================

        const items =
            Array.isArray(data.items)
                ? data.items
                : [];


        const productNames =
            items
                .map(item =>
                    item?.name
                )
                .filter(name =>
                    name &&
                    String(name).trim()
                );


        // =============================================
        // 4. Шукаємо актуальні товари Silpo
        // =============================================

        if (productNames.length > 0) {

            await loadSilpoProducts(
                productNames
            );
        }


        // =============================================
        // 5. Завантажуємо доставку
        // =============================================

        await loadDeliveryOptions();

    }
    catch (error) {

        console.error(
            'AI error:',
            error
        );


        resultBox.innerHTML = `

            <h3>
                ❌ Помилка
            </h3>

            <div class="meta">
                ${escapeHtml(error.message)}
            </div>

        `;
    }
    finally {

        sendBtn.disabled = false;
    }
}


// =====================================================
// AI RESULT
// =====================================================

function renderAssistantResult(data) {

    const items =
        Array.isArray(data.items)
            ? data.items
            : [];


    const total =
        Number(data.totalPrice) || 0;


    const budget =
        Number(data.budget) || 0;


    const remaining =
        budget - total;


    resultBox.innerHTML = `

        <h3>
            ${escapeHtml(
        data.message ??
        'Результат AI'
    )}
        </h3>

        <div class="meta">
            💰 Бюджет:
            <strong>
                ${formatPrice(budget)} грн
            </strong>
        </div>

        <div class="meta">
            🛒 Загальна сума:
            <strong>
                ${formatPrice(total)} грн
            </strong>
        </div>

        <div class="meta">
            ${remaining >= 0
            ? `Залишилось:
                       ${formatPrice(remaining)} грн`
            : `Перевищення бюджету:
                       ${formatPrice(
                Math.abs(remaining)
            )} грн`
        }
        </div>


        <div class="product-grid">

            ${items
            .map(item => {

                const name =
                    item?.name ??
                    'Товар';

                const quantity =
                    Number(item?.quantity) || 1;

                const itemTotal =
                    Number(item?.total) || 0;

                const image =
                    getProductImage(item);


                return `

                        <article class="product-card">

                            <img
                                src="${escapeHtml(image)}"
                                alt="${escapeHtml(name)}"
                                onerror="this.onerror=null;this.src='${defaultImage}'">

                            <div class="product-info">

                                <div class="product-name">
                                    ${escapeHtml(name)}
                                </div>

                                <div class="product-details">

                                    <span>
                                        × ${quantity}
                                    </span>

                                    <strong>
                                        ${formatPrice(
                    itemTotal
                )} грн
                                    </strong>

                                </div>

                            </div>

                        </article>

                    `;
            })
            .join('')}

        </div>


        <div class="total-row">

            <span>
                Разом
            </span>

            <span>
                ${formatPrice(total)} грн
            </span>

        </div>

    `;
}


// =====================================================
// DELIVERY
// =====================================================

async function loadDeliveryOptions() {

    const deliveryBox =
        document.getElementById(
            'deliveryBox'
        );

    const deliveryStatus =
        document.getElementById(
            'deliveryStatus'
        );

    const deliveryOptions =
        document.getElementById(
            'deliveryOptions'
        );


    if (!deliveryBox) {
        return;
    }


    deliveryBox.style.display =
        'block';


    deliveryStatus.textContent =
        'Завантажую способи доставки...';


    deliveryOptions.innerHTML =
        '';


    try {

        const latitude =
            50.44747065;

        const longitude =
            30.521505797601343;


        const response =
            await fetch(
                `/api/silpo/delivery` +
                `?latitude=${latitude}` +
                `&longitude=${longitude}`
            );


        if (!response.ok) {

            const text =
                await response.text();

            throw new Error(
                text ||
                'Не вдалося отримати способи доставки'
            );
        }


        const data =
            await response.json();


        const options =
            data.options || [];


        if (!options.length) {

            deliveryStatus.textContent =
                'Для цієї адреси доступних способів доставки немає.';

            return;
        }


        deliveryStatus.textContent =
            `Доступно способів доставки: ${options.length}`;


        deliveryOptions.innerHTML =
            options
                .map((option, index) => `

                    <button
                        class="delivery-option"
                        data-index="${index}"
                        data-branch-id="${escapeHtml(
                    option.branchId || ''
                )}"
                        data-delivery-type="${escapeHtml(
                    option.deliveryType || ''
                )}">

                        <strong>
                            ${escapeHtml(
                    getDeliveryName(
                        option.deliveryType
                    )
                )}
                        </strong>

                        <span>
                            ${escapeHtml(
                    option.description || ''
                )}
                        </span>

                    </button>

                `)
                .join('');


        document
            .querySelectorAll(
                '.delivery-option'
            )
            .forEach(button => {

                button.addEventListener(
                    'click',
                    async () => {

                        document
                            .querySelectorAll(
                                '.delivery-option'
                            )
                            .forEach(x =>
                                x.classList.remove(
                                    'selected'
                                )
                            );


                        button.classList.add(
                            'selected'
                        );


                        selectedBranchId =
                            button.dataset.branchId ||
                            null;


                        selectedDeliveryType =
                            button.dataset.deliveryType ||
                            null;


                        if (!selectedBranchId) {

                            document
                                .getElementById(
                                    'timeSlotsBox'
                                )
                                .style.display =
                                'none';


                            deliveryStatus.textContent =
                                `${getDeliveryName(
                                    selectedDeliveryType
                                )} вибрано.`;

                            return;
                        }


                        await loadTimeSlots(
                            selectedBranchId,
                            selectedDeliveryType
                        );
                    }
                );
            });

    }
    catch (error) {

        deliveryStatus.innerHTML = `

            <strong>
                Помилка доставки
            </strong>

            <br>

            ${escapeHtml(error.message)}

        `;
    }
}


// =====================================================
// DELIVERY NAME
// =====================================================

function getDeliveryName(type) {

    switch (type) {

        case 'DeliveryHome':
            return '🚚 Доставка додому';

        case 'WideAssortDelivery':
            return '📦 Доставка широкого асортименту';

        case 'B2B':
            return '🏢 B2B доставка';

        case 'NovaPoshta':
            return '📮 Нова Пошта';

        case 'SelfPickup':
            return '🏪 Самовивіз';

        default:
            return type || 'Доставка';
    }
}


// =====================================================
// TIME SLOTS
// =====================================================

async function loadTimeSlots(
    branchId,
    deliveryType
) {

    const timeSlotsBox =
        document.getElementById(
            'timeSlotsBox'
        );

    const timeSlots =
        document.getElementById(
            'timeSlots'
        );


    timeSlotsBox.style.display =
        'block';


    timeSlots.innerHTML =
        '<div class="meta">⏳ Завантажую доступний час...</div>';


    try {

        const url =
            `/api/silpo/timeslots` +
            `?branchId=${encodeURIComponent(
                branchId
            )}` +
            `&deliveryType=${encodeURIComponent(
                deliveryType
            )}`;


        const response =
            await fetch(url);


        if (!response.ok) {

            throw new Error(
                await response.text() ||
                'Не вдалося отримати час доставки'
            );
        }


        const data =
            await response.json();


        const slots =
            data.slots || [];


        if (!slots.length) {

            timeSlots.innerHTML = `

                <div class="meta">
                    На найближчий час
                    доступних слотів немає.
                </div>

            `;

            return;
        }


        timeSlots.innerHTML =
            slots
                .map((slot, index) => `

                    <button
                        class="time-slot"
                        data-index="${index}"
                        data-start="${escapeHtml(
                    slot.start || ''
                )}"
                        data-end="${escapeHtml(
                    slot.end || ''
                )}">

                        <strong>
                            ${escapeHtml(
                    slot.time || ''
                )}
                        </strong>

                        <span>
                            ${escapeHtml(
                    slot.date || ''
                )}
                        </span>

                        <small>
                            Доставка:
                            ${Number(
                    slot.deliveryCost
                ) || 0}
                            грн
                        </small>

                    </button>

                `)
                .join('');


        document
            .querySelectorAll(
                '.time-slot'
            )
            .forEach(button => {

                button.addEventListener(
                    'click',
                    () => {

                        document
                            .querySelectorAll(
                                '.time-slot'
                            )
                            .forEach(x =>
                                x.classList.remove(
                                    'selected'
                                )
                            );


                        button.classList.add(
                            'selected'
                        );


                        selectedTimeSlot = {

                            start:
                                button.dataset.start,

                            end:
                                button.dataset.end
                        };


                        console.log(
                            'Вибраний слот:',
                            selectedTimeSlot
                        );
                    }
                );
            });

    }
    catch (error) {

        timeSlots.innerHTML = `

            <div class="meta">

                <strong>
                    Помилка
                </strong>

                <br>

                ${escapeHtml(
            error.message
        )}

            </div>

        `;
    }
}


// =====================================================
// EVENTS
// =====================================================

fruitSelect.addEventListener(
    'change',
    () =>
        renderSelectedProduct(
            'Фрукти',
            fruitSelect,
            fruitList
        )
);


vegetableSelect.addEventListener(
    'change',
    () =>
        renderSelectedProduct(
            'Овочі',
            vegetableSelect,
            vegetableList
        )
);


categoryFilter.addEventListener(
    'change',
    () => {

        updateAssistantMessage();

        updateFilteredProducts();
    }
);


mealFilter.addEventListener(
    'change',
    () => {

        updateAssistantMessage();

        updateFilteredProducts();
    }
);


sendBtn.addEventListener(
    'click',
    askAssistant
);


messageInput.addEventListener(
    'keydown',
    event => {

        if (
            event.key === 'Enter' &&
            !event.shiftKey
        ) {

            event.preventDefault();

            askAssistant();
        }
    }
);
// =====================================================
// AUTHORIZATION
// =====================================================

async function checkAuthorization() {

    try {

        const response =
            await fetch('/api/silpo/status');

        if (!response.ok) {

            setLoggedOut();

            return;
        }

        const data =
            await response.json();

        if (data.authenticated === true) {

            setLoggedIn();

        } else {

            setLoggedOut();

        }

    }
    catch (error) {

        console.error(
            'Помилка перевірки авторизації:',
            error
        );

        setLoggedOut();
    }
}


function setLoggedIn() {

    loginBtn.textContent =
        '✓ Silpo підключено';

    loginBtn.classList.add(
        'logged-in'
    );

    sendBtn.disabled = false;
}


function setLoggedOut() {

    loginBtn.textContent =
        'Увійти через Silpo';

    loginBtn.classList.remove(
        'logged-in'
    );
}


loginBtn.addEventListener(
    'click',
    () => {

        window.location.href =
            '/api/silpo/login';

    }
);

// =====================================================
// START
// =====================================================
checkAuthorization();
loadProducts();
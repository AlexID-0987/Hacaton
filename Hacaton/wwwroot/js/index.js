

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


// =====================================================
// STATE
// =====================================================

let allProducts = [];


// =====================================================
// FALLBACK IMAGES
// =====================================================

const fallbackImages = {

    'Яйця':
        'https://vip.shuvar.com/pub/media/catalog/product/_/3/_3.jpg',

    'Огірки':
        'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcT0Z6lZm5_1JDzXO894hjTQM5HR6Om1aCuvi3uGDg2XAg&s=10',

    'Буряк':
        'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQxaCo9Jn3FkRJTsEEImyrWABwfO6SuFPye1h-XcgPG4Q&s=10',

    'Свинина':
        'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTg2ZGLUx2iCJADwY9SRzuX_dwloM_QlQ4Tyx11mcwgoA&s=10',

    'Груша':
        'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSHopj8mv3y5LiGrCV-tufiPCsflDj78H73ZByP2x9A_w&s',

    'Куряче філе':
        'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRvDp1l1CoLJXCl4nV8QwJuCBQJPo_T78QU9_guUehG0g&s',

    'Куряче филе':
        'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRvDp1l1CoLJXCl4nV8QwJuCBQJPo_T78QU9_guUehG0g&s'
};


const defaultImage =
    'https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=600&q=80';


// =====================================================
// HELPERS
// =====================================================

function getProductName(product) {

    return (
        product?.name ??
        product?.Name ??
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

    return (
        product?.price ??
        product?.Price ??
        0
    );
}


function getProductImage(product) {

    const name = getProductName(product).trim();

    return (
        fallbackImages[name] ??
        product?.imageUrl ??
        product?.ImageUrl ??
        defaultImage
    );
}


// =====================================================
// PRODUCT CARD
// =====================================================

function createProductCard(product) {

    const name = getProductName(product);
    const category = getProductCategory(product);
    const price = getProductPrice(product);
    const image = getProductImage(product);

    return `
    < article class="product-card" >

        <img
            src="${image}"
            alt="${name}"
            onerror="this.onerror=null;this.src='${defaultImage}'">

            <div class="product-info">

                <div class="product-name">
                    ${name}
                </div>

                <div class="product-details">

                    <span>
                        ${category}
                    </span>

                    <strong>
                        ${price} грн
                    </strong>

                </div>

            </div>

        </article>
`;
}


// =====================================================
// RENDER PRODUCTS
// =====================================================

function renderProducts(products, title, subtitle) {

    if (!products.length) {

        resultBox.innerHTML = `
    <h3> Товари не знайдено</h3>

        <div class="meta">
            Спробуйте іншу категорію або тип прийому їжі.
        </div>
`;

        return;
    }


    resultBox.innerHTML = `

    <h3>
    ${ title }
        </h3>

        <div class="meta">
            ${subtitle}
        </div>

        <div class="product-grid">

            ${products
                .map(createProductCard)
                .join('')}

        </div>
`;
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

        filtered = filtered.filter(
            product =>
                getProductCategory(product) === category
        );
    }


    if (meal !== 'all') {

        const allowedNames =
            mealMap[meal] ?? [];

        filtered = filtered.filter(
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
            : `Тип прийому їжі: ${ meal } `;


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
            : `продукти категорії ${ category } `;


    const mealText =
        meal === 'all'
            ? 'для повсякденного меню'
            : `для ${ meal } `;


    messageInput.value =
        `Підбери ${ categoryText } ${ mealText } до 350 грн`;
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

    < div class="mini-product-item" >

        <img
            src="${getProductImage(product)}"
            alt="${getProductName(product)}">

            <div class="name">
                ${getProductName(product)}
            </div>

            <div class="price">
                ${getProductPrice(product)} грн
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
        `<option value = ""> Оберіть фрукти</option> ` +
        fruits
            .map(product =>
                `<option value = "${getProductName(product)}">
    ${ getProductName(product) }
                </option> `
            )
            .join('');


    vegetableSelect.innerHTML =
        `< option value = "" > Оберіть овочі</option > ` +
        vegetables
            .map(product =>
                `< option value = "${getProductName(product)}" >
    ${ getProductName(product) }
                </option > `
            )
            .join('');
}


// =====================================================
// LOAD PRODUCTS
// =====================================================

async function loadProducts() {

    try {

        const response =
            await fetch('/api/assistant/products');


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
        ${error.message}
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

        resultBox.innerHTML = `
    <h3>
    Спочатку введіть запит.
            </h3>
    `;

        return;
    }


    resultBox.innerHTML = `
    <h3>
    Підбираю товари...
        </h3>
    `;


    sendBtn.disabled = true;


    try {

        const response =
            await fetch('/api/assistant', {

                method: 'POST',

                headers: {
                    'Content-Type':
                        'application/json'
                },

                body: JSON.stringify({
                    message
                })
            });


        const data =
            await response.json();


        if (!response.ok) {

            throw new Error(
                data.message ||
                'Помилка запиту'
            );
        }


        renderAssistantResult(data);
        await loadDeliveryOptions();

    }
    catch (error) {

        resultBox.innerHTML = `

    <h3>
    Помилка
            </h3>

    <div class="meta">
        ${error.message}
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
        data.items ?? [];

    const total =
        data.totalPrice ?? 0;

    const budget =
        data.budget ?? 0;


    resultBox.innerHTML = `

    <h3>
    ${ data.message ?? 'Результат' }
        </h3>

        <div class="meta">
            Бюджет: ${budget} грн
        </div>

        <div class="meta">
            Загальна сума: ${total} грн
        </div>

        <div class="product-grid">

            ${items
                .map(item => `

                    <article class="product-card">

                        <img
                            src="${getProductImage(item)}"
                            alt="${item.name}"
                            onerror="this.onerror=null;this.src='${defaultImage}'">

                        <div class="product-info">

                            <div class="product-name">
                                ${item.name}
                            </div>

                            <div class="product-details">

                                <span>
                                    × ${item.quantity}
                                </span>

                                <strong>
                                    ${item.total} грн
                                </strong>

                            </div>

                        </div>

                    </article>

                `)
                .join('')}

        </div>

        <div class="total-row">

            <span>
                Разом
            </span>

            <span>
                ${total} грн
            </span>

        </div>
`;
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


let selectedBranchId = null;
let selectedDeliveryType = null;


// ================================
// SILPO MCP — способи доставки
// ================================
async function loadDeliveryOptions() {
    const deliveryBox = document.getElementById('deliveryBox');
    const deliveryStatus = document.getElementById('deliveryStatus');
    const deliveryOptions = document.getElementById('deliveryOptions');

    deliveryBox.style.display = 'block';
    deliveryStatus.textContent = 'Завантажую способи доставки...';
    deliveryOptions.innerHTML = '';

    try {
        // Київ — координати, які ти вже використовував
        const latitude = 50.44747065;
        const longitude = 30.521505797601343;

        const response = await fetch(
            `/api/silpo/delivery?latitude=${latitude}&longitude=${longitude}`
        );

        if (!response.ok) {
            throw new Error(
                await response.text() || 'Не вдалося отримати способи доставки'
            );
        }

        const data = await response.json();

        const options = data.options || [];

        if (!options.length) {
            deliveryStatus.textContent =
                'Для цієї адреси доступних способів доставки немає.';
            return;
        }

        deliveryStatus.textContent =
            `Доступно способів доставки: ${options.length}`;

        deliveryOptions.innerHTML = options.map((option, index) => `
            <button
                class="delivery-option"
                data-index="${index}"
                data-branch-id="${option.branchId || ''}"
                data-delivery-type="${option.deliveryType}">
                
                <strong>${getDeliveryName(option.deliveryType)}</strong>
                <span>${option.description || ''}</span>
            </button>
        `).join('');

        document
            .querySelectorAll('.delivery-option')
            .forEach(button => {

                button.addEventListener('click', async () => {

                    document
                        .querySelectorAll('.delivery-option')
                        .forEach(x => x.classList.remove('selected'));

                    button.classList.add('selected');

                    selectedBranchId =
                        button.dataset.branchId || null;

                    selectedDeliveryType =
                        button.dataset.deliveryType;

                    // Час потрібен тільки якщо є branchId
                    if (!selectedBranchId) {
                        document.getElementById('timeSlotsBox').style.display = 'none';

                        deliveryStatus.textContent =
                            `${getDeliveryName(selectedDeliveryType)} вибрано. Для цього типу потрібні додаткові дані.`;

                        return;
                    }

                    await loadTimeSlots(
                        selectedBranchId,
                        selectedDeliveryType
                    );
                });
            });

    } catch (error) {
        deliveryStatus.innerHTML = `
            <strong>Помилка доставки</strong>
            <br>
            ${escapeHtml(error.message)}
        `;
    }
}


// ================================
// Назви способів доставки
// ================================
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
            return type;
    }
}


// ================================
// SILPO MCP — час доставки
// ================================
async function loadTimeSlots(branchId, deliveryType) {

    const timeSlotsBox =
        document.getElementById('timeSlotsBox');

    const timeSlots =
        document.getElementById('timeSlots');

    timeSlotsBox.style.display = 'block';

    timeSlots.innerHTML =
        '<div class="meta">⏳ Завантажую доступний час...</div>';

    try {

        const url =
            `/api/silpo/timeslots` +
            `?branchId=${encodeURIComponent(branchId)}` +
            `&deliveryType=${encodeURIComponent(deliveryType)}`;

        const response = await fetch(url);

        if (!response.ok) {
            throw new Error(
                await response.text() ||
                'Не вдалося отримати час доставки'
            );
        }

        const data = await response.json();

        const slots = data.slots || [];

        if (!slots.length) {
            timeSlots.innerHTML = `
                <div class="meta">
                    На найближчий час доступних слотів немає.
                </div>
            `;
            return;
        }

        timeSlots.innerHTML = slots.map((slot, index) => `
            <button
                class="time-slot"
                data-index="${index}"
                data-start="${slot.start}"
                data-end="${slot.end}">

                <strong>${slot.time}</strong>

                <span>
                    ${slot.date}
                </span>

                <small>
                    Доставка: ${slot.deliveryCost} грн
                </small>
            </button>
        `).join('');

        document
            .querySelectorAll('.time-slot')
            .forEach(button => {

                button.addEventListener('click', () => {

                    document
                        .querySelectorAll('.time-slot')
                        .forEach(x =>
                            x.classList.remove('selected')
                        );

                    button.classList.add('selected');

                    const start =
                        button.dataset.start;

                    const end =
                        button.dataset.end;

                    console.log(
                        'Вибраний слот:',
                        start,
                        end
                    );

                    // Тут пізніше можна передати
                    // вибраний слот у кошик Silpo.
                });
            });

    } catch (error) {

        timeSlots.innerHTML = `
            <div class="meta">
                <strong>Помилка</strong><br>
                ${escapeHtml(error.message)}
            </div>
        `;
    }
}


// ================================
// Безпечний текст
// ================================
function escapeHtml(value) {
    return String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}



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
// START APPLICATION
// =====================================================

loadProducts();


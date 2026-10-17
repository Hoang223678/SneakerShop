const CATALOG_APIS = ["http://localhost:5002", "http://localhost:5000"];

async function catalogFetch(path) {
    let lastError;
    for (const base of CATALOG_APIS) {
        try {
            const response = await fetch(`${base}${path}`);
            if (response.ok) return response.json();
            lastError = new Error(`Catalog returned ${response.status}`);
        } catch (error) {
            lastError = error;
        }
    }
    throw lastError ?? new Error("Could not load sneakers.");
}

async function fetchSneakers({ gender, page = 1, pageSize = 12, sort } = {}) {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (gender) params.set("gender", gender);
    if (sort) params.set("sort", sort);
    return catalogFetch(`/api/sneakers?${params}`);
}

async function fetchSneaker(id) {
    return catalogFetch(`/api/sneakers/${id}`);
}

function formatPrice(price) {
    return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(price);
}

function productCardHtml(item) {
    const image = item.imageUrl || "images/item-1.jpg";
    const href = `product-detail.html?id=${encodeURIComponent(item.id)}`;
    return `
        <div class="col-md-3 col-lg-3 mb-4 text-center">
            <div class="product-entry border">
                <a href="${href}" class="prod-img">
                    <img src="${image}" class="img-fluid" alt="${item.name}">
                </a>
                <div class="desc">
                    <h2><a href="${href}">${item.name}</a></h2>
                    <span class="price">${formatPrice(item.price)}</span>
                </div>
            </div>
        </div>`;
}

function renderProductGrid(container, items) {
    if (!items.length) {
        container.innerHTML = `<div class="col-12 text-center"><p>No products found.</p></div>`;
        return;
    }

    container.innerHTML = items.map((item, index) => {
        const rowBreak = (index + 1) % 4 === 0 && index !== items.length - 1
            ? `<div class="w-100"></div>`
            : "";
        return productCardHtml(item) + rowBreak;
    }).join("");
}

function renderPagination(container, page, totalPages, onPage) {
    if (!container) return;
    if (totalPages <= 1) {
        container.innerHTML = "";
        return;
    }

    const buttons = [];
    buttons.push(`<li><a href="#" data-page="${Math.max(1, page - 1)}"><i class="ion-ios-arrow-back"></i></a></li>`);
    for (let i = 1; i <= totalPages; i++) {
        buttons.push(i === page
            ? `<li class="active"><span>${i}</span></li>`
            : `<li><a href="#" data-page="${i}">${i}</a></li>`);
    }
    buttons.push(`<li><a href="#" data-page="${Math.min(totalPages, page + 1)}"><i class="ion-ios-arrow-forward"></i></a></li>`);
    container.innerHTML = `<ul>${buttons.join("")}</ul>`;
    container.querySelectorAll("a[data-page]").forEach(link => {
        link.addEventListener("click", event => {
            event.preventDefault();
            onPage(Number(link.dataset.page));
        });
    });
}

async function loadProductDetail() {
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
    if (!id) return;

    try {
        const sneaker = await fetchSneaker(id);
        const name = document.getElementById("product-name");
        const price = document.getElementById("product-price");
        const description = document.getElementById("product-description");
        const image = document.getElementById("product-image");
        const sizes = document.getElementById("product-sizes");
        if (name) name.textContent = sneaker.name;
        if (price) price.textContent = formatPrice(sneaker.price);
        if (description) description.textContent = sneaker.description || "";
        if (image) {
            image.src = sneaker.imageUrl || "images/item-1.jpg";
            image.alt = sneaker.name;
        }
        if (sizes && sneaker.variants?.length) {
            sizes.innerHTML = sneaker.variants
                .map(variant => `<li><a href="#">${Number(variant.size)}</a></li>`)
                .join("");
        }
        document.title = `${sneaker.name} - Footwear`;
    } catch (error) {
        console.error(error);
    }
}

async function loadMenCollection(page = 1) {
    const grid = document.getElementById("men-products");
    const pager = document.getElementById("men-pagination");
    if (!grid) return;

    grid.innerHTML = `<div class="col-12 text-center"><p>Loading men's sneakers...</p></div>`;
    try {
        const data = await fetchSneakers({ gender: "Men", page, pageSize: 12 });
        renderProductGrid(grid, data.items || []);
        renderPagination(pager, data.page, data.totalPages, loadMenCollection);
    } catch (error) {
        grid.innerHTML = `<div class="col-12 text-center"><p>Could not load men's sneakers. Is Catalog running on localhost:5002?</p></div>`;
        console.error(error);
    }
}

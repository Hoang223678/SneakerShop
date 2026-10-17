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

export async function fetchSneakers({ gender, page = 1, pageSize = 12, sort } = {}) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (gender) params.set("gender", gender);
  if (sort) params.set("sort", sort);
  return catalogFetch(`/api/sneakers?${params}`);
}

export async function fetchSneaker(id) {
  return catalogFetch(`/api/sneakers/${id}`);
}

export function formatPrice(price) {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(price);
}

import axios from "axios";

const CATALOG_APIS = ["http://localhost:5002", "http://localhost:5000"];

function errorMessage(error) {
  const data = error.response?.data;
  return data?.title || data?.detail || data?.message || error.message || "Request failed.";
}

async function catalogRequest({ method, path, data, token }) {
  let lastError;
  for (const base of CATALOG_APIS) {
    try {
      const response = await axios({
        method,
        url: `${base}${path}`,
        data,
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...(data ? { "Content-Type": "application/json" } : {})
        }
      });
      return response.data;
    } catch (error) {
      if (error.response) throw new Error(errorMessage(error));
      lastError = error;
    }
  }
  throw lastError ?? new Error("Could not reach Catalog.");
}

export async function fetchSneakers({ gender, search, page = 1, pageSize = 12, sort } = {}) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (gender) params.set("gender", gender);
  if (search) params.set("search", search);
  if (sort) params.set("sort", sort);
  return catalogRequest({ method: "get", path: `/api/sneakers?${params}` });
}

export async function fetchSneaker(id) {
  return catalogRequest({ method: "get", path: `/api/sneakers/${id}` });
}

export async function fetchBrands() {
  return catalogRequest({ method: "get", path: "/api/brands" });
}

export async function createBrand(token, body) {
  return catalogRequest({ method: "post", path: "/api/brands", data: body, token });
}

export async function updateBrand(token, id, body) {
  return catalogRequest({ method: "put", path: `/api/brands/${id}`, data: body, token });
}

export async function deleteBrand(token, id) {
  return catalogRequest({ method: "delete", path: `/api/brands/${id}`, token });
}

export async function createSneaker(token, body) {
  return catalogRequest({ method: "post", path: "/api/sneakers", data: body, token });
}

export async function updateSneaker(token, id, body) {
  return catalogRequest({ method: "put", path: `/api/sneakers/${id}`, data: body, token });
}

export async function deleteSneaker(token, id) {
  return catalogRequest({ method: "delete", path: `/api/sneakers/${id}`, token });
}

export function formatPrice(price) {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(price);
}

import axios from "axios";

const INVENTORY_APIS = ["http://localhost:5003", "http://localhost:5000"];

function errorMessage(error) {
  const data = error.response?.data;
  return data?.title || data?.detail || data?.message || error.message || "Request failed.";
}

async function inventoryRequest({ method, path, data, token }) {
  let lastError;
  for (const base of INVENTORY_APIS) {
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
  throw lastError ?? new Error("Could not reach Inventory.");
}

export async function fetchInventory({ sneakerId, page = 1, pageSize = 100 } = {}, token) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (sneakerId) params.set("sneakerId", sneakerId);
  return inventoryRequest({ method: "get", path: `/api/inventory?${params}`, token });
}

export async function setStock(token, variantId, quantity) {
  return inventoryRequest({
    method: "put",
    path: `/api/inventory/${variantId}`,
    data: { quantity },
    token
  });
}

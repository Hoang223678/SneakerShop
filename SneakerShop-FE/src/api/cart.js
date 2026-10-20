import axios from "axios";

const CART_APIS = ["http://localhost:5006", "http://localhost:5000"];

function errorMessage(error) {
  const data = error.response?.data;
  return data?.title || data?.detail || data?.message || error.message || "Request failed.";
}

async function cartRequest({ method, path, data, token, headers = {} }) {
  let lastError;
  for (const base of CART_APIS) {
    try {
      const response = await axios({
        method,
        url: `${base}${path}`,
        data,
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...(data ? { "Content-Type": "application/json" } : {}),
          ...headers
        }
      });
      return response.data;
    } catch (error) {
      if (error.response) throw new Error(errorMessage(error));
      lastError = error;
    }
  }
  throw lastError ?? new Error("Could not reach Cart.");
}

export async function fetchCart(token) {
  return cartRequest({ method: "get", path: "/api/cart", token });
}

export async function addCartItems(token, items) {
  return cartRequest({
    method: "post",
    path: "/api/cart/items",
    data: { items },
    token
  });
}

export async function setCartItem(token, variantId, quantity) {
  return cartRequest({
    method: "put",
    path: `/api/cart/items/${variantId}`,
    data: { quantity },
    token
  });
}

export async function removeCartItem(token, variantId) {
  return cartRequest({
    method: "delete",
    path: `/api/cart/items/${variantId}`,
    token
  });
}

export async function checkoutCart(token, body, idempotencyKey) {
  return cartRequest({
    method: "post",
    path: "/api/cart/checkout",
    data: { ...body, method: "vnpay" },
    token,
    headers: { "Idempotency-Key": idempotencyKey }
  });
}

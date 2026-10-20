import axios from "axios";

const ORDER_APIS = ["http://localhost:5004", "http://localhost:5000"];

function errorMessage(error) {
  const data = error.response?.data;
  return data?.title || data?.detail || data?.message || error.message || "Request failed.";
}

async function orderRequest({ method, path, token }) {
  let lastError;
  for (const base of ORDER_APIS) {
    try {
      const response = await axios({
        method,
        url: `${base}${path}`,
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      });
      return response.data;
    } catch (error) {
      if (error.response) throw new Error(errorMessage(error));
      lastError = error;
    }
  }
  throw lastError ?? new Error("Could not reach Ordering.");
}

export async function fetchOrderDashboard(token, year) {
  const params = year ? `?year=${year}` : "";
  return orderRequest({ method: "get", path: `/api/orders/dashboard${params}`, token });
}

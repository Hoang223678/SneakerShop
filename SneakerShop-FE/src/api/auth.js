import axios from "axios";

const IDENTITY_APIS = ["http://localhost:5001", "http://localhost:5000"];

function errorMessage(error) {
  const data = error.response?.data;
  return data?.title || data?.detail || data?.message || error.message || "Request failed.";
}

async function identityPost(path, body) {
  let lastError;
  for (const base of IDENTITY_APIS) {
    try {
      const { data } = await axios.post(`${base}${path}`, body, {
        headers: { "Content-Type": "application/json" }
      });
      return data;
    } catch (error) {
      if (error.response) throw new Error(errorMessage(error));
      lastError = error;
    }
  }
  throw lastError ?? new Error("Could not reach Identity.");
}

export async function login({ email, password }) {
  return identityPost("/api/auth/login", { email, password });
}

export async function register({ email, password, fullName }) {
  return identityPost("/api/auth/register", { email, password, fullName });
}

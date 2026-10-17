import { createContext, useContext, useMemo, useState } from "react";
import { isAdminUser } from "./roles.js";

const STORAGE_KEY = "sneakershop.auth";
const AuthContext = createContext(null);

function readStoredAuth() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }) {
  const [session, setSession] = useState(readStoredAuth);

  const value = useMemo(() => ({
    session,
    user: session?.user ?? null,
    token: session?.accessToken ?? null,
    isAdmin: isAdminUser(session?.user),
    setSession: next => {
      setSession(next);
      if (next) localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      else localStorage.removeItem(STORAGE_KEY);
    },
    logout: () => {
      setSession(null);
      localStorage.removeItem(STORAGE_KEY);
    }
  }), [session]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside AuthProvider");
  return context;
}

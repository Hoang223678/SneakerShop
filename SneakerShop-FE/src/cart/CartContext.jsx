import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { addCartItems, fetchCart, removeCartItem, setCartItem } from "../api/cart.js";
import { useAuth } from "../auth/AuthContext.jsx";

const emptyCart = { items: [], subtotal: 0 };
const CartContext = createContext(null);

export function CartProvider({ children }) {
  const { token } = useAuth();
  const [cart, setCart] = useState(emptyCart);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    if (!token) {
      setCart(emptyCart);
      return emptyCart;
    }
    const data = await fetchCart(token);
    setCart(data);
    return data;
  }

  useEffect(() => {
    let cancelled = false;
    setError("");
    refresh().catch(err => {
      if (!cancelled) {
        setError(err.message);
        setCart(emptyCart);
      }
    });
    return () => { cancelled = true; };
  }, [token]);

  const value = useMemo(() => ({
    cart,
    error,
    busy,
    count: (cart.items || []).reduce((sum, item) => sum + Number(item.quantity || 0), 0),
    refresh,
    async addItems(items) {
      setBusy(true);
      setError("");
      try {
        const data = await addCartItems(token, items);
        setCart(data);
        return data;
      } catch (err) {
        setError(err.message);
        throw err;
      } finally {
        setBusy(false);
      }
    },
    async updateItem(variantId, quantity) {
      setBusy(true);
      setError("");
      try {
        const data = await setCartItem(token, variantId, quantity);
        setCart(data);
        return data;
      } catch (err) {
        setError(err.message);
        throw err;
      } finally {
        setBusy(false);
      }
    },
    async removeItem(variantId) {
      setBusy(true);
      setError("");
      try {
        const data = await removeCartItem(token, variantId);
        setCart(data);
        return data;
      } catch (err) {
        setError(err.message);
        throw err;
      } finally {
        setBusy(false);
      }
    }
  }), [cart, error, busy, token]);

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart() {
  const context = useContext(CartContext);
  if (!context) throw new Error("useCart must be used inside CartProvider");
  return context;
}

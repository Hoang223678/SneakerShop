import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { fetchSneaker, formatPrice } from "../api/catalog.js";
import { fetchInventory, setStock } from "../api/inventory.js";
import { useAuth } from "../auth/AuthContext.jsx";

export default function AdminInventorySneaker() {
  const { sneakerId } = useParams();
  const { token } = useAuth();
  const [sneaker, setSneaker] = useState(null);
  const [stockByVariant, setStockByVariant] = useState({});
  const [quantities, setQuantities] = useState({});
  const [error, setError] = useState("");
  const [saving, setSaving] = useState("");

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      fetchSneaker(sneakerId),
      fetchInventory({ page: 1, pageSize: 200 }, token)
    ])
      .then(([detail, stockPage]) => {
        if (cancelled) return;
        const stock = {};
        const qty = {};
        for (const row of stockPage.items || []) {
          stock[row.variantId] = row;
          qty[row.variantId] = String(row.quantity);
        }
        setSneaker(detail);
        setStockByVariant(stock);
        setQuantities(qty);
      })
      .catch(err => {
        if (!cancelled) setError(err.message);
      });
    return () => { cancelled = true; };
  }, [sneakerId, token]);

  async function save(variantId) {
    const quantity = Number(quantities[variantId]);
    if (!Number.isFinite(quantity) || quantity < 0) {
      setError("Quantity must be 0 or more.");
      return;
    }
    setSaving(variantId);
    setError("");
    try {
      const row = await setStock(token, variantId, quantity);
      setStockByVariant(current => ({ ...current, [variantId]: row }));
      setQuantities(current => ({ ...current, [variantId]: String(row.quantity) }));
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving("");
    }
  }

  const variants = [...(sneaker?.variants || [])].sort((a, b) => Number(a.size) - Number(b.size) || String(a.color).localeCompare(String(b.color)));
  const sizes = [...new Set(variants.map(v => Number(v.size)))];
  const colors = [...new Set(variants.map(v => v.color).filter(Boolean))];

  return (
    <div className="ad-body ad-body-single">
      <section className="ad-content">
        <p className="ad-muted"><Link to="/admin/inventory">← Inventory</Link></p>
        {error ? <p className="ad-error">{error}</p> : null}
        {!sneaker && !error ? <p>Loading…</p> : null}
        {sneaker ? (
          <>
            <div className="ad-card ad-inv-detail">
              <img src={sneaker.imageUrl || "/images/item-1.jpg"} alt={sneaker.name} />
              <div>
                <h1>{sneaker.name}</h1>
                <dl className="ad-inv-meta">
                  <div><dt>Brand</dt><dd>{sneaker.brand?.name || "—"}</dd></div>
                  <div><dt>Gender</dt><dd>{sneaker.gender || "—"}</dd></div>
                  <div><dt>Price</dt><dd>{formatPrice(sneaker.price)}</dd></div>
                  <div><dt>Variants</dt><dd>{variants.length} ({sizes.length} size(s), {colors.length} color(s))</dd></div>
                </dl>
                {sneaker.description ? <p className="ad-help">{sneaker.description}</p> : null}
              </div>
            </div>
            <div className="ad-card">
              <h2>Variants</h2>
              <p className="ad-muted">Each row is a size and color of this sneaker.</p>
              <table className="ad-table">
                <thead>
                  <tr>
                    <th>Size</th>
                    <th>Color</th>
                    <th>SKU</th>
                    <th>On hand</th>
                    <th>Reserved</th>
                    <th>Available</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {variants.map(variant => {
                    const stock = stockByVariant[variant.id];
                    return (
                      <tr key={variant.id}>
                        <td>{Number(variant.size)}</td>
                        <td>{variant.color}</td>
                        <td>{variant.sku}</td>
                        <td>
                          {stock ? (
                            <input
                              className="ad-qty"
                              type="number"
                              min="0"
                              value={quantities[variant.id] ?? stock.quantity}
                              onChange={event => setQuantities(current => ({ ...current, [variant.id]: event.target.value }))}
                            />
                          ) : "—"}
                        </td>
                        <td>{stock ? stock.reserved : "—"}</td>
                        <td>{stock ? stock.available : "—"}</td>
                        <td className="ad-actions">
                          {stock ? (
                            <button type="button" disabled={saving === variant.id} onClick={() => save(variant.id)}>
                              {saving === variant.id ? "Saving…" : "Save"}
                            </button>
                          ) : (
                            <span>No stock record</span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
      </section>
    </div>
  );
}

import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { fetchSneaker, formatPrice } from "../api/catalog.js";
import { createVariant, deleteVariant, fetchInventory, setStock, updateVariant } from "../api/inventory.js";
import { useAuth } from "../auth/AuthContext.jsx";

const emptyForm = { sku: "", size: "", color: "", quantity: "0" };

export default function AdminInventorySneaker() {
  const { sneakerId } = useParams();
  const { token } = useAuth();
  const [sneaker, setSneaker] = useState(null);
  const [stockByVariant, setStockByVariant] = useState({});
  const [quantities, setQuantities] = useState({});
  const [error, setError] = useState("");
  const [saving, setSaving] = useState("");
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [busy, setBusy] = useState(false);

  async function load() {
    const [detail, stockPage] = await Promise.all([
      fetchSneaker(sneakerId),
      fetchInventory({ page: 1, pageSize: 100 }, token)
    ]);
    const stock = {};
    const qty = {};
    for (const row of stockPage.items || []) {
      stock[row.variantId] = row;
      qty[row.variantId] = String(row.quantity);
    }
    setSneaker(detail);
    setStockByVariant(stock);
    setQuantities(qty);
  }

  useEffect(() => {
    let cancelled = false;
    load().catch(err => {
      if (!cancelled) setError(err.message);
    });
    return () => { cancelled = true; };
  }, [sneakerId, token]);

  function onChange(event) {
    setForm(current => ({ ...current, [event.target.name]: event.target.value }));
  }

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setOpen(true);
    setError("");
  }

  function openEdit(variant) {
    setEditingId(variant.id);
    setForm({
      sku: variant.sku ?? "",
      size: String(variant.size ?? ""),
      color: variant.color ?? "",
      quantity: String(quantities[variant.id] ?? stockByVariant[variant.id]?.quantity ?? 0)
    });
    setOpen(true);
    setError("");
  }

  async function onSubmit(event) {
    event.preventDefault();
    const sku = form.sku.trim();
    const color = form.color.trim();
    const size = Number(form.size);
    const quantity = Number(form.quantity);
    if (!sku || !color || !Number.isFinite(size)) {
      setError("SKU, size, and color are required.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (editingId) {
        await updateVariant(token, editingId, { sku, size, color });
      } else {
        await createVariant(token, sneakerId, {
          sku,
          size,
          color,
          quantity: Number.isFinite(quantity) && quantity >= 0 ? quantity : 0
        });
      }
      setOpen(false);
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(variant) {
    if (!window.confirm(`Delete size ${Number(variant.size)} ${variant.color} (${variant.sku})?`)) return;
    setError("");
    try {
      await deleteVariant(token, variant.id);
      await load();
    } catch (err) {
      setError(err.message);
    }
  }

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
              <div className="ad-hello">
                <h2>Variants</h2>
                <button type="button" className="ad-btn ad-btn-primary" onClick={openCreate}>Add variant</button>
              </div>
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
                  {variants.length === 0 ? (
                    <tr><td colSpan={7} className="ad-muted">No variants yet. Add a size and color to stock this sneaker.</td></tr>
                  ) : null}
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
                          ) : null}
                          <button type="button" onClick={() => openEdit(variant)}>Edit</button>
                          <button type="button" className="is-danger" onClick={() => onDelete(variant)}>Delete</button>
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

      {open ? (
        <div className="ad-modal" role="dialog" aria-modal="true" aria-labelledby="ad-variant-form">
          <form className="ad-modal-card" onSubmit={onSubmit}>
            <h2 id="ad-variant-form">{editingId ? "Edit variant" : "Add variant"}</h2>
            <label>SKU<input name="sku" value={form.sku} onChange={onChange} required /></label>
            <label>Size<input name="size" type="number" min="1" max="25" step="0.5" value={form.size} onChange={onChange} required /></label>
            <label>Color<input name="color" value={form.color} onChange={onChange} required /></label>
            {editingId ? null : (
              <label>On hand<input name="quantity" type="number" min="0" value={form.quantity} onChange={onChange} /></label>
            )}
            <div className="ad-modal-actions">
              <button type="button" className="ad-btn" onClick={() => setOpen(false)}>Cancel</button>
              <button type="submit" className="ad-btn ad-btn-primary" disabled={busy}>{busy ? "Saving…" : "Save"}</button>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  );
}

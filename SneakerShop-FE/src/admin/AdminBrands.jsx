import { useEffect, useState } from "react";
import { createBrand, deleteBrand, fetchBrands, updateBrand } from "../api/catalog.js";
import { useAuth } from "../auth/AuthContext.jsx";

export default function AdminBrands() {
  const { token } = useAuth();
  const [items, setItems] = useState([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [name, setName] = useState("");
  const [open, setOpen] = useState(false);

  async function load() {
    const brands = await fetchBrands();
    setItems(brands || []);
  }

  useEffect(() => {
    load().catch(err => setError(err.message));
  }, []);

  function openCreate() {
    setEditingId(null);
    setName("");
    setError("");
    setOpen(true);
  }

  function openEdit(brand) {
    setEditingId(brand.id);
    setName(brand.name);
    setError("");
    setOpen(true);
  }

  async function onSubmit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    const body = { name: name.trim() };
    try {
      if (editingId) await updateBrand(token, editingId, body);
      else await createBrand(token, body);
      setOpen(false);
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(brand) {
    if (!window.confirm(`Delete brand “${brand.name}”?`)) return;
    setError("");
    try {
      await deleteBrand(token, brand.id);
      await load();
    } catch (err) {
      setError(err.message);
    }
  }

  const visible = items.filter(brand => brand.name.toLowerCase().includes(search.trim().toLowerCase()));

  return (
    <div className="ad-body ad-body-single">
      <section className="ad-content">
        <div className="ad-hello">
          <h1>Brands</h1>
          <button type="button" className="ad-btn ad-btn-primary" onClick={openCreate}>Add brand</button>
        </div>
        <form className="ad-toolbar" onSubmit={event => event.preventDefault()}>
          <input
            type="search"
            placeholder="Search brands"
            value={search}
            onChange={event => setSearch(event.target.value)}
          />
        </form>
        {error ? <p className="ad-error">{error}</p> : null}
        <div className="ad-card">
          <table className="ad-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Slug</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {visible.map(brand => (
                <tr key={brand.id}>
                  <td>{brand.name}</td>
                  <td>{brand.slug}</td>
                  <td className="ad-actions">
                    <button type="button" onClick={() => openEdit(brand)}>Edit</button>
                    <button type="button" className="is-danger" onClick={() => onDelete(brand)}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {open ? (
        <div className="ad-modal" role="dialog" aria-modal="true" aria-labelledby="ad-brand-form">
          <form className="ad-modal-card" onSubmit={onSubmit}>
            <h2 id="ad-brand-form">{editingId ? "Edit brand" : "Add brand"}</h2>
            <label>Name<input value={name} onChange={event => setName(event.target.value)} required /></label>
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

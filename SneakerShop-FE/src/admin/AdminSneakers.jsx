import { useEffect, useState } from "react";
import {
  createSneaker,
  deleteSneaker,
  fetchBrands,
  fetchSneaker,
  fetchSneakers,
  formatPrice,
  updateSneaker
} from "../api/catalog.js";
import { useAuth } from "../auth/AuthContext.jsx";

const GENDERS = ["Men", "Women", "Unisex", "Kids"];
const emptyForm = { name: "", description: "", brandId: "", gender: "Unisex", price: "", imageUrl: "" };

export default function AdminSneakers() {
  const { token } = useAuth();
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [brands, setBrands] = useState([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [open, setOpen] = useState(false);
  const pageSize = 10;

  async function load(nextPage = page, nextSearch = search) {
    const data = await fetchSneakers({ page: nextPage, pageSize, search: nextSearch, sort: "newest" });
    setItems(data.items || []);
    setTotal(data.totalCount || 0);
    setPage(data.page || nextPage);
  }

  useEffect(() => {
    Promise.all([load(1, ""), fetchBrands()])
      .then(([, brandList]) => setBrands(brandList || []))
      .catch(err => setError(err.message));
  }, []);

  function onChange(event) {
    setForm(current => ({ ...current, [event.target.name]: event.target.value }));
  }

  function openCreate() {
    setEditingId(null);
    setForm({ ...emptyForm, brandId: brands[0]?.id ? String(brands[0].id) : "" });
    setOpen(true);
    setError("");
  }

  async function openEdit(id) {
    setError("");
    try {
      const sneaker = await fetchSneaker(id);
      setEditingId(id);
      setForm({
        name: sneaker.name ?? "",
        description: sneaker.description ?? "",
        brandId: String(sneaker.brand?.id ?? ""),
        gender: sneaker.gender ?? "Unisex",
        price: String(sneaker.price ?? ""),
        imageUrl: sneaker.imageUrl ?? ""
      });
      setOpen(true);
    } catch (err) {
      setError(err.message);
    }
  }

  async function onSubmit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    const body = {
      name: form.name.trim(),
      description: form.description.trim() || null,
      brandId: Number(form.brandId),
      gender: form.gender,
      price: Number(form.price),
      imageUrl: form.imageUrl.trim() || null
    };
    try {
      if (editingId) await updateSneaker(token, editingId, body);
      else await createSneaker(token, body);
      setOpen(false);
      await load(editingId ? page : 1, search);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(id, name) {
    if (!window.confirm(`Remove “${name}” from the storefront?`)) return;
    setError("");
    try {
      await deleteSneaker(token, id);
      await load(page, search);
    } catch (err) {
      setError(err.message);
    }
  }

  async function onSearch(event) {
    event.preventDefault();
    try {
      await load(1, search);
    } catch (err) {
      setError(err.message);
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="ad-body ad-body-single">
      <section className="ad-content">
        <div className="ad-hello">
          <h1>Sneakers</h1>
          <button type="button" className="ad-btn ad-btn-primary" onClick={openCreate}>Add sneaker</button>
        </div>
        <form className="ad-toolbar" onSubmit={onSearch}>
          <input
            type="search"
            placeholder="Search sneakers"
            value={search}
            onChange={event => setSearch(event.target.value)}
          />
          <button type="submit" className="ad-btn">Search</button>
        </form>
        {error ? <p className="ad-error">{error}</p> : null}
        <div className="ad-card">
          <table className="ad-table">
            <thead>
              <tr>
                <th>Product</th>
                <th>Brand</th>
                <th>Gender</th>
                <th>Price</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {items.map(item => (
                <tr key={item.id}>
                  <td>
                    <div className="ad-product-cell">
                      <img src={item.imageUrl || "/images/item-1.jpg"} alt="" />
                      <span>{item.name}</span>
                    </div>
                  </td>
                  <td>{item.brand}</td>
                  <td>{item.gender}</td>
                  <td>{formatPrice(item.price)}</td>
                  <td className="ad-actions">
                    <button type="button" onClick={() => openEdit(item.id)}>Edit</button>
                    <button type="button" className="is-danger" onClick={() => onDelete(item.id, item.name)}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="ad-pager">
            <button type="button" disabled={page <= 1} onClick={() => load(page - 1, search).catch(err => setError(err.message))}>Previous</button>
            <span>Page {page} of {totalPages}</span>
            <button type="button" disabled={page >= totalPages} onClick={() => load(page + 1, search).catch(err => setError(err.message))}>Next</button>
          </div>
        </div>
      </section>

      {open ? (
        <div className="ad-modal" role="dialog" aria-modal="true" aria-labelledby="ad-sneaker-form">
          <form className="ad-modal-card" onSubmit={onSubmit}>
            <h2 id="ad-sneaker-form">{editingId ? "Edit sneaker" : "Add sneaker"}</h2>
            <label>Name<input name="name" value={form.name} onChange={onChange} required /></label>
            <label>Description<textarea name="description" value={form.description} onChange={onChange} rows={3} /></label>
            <label>
              Brand
              <select name="brandId" value={form.brandId} onChange={onChange} required>
                <option value="">Select brand</option>
                {brands.map(brand => (
                  <option key={brand.id} value={brand.id}>{brand.name}</option>
                ))}
              </select>
            </label>
            <label>
              Gender
              <select name="gender" value={form.gender} onChange={onChange}>
                {GENDERS.map(gender => <option key={gender} value={gender}>{gender}</option>)}
              </select>
            </label>
            <label>Price<input name="price" type="number" min="0.01" step="0.01" value={form.price} onChange={onChange} required /></label>
            <label>Image URL<input name="imageUrl" value={form.imageUrl} onChange={onChange} placeholder="/images/item-1.jpg" /></label>
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

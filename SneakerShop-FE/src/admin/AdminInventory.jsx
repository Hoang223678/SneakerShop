import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { fetchSneakers, fetchVariants } from "../api/catalog.js";

export default function AdminInventory() {
  const [sneakers, setSneakers] = useState([]);
  const [variants, setVariants] = useState([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([
      fetchSneakers({ page: 1, pageSize: 100, sort: "name" }),
      fetchVariants()
    ])
      .then(([sneakerPage, variantList]) => {
        setSneakers(sneakerPage.items || []);
        setVariants(variantList || []);
      })
      .catch(err => setError(err.message));
  }, []);

  const groups = useMemo(() => {
    const term = search.trim().toLowerCase();
    const bySneaker = new Map();
    for (const variant of variants) {
      if (!bySneaker.has(variant.sneakerId)) bySneaker.set(variant.sneakerId, []);
      bySneaker.get(variant.sneakerId).push(variant);
    }

    return sneakers
      .map(sneaker => ({
        sneakerId: sneaker.id,
        name: sneaker.name,
        variants: bySneaker.get(sneaker.id) || []
      }))
      .filter(group => {
        if (!term) return true;
        if (group.name.toLowerCase().includes(term)) return true;
        return group.variants.some(variant =>
          String(variant.size).includes(term) || (variant.color || "").toLowerCase().includes(term) || (variant.sku || "").toLowerCase().includes(term));
      });
  }, [sneakers, variants, search]);

  return (
    <div className="ad-body ad-body-single">
      <section className="ad-content">
        <div className="ad-hello">
          <h1>Inventory</h1>
        </div>
        <p className="ad-help">Click a sneaker name to add, edit, or delete its size/color variants.</p>
        <form className="ad-toolbar" onSubmit={event => event.preventDefault()}>
          <input
            type="search"
            placeholder="Search sneaker, size, or color"
            value={search}
            onChange={event => setSearch(event.target.value)}
          />
        </form>
        {error ? <p className="ad-error">{error}</p> : null}
        <div className="ad-name-list">
          {groups.map(group => {
            const sizes = [...new Set(group.variants.map(v => Number(v.size)))].sort((a, b) => a - b);
            const colors = [...new Set(group.variants.map(v => v.color).filter(Boolean))];
            return (
              <article className="ad-card ad-name-card" key={group.sneakerId}>
                <h2>
                  <Link to={`/admin/inventory/${group.sneakerId}`}>{group.name}</Link>
                </h2>
                <p className="ad-muted">
                  {group.variants.length} variant(s) · {sizes.length} size(s) · {colors.length} color(s)
                </p>
              </article>
            );
          })}
        </div>
      </section>
    </div>
  );
}

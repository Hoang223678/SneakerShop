import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { fetchVariants } from "../api/catalog.js";

export default function AdminInventory() {
  const [variants, setVariants] = useState([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    fetchVariants()
      .then(setVariants)
      .catch(err => setError(err.message));
  }, []);

  const groups = useMemo(() => {
    const term = search.trim().toLowerCase();
    const map = new Map();
    for (const variant of variants) {
      const sneakerName = variant.sneakerName || "Unknown sneaker";
      if (term && !sneakerName.toLowerCase().includes(term) && !String(variant.size).includes(term) && !(variant.color || "").toLowerCase().includes(term)) {
        continue;
      }
      if (!map.has(variant.sneakerId)) {
        map.set(variant.sneakerId, { sneakerId: variant.sneakerId, name: sneakerName, variants: [] });
      }
      map.get(variant.sneakerId).variants.push(variant);
    }
    return [...map.values()];
  }, [variants, search]);

  return (
    <div className="ad-body ad-body-single">
      <section className="ad-content">
        <div className="ad-hello">
          <h1>Inventory</h1>
        </div>
        <p className="ad-help">Click a sneaker name to see its details and every size/color variant.</p>
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

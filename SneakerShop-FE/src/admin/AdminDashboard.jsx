import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchSneakers, formatPrice } from "../api/catalog.js";
import { useAuth } from "../auth/AuthContext.jsx";

export default function AdminDashboard() {
  const { user } = useAuth();
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [error, setError] = useState("");

  useEffect(() => {
    fetchSneakers({ page: 1, pageSize: 12, sort: "newest" })
      .then(data => {
        setItems(data.items || []);
        setTotal(data.totalCount || 0);
      })
      .catch(err => setError(err.message));
  }, []);

  const featured = items.slice(0, 3);
  const greetingName = user?.fullName || "Admin";

  return (
    <div className="ad-body">
      <section className="ad-content">
        <div className="ad-hello">
          <h1>Hi, {greetingName}</h1>
          <Link to="/admin/sneakers">Manage sneakers</Link>
        </div>
        {error ? <p className="ad-error">{error}</p> : null}
        <div className="ad-feature-grid">
          {featured.map(item => (
            <article key={item.id} className="ad-feature-card">
              <img src={item.imageUrl || "/images/item-1.jpg"} alt={item.name} />
              <h3>{item.name}</h3>
              <p>{formatPrice(item.price)} · {item.brand}</p>
            </article>
          ))}
        </div>
        <div className="ad-card">
          <h2>Sneaker catalog</h2>
          <table className="ad-table">
            <thead>
              <tr>
                <th>Product</th>
                <th>Brand</th>
                <th>Gender</th>
                <th>Price</th>
              </tr>
            </thead>
            <tbody>
              {items.slice(0, 8).map(item => (
                <tr key={item.id}>
                  <td>{item.name}</td>
                  <td>{item.brand}</td>
                  <td>{item.gender}</td>
                  <td>{formatPrice(item.price)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      <aside className="ad-rail">
        <div className="ad-card">
          <h2>Overview</h2>
          <p className="ad-stat">{total}</p>
          <span>Active sneakers in catalog</span>
          <Link className="ad-btn ad-btn-primary" to="/admin/sneakers">Open sneaker CRUD</Link>
        </div>
        <div className="ad-card">
          <h2>Recent sneakers</h2>
          <ul className="ad-recent">
            {items.slice(0, 4).map(item => (
              <li key={item.id}>
                <img src={item.imageUrl || "/images/item-1.jpg"} alt="" />
                <div>
                  <strong>{item.name}</strong>
                  <span>{formatPrice(item.price)}</span>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </aside>
    </div>
  );
}

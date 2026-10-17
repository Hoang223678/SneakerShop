import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext.jsx";
import "./admin.css";

const nav = [
  {
    to: "/admin",
    end: true,
    label: "Dashboard",
    icon: (
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 13h7V4H4v9zm0 7h7v-5H4v5zm9 0h7V11h-7v9zm0-16v5h7V4h-7z" /></svg>
    )
  },
  {
    to: "/admin/sneakers",
    label: "Sneaker",
    icon: (
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M21.5 15.5 16 9l-3 2-4-6-7.5 10.5A3 3 0 0 0 4 20h16a3 3 0 0 0 1.5-4.5z" /></svg>
    )
  },
  {
    to: "/admin/brands",
    label: "Brand",
    icon: (
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2 2 7l10 5 10-5-10-5zm0 9L4.5 7.4 2 8.7 12 14l10-5.3-2.5-1.3L12 11zM2 12.3 12 18l10-5.7V16L12 22 2 16v-3.7z" /></svg>
    )
  }
];

export default function AdminLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  return (
    <div className="admin-shell">
      <aside className="ad-sidebar">
        <div className="ad-brand">
          <span className="ad-logo">S</span>
          <strong>SneakerShop</strong>
        </div>
        <nav className="ad-nav">
          {nav.map(item => (
            <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => `ad-nav-item${isActive ? " is-active" : ""}`}>
              {item.icon}
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="ad-sidebar-foot">
          <p>Customer Support</p>
          <span>Ask about sneakers, stock, or orders.</span>
          <NavLink to="/">View shop</NavLink>
          <button type="button" onClick={() => { logout(); navigate("/login", { replace: true }); }}>
            Log out
          </button>
        </div>
      </aside>
      <div className="ad-main">
        <header className="ad-topbar">
          <label className="ad-search">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M15.5 14h-.79l-.28-.27A6.5 6.5 0 1 0 14 15.5l.27.28v.79l5 5 1.5-1.5-5-5zm-6 0a4.5 4.5 0 1 1 0-9 4.5 4.5 0 0 1 0 9z" /></svg>
            <input type="search" placeholder="Search..." />
          </label>
          <div className="ad-user">
            <img src="/images/profile-icon.png" alt="" />
            <div>
              <strong>{user?.fullName || "Admin"}</strong>
              <span>{user?.role}</span>
            </div>
            <button type="button" className="ad-btn" onClick={() => { logout(); navigate("/login", { replace: true }); }}>
              Log out
            </button>
          </div>
        </header>
        <Outlet />
      </div>
    </div>
  );
}

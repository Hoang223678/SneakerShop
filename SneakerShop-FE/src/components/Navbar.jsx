import { useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext.jsx";

const links = [
  { to: "/", label: "Home", end: true },
  { to: "/men", label: "Men" },
  { to: "/women", label: "Women" },
  { to: "/about", label: "About" },
  { to: "/contact", label: "Contact" }
];

export default function Navbar() {
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const { user, logout } = useAuth();
  return (
    <nav className="colorlib-nav" role="navigation">
      <div className="top-menu">
        <div className="container">
          <div className="row">
            <div className="col-sm-7 col-md-9">
              <div id="colorlib-logo">
                <NavLink to="/">Footwear</NavLink>
              </div>
            </div>
            <div className="col-sm-5 col-md-3">
              <form className="search-wrap" onSubmit={event => event.preventDefault()}>
                <div className="form-group">
                  <input type="search" className="form-control search" placeholder="Search" />
                  <button className="btn btn-primary submit-search text-center" type="submit">
                    <i className="icon-search" />
                  </button>
                </div>
              </form>
            </div>
          </div>
          <div className="row">
            <div className="col-sm-12">
              <button type="button" className="nav-toggle" onClick={() => setOpen(value => !value)} aria-label="Open menu">
                Menu
              </button>
            </div>
            <div className={`col-sm-12 text-left menu-1${open ? " is-open" : ""}`}>
              <ul>
                {links.map(link => (
                  <li key={link.to} className={pathname === link.to || (link.to !== "/" && pathname.startsWith(link.to)) ? "active" : undefined}>
                    <NavLink to={link.to} end={link.end}>{link.label}</NavLink>
                  </li>
                ))}
                <li className="has-dropdown">
                  <NavLink to="/cart">Shop</NavLink>
                  <ul className="dropdown">
                    <li><NavLink to="/product/00000001-0000-0000-0000-000000000001">Product Detail</NavLink></li>
                    <li><NavLink to="/cart">Shopping Cart</NavLink></li>
                    <li><NavLink to="/checkout">Checkout</NavLink></li>
                    <li><NavLink to="/order-complete">Order Complete</NavLink></li>
                    <li><NavLink to="/wishlist">Wishlist</NavLink></li>
                  </ul>
                </li>
                <li className="cart">
                  <NavLink to="/cart"><i className="icon-shopping-cart" /> Cart [0]</NavLink>
                </li>
                <li>
                  {user ? (
                    <a href="#logout" onClick={event => { event.preventDefault(); logout(); }}>
                      {user.fullName || user.email}
                    </a>
                  ) : (
                    <NavLink to="/login">Log in</NavLink>
                  )}
                </li>
              </ul>
            </div>
          </div>
        </div>
      </div>
      <div className="sale">
        <div className="container">
          <div className="row">
            <div className="col-sm-8 offset-sm-2 text-center">
              <h3>25% off (Almost) Everything! Use Code: Summer Sale</h3>
            </div>
          </div>
        </div>
      </div>
    </nav>
  );
}

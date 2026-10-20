import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { checkoutCart } from "../api/cart.js";
import { formatPrice } from "../api/catalog.js";
import { useAuth } from "../auth/AuthContext.jsx";
import { useCart } from "../cart/CartContext.jsx";

export default function Checkout() {
  const { token, user } = useAuth();
  const { cart, refresh } = useCart();
  const navigate = useNavigate();
  const location = useLocation();
  const items = cart.items || [];
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    shippingName: user?.fullName || "",
    shippingPhone: user?.phone || "",
    shippingAddress: ""
  });

  function onChange(event) {
    setForm(current => ({ ...current, [event.target.name]: event.target.value }));
  }

  async function onSubmit(event) {
    event.preventDefault();
    if (!token) {
      navigate(`/login?from=${encodeURIComponent(location.pathname)}`);
      return;
    }
    setBusy(true);
    setError("");
    try {
      const result = await checkoutCart(token, {
        shippingName: form.shippingName.trim(),
        shippingPhone: form.shippingPhone.trim(),
        shippingAddress: form.shippingAddress.trim(),
        method: "vnpay"
      }, crypto.randomUUID());
      await refresh().catch(() => {});
      const url = result.payment?.paymentUrl;
      if (url) {
        window.location.assign(url);
        return;
      }
      setError("Order is still confirming stock. Wait a moment and try again.");
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>Checkout</span></p>
            </div>
          </div>
        </div>
      </div>
      <div className="colorlib-product">
        <div className="container">
          <div className="row row-pb-lg">
            <div className="col-md-10 offset-md-1">
              <div className="process-wrap">
                <div className="process text-center"><p><span>01</span></p><h3>Shopping Cart</h3></div>
                <div className="process text-center active"><p><span>02</span></p><h3>Checkout</h3></div>
                <div className="process text-center"><p><span>03</span></p><h3>Order Complete</h3></div>
              </div>
            </div>
          </div>
          {!token ? (
            <div className="row">
              <div className="col-md-12 text-center">
                <p>Log in to checkout with VNPay.</p>
                <p>
                  <Link className="btn btn-primary" to={`/login?from=${encodeURIComponent("/checkout")}`}>Log in</Link>
                </p>
              </div>
            </div>
          ) : items.length === 0 ? (
            <div className="row">
              <div className="col-md-12 text-center">
                <p>Your cart is empty.</p>
                <p><Link to="/cart" className="btn btn-primary">Back to cart</Link></p>
              </div>
            </div>
          ) : (
            <form className="row" onSubmit={onSubmit}>
              <div className="col-lg-8">
                <div className="colorlib-form">
                  <h2>Billing details</h2>
                  {error ? <p className="text-danger">{error}</p> : null}
                  <div className="form-group">
                    <label htmlFor="shippingName">Full name</label>
                    <input id="shippingName" name="shippingName" className="form-control" required value={form.shippingName} onChange={onChange} />
                  </div>
                  <div className="form-group">
                    <label htmlFor="shippingPhone">Phone</label>
                    <input id="shippingPhone" name="shippingPhone" className="form-control" required value={form.shippingPhone} onChange={onChange} />
                  </div>
                  <div className="form-group">
                    <label htmlFor="shippingAddress">Address</label>
                    <input id="shippingAddress" name="shippingAddress" className="form-control" required value={form.shippingAddress} onChange={onChange} />
                  </div>
                  <div className="form-group">
                    <label>Payment method</label>
                    <p className="mb-0"><strong>VNPay</strong> — you will be redirected to the VNPay sandbox to pay.</p>
                  </div>
                </div>
              </div>
              <div className="col-lg-4">
                <div className="cart-detail">
                  <h2>Cart total</h2>
                  <ul>
                    {items.map(item => (
                      <li key={item.variantId}>
                        <span>{item.sneakerName} × {item.quantity}</span>
                        <span>{item.unitPrice != null ? formatPrice(item.unitPrice * item.quantity) : "—"}</span>
                      </li>
                    ))}
                    <li>
                      <span>Subtotal</span>
                      <span>{formatPrice(cart.subtotal || 0)}</span>
                    </li>
                  </ul>
                  <button type="submit" className="btn btn-buynow btn-block" disabled={busy}>
                    {busy ? "Redirecting to VNPay…" : "Pay with VNPay"}
                  </button>
                </div>
              </div>
            </form>
          )}
        </div>
      </div>
    </>
  );
}

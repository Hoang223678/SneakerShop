import { Link, useLocation, useNavigate } from "react-router-dom";
import { formatPrice } from "../api/catalog.js";
import { useAuth } from "../auth/AuthContext.jsx";
import { useCart } from "../cart/CartContext.jsx";

export default function Cart() {
  const { token } = useAuth();
  const { cart, error, busy, updateItem, removeItem } = useCart();
  const navigate = useNavigate();
  const location = useLocation();
  const items = cart.items || [];

  function goLogin() {
    navigate(`/login?from=${encodeURIComponent(location.pathname)}`);
  }

  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>Shopping Cart</span></p>
            </div>
          </div>
        </div>
      </div>
      <div className="colorlib-product">
        <div className="container">
          <div className="row row-pb-lg">
            <div className="col-md-10 offset-md-1">
              <div className="process-wrap">
                <div className="process text-center active"><p><span>01</span></p><h3>Shopping Cart</h3></div>
                <div className="process text-center"><p><span>02</span></p><h3>Checkout</h3></div>
                <div className="process text-center"><p><span>03</span></p><h3>Order Complete</h3></div>
              </div>
            </div>
          </div>
          {!token ? (
            <div className="row">
              <div className="col-md-12 text-center">
                <p>Log in to see your cart.</p>
                <p><button type="button" className="btn btn-primary" onClick={goLogin}>Log in</button></p>
              </div>
            </div>
          ) : (
            <>
              {error ? <p className="text-center text-danger">{error}</p> : null}
              {items.length === 0 ? (
                <div className="row">
                  <div className="col-md-12 text-center">
                    <p>Your cart is empty.</p>
                    <p><Link to="/men" className="btn btn-primary">Continue shopping</Link></p>
                  </div>
                </div>
              ) : (
                <>
                  <div className="row row-pb-lg">
                    <div className="col-md-12">
                      <div className="product-name">
                        <div className="one-forth text-left"><span>Product Details</span></div>
                        <div className="one-eight text-center"><span>Price</span></div>
                        <div className="one-eight text-center"><span>Quantity</span></div>
                        <div className="one-eight text-center"><span>Total</span></div>
                        <div className="one-eight text-center"><span>Remove</span></div>
                      </div>
                      {items.map(item => (
                        <div className="product-cart" key={item.variantId}>
                          <div className="one-forth">
                            <div className="display-tc">
                              <h3>
                                {item.sneakerId ? <Link to={`/product/${item.sneakerId}`}>{item.sneakerName || "Sneaker"}</Link> : (item.sneakerName || "Sneaker")}
                                <small> Size {Number(item.size)} · {item.color}{item.available ? "" : " · unavailable"}</small>
                              </h3>
                            </div>
                          </div>
                          <div className="one-eight text-center">
                            <div className="display-tc">
                              <span className="price">{item.unitPrice != null ? formatPrice(item.unitPrice) : "—"}</span>
                            </div>
                          </div>
                          <div className="one-eight text-center">
                            <div className="display-tc">
                              <input
                                type="number"
                                min="1"
                                className="form-control"
                                disabled={busy}
                                value={item.quantity}
                                onChange={event => {
                                  const next = Number(event.target.value);
                                  if (Number.isFinite(next) && next >= 1) updateItem(item.variantId, next).catch(() => {});
                                }}
                              />
                            </div>
                          </div>
                          <div className="one-eight text-center">
                            <div className="display-tc">
                              <span className="price">{item.unitPrice != null ? formatPrice(item.unitPrice * item.quantity) : "—"}</span>
                            </div>
                          </div>
                          <div className="one-eight text-center">
                            <div className="display-tc">
                              <button type="button" className="closed" aria-label="Remove" disabled={busy} onClick={() => removeItem(item.variantId).catch(() => {})} />
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                  <div className="row">
                    <div className="col-sm-8 offset-sm-4">
                      <div className="total-wrap">
                        <div className="row">
                          <div className="col-sm-8 text-center">
                            <div className="total">
                              <div className="grand-total">
                                <p><span><strong>Subtotal:</strong></span> <span>{formatPrice(cart.subtotal || 0)}</span></p>
                              </div>
                            </div>
                            <p className="mt-3"><Link to="/checkout" className="btn btn-primary">Checkout with VNPay</Link></p>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </>
              )}
            </>
          )}
        </div>
      </div>
    </>
  );
}

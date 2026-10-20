import { useEffect, useMemo, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { fetchSneaker, formatPrice } from "../api/catalog.js";
import { fetchAvailability } from "../api/inventory.js";
import { useAuth } from "../auth/AuthContext.jsx";
import { useCart } from "../cart/CartContext.jsx";

export default function ProductDetail() {
  const { id } = useParams();
  const { token } = useAuth();
  const { addItems, busy } = useCart();
  const navigate = useNavigate();
  const location = useLocation();
  const [sneaker, setSneaker] = useState(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [selectedSize, setSelectedSize] = useState(null);
  const [selectedColor, setSelectedColor] = useState("");
  const [stockByVariant, setStockByVariant] = useState({});

  useEffect(() => {
    setError("");
    setMessage("");
    setSneaker(null);
    fetchSneaker(id)
      .then(async detail => {
        setSneaker(detail);
        const variants = detail.variants || [];
        if (variants[0]) {
          setSelectedSize(Number(variants[0].size));
          setSelectedColor(variants[0].color || "");
        }
        if (variants.length) {
          const availability = await fetchAvailability(variants.map(v => v.id)).catch(() => []);
          const map = {};
          for (const row of availability || []) map[row.variantId] = row;
          setStockByVariant(map);
        }
      })
      .catch(() => setError("Could not load this sneaker."));
  }, [id]);

  const variants = sneaker?.variants || [];
  const sizes = useMemo(() => [...new Set(variants.map(v => Number(v.size)))].sort((a, b) => a - b), [variants]);
  const colors = useMemo(
    () => [...new Set(variants.filter(v => Number(v.size) === selectedSize).map(v => v.color).filter(Boolean))],
    [variants, selectedSize]
  );
  const selected = variants.find(v => Number(v.size) === selectedSize && (v.color || "") === selectedColor) || null;
  const stock = selected ? stockByVariant[selected.id] : null;
  const available = stock?.available ?? 0;

  function requireLogin() {
    if (token) return true;
    navigate(`/login?from=${encodeURIComponent(location.pathname + location.search)}`);
    return false;
  }

  async function addSelected() {
    if (!selected) {
      setError("Choose a size and color.");
      return null;
    }
    if (stock && !stock.inStock) {
      setError("This size is out of stock.");
      return null;
    }
    const qty = Math.max(1, Number(quantity) || 1);
    setError("");
    setMessage("");
    return addItems([{ variantId: selected.id, quantity: qty }]);
  }

  async function onAddToCart() {
    if (!requireLogin()) return;
    try {
      await addSelected();
      setMessage("Added to cart.");
    } catch (err) {
      setError(err.message);
    }
  }

  async function onBuyNow() {
    if (!requireLogin()) return;
    try {
      await addSelected();
      navigate("/checkout");
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>Product Details</span></p>
            </div>
          </div>
        </div>
      </div>

      <div className="colorlib-product">
        <div className="container">
          {error && <p className="text-center text-danger">{error}</p>}
          {message && <p className="text-center">{message}</p>}
          {!sneaker && !error && <p className="text-center">Loading...</p>}
          {sneaker && (
            <div className="row row-pb-lg product-detail-wrap">
              <div className="col-12 col-md-7">
                <div className="product-detail-image">
                  <img src={sneaker.imageUrl || "/images/item-1.jpg"} alt={sneaker.name} />
                </div>
              </div>
              <div className="col-12 col-md-5">
                <div className="product-desc">
                  <h3>{sneaker.name}</h3>
                  <p className="price"><span>{formatPrice(sneaker.price)}</span></p>
                  <p>{sneaker.description}</p>
                  <div className="size-wrap">
                    <div className="block-26 mb-2">
                      <h4>Size</h4>
                      <ul>
                        {sizes.map(size => (
                          <li key={size} className={selectedSize === size ? "is-active" : undefined}>
                            <button
                              type="button"
                              onClick={() => {
                                setSelectedSize(size);
                                const next = variants.find(v => Number(v.size) === size);
                                setSelectedColor(next?.color || "");
                              }}
                            >
                              {size}
                            </button>
                          </li>
                        ))}
                      </ul>
                    </div>
                    {colors.length > 0 ? (
                      <div className="block-26 mb-2">
                        <h4>Color</h4>
                        <ul className="color-options">
                          {colors.map(color => (
                            <li key={color} className={selectedColor === color ? "is-active" : undefined} style={{ width: "auto", padding: "0 10px" }}>
                              <button type="button" onClick={() => setSelectedColor(color)}>{color}</button>
                            </li>
                          ))}
                        </ul>
                      </div>
                    ) : null}
                  </div>
                  {selected ? (
                    <p className="mb-3">
                      {stock?.inStock ? `${available} pair(s) available` : "Out of stock"}
                      {selected.sku ? ` · SKU ${selected.sku}` : ""}
                    </p>
                  ) : (
                    <p className="mb-3">Choose a size to add this sneaker to your cart.</p>
                  )}
                  <div className="input-group mb-4">
                    <button type="button" className="quantity-left-minus btn" onClick={() => setQuantity(q => Math.max(1, q - 1))}>
                      <i className="icon-minus2" />
                    </button>
                    <input type="text" className="form-control input-number" value={quantity} readOnly />
                    <button
                      type="button"
                      className="quantity-right-plus btn"
                      onClick={() => setQuantity(q => available > 0 ? Math.min(available, q + 1) : q + 1)}
                    >
                      <i className="icon-plus2" />
                    </button>
                  </div>
                  <div className="product-actions">
                    <button type="button" className="btn btn-primary btn-addtocart" disabled={busy || !selected} onClick={onAddToCart}>
                      <i className="icon-shopping-cart" /> Add to Cart
                    </button>
                    <button type="button" className="btn btn-buynow" disabled={busy || !selected} onClick={onBuyNow}>
                      Mua ngay
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

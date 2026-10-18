import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { fetchSneaker, formatPrice } from "../api/catalog.js";

export default function ProductDetail() {
  const { id } = useParams();
  const [sneaker, setSneaker] = useState(null);
  const [error, setError] = useState("");
  const [quantity, setQuantity] = useState(1);

  useEffect(() => {
    setError("");
    setSneaker(null);
    fetchSneaker(id)
      .then(setSneaker)
      .catch(() => setError("Could not load this sneaker."));
  }, [id]);

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
          {error && <p className="text-center">{error}</p>}
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
                        {(sneaker.variants || []).map(variant => (
                          <li key={variant.id}><a href="#">{Number(variant.size)}</a></li>
                        ))}
                      </ul>
                    </div>
                  </div>
                  <div className="input-group mb-4">
                    <button type="button" className="quantity-left-minus btn" onClick={() => setQuantity(q => Math.max(1, q - 1))}>
                      <i className="icon-minus2" />
                    </button>
                    <input type="text" className="form-control input-number" value={quantity} readOnly />
                    <button type="button" className="quantity-right-plus btn" onClick={() => setQuantity(q => q + 1)}>
                      <i className="icon-plus2" />
                    </button>
                  </div>
                  <p className="addtocart">
                    <Link to="/cart" className="btn btn-primary btn-addtocart"><i className="icon-shopping-cart" /> Add to Cart</Link>
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

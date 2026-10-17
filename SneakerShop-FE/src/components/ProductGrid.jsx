import { Link } from "react-router-dom";
import { formatPrice } from "../api/catalog.js";

export default function ProductGrid({ items, loading, error }) {
  if (loading) {
    return <div className="col-12 text-center"><p>Loading sneakers...</p></div>;
  }
  if (error) {
    return <div className="col-12 text-center"><p>{error}</p></div>;
  }
  if (!items?.length) {
    return <div className="col-12 text-center"><p>No products found.</p></div>;
  }

  return items.map(item => (
    <div className="col-md-3 col-lg-3 mb-4 text-center" key={item.id}>
      <div className="product-entry border">
        <Link to={`/product/${item.id}`} className="prod-img">
          <img src={item.imageUrl || "/images/item-1.jpg"} className="img-fluid" alt={item.name} />
        </Link>
        <div className="desc">
          <h2><Link to={`/product/${item.id}`}>{item.name}</Link></h2>
          <span className="price">{formatPrice(item.price)}</span>
        </div>
      </div>
    </div>
  ));
}

import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchSneakers } from "../api/catalog.js";
import Pagination from "../components/Pagination.jsx";
import ProductGrid from "../components/ProductGrid.jsx";

export default function Collection({ gender, title }) {
  const [page, setPage] = useState(1);
  const [data, setData] = useState({ items: [], totalPages: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    setPage(1);
  }, [gender]);

  useEffect(() => {
    setLoading(true);
    setError("");
    fetchSneakers({ gender, page, pageSize: 12 })
      .then(result => setData({ items: result.items || [], totalPages: result.totalPages || 0 }))
      .catch(() => setError(`Could not load ${title.toLowerCase()}'s sneakers. Is Catalog running on localhost:5002?`))
      .finally(() => setLoading(false));
  }, [gender, page, title]);

  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>{title}</span></p>
            </div>
          </div>
        </div>
      </div>

      <div className="breadcrumbs-two">
        <div className="container">
          <div className="row">
            <div className="col">
              <div className="breadcrumbs-img" style={{ backgroundImage: "url(/images/cover-img-1.jpg)" }}>
                <h2>{title}'s</h2>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="colorlib-product">
        <div className="container">
          <div className="row">
            <div className="col-sm-8 offset-sm-2 text-center colorlib-heading colorlib-heading-sm">
              <h2>View All Products</h2>
            </div>
          </div>
          <div id="products" className="row row-pb-md">
            <ProductGrid items={data.items} loading={loading} error={error} />
          </div>
          <div className="row">
            <div className="col-md-12 text-center">
              <Pagination page={page} totalPages={data.totalPages} onPage={setPage} />
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

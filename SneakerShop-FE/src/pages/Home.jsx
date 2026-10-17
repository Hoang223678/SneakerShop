import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchSneakers } from "../api/catalog.js";
import ProductGrid from "../components/ProductGrid.jsx";

const slides = [
  { image: "/images/img_bg_1.jpg", title: ["Men's", "Shoes", "Collection"], to: "/men", label: "New trending shoes" },
  { image: "/images/img_bg_2.jpg", title: ["Huge", "Sale", "50% Off"], to: "/women", label: "Big sale sandals" },
  { image: "/images/img_bg_3.jpg", title: ["New", "Arrival", "up to 30% off"], to: "/men", label: "New stylish shoes for men" }
];

export default function Home() {
  const [index, setIndex] = useState(0);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const timer = setInterval(() => setIndex(current => (current + 1) % slides.length), 5000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    fetchSneakers({ pageSize: 8, sort: "newest" })
      .then(data => setItems(data.items || []))
      .catch(() => setError("Could not load sneakers. Is Catalog running?"))
      .finally(() => setLoading(false));
  }, []);

  const slide = slides[index];

  return (
    <>
      <aside id="colorlib-hero">
        <div className="flexslider">
          <ul className="slides">
            <li style={{ backgroundImage: `url(${slide.image})`, display: "block" }}>
              <div className="overlay" />
              <div className="container-fluid">
                <div className="row">
                  <div className="col-sm-6 offset-sm-3 text-center slider-text">
                    <div className="slider-text-inner">
                      <div className="desc">
                        <h1 className="head-1">{slide.title[0]}</h1>
                        <h2 className="head-2">{slide.title[1]}</h2>
                        <h2 className="head-3">{slide.title[2]}</h2>
                        <p className="category"><span>{slide.label}</span></p>
                        <p><Link to={slide.to} className="btn btn-primary">Shop Collection</Link></p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </li>
          </ul>
        </div>
      </aside>

      <div className="colorlib-intro">
        <div className="container">
          <div className="row">
            <div className="col-sm-12 text-center">
              <h2 className="intro">It started with a simple idea: Create quality, well-designed products that I wanted myself.</h2>
            </div>
          </div>
        </div>
      </div>

      <div className="colorlib-product">
        <div className="container-fluid">
          <div className="row">
            <div className="col-sm-6 text-center">
              <div className="featured">
                <Link to="/men" className="featured-img" style={{ backgroundImage: "url(/images/men.jpg)" }} />
                <div className="desc">
                  <h2><Link to="/men">Shop Men's Collection</Link></h2>
                </div>
              </div>
            </div>
            <div className="col-sm-6 text-center">
              <div className="featured">
                <Link to="/women" className="featured-img" style={{ backgroundImage: "url(/images/women.jpg)" }} />
                <div className="desc">
                  <h2><Link to="/women">Shop Women's Collection</Link></h2>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="colorlib-product">
        <div className="container">
          <div className="row">
            <div className="col-sm-8 offset-sm-2 text-center colorlib-heading">
              <h2>Best Sellers</h2>
            </div>
          </div>
          <div className="row row-pb-md">
            <ProductGrid items={items} loading={loading} error={error} />
          </div>
          <div className="row">
            <div className="col-md-12 text-center">
              <p><Link to="/men" className="btn btn-primary btn-lg">Shop All Products</Link></p>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

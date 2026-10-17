import { Link } from "react-router-dom";

export default function Wishlist() {
  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>Wishlist</span></p>
            </div>
          </div>
        </div>
      </div>
      <div className="colorlib-product">
        <div className="container">
          <div className="row">
            <div className="col-md-12 text-center">
              <p>Your wishlist is empty.</p>
              <p><Link to="/men" className="btn btn-primary">Browse Men's</Link></p>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

import { Link } from "react-router-dom";

export default function OrderComplete() {
  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>Order Complete</span></p>
            </div>
          </div>
        </div>
      </div>
      <div className="colorlib-product">
        <div className="container">
          <div className="row">
            <div className="col-sm-10 offset-sm-1 text-center">
              <p><span className="icon"><i className="icon-check" /></span></p>
              <h2>Thank you for purchasing, Your order is complete</h2>
              <p><Link to="/" className="btn btn-primary">Home</Link> <Link to="/cart" className="btn btn-outline">Continue Shopping</Link></p>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

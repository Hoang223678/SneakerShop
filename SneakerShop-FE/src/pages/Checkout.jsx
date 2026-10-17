import { Link } from "react-router-dom";

export default function Checkout() {
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
          <div className="row">
            <div className="col-md-12 text-center">
              <p>Pay the current cart through the backend checkout API.</p>
              <p><Link to="/order-complete" className="btn btn-primary">Place Order</Link></p>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

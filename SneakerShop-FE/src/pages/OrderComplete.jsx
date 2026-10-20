import { Link, useSearchParams } from "react-router-dom";

export default function OrderComplete() {
  const [params] = useSearchParams();
  const status = params.get("status");
  const attemptId = params.get("attemptId");
  const failed = status === "failed";

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
              <p><span className="icon"><i className={failed ? "icon-close2" : "icon-check"} /></span></p>
              <h2>
                {failed
                  ? "VNPay payment was not confirmed"
                  : "Thank you for purchasing, your order is complete"}
              </h2>
              {attemptId ? <p>Transaction: {attemptId}</p> : null}
              <p>
                <Link to="/" className="btn btn-primary">Home</Link>{" "}
                <Link to={failed ? "/checkout" : "/men"} className="btn btn-outline">
                  {failed ? "Try checkout again" : "Continue shopping"}
                </Link>
              </p>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

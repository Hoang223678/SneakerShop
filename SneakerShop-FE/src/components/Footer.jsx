import { Link } from "react-router-dom";

export default function Footer() {
  return (
    <footer id="colorlib-footer" role="contentinfo">
      <div className="container">
        <div className="row row-pb-md">
          <div className="col footer-col colorlib-widget">
            <h4>About Footwear</h4>
            <p>Create quality, well-designed sneakers with a simple checkout from cart to VNPay.</p>
            <ul className="colorlib-social-icons">
              <li><a href="#"><i className="icon-twitter" /></a></li>
              <li><a href="#"><i className="icon-facebook" /></a></li>
              <li><a href="#"><i className="icon-linkedin" /></a></li>
            </ul>
          </div>
          <div className="col footer-col colorlib-widget">
            <h4>Customer Care</h4>
            <ul className="colorlib-footer-links">
              <li><Link to="/contact">Contact</Link></li>
              <li><Link to="/wishlist">Wishlist</Link></li>
              <li><Link to="/cart">Cart</Link></li>
            </ul>
          </div>
          <div className="col footer-col colorlib-widget">
            <h4>Information</h4>
            <ul className="colorlib-footer-links">
              <li><Link to="/about">About us</Link></li>
              <li><Link to="/contact">Support</Link></li>
            </ul>
          </div>
          <div className="col footer-col">
            <h4>Contact Information</h4>
            <ul className="colorlib-footer-links">
              <li>291 South 21th Street, <br /> Suite 721 New York NY 10016</li>
              <li><a href="tel://1234567920">+ 1235 2355 98</a></li>
              <li><a href="mailto:info@yoursite.com">info@yoursite.com</a></li>
            </ul>
          </div>
        </div>
      </div>
      <div className="copy">
        <div className="row">
          <div className="col-sm-12 text-center">
            <p>
              <span>Copyright &copy; {new Date().getFullYear()} Footwear. Template by Colorlib.</span>
            </p>
          </div>
        </div>
      </div>
    </footer>
  );
}

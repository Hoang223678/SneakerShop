import { Link } from "react-router-dom";

export default function Contact() {
  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>Contact</span></p>
            </div>
          </div>
        </div>
      </div>
      <div id="colorlib-contact">
        <div className="container">
          <div className="row">
            <div className="col-sm-12">
              <h3>Contact Information</h3>
              <div className="row contact-info-wrap">
                <div className="col-md-3"><p>198 West 21th Street, Suite 721 New York NY 10016</p></div>
                <div className="col-md-3"><p>+ 1235 2355 98</p></div>
                <div className="col-md-3"><p>info@yoursite.com</p></div>
                <div className="col-md-3"><p>yoursite.com</p></div>
              </div>
            </div>
          </div>
          <div className="row">
            <div className="col-md-6">
              <div className="contact-wrap">
                <h3>Get In Touch</h3>
                <form className="contact-form" onSubmit={event => event.preventDefault()}>
                  <div className="form-group">
                    <label htmlFor="fname">First Name</label>
                    <input type="text" id="fname" className="form-control" placeholder="Your firstname" />
                  </div>
                  <div className="form-group">
                    <label htmlFor="email">Email</label>
                    <input type="email" id="email" className="form-control" placeholder="Your email address" />
                  </div>
                  <div className="form-group">
                    <input type="submit" value="Send Message" className="btn btn-primary" />
                  </div>
                </form>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

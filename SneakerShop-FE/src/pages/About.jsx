import { Link } from "react-router-dom";

export default function About() {
  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>About</span></p>
            </div>
          </div>
        </div>
      </div>
      <div className="colorlib-about">
        <div className="container">
          <div className="row row-pb-lg">
            <div className="col-sm-6 mb-3">
              <div className="video colorlib-video" style={{ backgroundImage: "url(/images/about.jpg)" }}>
                <div className="overlay" />
              </div>
            </div>
            <div className="col-sm-6">
              <div className="about-wrap">
                <h2>Footwear the leading eCommerce Store around the Globe</h2>
                <p>The Big Oxmox advised her not to do so, because there were thousands of bad Commas, wild Question Marks and devious Semikoli, but the Little Blind Text didn’t listen.</p>
                <p>When she reached the first hills of the Italic Mountains, she had a last view back on the skyline of her hometown Bookmarksgrove.</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

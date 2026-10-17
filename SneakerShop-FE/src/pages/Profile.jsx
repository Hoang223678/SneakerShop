import { useEffect, useState } from "react";
import { Link, Navigate, useNavigate } from "react-router-dom";
import { fetchMe } from "../api/auth.js";
import { useAuth } from "../auth/AuthContext.jsx";

export default function Profile() {
  const { token, logout } = useAuth();
  const navigate = useNavigate();
  const [profile, setProfile] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    setLoading(true);
    fetchMe(token)
      .then(data => {
        if (!cancelled) setProfile(data);
      })
      .catch(err => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [token]);

  if (!token) return <Navigate to="/login" replace />;

  return (
    <>
      <div className="breadcrumbs">
        <div className="container">
          <div className="row">
            <div className="col">
              <p className="bread"><span><Link to="/">Home</Link></span> / <span>Profile</span></p>
            </div>
          </div>
        </div>
      </div>
      <div className="colorlib-product">
        <div className="container">
          <div className="row">
            <div className="col-lg-8 offset-lg-2">
              <div className="profile-card">
                <div className="profile-card-head">
                  <img src="/images/profile-icon.png" alt="" width="72" height="72" />
                  <div>
                    <h2>{profile?.fullName || "Your profile"}</h2>
                    <p>{loading ? "Loading from Identity…" : error || profile?.email}</p>
                  </div>
                </div>
                {profile ? (
                  <dl className="profile-fields">
                    <div><dt>Full name</dt><dd>{profile.fullName}</dd></div>
                    <div><dt>Email</dt><dd>{profile.email}</dd></div>
                    <div><dt>Phone</dt><dd>{profile.phone || "—"}</dd></div>
                    <div><dt>Role</dt><dd>{profile.role}</dd></div>
                    <div><dt>User id</dt><dd>{profile.id}</dd></div>
                  </dl>
                ) : null}
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => { logout(); navigate("/", { replace: true }); }}
                >
                  Log out
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

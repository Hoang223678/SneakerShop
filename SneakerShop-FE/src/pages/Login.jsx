import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { login, register } from "../api/auth.js";
import { useAuth } from "../auth/AuthContext.jsx";
import "./login.css";

export default function Login() {
  const { setSession } = useAuth();
  const navigate = useNavigate();
  const [tab, setTab] = useState("login");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  function selectTab(next, focus) {
    setTab(next);
    setError("");
    if (focus) document.getElementById(next === "login" ? "tb-tab-login" : "tb-tab-signup")?.focus();
  }

  function onTabKeyDown(event, current) {
    const order = ["login", "signup"];
    const i = order.indexOf(current);
    const nextIndex = { ArrowRight: i + 1, ArrowLeft: i - 1, Home: 0, End: order.length - 1 }[event.key];
    if (nextIndex === undefined) return;
    event.preventDefault();
    selectTab(order[(nextIndex + order.length) % order.length], true);
  }

  async function onLogin(event) {
    event.preventDefault();
    setError("");
    setBusy(true);
    const form = new FormData(event.currentTarget);
    try {
      const session = await login({
        email: String(form.get("email") ?? ""),
        password: String(form.get("password") ?? "")
      });
      setSession(session);
      navigate("/", { replace: true });
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function onSignup(event) {
    event.preventDefault();
    setError("");
    setBusy(true);
    const form = new FormData(event.currentTarget);
    try {
      const session = await register({
        fullName: String(form.get("name") ?? ""),
        email: String(form.get("email") ?? ""),
        password: String(form.get("new-password") ?? "")
      });
      setSession(session);
      navigate("/", { replace: true });
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="tb">
      <div className="tb-card">
        <div className="tb-tabs" role="tablist" aria-label="Log in or create an account">
          <button
            type="button"
            role="tab"
            id="tb-tab-login"
            aria-selected={tab === "login"}
            aria-controls="tb-panel-login"
            tabIndex={tab === "login" ? 0 : -1}
            onClick={() => selectTab("login", false)}
            onKeyDown={event => onTabKeyDown(event, "login")}
          >
            Log in
          </button>
          <button
            type="button"
            role="tab"
            id="tb-tab-signup"
            aria-selected={tab === "signup"}
            aria-controls="tb-panel-signup"
            tabIndex={tab === "signup" ? 0 : -1}
            onClick={() => selectTab("signup", false)}
            onKeyDown={event => onTabKeyDown(event, "signup")}
          >
            Create account
          </button>
        </div>

        <section role="tabpanel" id="tb-panel-login" aria-labelledby="tb-tab-login" hidden={tab !== "login"}>
          <h1>Welcome back</h1>
          <p className="tb-sub">Log in to manage your projects.</p>
          {error && tab === "login" ? <p className="tb-error" role="alert">{error}</p> : null}
          <form onSubmit={onLogin}>
            <label className="tb-label" htmlFor="tb-li-email">Email</label>
            <input className="tb-input" type="email" id="tb-li-email" name="email" placeholder="you@example.com" autoComplete="username" required />
            <label className="tb-label" htmlFor="tb-li-password">Password</label>
            <input className="tb-input" type="password" id="tb-li-password" name="password" autoComplete="current-password" required />
            <button className="tb-go" type="submit" disabled={busy}>{busy ? "Signing in…" : "Log in"}</button>
          </form>
          <p className="tb-small"><Link to="/">Back to shop</Link></p>
        </section>

        <section role="tabpanel" id="tb-panel-signup" aria-labelledby="tb-tab-signup" hidden={tab !== "signup"}>
          <h1>Create your account</h1>
          <p className="tb-sub">Free for 14 days. No credit card needed.</p>
          {error && tab === "signup" ? <p className="tb-error" role="alert">{error}</p> : null}
          <form onSubmit={onSignup}>
            <label className="tb-label" htmlFor="tb-su-name">Full name</label>
            <input className="tb-input" type="text" id="tb-su-name" name="name" placeholder="Ada Lovelace" autoComplete="name" required />
            <label className="tb-label" htmlFor="tb-su-email">Email</label>
            <input className="tb-input" type="email" id="tb-su-email" name="email" placeholder="you@example.com" autoComplete="email" required />
            <label className="tb-label" htmlFor="tb-su-password">Password</label>
            <input className="tb-input" type="password" id="tb-su-password" name="new-password" autoComplete="new-password" minLength={8} aria-describedby="tb-su-help" required />
            <p className="tb-help" id="tb-su-help">At least 8 characters.</p>
            <button className="tb-go" type="submit" disabled={busy}>{busy ? "Creating…" : "Create account"}</button>
          </form>
          <p className="tb-small">By signing up you agree to the <a href="#">Terms</a>.</p>
        </section>
      </div>
    </div>
  );
}

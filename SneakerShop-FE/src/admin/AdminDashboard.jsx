import { useEffect, useMemo, useState } from "react";
import { formatPrice } from "../api/catalog.js";
import { fetchOrderDashboard } from "../api/orders.js";
import { useAuth } from "../auth/AuthContext.jsx";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function percentChange(current, previous) {
  if (!previous) return current ? 100 : 0;
  return Math.round(((current - previous) / previous) * 100);
}

function formatCount(value) {
  return new Intl.NumberFormat("en-US").format(value || 0);
}

function Sparkline({ points, highlightIndex }) {
  const width = 640;
  const height = 220;
  const padX = 28;
  const padY = 28;
  const values = points.length ? points : [0];
  const max = Math.max(...values, 1);
  const step = values.length > 1 ? (width - padX * 2) / (values.length - 1) : 0;
  const coords = values.map((value, index) => {
    const x = padX + index * step;
    const y = height - padY - (value / max) * (height - padY * 2);
    return { x, y, value };
  });
  const d = coords.map((point, index) => `${index === 0 ? "M" : "L"}${point.x},${point.y}`).join(" ");
  const area = `${d} L${coords.at(-1).x},${height - padY} L${coords[0].x},${height - padY} Z`;
  const mark = coords[Math.min(Math.max(highlightIndex, 0), coords.length - 1)];

  return (
    <svg className="ad-spark" viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Monthly revenue">
      <defs>
        <linearGradient id="adChartStroke" x1="0" x2="1">
          <stop offset="0%" stopColor="#f59e0b" />
          <stop offset="100%" stopColor="#ef4444" />
        </linearGradient>
        <linearGradient id="adChartFill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="rgba(249,115,22,.22)" />
          <stop offset="100%" stopColor="rgba(239,68,68,0)" />
        </linearGradient>
      </defs>
      <path d={area} fill="url(#adChartFill)" />
      <path d={d} fill="none" stroke="url(#adChartStroke)" strokeWidth="4" strokeLinecap="round" />
      {mark ? (
        <>
          <line x1={mark.x} y1={padY / 2} x2={mark.x} y2={height - padY} stroke="#fb923c" strokeWidth="2" />
          <circle cx={mark.x} cy={mark.y} r="7" fill="#fff" stroke="#fb923c" strokeWidth="4" />
          <rect x={mark.x - 28} y={mark.y - 36} width="56" height="24" rx="8" fill="#fff" stroke="#fed7aa" />
          <text x={mark.x} y={mark.y - 19} textAnchor="middle" fontSize="12" fontWeight="700" fill="#ea580c">
            {formatCount(Math.round(mark.value))}
          </text>
        </>
      ) : null}
    </svg>
  );
}

function MonthCalendar({ year, month }) {
  const today = new Date();
  const first = new Date(year, month - 1, 1);
  const startPad = (first.getDay() + 6) % 7;
  const daysInMonth = new Date(year, month, 0).getDate();
  const cells = [
    ...Array.from({ length: startPad }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1)
  ];
  while (cells.length % 7 !== 0) cells.push(null);

  return (
    <div className="ad-cal">
      <div className="ad-cal-week">
        {WEEKDAYS.map(day => <span key={day}>{day}</span>)}
      </div>
      <div className="ad-cal-grid">
        {cells.map((day, index) => {
          const isToday = day
            && today.getFullYear() === year
            && today.getMonth() + 1 === month
            && today.getDate() === day;
          return (
            <span key={index} className={isToday ? "is-today" : undefined}>{day || ""}</span>
          );
        })}
      </div>
    </div>
  );
}

export default function AdminDashboard() {
  const { token } = useAuth();
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [data, setData] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setBusy(true);
    fetchOrderDashboard(token, year)
      .then(result => {
        if (!cancelled) {
          setData(result);
          setError("");
        }
      })
      .catch(err => {
        if (!cancelled) {
          setData(null);
          setError(err.message);
        }
      })
      .finally(() => {
        if (!cancelled) setBusy(false);
      });
    return () => { cancelled = true; };
  }, [token, year]);

  const monthIndex = (data?.month || now.getMonth() + 1) - 1;
  const points = useMemo(
    () => (data?.monthly || Array.from({ length: 12 }, () => ({ revenue: 0 }))).map(point => Number(point.revenue) || 0),
    [data]
  );
  const monthRevenue = Number(data?.monthRevenue) || 0;
  const yearRevenue = Number(data?.yearRevenue) || 0;
  const monthPairs = data?.monthPairsSold || 0;
  const yearPairs = data?.yearPairsSold || 0;
  const monthDelta = percentChange(monthPairs, data?.previousMonthPairsSold || 0);
  const yearDelta = percentChange(yearPairs, data?.previousYearPairsSold || 0);
  const earnShare = yearRevenue ? Math.min(100, Math.round((monthRevenue / yearRevenue) * 100)) : 0;
  const years = Array.from({ length: 5 }, (_, i) => now.getFullYear() - i);

  return (
    <div className="ad-dash">
      {error ? <p className="ad-error">{error}</p> : null}

      <div className="ad-dash-top">
        <article className="ad-visa">
          <p>Monthly revenue</p>
          <strong>{busy ? "…" : formatPrice(monthRevenue)}</strong>
          <div className="ad-visa-meta">
            <span>Paid orders {formatCount(data?.paidOrderCountMonth || 0)}</span>
            <span>{MONTHS[monthIndex]} {data?.year || year}</span>
          </div>
          <div className="ad-visa-foot">
            <span>Sneakers sold this month</span>
            <b>{formatCount(monthPairs)}</b>
          </div>
        </article>

        <article className="ad-card ad-stats">
          <header>
            <h2>Statistics</h2>
            <label>
              <span>Year</span>
              <select value={year} onChange={event => setYear(Number(event.target.value))}>
                {years.map(option => <option key={option} value={option}>{option}</option>)}
              </select>
            </label>
          </header>
          <Sparkline points={points} highlightIndex={monthIndex} />
          <div className="ad-stats-axis">
            {MONTHS.map(label => <span key={label}>{label}</span>)}
          </div>
        </article>
      </div>

      <div className="ad-dash-bottom">
        <article className="ad-card ad-earn">
          <h2>Earning in month</h2>
          <div className="ad-donut" style={{ background: `conic-gradient(#22c55e 0 ${earnShare}%, #e5e7eb ${earnShare}% 100%)` }}>
            <div>
              <strong>{earnShare}%</strong>
              <span>of year</span>
            </div>
          </div>
          <ul>
            <li><span>Deposit</span><b>{formatPrice(monthRevenue)}</b></li>
            <li><span>Year total</span><b>{formatPrice(yearRevenue)}</b></li>
            <li><span>Previous month</span><b>{formatPrice(Number(data?.previousMonthRevenue) || 0)}</b></li>
          </ul>
        </article>

        <article className="ad-card ad-kpi">
          <h2>Monthly sale</h2>
          <strong>{formatCount(monthPairs)}</strong>
          <p>Pairs sold this month</p>
          <div className={`ad-delta ${monthDelta < 0 ? "is-down" : "is-up"}`}>
            <span>{monthDelta < 0 ? "↓" : "↑"}</span>
            <b>{Math.abs(monthDelta)}%</b>
          </div>
        </article>

        <article className="ad-card ad-kpi">
          <h2>Yearly sale</h2>
          <strong>{formatCount(yearPairs)}</strong>
          <p>Pairs sold this year</p>
          <div className={`ad-delta ${yearDelta < 0 ? "is-down" : "is-up"}`}>
            <span>{yearDelta < 0 ? "↓" : "↑"}</span>
            <b>{Math.abs(yearDelta)}%</b>
          </div>
        </article>

        <article className="ad-card ad-cal-card">
          <header>
            <h2>Calendar</h2>
            <p>{MONTHS[monthIndex]} {data?.year || year}</p>
          </header>
          <MonthCalendar year={data?.year || year} month={data?.month || now.getMonth() + 1} />
        </article>
      </div>
    </div>
  );
}

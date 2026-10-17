export default function Pagination({ page, totalPages, onPage }) {
  if (!totalPages || totalPages <= 1) return null;

  const pages = Array.from({ length: totalPages }, (_, index) => index + 1);
  return (
    <div className="block-27">
      <ul>
        <li><a href="#products" onClick={event => { event.preventDefault(); onPage(Math.max(1, page - 1)); }}><i className="ion-ios-arrow-back" /></a></li>
        {pages.map(number => (
          <li key={number} className={number === page ? "active" : undefined}>
            {number === page
              ? <span>{number}</span>
              : <a href="#products" onClick={event => { event.preventDefault(); onPage(number); }}>{number}</a>}
          </li>
        ))}
        <li><a href="#products" onClick={event => { event.preventDefault(); onPage(Math.min(totalPages, page + 1)); }}><i className="ion-ios-arrow-forward" /></a></li>
      </ul>
    </div>
  );
}

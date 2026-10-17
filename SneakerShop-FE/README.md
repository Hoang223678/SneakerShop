# SneakerShop frontend (React)

Vite + React Router shop UI. Static Colorlib assets live in `public/`. Pages load sneakers from Catalog (`http://localhost:5002`, fallback `http://localhost:5000`).

```powershell
cd D:\SneakerShop\SneakerShop-FE
npm install
npm run dev
```

Open http://localhost:5173

| Route | Page |
| --- | --- |
| `/` | Home |
| `/men` | Men's collection (`gender=Men`) |
| `/women` | Women's collection (`gender=Women`) |
| `/product/:id` | Product detail |
| `/cart` `/checkout` `/order-complete` `/wishlist` | Cart flow |
| `/about` `/contact` | Content |

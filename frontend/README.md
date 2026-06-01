Simple Vue 3 frontend for TrixCompare API

Quick start:

1. Install dependencies

   cd frontend
   npm install

2. Configure backend URL (optional)

- Copy `.env.example` to `.env` and set `VITE_API_BASE_URL` to your backend address, e.g.

  VITE_API_BASE_URL=https://localhost:7149

If you leave it empty the app will request `/api/products` (CORS must be enabled on the backend).

3. Run dev server

   npm run dev

The app will open at http://localhost:5173 by default.

Notes:
- The backend already enables CORS so direct requests should work. If you use a different URL, set `VITE_API_BASE_URL` accordingly.
- This is a minimal example. Adjust fields in `ProductsList.vue` to match your product model (id, name, price, description).
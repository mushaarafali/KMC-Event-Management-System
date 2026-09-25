# CSE5013 Assignment Requirement to Implementation Map

## Brief-required API functions
1. Organizers create and manage events -> `POST/PUT/DELETE/PATCH api/events`; ownership checks use JWT user ID.
2. Participant registration -> `POST api/reservations` with ticket category, quantity, payment option and approval workflow.
3. Public search/find -> `GET api/events?q=&category=&location=&date=`.
4. Dedicated website consumes API -> `KmcEvents.Client` uses `ApiClient`/`HttpClient`; views do not access the database directly.

## Roles
- KMC Admin - protected administrative monitoring and user activation.
- Event Organizer - own-event CRUD/publish and own-event reservation approval.
- Public Guest - view/search without login.
- Public User - login required for seat reservation, payments and QR-ticket history.

## Extra client-requested functions
- Sri Lankan national-flag-inspired maroon/gold/orange/green custom CSS; no Bootstrap.
- KMC logo in navigation/footer; Kandy hero image on home.
- GlobeTrek-like premium nav, hero, feature cards, footer, quick links, contact details and responsive design.
- Footer credit: Developed by Mushaaraf Ali.
- Ticket category prices and seat limits.
- Pay Later pre-booking and demo card payment.
- 16-digit card validation + Luhn validation.
- Secure demo storage: masked PAN/last4 only; CVV is never stored.
- Organizer approves/rejects reservations; email service logs demo notifications unless SMTP is enabled.
- QR ticket is generated for approved reservations.
- NIC 10-12 alphanumeric validation.
- Event date at least 10 days from current date.
- Registration closing date cannot be in the past and must precede event date.
- Authenticated pages return no-store/no-cache headers to prevent browser-back access after logout.
- Contact Us includes supplied KMC Google Map iframe.

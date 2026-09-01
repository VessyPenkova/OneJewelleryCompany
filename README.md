# 💎 One Jewellery Company

**Custom jewellery design, ordering, inventory, and administration platform built with ASP.NET Core MVC.**

One Jewellery Company is a portfolio web application for creating and managing personalized jewellery. Customers can build bracelets and necklaces from individual components, create repeating design patterns, preview designs, add products to a cart, and submit orders. Administrators can manage components, stock, jewellery, design orders, purchase queues, and internal invoices.

The project also explores a broader idea: connecting personalized jewellery with flexible crafting opportunities for people who may have limited access to traditional employment.

## ✨ Implemented Features

### 👤 Customer Experience

- **Build Your Own Jewellery**
  - Choose between bracelet and necklace designs.
  - Select chains, cords, clasps, pendants, beads, pearls, stones, and other available components.
  - Set component quantities per finished piece and see estimated pricing.
  - Add a completed custom design to the shopping cart.
- **Design Studio**
  - Build a reusable component sequence/pattern.
  - Configure bracelet length and average bead size.
  - Switch between line and circular previews and auto-fill to calculated capacity.
  - Adjust rendered size, tilt, and rotation.
  - Submit a completed design to the administration workflow.
- **Collections & Jewellery Catalogue** — browse ready-made jewellery, collections, and product details.
- **Cart & Checkout** — session-based cart, order creation, inventory validation/deduction, and a development/demo payment flow.
- **Customer Accounts** — ASP.NET Core Identity registration/sign-in with branded authentication UI.
- **Our Story** — explains the concept, social purpose, customer-to-maker workflow, and origin of the project.

### 🛠 Administration

- **Dashboard** — central navigation for administrative workflows.
- **Components & Categories** — create/edit component categories and component data including pricing, stock, images, colors, and dimensions.
- **Inventory** — stock validation, deduction, and purchase-queue support.
- **Jewellery Catalogue** — create/edit ready-made jewellery and stock.
- **Design Orders** — review submitted designs, visual previews, component sequences, statuses, and production information.
- **Invoices** — internal invoice workflows and component/unit-cost calculations.

## 🧪 Testing

The solution contains two separate test projects:

- **OneJewelsCompany.UnitTests** — NUnit service/business-logic tests covering cart, orders, products, inventory, payment/model behavior, with EF Core InMemory test data.
- **OneJewelsCompany.PlaywrightTests** — Playwright + NUnit browser/end-to-end test project. Existing Qase reporter integration is retained in this project.

## 🧰 Technology

- **Framework:** .NET 8 / ASP.NET Core MVC
- **UI:** Razor Views, HTML5, CSS3, Bootstrap and project-specific styling
- **Database:** SQL Server
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity
- **Testing:** NUnit, EF Core InMemory, Microsoft Playwright
- **Development:** Visual Studio

## 👥 Roles

**Customer:** browse jewellery, create personalized designs, use Design Studio, manage a cart, submit orders, and use account functionality.

**Admin:** manage inventory, components, categories, jewellery, design orders, purchase queues, and invoices.

## 🏗️ Solution Structure

```text
OneJevelCompany
├── Controllers
├── Data
├── Models
├── Services
├── Views
├── wwwroot
├── OneJewelsCompany.UnitTests
└── OneJewelsCompany.PlaywrightTests
```

## 🚀 Planned Enhancements

- Stronger visual/functional differences between necklace and bracelet building workflows.
- More advanced 3D jewellery visualization and component positioning.
- Additional clasp and stringing-material visualization.
- Printable/PDF production and order documentation.
- Expanded automated invoice generation and pricing rules.
- Company/customer organization records and richer order history.
- Extended reporting and real-time administrative statistics.
- Production-ready payment provider integration.
- Replacement of representative maker stories with real participant stories as the project develops.

## 📸 Portfolio Screenshots

The repository contains screenshots from different stages of development. Some older images show previous versions of the interface and should be replaced as the current UI is finalised.

Recommended current portfolio views will be added: **Home**, **Build Your Own Jewellery**, **Design Studio**, **Our Story**, **Admin Design Orders**, and **Admin Dashboard / Inventory**.

## ℹ️ Development Notes

- Payment is currently a **development/demo flow**, not a production payment integration.
- Representative maker portraits and stories on **Our Story** are concept content, not real participant testimonials.
- Development administrator seeding is intended for local development only.
- Stock and pricing behavior should be validated with the included tests whenever business rules change.

---

**Developed with ASP.NET Core MVC, Entity Framework Core & SQL Server**  
© 2026 One Jewellery Company

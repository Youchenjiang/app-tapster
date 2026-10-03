# Microsoft Store Marketing & Presentation Assets

This directory contains visual and promotional assets used for publishing Tapster to the Microsoft Partner Center and Microsoft Store.

## Directory Structure

- `screenshots/`: Localized high-resolution application screenshots for desktop Store listings.
- `promo/`: Optional promotional artwork and hero banners.

## Automated Ingestion

`scripts/publish_store.py` scans `packaging/store_assets/screenshots/` during release builds:
- If screenshots are detected according to the filename convention (e.g., `tw1.png`, `en1.png`), they are automatically bundled into the submission archive and attached to the respective locale listing.
- If no screenshots exist locally, the submission pipeline uploads only the MSIX package and updates textual metadata without modifying existing Store screenshots.

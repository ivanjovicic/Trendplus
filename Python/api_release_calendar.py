from fastapi import FastAPI
from scraper.zalando_release_scraper import scrape_zalando_release_calendar

app = FastAPI()

@app.get("/api/release-calendar")
def release_calendar(gender: str = "mens"):
    if gender == "mens":
        url = "https://en.zalando.de/release-calendar/mens-shoes-sneakers/"
    else:
        url = "https://en.zalando.de/release-calendar/womens-shoes-sneakers/"

    items = scrape_zalando_release_calendar(url, headless=True)
    return {"items": items}

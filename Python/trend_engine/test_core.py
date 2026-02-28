import unittest
from trend_engine.core import (
    compute_trend_groups,
    apply_social_boost,
    _rank_score,
    TrendGroupResult
)
from collections import Counter

class TestCoreFunctions(unittest.TestCase):

    def test_rank_score(self):
        self.assertAlmostEqual(_rank_score(1), 0.667, places=3)
        self.assertAlmostEqual(_rank_score(2), 0.500, places=3)
        self.assertAlmostEqual(_rank_score(10), 0.250, places=3)
        self.assertAlmostEqual(_rank_score(100), 0.143, places=3)

    def test_compute_trend_groups(self):
        # Mock data for testing
        class MockScrapedItem:
            def __init__(self, brand, rank, source, market, hasImage, isNew, isOnSale):
                self.brand = brand
                self.rank = rank
                self.source = source
                self.market = market
                self.hasImage = hasImage
                self.isNew = isNew
                self.isOnSale = isOnSale

        items = [
            MockScrapedItem("Nike", 1, "zalando", "DE", True, True, False),
            MockScrapedItem("Nike", 2, "zalando", "DE", True, False, True),
        ]

        results = compute_trend_groups(items)
        self.assertEqual(len(results), 1)
        self.assertEqual(results[0].brand, "Nike")
        self.assertGreater(results[0].final_score, 0)

    def test_apply_social_boost(self):
        groups = [
            TrendGroupResult(
                canonical_key="nike|id:123",
                brand="Nike",
                name="Air Max",
                markets=["DE"],
                sources=["zalando"],
                total_occurrences=10,
                unique_sources=1,
                unique_markets=1,
                base_score=1.0,
                final_score=1.0,
                source_counts=Counter({"zalando": 10}),
                market_counts=Counter({"DE": 10}),
            )
        ]

        social_scores = {"nike": 100}
        boosted_groups = apply_social_boost(groups, social_scores)
        self.assertAlmostEqual(boosted_groups[0].final_score, 1.3, places=2)

if __name__ == "__main__":
    unittest.main()
import unittest

from goal_getter import analyze, generate_match


class T(unittest.TestCase):
    def test_deterministic(self):
        self.assertEqual(generate_match(1), generate_match(1))

    def test_insights_explainable(self):
        events = generate_match(3)
        ids = {e.id for e in events}
        insights = analyze(events)
        self.assertTrue(insights)
        for i in insights:
            self.assertTrue(i.explanation)
            self.assertTrue(set(i.evidence) <= ids)


if __name__ == "__main__":
    unittest.main()

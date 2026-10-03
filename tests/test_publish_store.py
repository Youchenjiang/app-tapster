#!/usr/bin/env python3
"""
Unit and integration tests for Microsoft Store publishing tooling and metadata.
"""

import os
import subprocess
import sys
import unittest

# Add scripts directory to sys.path
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SCRIPTS_DIR = os.path.join(REPO_ROOT, "scripts")
if SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, SCRIPTS_DIR)

import publish_store


class TestStoreListingParsing(unittest.TestCase):
    """Test parsing of all multilingual store listings in docs/."""

    def test_all_locales_present_and_valid(self):
        docs_dir = os.path.join(REPO_ROOT, "docs")
        expected_prefixes = ['ZH', 'ZH-CN', 'EN', 'JA', 'KO']

        for prefix in expected_prefixes:
            md_path = os.path.join(docs_dir, f"StoreListing_{prefix}.md")
            self.assertTrue(os.path.exists(md_path), f"Missing store listing: {md_path}")

            data = publish_store.parse_markdown_listing(md_path)
            self.assertIsNotNone(data, f"Failed to parse: {md_path}")

            # Description checks
            self.assertGreater(len(data['description']), 20, f"Description too short for {prefix}")
            self.assertGreater(len(data['shortDescription']), 10, f"Short description too short for {prefix}")

            # Features checks (Store requires at least 1, max 20)
            features = data['features']
            self.assertGreaterEqual(len(features), 5, f"Expected >= 5 features for {prefix}, got {len(features)}")
            self.assertLessEqual(len(features), 20, f"Expected <= 20 features for {prefix}, got {len(features)}")

            # Keywords checks (Store requires max 7)
            keywords = data['keywords']
            self.assertGreaterEqual(len(keywords), 1, f"Expected >= 1 keywords for {prefix}, got {len(keywords)}")
            self.assertLessEqual(len(keywords), 7, f"Expected <= 7 keywords for {prefix}, got {len(keywords)}")


class TestSectionMatching(unittest.TestCase):
    """Test multilingual section header matching."""

    def test_section_matching_variations(self):
        self.assertEqual(publish_store.match_section_name("產品亮點"), "features")
        self.assertEqual(publish_store.match_section_name("产品特性"), "features")
        self.assertEqual(publish_store.match_section_name("主な機能"), "features")
        self.assertEqual(publish_store.match_section_name("제품 기능 목록"), "features")
        self.assertEqual(publish_store.match_section_name("Features"), "features")

        self.assertEqual(publish_store.match_section_name("簡短描述"), "shortDescription")
        self.assertEqual(publish_store.match_section_name("简短描述"), "shortDescription")
        self.assertEqual(publish_store.match_section_name("簡単な説明"), "shortDescription")
        self.assertEqual(publish_store.match_section_name("간단한 설명"), "shortDescription")

        self.assertEqual(publish_store.match_section_name("搜尋關鍵字"), "keywords")
        self.assertEqual(publish_store.match_section_name("検索キーワード"), "keywords")
        self.assertEqual(publish_store.match_section_name("검색 키워드"), "keywords")
        self.assertEqual(publish_store.match_section_name("Search Terms"), "keywords")


class TestKeywordSanitization(unittest.TestCase):
    """Test recursive keyword truncation to Microsoft Store limit of 7."""

    def test_truncation(self):
        sample_data = {
            "keywords": ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"],
            "nested": {
                "Keywords": [1, 2, 3, 4, 5, 6, 7, 8]
            }
        }
        publish_store.sanitize_keywords_recursive(sample_data)
        self.assertEqual(len(sample_data["keywords"]), 7)
        self.assertEqual(len(sample_data["nested"]["Keywords"]), 7)


class TestPublishStoreDryRun(unittest.TestCase):
    """Test end-to-end dry-run execution."""

    def test_dry_run_execution(self):
        script_path = os.path.join(SCRIPTS_DIR, "publish_store.py")
        result = subprocess.run(
            [sys.executable, script_path, "--dry-run"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            timeout=30
        )
        self.assertEqual(result.returncode, 0, f"Dry-run failed with stderr: {result.stderr}")
        self.assertIn("Tapster submitted to Microsoft Partner Center! (Outcome: confirmed)", result.stdout)


if __name__ == '__main__':
    unittest.main()

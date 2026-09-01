#!/usr/bin/env python3
"""Tests for altstore_source.py - run with `python3 -m unittest tools/test_altstore_source.py`."""
import json
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import altstore_source as src  # noqa: E402

ICON = "https://codex64ai.github.io/daggerfall-unity-ios/icon.png"


def release(tag, *, draft=False, published="2026-09-01T04:23:52Z", body="notes",
            assets=None, html_url=None):
    if assets is None:
        assets = [{"name": f"DaggerfallUnity-iOS-{tag}.ipa", "size": 1000,
                   "browser_download_url": f"https://example.test/{tag}/app.ipa"}]
    return {"tag_name": tag, "draft": draft, "prerelease": True, "published_at": published,
            "body": body, "assets": assets,
            "html_url": html_url or f"https://github.com/x/y/releases/tag/{tag}"}


class SelectReleases(unittest.TestCase):
    def test_drafts_are_never_listed(self):
        drafts = [release("testapp-unity6", draft=True, published=None)]
        self.assertEqual(src.select_releases(drafts), [])

    def test_only_prealpha_version_tags(self):
        rels = [release("v0.1.9-prealpha"), release("nightly"), release("v0.2"),
                release("testapp-unity6", draft=True, published=None)]
        tags = [r["tag_name"] for r in src.select_releases(rels)]
        self.assertEqual(tags, ["v0.1.9-prealpha"])

    def test_release_without_an_ipa_is_skipped(self):
        rels = [release("v0.1.9-prealpha", assets=[{"name": "notes.zip", "size": 1,
                                                     "browser_download_url": "u"}])]
        self.assertEqual(src.select_releases(rels), [])

    def test_newest_first_by_version_not_by_string(self):
        rels = [release("v0.1.9-prealpha"), release("v0.1.10-prealpha"), release("v0.1.12-prealpha")]
        tags = [r["tag_name"] for r in src.select_releases(rels)]
        self.assertEqual(tags, ["v0.1.12-prealpha", "v0.1.10-prealpha", "v0.1.9-prealpha"])


class VersionEntry(unittest.TestCase):
    def test_fields_come_from_the_release_and_its_ipa(self):
        v = src.version_entry(release("v0.1.9-prealpha", body="# Title\n\nHello **world**."))
        self.assertEqual(v["version"], "0.1.9")
        self.assertEqual(v["buildVersion"], "0")
        self.assertEqual(v["date"], "2026-09-01T04:23:52Z")
        self.assertEqual(v["downloadURL"], "https://example.test/v0.1.9-prealpha/app.ipa")
        self.assertEqual(v["size"], 1000)
        self.assertEqual(v["minOSVersion"], "15.0")
        self.assertIn("Hello world.", v["localizedDescription"])
        self.assertNotIn("**", v["localizedDescription"])


class FeedFloor(unittest.TestCase):
    def test_builds_before_0_1_9_are_excluded_because_their_ipa_says_1_0_0(self):
        rels = [release("v0.1.9-prealpha"), release("v0.1.8-prealpha"), release("v0.1.7-prealpha")]
        tags = [r["tag_name"] for r in src.select_releases(rels)]
        self.assertEqual(tags, ["v0.1.9-prealpha"])


class ReleaseNotes(unittest.TestCase):
    def test_markdown_is_flattened_to_plain_text(self):
        md = "# Pre-Alpha 0.1.9\n\nSee [upstream](https://u.test) and `code`.\n\n---\n\n## Section\n\n- one\n- two"
        txt = src.release_notes_text(md, "https://rel.test", limit=10_000)
        self.assertNotIn("#", txt)
        self.assertNotIn("](", txt)
        self.assertNotIn("`", txt)
        self.assertNotIn("---", txt)
        self.assertIn("See upstream and code.", txt)
        self.assertIn("• one", txt)

    def test_long_notes_are_cut_at_a_paragraph_and_link_to_the_release(self):
        md = "\n\n".join(f"Paragraph {i} " + "x" * 200 for i in range(20))
        txt = src.release_notes_text(md, "https://rel.test", limit=1000)
        self.assertLess(len(txt), 1200)
        self.assertTrue(txt.endswith("Full release notes: https://rel.test"))
        self.assertNotIn("xxx\nFull", txt)  # cut lands between paragraphs, not mid-word


class BuildSource(unittest.TestCase):
    def test_shape_matches_the_altstore_schema(self):
        rels = [release("v0.1.9-prealpha", published="2026-08-30T21:40:50Z"), release("v0.1.10-prealpha")]
        s = src.build_source(rels, icon_url=ICON)
        self.assertEqual(set(s) >= {"name", "identifier", "apps", "news"}, True)
        self.assertEqual(s["news"], [])
        app = s["apps"][0]
        for key in ("name", "bundleIdentifier", "developerName", "localizedDescription",
                    "iconURL", "versions", "appPermissions"):
            self.assertIn(key, app)
        self.assertEqual(app["bundleIdentifier"], "net.codex64.daggerfall")
        self.assertEqual(app["iconURL"], ICON)
        self.assertEqual(app["category"], "games")
        self.assertEqual(app["appPermissions"], {"entitlements": [], "privacy": {}})
        self.assertEqual([v["version"] for v in app["versions"]], ["0.1.10", "0.1.9"])
        json.dumps(s)  # serialisable

    def test_no_releases_still_yields_a_valid_empty_source(self):
        s = src.build_source([], icon_url=ICON)
        self.assertEqual(s["apps"][0]["versions"], [])


if __name__ == "__main__":
    unittest.main()

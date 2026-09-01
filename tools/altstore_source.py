#!/usr/bin/env python3
"""Build (and optionally publish) the AltStore / SideStore source feed for this port.

A "source" is one JSON file that AltStore and SideStore poll. It lists the app, and one
entry per version pointing at a publicly downloadable, UNSIGNED .ipa. Ours are the
GitHub release assets, so this script asks the GitHub API for the published releases and
writes the feed. Nothing here signs or uploads an .ipa.

    python3 tools/altstore_source.py                 # write altstore.json + icon.png to ./altstore-out
    python3 tools/altstore_source.py --out DIR       # ... to DIR
    python3 tools/altstore_source.py --publish       # commit + push to the gh-pages branch

Rules, and why:
  * Draft releases are never listed. The testapp draft carries third-party content.
  * Only tags shaped like vX.Y.Z-prealpha are listed; a version needs exactly one .ipa asset.
  * Versions before 0.1.9 are excluded: their Info.plist reports CFBundleShortVersionString
    1.0.0 (fixed in 436d7d826), so AltStore would treat them all as one version and refuse
    to see them as updates of each other.
  * CFBundleVersion is "0" on every build so far, hence buildVersion "0". Bump here if the
    build setup ever starts writing a real one.
  * Release notes are markdown written for GitHub; the feed wants plain text. They are
    flattened and cut at a paragraph boundary with a link to the full notes.

Feed URL once published (GitHub Pages, gh-pages branch, root):
    https://codex64ai.github.io/daggerfall-unity-ios/altstore.json
Add-to-app links:
    sidestore://source?url=https://codex64ai.github.io/daggerfall-unity-ios/altstore.json
    altstore://source?url=https://codex64ai.github.io/daggerfall-unity-ios/altstore.json
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = "Codex64ai/daggerfall-unity-ios"
PAGES_BRANCH = "gh-pages"
PAGES_BASE = "https://codex64ai.github.io/daggerfall-unity-ios"
FEED_NAME = "altstore.json"
ICON_NAME = "icon.png"
ICON_SOURCE = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "AppIcon1.png")

BUNDLE_ID = "net.codex64.daggerfall"
APP_NAME = "Daggerfall Unity"
DEVELOPER = "Codex64"
TINT = "#8B5A2B"
MIN_OS = "15.0"            # Unity 6 floor; every listed build is a Unity 6 build
BUILD_VERSION = "0"        # CFBundleVersion in every ipa so far
FIRST_FEED_VERSION = (0, 1, 9)
TAG_RE = re.compile(r"^v(\d+)\.(\d+)\.(\d+)-prealpha$")
NOTES_LIMIT = 1500

SUBTITLE = "The 1996 RPG on iPad and iPhone, with touch, controller and mods."
DESCRIPTION = (
    "An iOS/iPadOS port of Daggerfall Unity, the open-source recreation of The Elder Scrolls II. "
    "Free, non-commercial and source-only. You supply your own copy of Daggerfall's arena2 folder; "
    "no game data is distributed.\n\n"
    "Pre-alpha: expect rough edges. Touch layout, controller and keyboard/mouse support, real "
    "travel along roads, and loose-file mods are in. Full instructions and the mod guide live in "
    "README-iOS.md in the repository."
)


# --------------------------------------------------------------------------- selection

def tag_version(tag):
    m = TAG_RE.match(tag or "")
    return tuple(int(x) for x in m.groups()) if m else None


def ipa_asset(release):
    ipas = [a for a in release.get("assets", []) if a.get("name", "").lower().endswith(".ipa")]
    return ipas[0] if len(ipas) == 1 else None


def select_releases(releases):
    """Published, versioned pre-alphas with exactly one .ipa, newest first."""
    keep = []
    for r in releases:
        if r.get("draft"):
            continue
        v = tag_version(r.get("tag_name"))
        if v is None or v < FIRST_FEED_VERSION:
            continue
        if ipa_asset(r) is None:
            continue
        keep.append((v, r))
    keep.sort(key=lambda t: t[0], reverse=True)
    return [r for _, r in keep]


# --------------------------------------------------------------------------- notes

_LINK = re.compile(r"\[([^\]]+)\]\([^)]+\)")
_HEADING = re.compile(r"^[ \t]{0,3}#{1,6}[ \t]*", re.M)
_RULE = re.compile(r"^\s*([-*_]\s*){3,}$", re.M)
_BULLET = re.compile(r"^\s*[-*+]\s+", re.M)
_EMPH = re.compile(r"(\*\*|__|\*|_|`)")


def release_notes_text(markdown, release_url, limit=NOTES_LIMIT):
    text = markdown.replace("\r\n", "\n")
    text = _RULE.sub("", text)
    text = _LINK.sub(r"\1", text)
    text = _HEADING.sub("", text)
    text = _BULLET.sub("• ", text)
    text = _EMPH.sub("", text)
    text = re.sub(r"\n{3,}", "\n\n", text).strip()

    footer = f"Full release notes: {release_url}"
    if len(text) <= limit:
        return f"{text}\n\n{footer}" if text else footer
    cut = text.rfind("\n\n", 0, limit)
    if cut <= 0:
        cut = limit
    return text[:cut].rstrip() + "\n\n" + footer


# --------------------------------------------------------------------------- feed

def version_entry(release):
    v = tag_version(release["tag_name"])
    asset = ipa_asset(release)
    return {
        "version": ".".join(str(x) for x in v),
        "buildVersion": BUILD_VERSION,
        "date": release.get("published_at"),
        "localizedDescription": release_notes_text(release.get("body") or "", release.get("html_url", "")),
        "downloadURL": asset["browser_download_url"],
        "size": int(asset["size"]),
        "minOSVersion": MIN_OS,
    }


def build_source(releases, icon_url):
    versions = [version_entry(r) for r in select_releases(releases)]
    return {
        "name": "Daggerfall Unity iOS",
        "identifier": "net.codex64.daggerfall.source",
        "subtitle": "Pre-alpha builds of the Daggerfall Unity iOS port",
        "description": "Sideloadable pre-alpha builds of the Daggerfall Unity iOS/iPadOS port. "
                       "Unsigned; SideStore or AltStore signs them with your own Apple ID.",
        "iconURL": icon_url,
        "website": f"https://github.com/{REPO}",
        "tintColor": TINT,
        "apps": [{
            "name": APP_NAME,
            "bundleIdentifier": BUNDLE_ID,
            "developerName": DEVELOPER,
            "subtitle": SUBTITLE,
            "localizedDescription": DESCRIPTION,
            "iconURL": icon_url,
            "tintColor": TINT,
            "category": "games",
            "versions": versions,
            "appPermissions": {"entitlements": [], "privacy": {}},
        }],
        "news": [],
    }


# --------------------------------------------------------------------------- I/O

def fetch_releases():
    out = subprocess.run(["gh", "api", f"repos/{REPO}/releases", "--paginate"],
                         check=True, capture_output=True, text=True).stdout
    # --paginate concatenates JSON arrays; make it one list.
    releases = []
    for chunk in re.split(r"\]\s*\[", out.strip()):
        chunk = chunk if chunk.startswith("[") else "[" + chunk
        chunk = chunk if chunk.endswith("]") else chunk + "]"
        releases.extend(json.loads(chunk))
    return releases


def write_feed(out_dir, source):
    os.makedirs(out_dir, exist_ok=True)
    feed = os.path.join(out_dir, FEED_NAME)
    with open(feed, "w", encoding="utf-8") as f:
        json.dump(source, f, indent=2, ensure_ascii=False)
        f.write("\n")
    shutil.copyfile(ICON_SOURCE, os.path.join(out_dir, ICON_NAME))
    return feed


def git(*args, cwd=None):
    return subprocess.run(["git", *args], check=True, cwd=cwd, capture_output=True, text=True).stdout


def publish(source, repo_root, remote="fork"):
    """Write the feed into a throwaway worktree of gh-pages, commit if changed, push."""
    tmp = tempfile.mkdtemp(prefix="altstore-pages-")
    try:
        subprocess.run(["git", "fetch", remote, PAGES_BRANCH], cwd=repo_root, capture_output=True)
        have_branch = subprocess.run(["git", "rev-parse", "--verify", f"{remote}/{PAGES_BRANCH}"],
                                     cwd=repo_root, capture_output=True).returncode == 0
        if have_branch:
            git("worktree", "add", "-B", PAGES_BRANCH, tmp, f"{remote}/{PAGES_BRANCH}", cwd=repo_root)
        else:
            git("worktree", "add", "--detach", tmp, cwd=repo_root)
            git("checkout", "--orphan", PAGES_BRANCH, cwd=tmp)
            git("rm", "-rfq", ".", cwd=tmp)
        write_feed(tmp, source)
        with open(os.path.join(tmp, ".nojekyll"), "w"):
            pass
        git("add", "-A", cwd=tmp)
        if not git("status", "--porcelain", cwd=tmp).strip():
            print("gh-pages already up to date")
            return
        newest = source["apps"][0]["versions"][0]["version"] if source["apps"][0]["versions"] else "none"
        git("commit", "-q", "-m", f"Source feed: newest build {newest}", cwd=tmp)
        git("push", "-q", remote, f"HEAD:{PAGES_BRANCH}", cwd=tmp)
        print(f"pushed {PAGES_BRANCH}: {PAGES_BASE}/{FEED_NAME}")
    finally:
        subprocess.run(["git", "worktree", "remove", "--force", tmp], cwd=repo_root, capture_output=True)
        shutil.rmtree(tmp, ignore_errors=True)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--out", default="altstore-out", help="directory for altstore.json + icon.png")
    ap.add_argument("--publish", action="store_true", help="commit and push to gh-pages instead")
    ap.add_argument("--remote", default="fork", help="git remote that is %s" % REPO)
    args = ap.parse_args(argv)

    source = build_source(fetch_releases(), icon_url=f"{PAGES_BASE}/{ICON_NAME}")
    versions = [v["version"] for v in source["apps"][0]["versions"]]
    print("versions:", ", ".join(versions) or "(none)")

    if args.publish:
        repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
        publish(source, repo_root, remote=args.remote)
    else:
        print("wrote", write_feed(args.out, source))


if __name__ == "__main__":
    sys.exit(main())

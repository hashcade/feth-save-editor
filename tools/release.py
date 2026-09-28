#!/usr/bin/env python3
"""Tag a reviewed main commit; GitHub Actions builds and publishes it."""

from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path

from package import ROOT, version


def run(*args: str) -> str:
    result = subprocess.run(args, cwd=ROOT, check=True, text=True, capture_output=True)
    return result.stdout.strip()


def check_ready(tag: str) -> None:
    if Path(run("git", "rev-parse", "--show-toplevel")) != ROOT:
        raise ValueError("run from this repository")
    if run("git", "branch", "--show-current") != "main":
        raise ValueError("releases must come from main")
    if run("git", "status", "--porcelain"):
        raise ValueError("commit working-tree changes first")

    run("git", "fetch", "origin", "main", "--tags")
    head = run("git", "rev-parse", "HEAD")
    if head != run("git", "rev-parse", "origin/main"):
        raise ValueError("local main is not synchronized with origin/main")
    if run("git", "tag", "--list", tag):
        raise ValueError(f"tag already exists: {tag}")

    builds = json.loads(run(
        "gh", "run", "list", "--workflow", "build.yml", "--commit", head,
        "--json", "conclusion,status", "--limit", "20",
    ))
    if not any(build["status"] == "completed" and build["conclusion"] == "success" for build in builds):
        raise ValueError("wait for a successful build of this commit before releasing")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--current", action="store_true", required=True)
    parser.add_argument("--check", action="store_true", help="validate without tagging")
    parser.add_argument("--yes", action="store_true", help="skip the confirmation prompt")
    args = parser.parse_args()

    tag = f"v{version()}"
    check_ready(tag)
    if args.check:
        print(f"Ready to publish {tag} from {run('git', 'rev-parse', '--short', 'HEAD')}")
        return
    if not args.yes and input(f"Create and push {tag}? [y/N] ").lower() not in ("y", "yes"):
        return

    run("git", "tag", "-a", tag, "-m", tag)
    run("git", "push", "origin", tag)
    print(f"Pushed {tag}. Check the release workflow before sharing its downloads.")


if __name__ == "__main__":
    main()
